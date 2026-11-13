<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { fetchAssetTrend, fetchDayChange, fetchMonthChange } from '../api/ledger'
import { refreshQuotes } from '../api/quotes'
import { getApiErrorMessage } from '../api/client'
import { toast } from '../composables/useMessage'
import AppIcon from '../components/AppIcon.vue'
import AppNav from '../components/AppNav.vue'
import AnimatedMoney from '../components/AnimatedMoney.vue'
import AssetDistribution from '../components/AssetDistribution.vue'
import AssetTrendChart from '../components/AssetTrendChart.vue'
import BottomSheet from '../components/BottomSheet.vue'
import LoadingState from '../components/LoadingState.vue'
import LoadingButton from '../components/LoadingButton.vue'
import { useHomePreferencesStore } from '../stores/homePreferences'
import { useLedgerStore } from '../stores/ledger'
import { amountTone, formatBusinessDate, formatMaskedMoney, formatMoney, formatSignedMoney, todayInBusinessTimeZone } from '../utils/format'
import { useCountUp } from '../utils/countUp'
import { buildAssetTrend, trendAmountLabel } from '../utils/trend'
import type { MonthChange } from '../types/ledger'
import type { AssetTrend, AssetTrendPoint, TrendRange } from '../types/trend'

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
const { amountsMasked } = storeToRefs(useHomePreferencesStore())
const trendSelection = ref<AssetTrendPoint | null>(null)
const trendLoading = ref(false)
const trendError = ref('')
const dayChange = ref<MonthChange | null>(null)
const monthChange = ref<MonthChange | null>(null)
const changeScope = ref<'day' | 'month'>('day')
const dayChangeError = ref('')
const monthChangeError = ref('')
const changeSheetOpen = ref(false)
const quoteRefreshing = ref(false)
const quoteError = ref('')

const monthFlow = computed(() => {
  const { income = 0, expense = 0 } = overview.value?.month ?? {}
  const total = income + expense
  const dominant = total === 0 ? undefined
    : income >= expense * 4 ? 'income'
    : expense >= income * 4 ? 'expense' : undefined

  return {
    incomeWidth: total > 0 ? `${income / total * 100}%` : '0%',
    expenseWidth: total > 0 ? `${expense / total * 100}%` : '0%',
    dominant
  }
})

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
const assetTrendSeries = computed(() => trend.value ? buildAssetTrend(trend.value) : null)
const selectedPoint = computed(() => {
  const series = assetTrendSeries.value

  if (!series) {
    return null
  }

  const date = trendSelection.value?.date

  return series.points.find((point) => point.date === date) ?? series.latest
})
const activeChange = computed(() => changeScope.value === 'day' ? dayChange.value : monthChange.value)
const activeChangeError = computed(() =>
  changeScope.value === 'day' ? dayChangeError.value : monthChangeError.value)
const changeSheetTitle = computed(() => changeScope.value === 'day' ? '当日变化构成' : '本月变化构成')
const addedRecords = computed(() => activeChange.value?.addedRecords ?? 0)
const changePercent = computed(() => {
  const change = activeChange.value

  if (!change?.hasComparableData || change.comparableChange === null || !change.comparableBase) {
    return null
  }

  return change.comparableChange / change.comparableBase * 100
})

function daysBetween(from: string, to: string): number {
  const start = Date.parse(`${from}T00:00:00Z`)
  const end = Date.parse(`${to}T00:00:00Z`)

  if (Number.isNaN(start) || Number.isNaN(end)) {
    return 0
  }

  return Math.round((end - start) / 86_400_000)
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
  const detailPromise = Promise.all([loadTrend(trendRange.value), loadChanges()])
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

async function loadChanges(): Promise<void> {
  await Promise.all([loadDayChange(), loadMonthChange()])
}

async function loadDayChange(): Promise<void> {
  dayChangeError.value = ''

  try {
    dayChange.value = await fetchDayChange()
  } catch {
    dayChange.value = null
    dayChangeError.value = '当日变化暂时无法读取，请稍后重试。'
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

async function syncQuotes(): Promise<void> {
  if (quoteRefreshing.value) {
    return
  }

  quoteRefreshing.value = true
  quoteError.value = ''

  try {
    const run = await refreshQuotes('all')
    const failed = run.fundFailed + run.goldFailed + run.stockFailed
    const succeeded = run.fundSucceeded + run.goldSucceeded + run.stockSucceeded

    const [status] = await Promise.all([ledgerStore.load(), loadTrend(trendRange.value), loadChanges()])
    if (status !== 'ready' || trendError.value || dayChangeError.value || monthChangeError.value) {
      quoteError.value = failed > 0
        ? `行情刷新失败 ${failed} 项，且页面数据未能完整重新加载。`
        : '行情请求已完成，但页面数据未能完整重新加载。'
      return
    }

    if (failed > 0) toast.info(`行情刷新结果：成功 ${succeeded} 项，失败 ${failed} 项。`)
    else if (succeeded === 0) toast.info('暂无可刷新的行情。')
    else if (run.updated === 0) toast.info('行情已是最新。')
    else toast.success(`行情已更新：更新 ${run.updated} 项。`)
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

      <p v-if="quoteError" class="sync-note error" role="alert">{{ quoteError }}</p>

      <LoadingState v-if="isLoading && !overview" variant="hero" label="正在读取账本" />

      <template v-if="isLoading && !overview">
        <section class="month-flow">
          <LoadingState variant="text" :rows="3" compact label="正在读取本月收支" />
        </section>
        <LoadingState variant="list" :rows="3" compact label="正在读取资产分布" />
      </template>

      <section v-else-if="errorMessage" class="hero-error">
        <p>{{ errorMessage }}</p>
        <LoadingButton class="hero-reload" :loading="isLoading" loading-label="加载中…" @click="loadLedger">重新加载</LoadingButton>
      </section>

      <template v-else-if="overview">
        <div class="hero-amount">
          <p class="hero-label">
            {{ overview.coverageComplete ? '总资产' : '已记录资产' }}
            <span class="tag">{{ overview.currency }}</span>
            <button
              type="button"
              class="hero-visibility"
              :aria-label="amountsMasked ? '显示资产金额' : '隐藏资产金额'"
              @click="amountsMasked = !amountsMasked"
            >
              <AppIcon :name="amountsMasked ? 'eye-off' : 'eye'" />
            </button>
          </p>
          <h2 class="hero-value">
            <template v-if="amountsMasked">
              <span class="currency">¥</span>{{ formatMaskedMoney(overview.totalAssets ?? 0).replace('¥', '') }}
            </template>
            <template v-else>
              <span class="currency">¥</span>{{ totalAmountParts.integer }}<span class="fraction">.{{ totalAmountParts.fraction }}</span>
            </template>
          </h2>

          <div class="hero-change-row">
            <button
              v-if="activeChange?.hasComparableData"
              type="button"
              class="hero-change"
              :aria-expanded="changeSheetOpen"
              @click="changeSheetOpen = true"
            >
              <span class="change-arrow" aria-hidden="true" :class="amountTone(activeChange.comparableChange)">{{ (activeChange.comparableChange ?? 0) > 0 ? '▲' : (activeChange.comparableChange ?? 0) < 0 ? '▼' : '—' }}</span>
              <b :class="amountTone(activeChange.comparableChange)">
                {{ amountsMasked ? '••••' : formatSignedMoney(activeChange.comparableChange ?? 0) }}
              </b>
              <span v-if="changePercent !== null" class="change-percent" :class="amountTone(changePercent)">
                {{ amountsMasked ? '••••' : percentLabel(changePercent) }}
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
              <span>{{ changeScope === 'day' ? '当日变化' : '本月变化' }}</span>
              <b>暂无可比数据</b>
              <span class="info-mark">ⓘ</span>
            </button>
            <div class="change-scope" role="group" aria-label="资产变化范围">
              <button
                type="button"
                :class="{ active: changeScope === 'day' }"
                :aria-pressed="changeScope === 'day'"
                @click="changeScope = 'day'"
              >当日</button>
              <button
                type="button"
                :class="{ active: changeScope === 'month' }"
                :aria-pressed="changeScope === 'month'"
                @click="changeScope = 'month'"
              >本月</button>
            </div>
          </div>
        </div>

        <section class="trend-panel">
          <h2 class="sr-only">资产趋势</h2>

          <template v-if="trendReady">
            <div class="chart-readout">
              <span>{{ selectedPoint ? formatBusinessDate(selectedPoint.date) : '按日重算' }}</span>
              <b :class="{ faint: !selectedPoint || selectedPoint.value === null }">
                {{ selectedPoint?.value != null && selectedPoint.source.totalAssets === null ? '已记录资产 ' : '' }}{{ trendAmountLabel(selectedPoint, amountsMasked) }}
              </b>
            </div>
            <LoadingState v-if="trendLoading && !trend" variant="chart" compact label="正在读取趋势" />
            <p v-else-if="trendError" class="hero-hint">{{ trendError }}</p>
            <AssetTrendChart
              v-else-if="trend && assetTrendSeries"
              :trend="trend"
              variant="hero"
              :amounts-masked="amountsMasked"
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

    <p v-if="activeChangeError" class="home-error">{{ activeChangeError }}</p>

    <template v-if="!errorMessage && overview">
      <section class="month-flow" aria-label="本月收支">
        <div class="month-flow-values">
          <div>
            <span class="metric-label"><i class="metric-dot income" />本月收入</span>
            <b class="metric-value"><AnimatedMoney :value="overview.month.income" /></b>
          </div>
          <div>
            <span class="metric-label"><i class="metric-dot expense" />本月支出</span>
            <b class="metric-value"><AnimatedMoney :value="overview.month.expense" /></b>
          </div>
        </div>
        <div class="month-flow-track" :data-dominant="monthFlow.dominant" aria-hidden="true">
          <span class="month-flow-segment income" :style="{ width: monthFlow.incomeWidth }" />
          <span class="month-flow-segment expense" :style="{ width: monthFlow.expenseWidth }" />
        </div>
        <p class="month-surplus">
          <span>本月结余</span>
          <b><AnimatedMoney :value="overview.month.surplus" signed /></b>
        </p>
        <p v-if="!overview.month.hasRecords" class="month-empty">
          本月还没有收支记录。<RouterLink class="text-btn" :to="{ name: 'entry' }">去记一笔 ›</RouterLink>
        </p>
      </section>

      <AssetDistribution
        :overview="overview"
        :changes="activeChange?.assetChanges"
        :comparable="activeChange?.hasComparableData ?? false"
        :change-scope="changeScope"
        cards
        :amounts-masked="amountsMasked"
        hide-fund-count
      />
    </template>

    <AppNav />

    <BottomSheet :open="changeSheetOpen" :title="changeSheetTitle" @close="changeSheetOpen = false">
      <div v-if="activeChange" class="breakdown">
        <template v-if="activeChange.hasComparableData">
          <div class="breakdown-row">
            <span>市值变动</span>
            <b :class="amountTone(activeChange.marketValueChange)">{{ amountsMasked ? '••••' : formatSignedMoney(activeChange.marketValueChange ?? 0) }}</b>
          </div>
          <div class="breakdown-row">
            <span>净存入·转账</span>
            <b :class="amountTone(activeChange.netDeposit)">{{ amountsMasked ? '••••' : formatSignedMoney(activeChange.netDeposit ?? 0) }}</b>
          </div>
          <div class="breakdown-total">
            <span>{{ changeScope === 'day' ? '当日变化' : '本月变化' }}</span>
            <b :class="amountTone(activeChange.comparableChange)">{{ amountsMasked ? '••••' : formatSignedMoney(activeChange.comparableChange ?? 0) }}</b>
          </div>
        </template>
        <p v-else class="hint">
          {{ changeScope === 'day'
            ? '账本从今天开始，暂无可比基线。记录满一天后即可看到当日变化。'
            : '账本起始日晚于本月月初，暂无可比基线。记录满一个完整月份后即可看到同口径变化。' }}
        </p>
        <div v-if="addedRecords > 0" class="breakdown-row new-record">
          <span>新增记录（不计入变化）</span>
          <b>{{ amountsMasked ? '••••' : formatSignedMoney(addedRecords) }}</b>
        </div>
        <p class="sheet-note">
          {{ changeScope === 'day'
            ? '只比较昨天和今天都已记录的类别；现金和公积金只叫余额变化，基金和贵金属的市值变化才叫收益。'
            : '只比较月初和今天都已记录的类别；现金和公积金只叫余额变化，基金和贵金属的市值变化才叫收益。' }}
        </p>
      </div>
      <p v-else class="hint">{{ activeChangeError || '资产变化暂时无法读取。' }}</p>
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
  background: var(--color-hero-note-bg);
  color: var(--color-hero-note-text);
  font-size: var(--text-caption);
  line-height: 1.6;
}

.sync-note.error {
  background: var(--color-hero-note-error-bg);
  color: var(--color-hero-note-error-text);
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

.hero-visibility {
  display: grid;
  place-items: center;
  width: var(--touch-target);
  height: var(--touch-target);
  margin-block: -8px;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: transparent;
  color: var(--color-hero-text-secondary);
  cursor: pointer;
}

.hero-visibility .app-icon {
  width: 18px;
  height: 18px;
}

.tag {
  padding: 2px 7px;
  border-radius: var(--radius-pill);
  background: var(--color-hero-chip);
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
  background: var(--color-hero-chip);
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

.change-scope {
  display: inline-flex;
  gap: 2px;
  padding: 3px;
  border-radius: var(--radius-pill);
  background: var(--color-hero-chip);
}

.change-scope button {
  min-height: 30px;
  padding: 0 8px;
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
  font-variant-numeric: tabular-nums;
}

.change-scope button.active {
  background: var(--color-hero-active);
  color: var(--color-hero-active-text);
  font-weight: 600;
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
  background: var(--color-hero-chip);
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

.month-flow {
  margin-bottom: 18px;
  padding: var(--card-padding);
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.month-flow-values {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.month-flow-values > :last-child {
  text-align: right;
}

.month-flow-values > :last-child .metric-label {
  justify-content: flex-end;
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
  background: var(--color-month-flow-income);
}

.metric-dot.expense {
  background: var(--color-month-flow-expense);
}

.metric-value {
  display: block;
  margin-top: 10px;
  color: var(--color-text-primary);
  font-size: clamp(1rem, 5.4vw, 1.5rem);
  font-weight: 700;
  letter-spacing: -0.04em;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
  overflow-wrap: anywhere;
}

.month-flow-track {
  display: flex;
  align-items: center;
  justify-content: space-between;
  overflow: hidden;
  height: 16px;
  margin: 14px 0 12px;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
}

.month-flow-segment {
  flex: none;
  height: 100%;
  transition: width var(--motion-sheet) var(--ease-standard), height var(--motion-fast) ease;
}

.month-flow-segment.income {
  background: var(--color-month-flow-income);
}

.month-flow-segment.expense {
  background: var(--color-month-flow-expense);
}

.month-flow-track[data-dominant] .month-flow-segment {
  height: 6px;
  border-radius: var(--radius-pill);
}

.month-flow-track[data-dominant='income'] .income,
.month-flow-track[data-dominant='expense'] .expense {
  height: 100%;
}

@media (prefers-reduced-motion: reduce) {
  .month-flow-segment {
    transition: none;
  }
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
  margin: 8px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
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
