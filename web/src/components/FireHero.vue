<script setup lang="ts">
import { computed } from 'vue'
import type { CapitalCurrent, CapitalProjection, FireOverview, FireTrackMode } from '../types/capital'
import { formatCapitalAmount, formatMonthLabel } from '../utils/capital'
import { formatSignedMoney } from '../utils/format'
import AmountVisibilityButton from './AmountVisibilityButton.vue'
import GlassTrack from './GlassTrack.vue'
import LoadingButton from './LoadingButton.vue'

const props = defineProps<{
  fire: FireOverview
  current: CapitalCurrent
  projection: CapitalProjection
  currentLabel: string
  amountsMasked: boolean
  animateUpdates: boolean
  savingMode: FireTrackMode | null
}>()

const emit = defineEmits<{
  'switch-mode': [mode: FireTrackMode]
  'open-assumptions': []
}>()

const trackModes = [
  { value: 'standard', label: '标准 FIRE' },
  { value: 'lean', label: 'Lean FIRE' }
] as const
const active = computed(() => props.fire[props.fire.activeMode])
const modeLabel = computed(() => props.fire.activeMode === 'lean' ? 'Lean FIRE' : '标准 FIRE')
const horizon = computed(() => {
  const { status, etaMonth, monthsRemaining } = active.value
  switch (status) {
    case 'reached':
      return { title: '已达成 FIRE', note: '被动月薪已覆盖目标支出' }
    case 'noTarget':
      return { title: '待补充支出', note: '设置支出基准后开始测算' }
    case 'beyondHorizon':
      return { title: '100 年内难达成', note: '按当前收支与收益假设' }
    case 'depleted':
      return { title: '预计资产耗尽', note: '按当前收支与收益假设' }
    case 'unreachable':
      return { title: '当前假设下难达成', note: '调整收支与收益假设后重算' }
    default: {
      const remaining = monthsRemaining ?? 0
      const years = Math.floor(remaining / 12)
      const months = remaining % 12
      const duration = [years > 0 ? `${years} 年` : '', months > 0 ? `${months} 个月` : ''].filter(Boolean).join(' ')
      return {
        title: duration ? `还需 ${duration}` : '预计达成 FIRE',
        note: etaMonth ? `预计 ${formatMonthLabel(etaMonth)}自由` : null
      }
    }
  }
})
const showCoastReached = computed(() =>
  props.fire.coast.achievable && props.fire.coast.reached && active.value.status !== 'reached'
)
const coverageLabel = computed(() => props.current.coverageComplete ? '总资产' : '已记录资产')
const trackSummary = computed(() => `${modeLabel.value} 轨道，自由度 ${active.value.progressPercent.toFixed(1)}%，${horizon.value.title}`)

function money(value: number, compact = false): string {
  return formatCapitalAmount(value, props.amountsMasked, compact)
}
</script>

<template>
  <section class="fire-hero" aria-label="FIRE 自由度">
    <div class="fire-top">
      <div class="mode-switch" role="group" aria-label="FIRE 轨道">
        <LoadingButton
          v-for="mode in trackModes"
          :key="mode.value"
          class="mode-chip"
          :data-fire-mode="mode.value"
          :aria-pressed="fire.activeMode === mode.value"
          :aria-label="mode.label"
          :loading="savingMode === mode.value"
          :disabled="savingMode !== null"
          loading-label="切换中"
          @click="emit('switch-mode', mode.value)"
        >{{ mode.label }}</LoadingButton>
      </div>
      <div class="fire-actions">
        <button type="button" class="swr-chip" @click="emit('open-assumptions')">{{ fire.safeWithdrawalRatePercent }}% 提取</button>
        <AmountVisibilityButton />
      </div>
    </div>

    <div class="freedom">
      <div>
        <span class="micro">{{ modeLabel }} 自由度</span>
        <h2>{{ active.progressPercent.toFixed(1) }}<span>%</span></h2>
      </div>
      <div class="horizon">
        <strong>{{ horizon.title }}</strong>
        <span v-if="horizon.note" class="micro">{{ horizon.note }}</span>
        <span v-if="showCoastReached" class="coast-chip">Coast FIRE 已达</span>
      </div>
    </div>

    <div class="fire-cashflow-well">
      <div class="cashflow-row">
        <span>被动月薪 <b class="mint">{{ money(fire.currentPassiveMonthlyCashflow) }}</b> / {{ money(active.monthlyExpense) }}</span>
        <span v-if="active.status === 'noTarget'">待设基准</span>
        <b v-else-if="active.monthlyCashflowGap === 0" class="mint">已全覆盖</b>
        <span v-else>月缺口 <b>{{ money(active.monthlyCashflowGap) }}</b></span>
      </div>
      <div class="cashflow-row">
        <span>{{ coverageLabel }} {{ money(current.total, true) }} / 目标 {{ active.status === 'noTarget' ? '—' : money(active.targetCapital, true) }}</span>
        <span v-if="fire.coast.achievable && fire.coast.thresholdCapital != null" :class="{ mint: fire.coast.reached }">
          Coast {{ fire.coast.reached ? '已达' : money(fire.coast.thresholdCapital, true) }}
        </span>
      </div>
    </div>

    <GlassTrack
      mode="fire"
      :fire-milestones="fire.milestones"
      :progress-percent="active.progressPercent"
      :current-label="currentLabel"
      :eta-label="horizon.note"
      :years="[]"
      :milestones="[]"
      :status="active.status"
      :amounts-masked="amountsMasked"
      :animate-updates="animateUpdates"
      :ariaSummary="trackSummary"
    />

    <div class="fire-metrics">
      <div>
        <span>综合储蓄率</span>
        <b :class="{ mint: fire.savingsRatePercent >= 30 }">{{ fire.savingsRatePercent.toFixed(1) }}%</b>
      </div>
      <div>
        <span>月均积累</span>
        <b :class="projection.monthlyNetAccumulation < 0 ? 'negative' : 'mint'">
          {{ amountsMasked ? '••••' : formatSignedMoney(projection.monthlyNetAccumulation) }}
        </b>
      </div>
      <div>
        <span>组合年化</span>
        <b>{{ projection.blendedAnnualReturnPercent.toFixed(1) }}%</b>
      </div>
    </div>
  </section>
</template>

<style scoped>
.fire-hero {
  position: relative;
  padding: var(--card-padding);
  border-radius: var(--radius-card);
  background: var(--color-hero-glow), linear-gradient(150deg, var(--color-hero-bg), var(--color-hero-bg-end));
  box-shadow: var(--shadow-hero);
  color: var(--color-hero-text);
  font-variant-numeric: tabular-nums;
}

.fire-top,
.fire-actions,
.mode-switch {
  display: flex;
  align-items: center;
}

.fire-top {
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 4px;
}

.fire-actions {
  gap: 2px;
  margin-left: auto;
}

.mode-switch,
.swr-chip {
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: var(--radius-pill);
  background: rgba(12, 10, 40, 0.22);
}

.mode-switch {
  padding: 2px;
}

.mode-chip,
.swr-chip {
  min-width: var(--touch-target);
  min-height: 36px;
  padding: 0 10px;
  color: inherit;
  font-size: var(--text-caption);
  font-weight: 600;
  white-space: nowrap;
}

.swr-chip {
  min-height: var(--touch-target);
}

.mode-chip {
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-hero-text-secondary);
  transition:
    background-color var(--motion-feedback) var(--ease-move),
    color var(--motion-feedback) var(--ease-move);
}

.mode-chip[aria-pressed='true'] {
  background: var(--color-hero-active);
  color: var(--color-hero-active-text);
  box-shadow: 0 2px 8px rgba(15, 10, 45, 0.22);
}

.freedom {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: 8px;
  margin: 16px 0 14px;
}

.micro {
  display: block;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.freedom h2 {
  margin: 4px 0 0;
  font-size: var(--text-display);
  font-weight: 800;
  line-height: 1;
  letter-spacing: -0.04em;
  white-space: nowrap;
}

.freedom h2 span {
  margin-left: 2px;
  color: var(--color-accent-mint);
  font-size: 0.65em;
}

.horizon {
  display: grid;
  justify-items: end;
  gap: 3px;
  text-align: right;
}

.horizon strong {
  font-size: var(--text-subhead);
}

.coast-chip {
  padding: 3px 7px;
  border: 1px solid rgba(124, 231, 177, 0.3);
  border-radius: var(--radius-tag);
  background: rgba(124, 231, 177, 0.12);
  color: var(--color-accent-mint);
  font-size: var(--text-caption);
}

.fire-cashflow-well {
  padding: 12px 14px;
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: 16px;
  background: rgba(12, 10, 40, 0.22);
}

.cashflow-row {
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 4px 8px;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.cashflow-row + .cashflow-row {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid rgba(255, 255, 255, 0.10);
}

.cashflow-row b {
  color: var(--color-hero-text);
}

.fire-metrics {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  margin-top: 4px;
  text-align: center;
}

.fire-metrics div + div {
  border-left: 1px solid rgba(255, 255, 255, 0.14);
}

.fire-metrics span,
.fire-metrics b {
  display: block;
}

.fire-metrics span {
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.fire-metrics b {
  margin-top: 4px;
  font-size: var(--text-subhead);
  overflow-wrap: anywhere;
}

.mint,
.cashflow-row .mint {
  color: var(--color-accent-mint);
}

.negative {
  color: var(--color-finance-gain-on-hero);
}
</style>
