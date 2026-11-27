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

const inFlightReads = new Map<string, Promise<unknown>>()
let cacheGeneration = 0

export function clearReadCache(): void {
  cacheGeneration++
  inFlightReads.clear()
  readCache.clear()
}

export interface ApiRequestInit extends RequestInit {
  skipCacheInvalidation?: boolean
}

function isCacheableRead(init?: ApiRequestInit): boolean {
  return (init?.method?.toUpperCase() ?? 'GET') === 'GET' && !init?.body && !init?.headers && init?.cache !== 'no-store'
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

export async function readOnlyRequest<T>(path: string, init?: ApiRequestInit): Promise<T> {
  // Only default reads share work; custom headers and cancellation keep independent semantics.
  if (init !== undefined) return performRequest<T>(path, init)
  const key = `${baseUrl}${path}`
  const cached = readCached<T>(key)
  if (cached !== undefined) {
    return cached
  }

  let pending = inFlightReads.get(key)
  if (!pending) {
    pending = performRequest<T>(path, undefined, true)
    inFlightReads.set(key, pending)
  }
  try {
    return structuredClone(await pending) as T
  } finally {
    if (inFlightReads.get(key) === pending) inFlightReads.delete(key)
  }
}

async function performRequest<T>(path: string, init?: ApiRequestInit, skipCacheRead = false): Promise<T> {
  const generation = cacheGeneration
  const cacheable = isCacheableRead(init)
  const cacheKey = `${baseUrl}${path}`

  if (cacheable && !skipCacheRead) {
    const cached = readCached<T>(cacheKey)

    if (cached !== undefined) {
      return cached
    }
  }

  const headers = new Headers(init?.headers)

  if (!headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const controller = new AbortController()
  let timedOut = false
  const timeout = window.setTimeout(() => {
    timedOut = true
    controller.abort()
  }, requestTimeoutMs)
  const externalSignal = init?.signal
  const onExternalAbort = () => controller.abort()

  if (externalSignal) {
    if (externalSignal.aborted) {
      controller.abort()
    } else {
      externalSignal.addEventListener('abort', onExternalAbort, { once: true })
    }
  }

  try {
    const response = await fetch(`${baseUrl}${path}`, {
      ...init,
      credentials: 'include',
      headers,
      signal: controller.signal
    }).catch((error: unknown) => {
      if (error instanceof ApiError || externalSignal?.aborted
        || (error instanceof Error && error.name === 'AbortError')) {
        throw error
      }

      throw new ApiError(0, '网络异常，请检查网络后重试。')
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

    if (!cacheable && !init?.skipCacheInvalidation) {
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
    if (timedOut && !externalSignal?.aborted) {
      throw new ApiError(0, '请求超时，请检查网络后重试。')
    }

    throw error
  } finally {
    window.clearTimeout(timeout)
    externalSignal?.removeEventListener('abort', onExternalAbort)
  }
}

export const activeWriteCount = ref(0)

export async function apiRequest<T>(path: string, init?: ApiRequestInit): Promise<T> {
  const method = init?.method?.toUpperCase() ?? 'GET'
  if (method === 'GET' || method === 'HEAD') return readOnlyRequest<T>(path, init)

  activeWriteCount.value++
  try {
    return await readOnlyRequest<T>(path, init)
  } finally {
    activeWriteCount.value--
  }
}

export async function apiDownload(path: string, fallbackName: string): Promise<void> {
  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), requestTimeoutMs)

  try {
    const response = await fetch(`${baseUrl}${path}`, {
      credentials: 'include',
      signal: controller.signal
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

    const blob = await response.blob()
    const disposition = response.headers.get('Content-Disposition') ?? ''
    const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition)
    const fileName = match?.[1] ? decodeURIComponent(match[1].replaceAll('"', '')) : fallbackName
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = fileName
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new ApiError(0, '请求超时，请检查网络后重试。')
    }

    throw error
  } finally {
    window.clearTimeout(timeout)
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
