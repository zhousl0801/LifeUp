<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { fetchAssetTrend, fetchMonthChange } from '../api/ledger'
import { fetchPlanOverview } from '../api/plans'
import { refreshQuotes } from '../api/quotes'
import { getApiErrorMessage } from '../api/client'
import AppIcon from '../components/AppIcon.vue'
import AppNav from '../components/AppNav.vue'
import AnimatedMoney from '../components/AnimatedMoney.vue'
import AssetDistribution from '../components/AssetDistribution.vue'
import AssetTrendChart from '../components/AssetTrendChart.vue'
import BottomSheet from '../components/BottomSheet.vue'
import EmptyState from '../components/EmptyState.vue'
import LoadingState from '../components/LoadingState.vue'
import { useLedgerStore } from '../stores/ledger'
import { amountTone, formatBusinessDate, formatMoney, formatSignedMoney, sumAmounts, todayInBusinessTimeZone } from '../utils/format'
import { useCountUp } from '../utils/countUp'
import { buildScopedTrend } from '../utils/trend'
import type { MonthChange } from '../types/ledger'
import type { PlanCycle, PlanOverview, PlanType } from '../types/plan'
import type { AssetTrend, AssetTrendPoint, TrendRange } from '../types/trend'

interface PlanPreviewRow {
  key: string
  date: string
  title: string
  cycleLabel: string
  amount: number
  pending: boolean
  hint?: string
}

const planTypeOrder: PlanType[] = ['fundSip', 'providentDeposit', 'providentWithdrawal', 'salaryIncome']
const planTypeLabels: Record<PlanType, string> = {
  fundSip: '基金定投',
  providentDeposit: '公积金缴存',
  providentWithdrawal: '公积金提取',
  salaryIncome: '定期收入'
}

const rangeOptions: { value: TrendRange; label: string }[] = [
  { value: 'week', label: '7日' },
  { value: 'month', label: '1月' },
  { value: 'quarter', label: '3月' },
  { value: 'all', label: '全部' }
]

const router = useRouter()
const ledgerStore = useLedgerStore()
const { overview, isLoading, errorMessage } = storeToRefs(ledgerStore)
const today = todayInBusinessTimeZone()
const trend = ref<AssetTrend | null>(null)
const trendRange = ref<TrendRange>('week')
const trendSelection = ref<AssetTrendPoint | null>(null)
const trendLoading = ref(false)
const trendError = ref('')
const monthChange = ref<MonthChange | null>(null)
const monthChangeError = ref('')
const planOverview = ref<PlanOverview | null>(null)
const planError = ref('')
const planLoading = ref(false)
const changeSheetOpen = ref(false)
const quoteRefreshing = ref(false)
const quoteNotice = ref('')
const quoteError = ref('')

const daysRecorded = computed(() => {
  if (!overview.value) {
    return 0
  }

  return Math.max(0, daysBetween(overview.value.startDate, today))
})

const trendReady = computed(() => daysRecorded.value >= 7)
const trendProgress = computed(() => Math.min(100, Math.round(daysRecorded.value / 7 * 100)))
const updateDate = computed(() => trend.value?.to ?? today)
const animatedTotal = useCountUp(computed(() => overview.value?.totalAssets ?? null))
const totalAmountParts = computed(() => {
  const [integer, fraction = '00'] = formatMoney(animatedTotal.value ?? 0).split('.')
  return { integer: integer ?? '0', fraction }
})
const scopedTrend = computed(() => trend.value ? buildScopedTrend(trend.value) : null)
const selectedPoint = computed(() => {
  const scoped = scopedTrend.value

  if (!scoped) {
    return null
  }

  const date = trendSelection.value?.date

  return scoped.points.find((point) => point.date === date) ?? scoped.latest
})
const addedRecords = computed(() => monthChange.value?.addedRecords ?? 0)
const changePercent = computed(() => {
  const change = monthChange.value

  if (!change?.hasComparableData || change.comparableChange === null || !change.comparableBase) {
    return null
  }

  return change.comparableChange / change.comparableBase * 100
})

const planPreview = computed<PlanPreviewRow[]>(() => {
  const data = planOverview.value

  if (!data) {
    return []
  }

  const rows: PlanPreviewRow[] = []

  for (const type of planTypeOrder) {
    const pending = data.pendingOccurrences
      .filter((occurrence) => occurrence.type === type)
      .sort((first, second) => first.dueDate.localeCompare(second.dueDate))
    const plans = data.plans.filter((plan) => plan.type === type && plan.isActive)
    const pendingPlanIds = new Set(pending.map((occurrence) => occurrence.planId))
    const remainingPlans = plans.filter((plan) => !pendingPlanIds.has(plan.id))
    const dates = [
      ...pending.map((occurrence) => occurrence.dueDate),
      ...remainingPlans
        .map((plan) => plan.nextDueDate)
        .filter((date): date is string => date !== null)
    ].sort()

    if (!dates.length && !pending.length) {
      continue
    }

    const cycle = cycleSummary(remainingPlans.map((plan) => plan.cycle))
    let cycleLabel: string

    if (pending.length) {
      cycleLabel = pending.length > 1 ? `${pending.length} 项待处理` : '待处理'
    } else {
      cycleLabel = plans.length > 1 ? `${plans.length} 项 · ${cycle}` : cycle
    }

    rows.push({
      key: pending.length ? `pending-${type}` : `plan-${type}`,
      date: dates[0]!,
      title: planTypeLabels[type],
      cycleLabel,
      amount: sumAmounts(pending.map((occurrence) => occurrence.amount))
        + sumAmounts(remainingPlans.map((plan) => plan.amount)),
      pending: pending.length > 0,
      hint: pending.length && remainingPlans.length ? `另有 ${remainingPlans.length} 项待执行` : undefined
    })
  }

  return rows.sort((first, second) => first.date.localeCompare(second.date))
})

function cycleSummary(cycles: (PlanCycle | undefined)[]): string {
  const unique = [...new Set(cycles.filter((cycle): cycle is PlanCycle => cycle !== undefined))]

  if (unique.length === 0) {
    return ''
  }

  if (unique.length > 1) {
    return '多类型'
  }

  switch (unique[0]) {
    case 'monthly':
      return '每月'
    case 'weekly':
      return '每周'
    case 'tradingDay':
      return '交易日'
  }
}

function daysBetween(from: string, to: string): number {
  const start = Date.parse(`${from}T00:00:00Z`)
  const end = Date.parse(`${to}T00:00:00Z`)

  if (Number.isNaN(start) || Number.isNaN(end)) {
    return 0
  }

  return Math.round((end - start) / 86_400_000)
}

function monthLabel(date: string): string {
  return `${Number(date.slice(5, 7))}月`
}

function dayLabel(date: string): string {
  return date.slice(8, 10)
}

function percentLabel(value: number | null): string {
  if (value === null) {
    return ''
  }

  const sign = value > 0 ? '+' : ''

  return `${sign}${value.toFixed(2)}%`
}

function selectPoint(point: AssetTrendPoint): void {
  trendSelection.value = point
}

async function loadLedger(): Promise<void> {
  const statusPromise = ledgerStore.load()
  const detailPromise = Promise.all([loadTrend(trendRange.value), loadMonthChange(), loadPlans()])
  const status = await statusPromise

  if (status === 'missing') {
    await router.replace({ name: 'setup' })
    return
  }

  if (status === 'ready') {
    await detailPromise
  }
}

async function loadTrend(range: TrendRange): Promise<void> {
  trendRange.value = range
  trendSelection.value = null
  trendLoading.value = true
  trendError.value = ''

  try {
    trend.value = await fetchAssetTrend(range)
  } catch {
    trend.value = null
    trendError.value = '趋势暂时无法读取，请稍后重试。'
  } finally {
    trendSelection.value = null
    trendLoading.value = false
  }
}

async function loadMonthChange(): Promise<void> {
  monthChangeError.value = ''

  try {
    monthChange.value = await fetchMonthChange()
  } catch {
    monthChange.value = null
    monthChangeError.value = '本月变化暂时无法读取，请稍后重试。'
  }
}

async function loadPlans(): Promise<void> {
  planError.value = ''
  planLoading.value = true

  try {
    planOverview.value = await fetchPlanOverview()
  } catch {
    planOverview.value = null
    planError.value = '自动记账计划暂时无法读取，请稍后重试。'
  } finally {
    planLoading.value = false
  }
}

async function syncQuotes(): Promise<void> {
  if (quoteRefreshing.value) {
    return
  }

  quoteRefreshing.value = true
  quoteNotice.value = ''
  quoteError.value = ''

  try {
    const run = await refreshQuotes('all')
    const failed = run.fundFailed + run.goldFailed + run.stockFailed
    const succeeded = run.fundSucceeded + run.goldSucceeded + run.stockSucceeded

    quoteNotice.value = failed > 0
      ? `行情同步完成：成功 ${succeeded} 项，失败 ${failed} 项。`
      : `行情已同步：更新 ${run.updated} 项。`

    await Promise.all([ledgerStore.load(), loadTrend(trendRange.value), loadMonthChange()])
  } catch (error) {
    quoteError.value = getApiErrorMessage(error, '行情同步失败，请稍后重试。')
  } finally {
    quoteRefreshing.value = false
  }
}

onMounted(() => {
  const statusBar = document.querySelector<HTMLMetaElement>('meta[name="apple-mobile-web-app-status-bar-style"]')

  if (statusBar) {
    statusBar.dataset.previousContent = statusBar.getAttribute('content') ?? 'default'
    statusBar.setAttribute('content', 'black-translucent')
  }

  void loadLedger()
})

onBeforeUnmount(() => {
  const statusBar = document.querySelector<HTMLMetaElement>('meta[name="apple-mobile-web-app-status-bar-style"]')

  if (statusBar) {
    statusBar.setAttribute('content', statusBar.dataset.previousContent ?? 'default')
    delete statusBar.dataset.previousContent
  }
})
</script>

<template>
  <main class="page home-page with-nav">
    <section class="home-hero">
      <header class="hero-top">
        <div class="hero-brand">
          <p class="brand">资产管家</p>
          <h1>总览</h1>
        </div>
        <div class="hero-actions">
          <span class="hero-date">{{ formatBusinessDate(updateDate) }}</span>
          <button
            type="button"
            class="sync-button"
            :disabled="quoteRefreshing"
            :aria-label="quoteRefreshing ? '正在同步行情' : '同步行情'"
            :title="quoteRefreshing ? '正在同步行情' : '同步行情'"
            @click="syncQuotes"
          >
            <AppIcon name="refresh" :class="{ spinning: quoteRefreshing }" />
          </button>
        </div>
      </header>

      <p v-if="quoteNotice" class="sync-note" role="status">{{ quoteNotice }}</p>
      <p v-else-if="quoteError" class="sync-note error" role="alert">{{ quoteError }}</p>

      <LoadingState v-if="isLoading" variant="hero" label="正在读取账本" />

      <template v-if="isLoading">
        <section class="metric-grid">
          <article class="metric-card"><LoadingState variant="text" compact label="正在读取本月收入" /></article>
          <article class="metric-card"><LoadingState variant="text" compact label="正在读取本月支出" /></article>
        </section>
        <LoadingState variant="list" :rows="3" compact label="正在读取资产分布" />
        <LoadingState variant="list" :rows="3" compact label="正在读取自动记账" />
      </template>

      <section v-else-if="errorMessage" class="hero-error">
        <p>{{ errorMessage }}</p>
        <button class="hero-reload" type="button" @click="loadLedger">重新加载</button>
      </section>

      <template v-else-if="overview">
        <div class="hero-amount">
          <p class="hero-label">
            {{ overview.coverageComplete ? '总资产' : '已记录资产' }}
            <span class="tag">{{ overview.currency }}</span>
          </p>
          <h2 class="hero-value">
            <span class="currency">¥</span>{{ totalAmountParts.integer }}<span class="fraction">.{{ totalAmountParts.fraction }}</span>
          </h2>

          <div class="hero-change-row">
            <button
              v-if="monthChange?.hasComparableData"
              type="button"
              class="hero-change"
              :aria-expanded="changeSheetOpen"
              @click="changeSheetOpen = true"
            >
              <span class="change-arrow" :class="amountTone(monthChange.comparableChange)">▲</span>
              <b :class="amountTone(monthChange.comparableChange)">
                {{ formatSignedMoney(monthChange.comparableChange ?? 0) }}
              </b>
              <span v-if="changePercent !== null" class="change-percent" :class="amountTone(changePercent)">
                {{ percentLabel(changePercent) }}
              </span>
              <span class="info-mark">ⓘ</span>
            </button>
            <button
              v-else
              type="button"
              class="hero-change muted"
              :aria-expanded="changeSheetOpen"
              @click="changeSheetOpen = true"
            >
              <span>本月变化</span>
              <b>暂无可比数据</b>
              <span class="info-mark">ⓘ</span>
            </button>
            <span class="change-caption">本月</span>
          </div>
        </div>

        <section class="trend-panel">
          <h2 class="sr-only">资产趋势</h2>

          <template v-if="trendReady">
            <div class="chart-readout">
              <span>{{ selectedPoint ? formatBusinessDate(selectedPoint.date) : '按日重算' }}</span>
              <b :class="{ faint: !selectedPoint || selectedPoint.value === null }">
                {{ selectedPoint?.value == null ? '依据不足' : `¥${formatMoney(selectedPoint.value)}` }}
              </b>
            </div>
            <LoadingState v-if="trendLoading && !trend" variant="chart" compact label="正在读取趋势" />
            <p v-else-if="trendError" class="hero-hint">{{ trendError }}</p>
            <AssetTrendChart
              v-else-if="trend && scopedTrend"
              :trend="trend"
              variant="hero"
              @select="selectPoint"
            />
            <p v-else class="hero-hint">还没有可展示的趋势数据。</p>
          </template>

          <div v-else class="trend-wait">
            <p>记录满 7 天后显示趋势</p>
            <div class="progress" role="progressbar" :aria-valuenow="daysRecorded" aria-valuemin="0" aria-valuemax="7">
              <span :style="{ width: `${trendProgress}%` }" />
            </div>
            <small>已记录 {{ Math.min(daysRecorded, 7) }}/7 天</small>
          </div>

          <div class="hero-range" role="tablist" aria-label="趋势范围">
            <button
              v-for="option in rangeOptions"
              :key="option.value"
              type="button"
              class="range-button"
              :class="{ active: trendRange === option.value }"
              :disabled="trendLoading"
              @click="loadTrend(option.value)"
            >
              {{ option.label }}
            </button>
          </div>
        </section>
      </template>
    </section>

    <p v-if="monthChangeError" class="home-error">{{ monthChangeError }}</p>

    <template v-if="!isLoading && !errorMessage && overview">
      <section class="metric-grid">
        <article class="metric-card">
          <span class="metric-label"><i class="metric-dot income" />本月收入</span>
          <b class="metric-value"><AnimatedMoney :value="overview.month.income" /></b>
        </article>
        <article class="metric-card">
          <span class="metric-label"><i class="metric-dot expense" />本月支出</span>
          <b class="metric-value"><AnimatedMoney :value="overview.month.expense" /></b>
        </article>
      </section>

      <div class="month-summary">
        <p class="month-surplus">
          <span>本月结余</span>
          <b :class="amountTone(overview.month.hasRecords ? overview.month.surplus : null)">
            <AnimatedMoney :value="overview.month.surplus" signed tone />
          </b>
        </p>
        <RouterLink class="text-btn" :to="{ name: 'ledger' }">查看账本 ›</RouterLink>
      </div>
      <p v-if="!overview.month.hasRecords" class="month-empty">
        本月还没有收支记录。<RouterLink class="text-btn" :to="{ name: 'entry' }">去记一笔 ›</RouterLink>
      </p>

      <AssetDistribution
        :overview="overview"
        :changes="monthChange?.assetChanges"
        :comparable="monthChange?.hasComparableData ?? false"
        manage-to="/assets"
        hide-fund-count
      />

      <div class="section-head">
        <h2>自动记账</h2>
        <RouterLink v-if="planPreview.length" class="text-btn" to="/plans">管理计划 ›</RouterLink>
      </div>
      <div v-if="planOverview" class="plan-box">
        <RouterLink
          v-for="(row, index) in planPreview"
          :key="row.key"
          class="plan-row stagger-item"
          :style="{ '--stagger': Math.min(index, 8) }"
          to="/plans"
        >
          <span class="date-box">
            <small>{{ monthLabel(row.date) }}</small>
            <b>{{ dayLabel(row.date) }}</b>
          </span>
          <span class="plan-main">
            <span class="plan-title-row">
              <b>{{ row.title }}</b>
              <em v-if="row.cycleLabel" class="plan-cycle" :class="{ pending: row.pending }">{{ row.cycleLabel }}</em>
            </span>
            <small v-if="row.hint" class="plan-hint">{{ row.hint }}</small>
          </span>
          <span class="plan-amount"><AnimatedMoney :value="row.amount" /></span>
        </RouterLink>
        <EmptyState v-if="!planPreview.length" compact icon="clock" title="让固定收支自动记录" description="工资、缴存和定投，到期后按计划记账。" action-label="设置自动记账" @action="router.push('/plans')" />
      </div>
      <LoadingState v-else-if="planLoading" variant="list" :rows="3" compact label="正在读取自动记账" />
      <p v-else-if="planError" class="hint">{{ planError }}</p>
    </template>

    <AppNav />

    <BottomSheet :open="changeSheetOpen" title="本月变化构成" @close="changeSheetOpen = false">
      <div v-if="monthChange" class="breakdown">
        <template v-if="monthChange.hasComparableData">
          <div class="breakdown-row">
            <span>市值变动</span>
            <b :class="amountTone(monthChange.marketValueChange)">{{ formatSignedMoney(monthChange.marketValueChange ?? 0) }}</b>
          </div>
          <div class="breakdown-row">
            <span>净存入·转账</span>
            <b :class="amountTone(monthChange.netDeposit)">{{ formatSignedMoney(monthChange.netDeposit ?? 0) }}</b>
          </div>
          <div class="breakdown-total">
            <span>本月变化</span>
            <b :class="amountTone(monthChange.comparableChange)">{{ formatSignedMoney(monthChange.comparableChange ?? 0) }}</b>
          </div>
        </template>
        <p v-else class="hint">账本起始日晚于本月月初，暂无可比基线。记录满一个完整月份后即可看到同口径变化。</p>
        <div v-if="addedRecords > 0" class="breakdown-row new-record">
          <span>新增记录（不计入变化）</span>
          <b>{{ formatSignedMoney(addedRecords) }}</b>
        </div>
        <p class="sheet-note">只比较月初和今天都已记录的类别；现金和公积金只叫余额变化，基金和积存金的市值变化才叫收益。</p>
      </div>
      <p v-else class="hint">{{ monthChangeError || '本月变化暂时无法读取。' }}</p>
    </BottomSheet>
  </main>
</template>

<style scoped>
.home-page {
  padding-bottom: calc(97px + env(safe-area-inset-bottom));
}

.home-hero {
  margin: calc(max(20px, env(safe-area-inset-top)) * -1) calc(var(--page-gutter) * -1) 14px;
  padding: calc(max(20px, env(safe-area-inset-top)) + 16px) var(--page-gutter) 20px;
  border-radius: 0 0 28px 28px;
  background: linear-gradient(180deg, var(--color-hero-bg) 0%, var(--color-hero-bg-end) 100%);
  box-shadow: var(--shadow-hero);
  color: var(--color-hero-text);
}

.hero-top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
}

.brand {
  margin: 0 0 3px;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
  font-weight: 500;
  letter-spacing: 0.2px;
}

.hero-brand h1 {
  margin: 0;
  color: var(--color-hero-text);
  font-size: 1.5rem;
  font-weight: 700;
  letter-spacing: -0.5px;
  line-height: 1.15;
}

.hero-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.hero-date {
  padding-top: 4px;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.sync-button {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  width: var(--touch-target);
  height: var(--touch-target);
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: var(--color-hero-surface);
  color: var(--color-hero-text);
  transition: background var(--motion-fast) ease, transform var(--motion-fast) ease;
}

.sync-button:hover:not(:disabled) {
  background: var(--color-hero-surface-strong);
}

.sync-button:active:not(:disabled) {
  transform: scale(.94);
}

.sync-button:disabled {
  color: var(--color-hero-text-muted);
}

.sync-button :deep(.app-icon) {
  width: 20px;
  height: 20px;
}

.spinning {
  animation: spin .9s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.sync-note {
  margin: 14px 0 0;
  padding: 9px 12px;
  border-radius: var(--radius-tag);
  background: var(--color-accent-mint-soft);
  color: var(--color-accent-mint);
  font-size: var(--text-caption);
  line-height: 1.6;
}

.sync-note.error {
  background: rgba(255, 122, 122, 0.12);
  color: #FF9B9B;
}

.hero-error {
  padding: 28px 0 8px;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.7;
}

.hero-error p {
  margin: 0 0 14px;
}

.hero-reload {
  min-height: 42px;
  padding: 0 16px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-hero-surface);
  color: var(--color-hero-text);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.hero-amount {
  margin-top: 24px;
}

.hero-label {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.tag {
  padding: 2px 7px;
  border-radius: var(--radius-pill);
  background: var(--color-hero-surface);
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.hero-value {
  display: flex;
  align-items: baseline;
  gap: 2px;
  margin: 10px 0 12px;
  color: var(--color-hero-text);
  font-size: clamp(2.75rem, 13.5vw, 3.5rem);
  font-weight: 700;
  letter-spacing: -0.05em;
  line-height: 1;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.hero-value .currency {
  margin-right: 5px;
  color: var(--color-hero-text-secondary);
  font-size: 1.5rem;
  font-weight: 500;
  letter-spacing: 0;
}

.hero-value .fraction {
  color: var(--color-hero-text-secondary);
  font-size: 1.625rem;
  font-weight: 500;
  letter-spacing: -0.02em;
}

.hero-change-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}

.hero-change {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  min-height: 38px;
  padding: 7px 12px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-hero-surface);
  color: var(--color-hero-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.4;
}

.hero-change b {
  color: var(--color-hero-text);
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.hero-change.muted b {
  color: var(--color-hero-text-secondary);
  font-weight: 500;
}

.change-arrow {
  font-size: 0.625rem;
}

.change-percent {
  font-variant-numeric: tabular-nums;
}

.change-caption {
  color: var(--color-hero-text-muted);
  font-size: var(--text-caption);
}

.info-mark {
  color: var(--color-hero-text-muted);
  font-size: var(--text-footnote);
}

.home-error {
  margin: 0 4px 14px;
  color: var(--color-error);
  font-size: var(--text-caption);
  line-height: 1.6;
}

.trend-panel {
  position: relative;
  margin-top: 10px;
}

.sr-only {
  position: absolute;
  overflow: hidden;
  width: 1px;
  height: 1px;
  margin: -1px;
  padding: 0;
  border: 0;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}

.chart-readout {
  position: absolute;
  top: 0;
  right: 0;
  left: 0;
  z-index: 1;
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
  min-height: 22px;
  margin: 0 1px;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
  pointer-events: none;
}

.chart-readout b {
  color: var(--color-hero-text);
  font-size: 0.8125rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.chart-readout b.faint {
  color: var(--color-hero-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.hero-hint {
  margin: 10px 0 0;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
}

.trend-wait {
  padding: 14px 2px 6px;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-footnote);
}

.trend-wait p {
  margin: 0 0 10px;
  color: var(--color-hero-text);
  font-size: 0.75rem;
  font-weight: 500;
}

.trend-wait small {
  display: block;
  margin-top: 7px;
  font-size: var(--text-caption);
}

.progress {
  overflow: hidden;
  height: 5px;
  border-radius: var(--radius-pill);
  background: var(--color-hero-track);
}

.progress span {
  display: block;
  height: 100%;
  border-radius: var(--radius-pill);
  background: var(--color-hero-line);
  transition: width 0.2s ease;
}

.hero-range {
  display: flex;
  gap: 2px;
  margin-top: 12px;
  padding: 4px;
  border-radius: var(--radius-pill);
  background: var(--color-hero-surface);
}

.range-button {
  flex: 1;
  min-height: 38px;
  padding: 0 6px;
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
  font-weight: 600;
}

.range-button:hover:not(:disabled):not(.active) {
  color: var(--color-hero-text);
}

.range-button.active {
  background: var(--color-hero-active);
  color: var(--color-hero-active-text);
  font-weight: 700;
}

.range-button:disabled {
  opacity: 0.6;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 14px;
}

.metric-card {
  padding: 18px;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.metric-label {
  display: flex;
  align-items: center;
  gap: 7px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 500;
}

.metric-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}

.metric-dot.income {
  background: var(--color-finance-income);
}

.metric-dot.expense {
  background: var(--color-finance-expense);
}

.metric-value {
  display: block;
  margin-top: 10px;
  color: var(--color-text-primary);
  font-size: 1.5rem;
  font-weight: 700;
  letter-spacing: -0.04em;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}

.month-summary {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  margin: 0 4px 18px;
}

.month-surplus {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.month-surplus b {
  color: var(--color-text-primary);
  font-size: 1rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.month-empty {
  margin: -8px 4px 18px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.section-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  margin: 24px 0 12px;
}

.section-head h2 {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
  letter-spacing: -0.3px;
}

.text-btn {
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-footnote);
  font-weight: 600;
  text-decoration: none;
}

.text-btn:hover {
  color: var(--color-brand-hover);
}

.plan-box {
  overflow: hidden;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.plan-row {
  display: flex;
  align-items: center;
  gap: 12px;
  box-sizing: border-box;
  padding: 16px;
  color: inherit;
  text-decoration: none;
}

.plan-row + .plan-row {
  border-top: 1px solid var(--color-border-subtle);
}

.plan-row:hover {
  background: var(--color-bg-hover);
}

.date-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-width: 44px;
  padding: 8px;
  border-radius: var(--radius-control);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: var(--text-caption);
}

.date-box b {
  margin-top: 3px;
  font-size: 0.9375rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.plan-main {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

.plan-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.plan-main b {
  overflow: hidden;
  font-size: 0.875rem;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.plan-cycle {
  flex: 0 0 auto;
  padding: 2px 7px;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 0.6875rem;
  font-style: normal;
  font-weight: 600;
  line-height: 1.4;
  white-space: nowrap;
}

.plan-cycle.pending {
  background: var(--color-error-bg);
  color: var(--color-error);
}

.plan-hint {
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.4;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.plan-amount {
  color: var(--color-text-primary);
  font-size: 0.875rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.hint {
  margin: 12px 0 0;
  padding-top: 11px;
  border-top: 1px solid var(--color-border-subtle);
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
}

.breakdown {
  display: grid;
  gap: 10px;
}

.breakdown-row,
.breakdown-total {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  color: var(--color-text-secondary);
  font-size: 0.8125rem;
}

.breakdown-row b,
.breakdown-total b {
  color: var(--color-text-primary);
  font-size: 0.875rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.breakdown-total {
  margin-top: 2px;
  padding-top: 12px;
  border-top: 1px solid var(--color-border-subtle);
  color: var(--color-text-primary);
}

.new-record {
  color: var(--color-text-secondary);
}

.new-record b {
  color: var(--color-text-secondary);
  font-weight: 500;
}

.sheet-note {
  margin: 6px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
}
</style>
