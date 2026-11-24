import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, expect, test, vi } from 'vitest'
import App from './App.vue'
import { apiRequest } from './api/client'

vi.mock('./components/UpdateBanner.vue', () => ({
  default: { render: () => null }
}))

function mountApp() {
  return mount(App, {
    global: { stubs: { RouterView: { template: '<div data-test="page" />' } } }
  })
}

function healthyResponse() {
  return Response.json({ status: 'ok', time: '2026-11-20T00:00:00Z' })
}

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

test('opens the original page without showing the waiting animation when the service responds promptly', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => healthyResponse())
  vi.stubGlobal('fetch', fetchMock)
  const app = mountApp()

  expect(app.find('[data-test="page"]').exists()).toBe(false)
  expect(app.text()).not.toContain('正在连接服务')
  await flushPromises()

  expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/api\/health$/), expect.objectContaining({
    cache: 'no-store'
  }))
  expect(app.find('[data-test="page"]').exists()).toBe(true)
  expect(app.text()).not.toContain('正在连接服务')
  app.unmount()
})

test('keeps the current page visible without a global loading bar during a slow request', async () => {
  vi.useFakeTimers()
  let respond!: (response: Response) => void
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(healthyResponse())
    .mockImplementationOnce(() => new Promise<Response>(resolve => { respond = resolve })))
  const app = mountApp()
  await vi.advanceTimersByTimeAsync(0)
  const request = apiRequest('/api/test/slow-read')
  try {
    await vi.advanceTimersByTimeAsync(500)

    expect(app.find('[data-test="page"]').exists()).toBe(true)
    expect(app.find('[role="progressbar"][aria-label="正在加载"]').exists()).toBe(false)
  } finally {
    respond(Response.json({}))
    await request
    app.unmount()
  }
})

test('waits through temporary HTTP failures and startup HTML, then opens the original page automatically', async () => {
  vi.useFakeTimers()
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(null, { status: 503 }))
    .mockResolvedValueOnce(new Response('<html>Starting</html>'))
    .mockImplementation(async () => healthyResponse())
  vi.stubGlobal('fetch', fetchMock)
  const app = mountApp()

  await vi.advanceTimersByTimeAsync(650)
  expect(app.get('[role="status"]').text()).toContain('正在连接服务')
  expect(app.get('[role="status"] .loading-orbit').attributes('aria-hidden')).toBe('true')
  expect(app.find('[data-test="page"]').exists()).toBe(false)

  await vi.advanceTimersByTimeAsync(4_000)
  expect(app.find('[data-test="page"]').exists()).toBe(false)

  await vi.advanceTimersByTimeAsync(4_000)
  expect(app.find('[data-test="page"]').exists()).toBe(true)
  expect(app.text()).not.toContain('正在连接服务')
  app.unmount()
})

test('stops automatic checks at the deadline and can reconnect manually', async () => {
  vi.useFakeTimers()
  const fetchMock = vi.fn((_url: string, init: RequestInit) => new Promise<Response>((_resolve, reject) => {
    init.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')), { once: true })
  }))
  vi.stubGlobal('fetch', fetchMock)
  const app = mountApp()

  await vi.advanceTimersByTimeAsync(120_000)
  expect(app.get('button').text()).toBe('重新连接')
  expect(app.find('[data-test="page"]').exists()).toBe(false)
  expect(fetchMock.mock.lastCall?.[1].signal?.aborted).toBe(true)
  const attempts = fetchMock.mock.calls.length
  await vi.advanceTimersByTimeAsync(8_000)
  expect(fetchMock).toHaveBeenCalledTimes(attempts)

  fetchMock.mockImplementation(async () => healthyResponse())
  await app.get('button').trigger('click')
  await vi.advanceTimersByTimeAsync(0)
  expect(app.find('[data-test="page"]').exists()).toBe(true)
  app.unmount()
})
