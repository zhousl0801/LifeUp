<script setup lang="ts">
import { amountTone, formatPercent, formatSignedMoney } from '../utils/format'

defineProps<{
  dailyProfit: number | null
  dailyRate: number | null
  totalProfit: number | null
  totalRate: number | null
  totalUnavailable?: string
}>()
</script>

<template>
  <div class="hero-metrics" role="group" aria-label="收益概览">
    <div class="hero-today">
      <span>今日盈亏</span>
      <b :class="amountTone(dailyProfit)">{{ formatSignedMoney(dailyProfit) }}</b>
      <small class="hero-metric-rate" :class="amountTone(dailyRate)">{{ formatPercent(dailyRate, '—') }}</small>
    </div>
    <div>
      <span>总收益</span>
      <b :class="amountTone(totalProfit)">
        {{ totalProfit === null && totalUnavailable ? totalUnavailable : formatSignedMoney(totalProfit) }}
      </b>
    </div>
    <div>
      <span>总收益率</span>
      <b :class="amountTone(totalRate)">{{ formatPercent(totalRate, '—') }}</b>
    </div>
  </div>
</template>
