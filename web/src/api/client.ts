import { ref } from 'vue'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

const readCacheTtlMs = 15_000
const readCacheLimit = 50
const requestTimeoutMs = 15_000
const readCache = new Map<string, { at: number; data: unknown }>()

export const activeRequestCount = ref(0)

const inFlightReads = new Map<string, Promise<unknown>>()
let cacheGeneration = 0

export function clearReadCache(): void {
  cacheGeneration++
  inFlightReads.clear()
  readCache.clear()
}

function isCacheableRead(init?: RequestInit): boolean {
  return (init?.method?.toUpperCase() ?? 'GET') === 'GET' && !init?.body
}

function readCached<T>(key: string): T | undefined {
  const entry = readCache.get(key)

  if (!entry) {
    return undefined
  }

  if (Date.now() - entry.at > readCacheTtlMs) {
    readCache.delete(key)
    return undefined
  }

  return structuredClone(entry.data) as T
}

function writeCached(key: string, data: unknown): void {
  if (readCache.size >= readCacheLimit) {
    const oldest = readCache.keys().next().value

    if (oldest !== undefined) {
      readCache.delete(oldest)
    }
  }

  readCache.set(key, { at: Date.now(), data: structuredClone(data) })
}

export async function readOnlyRequest<T>(path: string, init?: RequestInit): Promise<T> {
  // Only default reads share work; custom headers and cancellation keep independent semantics.
  if (init !== undefined) return performRequest<T>(path, init)
  const key = `${baseUrl}${path}`
  let pending = inFlightReads.get(key)
  if (!pending) {
    pending = performRequest<T>(path)
    inFlightReads.set(key, pending)
  }
  try {
    return structuredClone(await pending) as T
  } finally {
    if (inFlightReads.get(key) === pending) inFlightReads.delete(key)
  }
}

async function performRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const generation = cacheGeneration
  const cacheable = isCacheableRead(init)
  const cacheKey = `${baseUrl}${path}`

  if (cacheable) {
    const cached = readCached<T>(cacheKey)

    if (cached !== undefined) {
      return cached
    }
  }

  const headers = new Headers(init?.headers)

  if (!headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  activeRequestCount.value++
  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), requestTimeoutMs)

  try {
    const response = await fetch(`${baseUrl}${path}`, {
      ...init,
      credentials: 'include',
      headers,
      signal: init?.signal ?? controller.signal
    })

    if (!response.ok) {
      const problem = await readProblem(response)
      const firstFieldError = problem?.errors
        ? Object.values(problem.errors).flat()[0]
        : undefined
      const message = firstFieldError
        ?? problem?.detail
        ?? problem?.title
        ?? `请求失败（HTTP ${response.status}）`

      throw new ApiError(response.status, message)
    }

    if (!cacheable) {
      clearReadCache()
    }

    if (response.status === 204) {
      return undefined as T
    }

    const data = (await response.json()) as T

    if (cacheable && generation === cacheGeneration) {
      writeCached(cacheKey, data)
    }

    return data
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new ApiError(0, '请求超时，请检查网络后重试。')
    }

    throw error
  } finally {
    window.clearTimeout(timeout)
    activeRequestCount.value--
  }
}

const PendingPrefix = 'asset-keeper.pending.'

export interface PendingMutation {
  requestId: string
  path: string
  method: string
  body: string
}

export const pendingMutationCount = ref(0)
const inFlightMutations = new Map<string, number>()

export function readPendingMutations(): PendingMutation[] {
  const result: PendingMutation[] = []

  for (let index = 0; index < localStorage.length; index++) {
    const key = localStorage.key(index)

    if (key?.startsWith(PendingPrefix)) {
      result.push(JSON.parse(localStorage.getItem(key)!) as PendingMutation)
    }
  }

  pendingMutationCount.value = result.filter((request) => !inFlightMutations.has(request.requestId)).length
  return result
}

function remember(request: PendingMutation): void {
  try {
    localStorage.setItem(`${PendingPrefix}${request.requestId}`, JSON.stringify(request))
    readPendingMutations()
  } catch {
    throw new ApiError(0, '无法保存待核实记录，请检查浏览器存储后重试。')
  }
}

function forget(requestId: string): void {
  localStorage.removeItem(`${PendingPrefix}${requestId}`)
  readPendingMutations()
}

export async function lookupMutation<T>(requestId: string): Promise<T> {
  const receipt = await readOnlyRequest<{ result: T }>(`/api/ledger/requests/${requestId}`)
  return receipt.result
}

export async function recoverMutation(request: PendingMutation, retry: boolean): Promise<unknown> {
  try {
    const result = await lookupMutation(request.requestId)
    forget(request.requestId)
    clearReadCache()
    return result
  } catch (error) {
    if (retry && error instanceof ApiError && error.status === 404) {
      const result = await apiRequest(request.path, { method: request.method, body: request.body })
      clearReadCache()
      return result
    }

    throw error
  }
}

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const body = typeof init?.body === 'string' ? init.body : undefined
  const method = init?.method?.toUpperCase() ?? 'GET'
  const parsed = body ? JSON.parse(body) as { requestId?: string; change?: { requestId?: string } } : undefined
  const requestId = method !== 'GET' ? (parsed?.requestId ?? parsed?.change?.requestId) : undefined

  if (!requestId) {
    return readOnlyRequest<T>(path, init)
  }

  const pending = readPendingMutations()
  const original = pending.find((request) => request.requestId === requestId)

  if (pending.some((request) => request.requestId !== requestId)
    || (original && (original.path !== path || original.body !== body || original.method !== method))) {
    throw new ApiError(0, '上次保存结果待核实，请先在顶部查询结果或重试原请求。')
  }

  inFlightMutations.set(requestId, (inFlightMutations.get(requestId) ?? 0) + 1)
  try {
    remember({ requestId, path, method, body: body! })
    try {
      const result = await readOnlyRequest<T>(path, init)
      forget(requestId)
      return result
    } catch (error) {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        forget(requestId)
        throw error
      }

      try {
        const result = await lookupMutation<T>(requestId)
        forget(requestId)
        return result
      } catch {
        throw new ApiError(0, '保存结果待核实，原内容已保留；请查询结果或重试原请求，勿重新记账。')
      }
    }
  } finally {
    const remaining = (inFlightMutations.get(requestId) ?? 1) - 1
    if (remaining) inFlightMutations.set(requestId, remaining)
    else inFlightMutations.delete(requestId)
    readPendingMutations()
  }
}

export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.message
  }

  return fallback
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}
