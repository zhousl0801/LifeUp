import { beforeEach, expect, test, vi } from 'vitest'
import { activeWriteCount, apiRequest, clearReadCache, readOnlyRequest } from './client'

beforeEach(() => {
  localStorage.clear()
  clearReadCache()
  vi.restoreAllMocks()
})

test('known validation failure permits corrected content with the same unused ID', async () => {
  vi.spyOn(globalThis, 'fetch')
    .mockResolvedValueOnce(new Response(JSON.stringify({ detail: '金额无效' }), { status: 400 }))
    .mockResolvedValueOnce(new Response(JSON.stringify({ balance: 100 }), { status: 200 }))
  const input = { amount: -1, requestId: '40b4ecb9-fca5-4cd4-a551-e53e3bdf3c2b' }
  await expect(apiRequest('/api/ledger/provident/deposits', { method: 'POST', body: JSON.stringify(input) })).rejects.toThrow('金额无效')
  await expect(apiRequest('/api/ledger/provident/deposits', { method: 'POST', body: JSON.stringify({ ...input, amount: 100 }) })).resolves.toEqual({ balance: 100 })
})

test('repeated reads use the short-lived cache until a write invalidates it', async () => {
  const fetch = vi.spyOn(globalThis, 'fetch')
    .mockResolvedValueOnce(new Response(JSON.stringify({ amount: 100 }), { status: 200 }))
    .mockResolvedValueOnce(new Response(JSON.stringify({ amount: 200 }), { status: 200 }))
    .mockResolvedValueOnce(new Response(JSON.stringify({ amount: 300 }), { status: 200 }))

  const first = await apiRequest<{ amount: number }>('/api/ledger')
  const cached = await apiRequest<{ amount: number }>('/api/ledger')

  expect(cached).toEqual(first)
  expect(fetch).toHaveBeenCalledTimes(1)

  await apiRequest('/api/ledger/entries', {
    method: 'POST', body: JSON.stringify({ amount: 1, requestId: 'cache-invalidation' })
  })
  const refreshed = await apiRequest<{ amount: number }>('/api/ledger')

  expect(refreshed.amount).toBe(300)
  expect(fetch).toHaveBeenCalledTimes(3)
})

test('concurrent default reads share a fetch but receive independent objects', async () => {
  let resolve!: (response: Response) => void
  const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise(r => { resolve = r }))
  const first = apiRequest<{ amount: number }>('/api/test/concurrent')
  const second = apiRequest<{ amount: number }>('/api/test/concurrent')
  resolve(new Response(JSON.stringify({ amount: 100 })))
  const [a, b] = await Promise.all([first, second])
  a.amount = 0
  expect(b.amount).toBe(100)
  expect(fetch).toHaveBeenCalledTimes(1)
})

test('cache invalidation prevents an older read from caching stale data', async () => {
  let resolve!: (response: Response) => void
  const fetch = vi.spyOn(globalThis, 'fetch')
    .mockImplementationOnce(() => new Promise(r => { resolve = r }))
    .mockResolvedValue(new Response(JSON.stringify({ amount: 200 })))
  const old = apiRequest('/api/test/stale')
  clearReadCache()
  resolve(new Response(JSON.stringify({ amount: 100 })))
  await old
  expect(await apiRequest('/api/test/stale')).toEqual({ amount: 200 })
  expect(fetch).toHaveBeenCalledTimes(2)
})

test('caller-initiated AbortSignal cancellation is not misreported as a timeout', async () => {
  const controller = new AbortController()
  vi.spyOn(globalThis, 'fetch').mockImplementation((_input, init) => new Promise((_resolve, reject) => {
    init?.signal?.addEventListener('abort', () => {
      reject(new DOMException('The operation was aborted.', 'AbortError'))
    })
  }))

  const pending = apiRequest('/api/test/abort', { signal: controller.signal })
  controller.abort()

  await expect(pending).rejects.toMatchObject({ name: 'AbortError' })
})

test('readOnlyRequest with cache no-store bypasses in-memory read cache', async () => {
  const fetchSpy = vi.spyOn(globalThis, 'fetch')
    .mockResolvedValueOnce(new Response(JSON.stringify({ version: 1 }), { status: 200 }))
    .mockResolvedValueOnce(new Response(JSON.stringify({ version: 2 }), { status: 200 }))

  expect(await readOnlyRequest<{ version: number }>('/api/ledger/backup')).toEqual({ version: 1 })
  expect(await readOnlyRequest<{ version: number }>('/api/ledger/backup', { cache: 'no-store' })).toEqual({ version: 2 })
  expect(fetchSpy).toHaveBeenCalledTimes(2)
})

test('activeWriteCount tracks non-GET writes even without requestId', async () => {
  let resolveFetch!: (value: Response) => void
  vi.spyOn(globalThis, 'fetch').mockReturnValueOnce(new Promise<Response>((resolve) => {
    resolveFetch = resolve
  }))

  expect(activeWriteCount.value).toBe(0)
  const pending = apiRequest('/api/ledger/backup/restore', { method: 'POST', body: '{}' })
  expect(activeWriteCount.value).toBe(1)
  resolveFetch(new Response(JSON.stringify({ restored: true }), { status: 200 }))
  await pending
  expect(activeWriteCount.value).toBe(0)
})

test('a failed write can be retried with the same body while another write proceeds', async () => {
  const fetchMock = vi.spyOn(globalThis, 'fetch')
    .mockRejectedValueOnce(new TypeError('Failed to fetch'))
    .mockImplementation(async () => new Response(null, { status: 204 }))
  const init = { method: 'POST', body: JSON.stringify({ requestId: 'same-id' }) }

  await expect(apiRequest('/entries', init)).rejects.toMatchObject({
    status: 0,
    message: '网络异常，请检查网络后重试。'
  })
  expect(fetchMock).toHaveBeenCalledTimes(1)
  expect(localStorage.length).toBe(0)
  expect(activeWriteCount.value).toBe(0)

  await Promise.all([apiRequest('/entries', init), apiRequest('/other', { method: 'POST' })])
  expect(fetchMock.mock.calls.map(([url]) => url)).toEqual(['/entries', '/entries', '/other'])
  expect(fetchMock.mock.calls[1]?.[1]?.body).toBe(init.body)
  expect(activeWriteCount.value).toBe(0)
})

test('a write timeout reports a retryable error and releases the active write count', async () => {
  vi.useFakeTimers()
  try {
    vi.spyOn(globalThis, 'fetch').mockImplementation((_url, init) => new Promise((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => reject(init.signal?.reason), { once: true })
    }))

    const request = apiRequest('/entries', { method: 'POST' })
    const assertion = expect(request).rejects.toMatchObject({
      status: 0,
      message: '请求超时，请检查网络后重试。'
    })
    expect(activeWriteCount.value).toBe(1)
    await vi.advanceTimersByTimeAsync(15_000)
    await assertion
    expect(activeWriteCount.value).toBe(0)
  } finally {
    vi.useRealTimers()
  }
})
