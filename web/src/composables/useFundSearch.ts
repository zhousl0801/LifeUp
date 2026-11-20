import { onBeforeUnmount, ref, watch } from 'vue'
import { getApiErrorMessage } from '../api/client'
import { searchFunds } from '../api/funds'
import type { FundSearchResult } from '../types/fund'

export interface UseFundSearchOptions {
  debounceMs?: number
  onNonEmptyInput?: () => void
}

export function useFundSearch(options: UseFundSearchOptions = {}) {
  const { debounceMs = 300, onNonEmptyInput } = options
  const fundSearch = ref('')
  const fundSearchResults = ref<FundSearchResult[]>([])
  const fundSearching = ref(false)
  const fundSearchError = ref('')
  let fundSearchTimer: ReturnType<typeof setTimeout> | null = null
  let fundSearchSequence = 0

  function cancelFundSearch(): void {
    fundSearchSequence++
    if (fundSearchTimer) {
      clearTimeout(fundSearchTimer)
      fundSearchTimer = null
    }
    fundSearching.value = false
  }

  function resetFundSearch(): void {
    cancelFundSearch()
    fundSearchResults.value = []
    fundSearchError.value = ''
    fundSearch.value = ''
  }

  async function runFundSearch(query: string): Promise<void> {
    const sequence = ++fundSearchSequence
    fundSearching.value = true

    try {
      const results = await searchFunds(query)

      if (sequence === fundSearchSequence && fundSearch.value.trim() === query) {
        fundSearchResults.value = results
        fundSearchError.value = results.length ? '' : '没有找到匹配的基金，请换一个名称或代码。'
      }
    } catch (error) {
      if (sequence === fundSearchSequence && fundSearch.value.trim() === query) {
        fundSearchResults.value = []
        fundSearchError.value = getApiErrorMessage(error, '基金搜索暂时不可用，请稍后重试。')
      }
    } finally {
      if (sequence === fundSearchSequence) {
        fundSearching.value = false
      }
    }
  }

  watch(fundSearch, (value) => {
    fundSearchError.value = ''
    fundSearchSequence++

    if (fundSearchTimer) {
      clearTimeout(fundSearchTimer)
      fundSearchTimer = null
    }

    const query = value.trim()

    if (!query) {
      fundSearching.value = false
      fundSearchResults.value = []
      return
    }

    onNonEmptyInput?.()

    fundSearchTimer = setTimeout(() => {
      void runFundSearch(query)
    }, debounceMs)
  })

  onBeforeUnmount(() => {
    cancelFundSearch()
  })

  return {
    fundSearch,
    fundSearchResults,
    fundSearching,
    fundSearchError,
    cancelFundSearch,
    resetFundSearch
  }
}
