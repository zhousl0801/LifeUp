<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import AppIcon from '../components/AppIcon.vue'
import AppNav from '../components/AppNav.vue'
import BottomSheet from '../components/BottomSheet.vue'
import CapitalActualCard from '../components/CapitalActualCard.vue'
import CapitalAssumptionsSheet from '../components/CapitalAssumptionsSheet.vue'
import CapitalProjectionChart from '../components/CapitalProjectionChart.vue'
import CapitalYearlyTable from '../components/CapitalYearlyTable.vue'
import GlassTrack from '../components/GlassTrack.vue'
import GoalTabs from '../components/GoalTabs.vue'
import LoadingButton from '../components/LoadingButton.vue'
import LoadingState from '../components/LoadingState.vue'
import { ApiError, getApiErrorMessage } from '../api/client'
import { fetchCapital, previewCapital, saveCapital } from '../api/capital'
import { fetchMonthChange } from '../api/ledger'
import { toast } from '../composables/useMessage'
import { useHomePreferencesStore } from '../stores/homePreferences'
import type { CapitalGoalSettings, CapitalOverview } from '../types/capital'
import type { MonthChange } from '../types/ledger'
import { formatMonthLabel, selectYearLabels } from '../utils/capital'
import {
  amountTone,
  formatMaskedMoney,
  formatMoney,
  formatSignedMoney,
  todayInBusinessTimeZone
} from '../utils/format'

const overview = ref<CapitalOverview | null>(null)
const previewOverview = ref<CapitalOverview | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')
const notInitialized = ref(false)
const sheetOpen = ref(false)
const saving = ref(false)
const saveError = ref('')
const chartView = ref<'capital' | 'monthly'>('capital')
const suppressAnimation = ref(false)
const monthChangeOpen = ref(false)
const monthChange = ref<MonthChange | null>(null)
const monthChangeLoading = ref(false)
const monthChangeError = ref('')
const { amountsMasked } = storeToRefs(useHomePreferencesStore())
let previewSequence = 0

const display = computed(() => previewOverview.value ?? overview.value)
const currentMonth = todayInBusinessTimeZone().slice(0, 7)
const currentYear = Number(currentMonth.slice(0, 4))
const currentLabel = formatMonthLabel(currentMonth)
const targetAmount = computed(() => display.value?.projection.targetAmount ?? null)
const etaLabel = computed(() => display.value?.projection.etaMonth
  ? `预计 ${formatMonthLabel(display.value.projection.etaMonth)}达成`
  : null)
const years = computed(() => {
  const item = display.value

  if (!item || targetAmount.value === null) {
    return []
  }

  const targetYear = item.projection.etaMonth ? Number(item.projection.etaMonth.slice(0, 4)) : null

  return selectYearLabels(
    item.projection.years,
    currentYear,
    targetAmount.value,
    targetYear,
    item.projection.progressPercent
  )
})
const animateUpdates = computed(() => !sheetOpen.value && !suppressAnimation.value)
const amountParts = computed(() => {
  const [integer, fraction] = formatMoney(display.value?.current.total ?? 0).split('.')
  return { integer: integer ?? '0', fraction: fraction ?? '00' }
})
const visibleNotes = computed(() => display.value?.projection.notes
  .filter((note) => note !== '按当前资产结构推演') ?? [])
const statusText = computed(() => {
  const projection = display.value?.projection

  if (!projection) {
    return ''
  }

  switch (projection.status) {
    case 'reached':
      return '目标已达成'
    case 'noTarget':
      return '尚未设定目标'
    case 'depleted':
      return '按当前假设，资本将耗尽'
    case 'unreachable':
      return '按当前假设无法达成目标'
    case 'beyondHorizon':
      return '100 年内无法达成目标'
    default:
      return etaLabel.value ?? ''
  }
})
const ariaSummary = computed(() => {
  const item = display.value

  if (!item) {
    return '资本积累轨道'
  }

  const target = item.projection.targetAmount
  const targetText = target === null ? '未设目标' : `目标 ${moneyLabel(target)}`
  const etaText = etaLabel.value ? `，${etaLabel.value}` : ''

  return `资本积累轨道，${item.current.coverageComplete ? '当前资本' : '已记录资产'} ${moneyLabel(item.current.total)}，${targetText}，进度 ${item.projection.progressPercent}%${etaText}`
})

onMounted(load)

async function load(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  notInitialized.value = false

  try {
    overview.value = await fetchCapital()
    previewOverview.value = null
  } catch (error) {
    notInitialized.value = error instanceof ApiError && error.status === 404
    errorMessage.value = getApiErrorMessage(error, '资本目标加载失败，请重试。')
  } finally {
    isLoading.value = false
  }
}

function openAssumptions(): void {
  previewSequence++
  saveError.value = ''
  previewOverview.value = null
  sheetOpen.value = true
}

async function closeAssumptions(): Promise<void> {
  if (saving.value) {
    return
  }

  previewSequence++
  suppressAnimation.value = true
  sheetOpen.value = false
  previewOverview.value = null
  saveError.value = ''
  await nextTick()
  suppressAnimation.value = false
}

async function handlePreview(settings: CapitalGoalSettings): Promise<void> {
  const sequence = ++previewSequence

  try {
    const result = await previewCapital(settings)

    if (sequence === previewSequence) {
      previewOverview.value = result
      saveError.value = ''
    }
  } catch (error) {
    if (sequence === previewSequence) {
      saveError.value = getApiErrorMessage(error, '预览失败，请检查输入。')
    }
  }
}

async function handleSave(settings: CapitalGoalSettings): Promise<void> {
  previewSequence++
  saving.value = true
  saveError.value = ''
  suppressAnimation.value = true

  try {
    const result = await saveCapital(settings)
    overview.value = result
    previewOverview.value = null
    sheetOpen.value = false
    toast.success('资本目标已保存')
  } catch (error) {
    saveError.value = error instanceof ApiError && error.status === 409
      ? '账本刚有新变化，请重试。'
      : getApiErrorMessage(error, '保存失败，请重试。')
  } finally {
    saving.value = false
    await nextTick()
    suppressAnimation.value = false
  }
}

async function openMonthChange(): Promise<void> {
  monthChangeOpen.value = true
  monthChangeLoading.value = true
  monthChangeError.value = ''

  try {
    monthChange.value = await fetchMonthChange()
  } catch (error) {
    monthChangeError.value = getApiErrorMessage(error, '本月变化加载失败，请重试。')
  } finally {
    monthChangeLoading.value = false
  }
}

function moneyLabel(value: number): string {
  return amountsMasked.value ? '••••' : `¥${formatMoney(value)}`
}

function shortMoney(value: number): string {
  if (value >= 10_000) {
    return `${(value / 10_000).toFixed(1).replace(/\.0$/, '')} 万`
  }

  return formatMoney(value)
}

function formatRate(value: number): string {
  return `${value.toFixed(1)}%`
}

function signedLabel(value: number | null | undefined): string {
  return amountsMasked.value ? '••••' : formatSignedMoney(value)
}

function netTone(value: number): string {
  return value < 0 ? 'is-negative' : 'is-mint'
}
</script>

<template>
  <main class="page capital-page with-nav">
    <header class="capital-header">
      <h1>目标</h1>
      <button type="button" class="header-action" aria-label="调整假设" @click="openAssumptions">
        <AppIcon name="settings" />
      </button>
    </header>

    <GoalTabs />

    <LoadingState v-if="isLoading && !display" variant="card" label="正在读取资本目标" />

    <section v-else-if="errorMessage" class="state">
      <p>{{ errorMessage }}</p>
      <RouterLink v-if="notInitialized" class="state-link" to="/setup">去初始化账本</RouterLink>
      <LoadingButton v-else class="secondary-button" :loading="isLoading" loading-label="加载中…" @click="load">重新加载</LoadingButton>
    </section>

    <template v-else-if="display">
      <section class="hero">
        <div class="hero-top">
          <span class="hero-label">{{ display.current.coverageComplete ? '当前资本' : '已记录资产' }}</span>
        </div>

        <h2 class="hero-amount">
          <span class="currency">¥</span>
          <template v-if="amountsMasked">{{ formatMaskedMoney(display.current.total).replace('¥', '') }}</template>
          <template v-else>{{ amountParts.integer }}<span class="fraction">.{{ amountParts.fraction }}</span></template>
        </h2>

        <p v-if="targetAmount !== null" class="hero-gap">
          距 {{ amountsMasked ? '••••' : `¥${shortMoney(targetAmount)}` }} 还差
          <b>{{ amountsMasked ? '••••' : shortMoney(display.projection.remaining ?? 0) }}</b>
        </p>
        <p v-else class="hero-gap">还没有目标，先看看按当前速度能积累多少。</p>

        <div class="hero-stats">
          <div class="stat">
            <span>月均积累</span>
            <b :class="netTone(display.projection.monthlyNetAccumulation)">
              {{ signedLabel(display.projection.monthlyNetAccumulation) }}
            </b>
          </div>
          <span class="divider" aria-hidden="true"></span>
          <div class="stat right">
            <span>组合年化</span>
            <b>{{ formatRate(display.projection.blendedAnnualReturnPercent) }}</b>
          </div>
        </div>

        <GlassTrack
          :progress-percent="display.projection.progressPercent"
          :current-label="currentLabel"
          :eta-label="etaLabel"
          :years="years"
          :milestones="display.projection.milestones"
          :status="display.projection.status"
          :amounts-masked="amountsMasked"
          :animate-updates="animateUpdates"
          :ariaSummary="ariaSummary"
        />

        <p class="eta-line">{{ statusText }}</p>
        <p v-for="note in visibleNotes" :key="note" class="projection-note">{{ note }}</p>
      </section>

      <p v-if="display.current.note" class="coverage-note">
        {{ display.current.note }}
        <RouterLink to="/assets">去补录</RouterLink>
      </p>

      <button v-if="targetAmount === null" type="button" class="set-goal" @click="openAssumptions">
        设定目标
      </button>

      <template v-if="display.projection.status !== 'reached'">
        <CapitalProjectionChart
          :points="display.projection.points"
          :target-amount="targetAmount"
          :view="chartView"
          :amounts-masked="amountsMasked"
          :current-total="display.current.total"
          :current-month="currentMonth"
          @update:view="chartView = $event"
        />

        <CapitalYearlyTable
          :years="display.projection.years"
          :eta-month="display.projection.etaMonth"
          :target-amount="targetAmount"
          :amounts-masked="amountsMasked"
        />
      </template>

      <section v-else class="capital-card reached-card">
        <h2>目标已达成</h2>
        <p>{{ amountsMasked ? '••••' : `¥${shortMoney(targetAmount ?? 0)}` }} 的目标已经完成。</p>
      </section>

      <CapitalActualCard
        :actual="display.actualMonth"
        :amounts-masked="amountsMasked"
        @open-change="openMonthChange"
      />

      <CapitalAssumptionsSheet
        :open="sheetOpen"
        :settings="display.settings"
        :effective="display.effective"
        :preview="previewOverview"
        :saving="saving"
        :server-error="saveError"
        @close="closeAssumptions"
        @preview="handlePreview"
        @save="handleSave"
      />

      <BottomSheet
        :open="monthChangeOpen"
        title="本月变化"
        :subtitle="monthChange ? `${monthChange.from} 至 ${monthChange.to}` : ''"
        @close="monthChangeOpen = false"
      >
        <LoadingState v-if="monthChangeLoading" variant="list" :rows="3" label="正在读取本月变化" />
        <p v-else-if="monthChangeError" class="form-error">{{ monthChangeError }}</p>
        <div v-else-if="monthChange" class="change-list">
          <div class="change-row">
            <span>市值变动</span>
            <b :class="amountTone(monthChange.marketValueChange)">{{ signedLabel(monthChange.marketValueChange) }}</b>
          </div>
          <div class="change-row">
            <span>净存入</span>
            <b>{{ signedLabel(monthChange.netDeposit) }}</b>
          </div>
          <div class="change-row">
            <span>生活结余</span>
            <b>{{ signedLabel(monthChange.livingSurplus) }}</b>
          </div>
          <div class="change-row">
            <span>公积金缴存</span>
            <b>{{ signedLabel(monthChange.providentDeposit) }}</b>
          </div>
          <div class="change-row">
            <span>核对差额</span>
            <b>{{ signedLabel(monthChange.reconciliationDifference) }}</b>
          </div>
          <div v-if="monthChange.addedRecords" class="change-row">
            <span>新增记录</span>
            <b>{{ signedLabel(monthChange.addedRecords) }}</b>
          </div>
          <p v-for="note in monthChange.notes" :key="note" class="sheet-note">{{ note }}</p>
        </div>
      </BottomSheet>
    </template>

    <AppNav />
  </main>
</template>

<style scoped>
.capital-page {
  padding-bottom: calc(97px + env(safe-area-inset-bottom));
}

.capital-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 14px;
}

.capital-header h1 {
  margin: 0;
  font-size: var(--text-title3);
  font-weight: 700;
  letter-spacing: -0.3px;
}

.header-action {
  display: grid;
  width: 44px;
  height: 44px;
  padding: 0;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
  color: var(--color-text-primary);
  place-items: center;
}

.hero {
  position: relative;
  overflow: hidden;
  padding: 20px;
  border-radius: var(--radius-card);
  background: linear-gradient(150deg, var(--color-hero-bg), var(--color-hero-bg-end));
  box-shadow: var(--shadow-hero);
  color: var(--color-hero-text);
}

.hero::after {
  position: absolute;
  top: -90px;
  right: -70px;
  width: 220px;
  height: 220px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.07);
  content: "";
  pointer-events: none;
}

.hero-top {
  position: relative;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.hero-label {
  color: var(--color-hero-text-secondary);
  font-size: 13px;
  font-weight: 500;
}

.hero-amount {
  position: relative;
  z-index: 1;
  margin: 8px 0 4px;
  font-size: 50px;
  font-variant-numeric: tabular-nums;
  font-weight: 800;
  letter-spacing: -0.045em;
  line-height: 1.02;
}

.hero-amount .currency {
  margin-right: 1px;
  font-size: 23px;
  font-weight: 700;
  vertical-align: 7px;
}

.hero-amount .fraction {
  color: var(--color-hero-text-muted);
  font-size: 23px;
  font-weight: 700;
}

.hero-gap {
  position: relative;
  z-index: 1;
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.hero-gap b {
  color: var(--color-hero-line);
  font-weight: 700;
}

.hero-stats {
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: 1fr 1px 1fr;
  gap: 14px;
  align-items: center;
  margin-top: 18px;
}

.stat {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat.right {
  text-align: right;
}

.stat span {
  color: var(--color-hero-text-secondary);
  font-size: 11px;
  font-weight: 500;
}

.stat b {
  font-size: 28px;
  font-variant-numeric: tabular-nums;
  font-weight: 800;
  letter-spacing: -0.03em;
  line-height: 1.1;
}

.stat b.is-mint {
  color: var(--color-hero-line);
}

.stat b.is-negative {
  color: var(--color-finance-gain-on-hero);
}

.divider {
  width: 1px;
  height: 34px;
  background: rgba(255, 255, 255, 0.18);
}

.eta-line {
  position: relative;
  z-index: 1;
  margin: 6px 0 0;
  color: var(--color-hero-text-secondary);
  font-size: 12px;
}

.projection-note {
  position: relative;
  z-index: 1;
  margin: 4px 0 0;
  color: var(--color-hero-text-secondary);
  font-size: 11px;
  line-height: 1.5;
}

.coverage-note {
  margin: 10px 0 0;
  padding: 10px 12px;
  border-radius: var(--radius-control);
  background: var(--color-warning-bg);
  color: var(--color-warning);
  font-size: 12px;
  line-height: 1.5;
}

.coverage-note a {
  color: inherit;
  font-weight: 600;
}

.set-goal {
  width: 100%;
  min-height: 48px;
  margin-top: 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-size: 15px;
  font-weight: 600;
}

.capital-card {
  margin-top: 14px;
  padding: 17px;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.reached-card h2 {
  margin: 0;
  font-size: var(--text-headline);
  font-weight: 700;
}

.reached-card p {
  margin: 6px 0 0;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.state {
  padding: 24px 0;
  color: var(--color-text-secondary);
  font-size: 13px;
  text-align: center;
}

.state-link {
  display: inline-flex;
  align-items: center;
  min-height: 44px;
  color: var(--color-brand);
  font-weight: 600;
  text-decoration: none;
}

.form-error {
  margin: 0;
  color: var(--color-error);
  font-size: 12px;
}

.change-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.change-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 0;
  border-bottom: 1px solid var(--color-border-subtle);
  color: var(--color-text-secondary);
  font-size: 13px;
}

.change-row b {
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.sheet-note {
  margin: 8px 0 0;
  color: var(--color-text-secondary);
  font-size: 12px;
  line-height: 1.5;
}
</style>
