<script setup lang="ts">
import { useAmountPrivacy } from '../composables/useAmountPrivacy'
import { amountTone, formatPercent, formatSignedMoney, MaskedMoney } from '../utils/format'

defineProps<{
  dailyProfit: number | null
  dailyRate: number | null
  totalProfit: number | null
  totalRate: number | null
  totalUnavailable?: string
}>()

const { amountsMasked } = useAmountPrivacy()
</script>

<template>
  <div class="hero-metrics" role="group" aria-label="收益概览">
    <div class="hero-today">
      <span>今日盈亏</span>
      <b :class="amountsMasked ? '' : amountTone(dailyProfit)">{{ amountsMasked && dailyProfit !== null ? MaskedMoney : formatSignedMoney(dailyProfit) }}</b>
      <small class="hero-metric-rate" :class="amountsMasked ? '' : amountTone(dailyRate)">{{ amountsMasked && dailyRate !== null ? MaskedMoney : formatPercent(dailyRate, '—') }}</small>
    </div>
    <div>
      <span>总收益</span>
      <b :class="amountsMasked ? '' : amountTone(totalProfit)">
        {{ totalProfit === null && totalUnavailable ? totalUnavailable : (amountsMasked && totalProfit !== null ? MaskedMoney : formatSignedMoney(totalProfit)) }}
      </b>
    </div>
    <div>
      <span>总收益率</span>
      <b :class="amountsMasked ? '' : amountTone(totalRate)">{{ amountsMasked && totalRate !== null ? MaskedMoney : formatPercent(totalRate, '—') }}</b>
    </div>
  </div>
</template>
