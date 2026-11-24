import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { ApiError, getApiErrorMessage } from '../api/client'
import { fetchLedger, initializeLedger } from '../api/ledger'
import type { InitializeLedgerInput, LedgerOverview } from '../types/ledger'
import type { AssetTrend, TrendRange } from '../types/trend'

export type LedgerLoadStatus = 'ready' | 'missing' | 'error'

export const useLedgerStore = defineStore('ledger', () => {
  const overview = ref<LedgerOverview | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  const errorMessage = ref<string | null>(null)
  const lastLoadStatus = ref<LedgerLoadStatus | null>(null)
  const isInitialized = computed(() => overview.value !== null)
  const trendRange = ref<TrendRange>('week')
  const trendCache = ref<Partial<Record<TrendRange, AssetTrend>>>({})

  function setTrend(range: TrendRange, trend: AssetTrend): void {
    trendRange.value = range
    trendCache.value[range] = trend
  }

  function clearTrendCache(): void {
    trendCache.value = {}
  }

  async function load(): Promise<LedgerLoadStatus> {
    isLoading.value = true
    errorMessage.value = null

    try {
      overview.value = await fetchLedger()
      lastLoadStatus.value = 'ready'
      return 'ready'
    } catch (error) {
      overview.value = null

      if (error instanceof ApiError && error.status === 404) {
        clearTrendCache()
        lastLoadStatus.value = 'missing'
        return 'missing'
      }

      errorMessage.value = getApiErrorMessage(error, '无法连接服务器，请检查网络或后端服务。')
      lastLoadStatus.value = 'error'
      return 'error'
    } finally {
      isLoading.value = false
    }
  }

  async function initialize(input: InitializeLedgerInput): Promise<LedgerOverview> {
    isSaving.value = true

    try {
      const result = await initializeLedger(input)
      clearTrendCache()
      overview.value = result
      errorMessage.value = null
      lastLoadStatus.value = 'ready'
      return result
    } finally {
      isSaving.value = false
    }
  }

  return {
    overview,
    isLoading,
    isSaving,
    errorMessage,
    lastLoadStatus,
    isInitialized,
    trendRange,
    trendCache,
    setTrend,
    clearTrendCache,
    load,
    initialize
  }
})
