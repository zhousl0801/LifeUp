import { getActivePinia, storeToRefs } from 'pinia'
import { computed, shallowRef } from 'vue'
import { useHomePreferencesStore } from '../stores/homePreferences'
import { formatMoney, formatPercent, formatSignedMoney, MaskedMoney } from '../utils/format'

export function useAmountPrivacy(fallbackMasked = false) {
  const store = getActivePinia() ? useHomePreferencesStore() : null
  const amountsMasked = store ? storeToRefs(store).amountsMasked : shallowRef(fallbackMasked)

  function toggleAmountsMasked(): void {
    amountsMasked.value = !amountsMasked.value
  }

  function maskMoney(value: number | null | undefined, prefix = '¥'): string {
    if (value == null) return '—'
    return amountsMasked.value ? `${prefix}${MaskedMoney}` : `${prefix}${formatMoney(value)}`
  }

  function maskSignedMoney(value: number | null | undefined): string {
    if (value == null) return '—'
    return amountsMasked.value ? MaskedMoney : formatSignedMoney(value)
  }

  function maskPercent(value: number | null | undefined, fallback = '—'): string {
    if (value == null) return fallback
    return amountsMasked.value ? MaskedMoney : formatPercent(value, fallback)
  }

  return {
    amountsMasked,
    isMasked: computed(() => amountsMasked.value),
    toggleAmountsMasked,
    maskMoney,
    maskSignedMoney,
    maskPercent
  }
}
