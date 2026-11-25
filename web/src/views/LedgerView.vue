<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { onBeforeRouteLeave, useRoute, useRouter } from 'vue-router'
import { fetchEntryPeriod, fetchEntryStats } from '../api/entries'
import { fetchCategories } from '../api/categories'
import { ApiError, getApiErrorMessage } from '../api/client'
import { useAmountPrivacy } from '../composables/useAmountPrivacy'
import { toast } from '../composables/useMessage'
import AppNav from '../components/AppNav.vue'
import AppIcon from '../components/AppIcon.vue'
import CategoryIcon from '../components/CategoryIcon.vue'
import EmptyState from '../components/EmptyState.vue'
import LoadingState from '../components/LoadingState.vue'
import LoadingButton from '../components/LoadingButton.vue'
import LedgerCalendar from '../components/LedgerCalendar.vue'
import LedgerMonthlyBudget from '../components/LedgerMonthlyBudget.vue'
import LedgerStatsPanel from '../components/LedgerStatsPanel.vue'
import type { StatsChartMode } from '../components/LedgerStatsChart.vue'
import { ExpenseCategories, IncomeCategories } from '../constants/categories'
import type { EntryCategory } from '../types/category'
import { formatBusinessDateWithWeekday, formatMoney, sumAmounts, todayInBusinessTimeZone } from '../utils/format'
import { takeLedgerReturn } from '../utils/ledgerReturn'
import { formatPeriodLabel, periodStart, shiftAnchor } from '../utils/period'
import type { EntryKind, EntryPeriod, EntryPeriodSummary, EntryStats, LedgerEntry } from '../types/entry'

interface LedgerContext {
  period: EntryPeriod
  anchor: string
  view: 'calendar' | 'list' | 'chart'
  selectedDate: string
  kind: 'all' | EntryKind
  category: string
  search: string
  statsKind: StatsChartMode
  statsByGroup: boolean
  page: number
  scrollTop: number
  visibleRow?: { id: string; top: number }
}

const route = useRoute()
const router = useRouter()
const { amountsMasked, maskMoney } = useAmountPrivacy()
const savedContext = window.history.state?.ledgerContext as LedgerContext | undefined
const ledgerChange = takeLedgerReturn()
function ledgerDate(value: unknown): string | null {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : null
}
const today = todayInBusinessTimeZone()
const periodOptions: Array<{ value: EntryPeriod; label: string }> = [
  { value: 'day', label: '日' },
  { value: 'month', label: '月' },
  { value: 'year', label: '年' }
]
const chartPeriodOptions: Array<{ value: EntryPeriod; label: string }> = [
  { value: 'month', label: '月' },
  { value: 'year', label: '年' }
]

const initialView = savedContext?.view ?? 'calendar'
const period = ref<EntryPeriod>(
  initialView === 'chart' && savedContext?.period === 'day'
    ? 'month'
    : (savedContext?.period ?? 'month')
)
const anchor = ref(savedContext?.anchor ?? ledgerDate(route.query.date) ?? today)
const view = ref<'calendar' | 'list' | 'chart'>(initialView)
const selectedDate = ref(savedContext?.selectedDate ?? anchor.value)
const displayExpense = computed(() =>
  period.value === 'month' || view.value !== 'list' ? stats.value?.expense ?? null : summary.value?.expense ?? null)
const statsKind = ref<StatsChartMode>(savedContext?.statsKind ?? 'expense')
const statsByGroup = ref(savedContext?.statsByGroup ?? false)
const stats = ref<EntryStats | null>(null)
const statsLoading = ref(false)
const statsError = ref('')
const kindFilter = ref<'all' | EntryKind>(savedContext?.kind ?? 'all')
const categoryFilter = ref(savedContext?.category ?? '')
const search = ref(savedContext?.search ?? '')
const pageElement = ref<HTMLElement | null>(null)
const highlightedEntry = ref('')
let focusEntry = !savedContext && typeof route.query.focus === 'string' ? route.query.focus : ''
let restorePosition = savedContext
let restorePages = savedContext?.page ?? 1
const summary = ref<EntryPeriodSummary | null>(null)
const categoryItems = ref<EntryCategory[]>([])
const categoryIconsByKind = computed(() => new Map(
  categoryItems.value.filter(category => !category.isGroup)
    .map(category => [`${category.kind}:${category.name}`, category.iconKey])
))
const isLoading = ref(false)
const errorMessage = ref('')
let searchTimer: number | undefined
let loadSequence = 0
let statsSequence = 0

const periodLabel = computed(() => period.value === 'month' && anchor.value.slice(0, 4) === today.slice(0, 4)
  ? `${Number(anchor.value.slice(5, 7))}月`
  : formatPeriodLabel(anchor.value, period.value))
const canGoNext = computed(() =>
  periodStart(anchor.value, period.value) < periodStart(today, period.value))
const categoryOptions = computed(() => {
  const loaded = categoryItems.value
    .filter((category) => !category.isGroup && (kindFilter.value === 'all' || category.kind === kindFilter.value))
    .map((category) => category.name)

  if (loaded.length) {
    return [...new Set(loaded)]
  }

  if (kindFilter.value === 'income') {
    return IncomeCategories
  }

  if (kindFilter.value === 'expense') {
    return ExpenseCategories
  }

  return [...ExpenseCategories, ...IncomeCategories]
})
const hasFilters = computed(() =>
  kindFilter.value !== 'all' || categoryFilter.value !== '' || search.value.trim() !== '')
const groupedEntries = computed(() => {
  const groups = new Map<string, LedgerEntry[]>()

  for (const entry of summary.value?.entries ?? []) {
    const entries = groups.get(entry.businessDate) ?? []
    entries.push(entry)
    groups.set(entry.businessDate, entries)
  }

  return [...groups.entries()].map(([date, entries]) => ({
    date,
    entries,
    income: sumAmounts(entries.filter((entry) => entry.kind === 'income').map(entry => entry.amount)),
    expense: sumAmounts(entries.filter((entry) => entry.kind === 'expense').map(entry => entry.amount))
  }))
})

if (ledgerChange) {
  const { action, entry } = ledgerChange
  if (action === 'reverse') {
    toast.success('账单已撤销')
  } else {
    const matchesPeriod = periodStart(entry.businessDate, period.value) === periodStart(anchor.value, period.value)
      && (view.value !== 'calendar' || selectedDate.value === entry.businessDate)
    if (matchesPeriod && matchesFilters(entry)) {
      highlightedEntry.value = entry.id
    }

    toast.success(action === 'create' ? '已保存' : '已保存修改')
  }
} else if (focusEntry) {
  toast.success('已保存')
}

onBeforeRouteLeave((to) => {
  if (to.name !== 'entry' && to.name !== 'categories') return
  const visibleRow = entryRows().find(row => row.getBoundingClientRect().bottom > 0)
  const context: LedgerContext = {
    period: period.value,
    anchor: anchor.value,
    view: view.value,
    selectedDate: selectedDate.value,
    kind: kindFilter.value,
    category: categoryFilter.value,
    search: search.value,
    statsKind: statsKind.value,
    statsByGroup: statsByGroup.value,
    page: summary.value?.page ?? 1,
    scrollTop: window.scrollY,
    visibleRow: visibleRow?.dataset.entryId
      ? { id: visibleRow.dataset.entryId, top: visibleRow.getBoundingClientRect().top }
      : undefined
  }
  window.history.replaceState({ ...window.history.state, ledgerContext: context }, '')
})

function entryRows(): HTMLElement[] {
  return [...(pageElement.value?.querySelectorAll<HTMLElement>('[data-entry-id]') ?? [])]
}

function matchesFilters(entry: LedgerEntry): boolean {
  const term = search.value.trim().toLowerCase()
  const category = entry.category.toLowerCase()
  return (kindFilter.value === 'all' || kindFilter.value === entry.kind)
    && (!categoryFilter.value || categoryFilter.value.toLowerCase() === category)
    && (!term || category.includes(term) || Boolean(entry.note?.toLowerCase().includes(term)))
}

watch(() => route.query, (query, previous) => {
  if (route.name !== 'ledger' || (query.date === previous.date && query.focus === previous.focus)) return

  const nextAnchor = ledgerDate(query.date) ?? today
  const queryChanges = hasFilters.value || period.value !== 'month' || anchor.value !== nextAnchor
    || view.value !== 'calendar' || selectedDate.value !== nextAnchor
  period.value = 'month'
  anchor.value = nextAnchor
  selectedDate.value = nextAnchor
  view.value = 'calendar'
  clearFilters()
  highlightedEntry.value = ''
  focusEntry = typeof query.focus === 'string' ? query.focus : ''
  if (focusEntry) {
    toast.success('已保存')
  }
  if (!queryChanges) void loadEntries()
})

watch([period, anchor, kindFilter, categoryFilter, view, selectedDate], () => {
  window.clearTimeout(searchTimer)
  void loadEntries()
})

watch([period, anchor, view], () => {
  if (period.value === 'month' || view.value !== 'list') {
    void loadStats()
  }
})

watch(search, () => {
  window.clearTimeout(searchTimer)
  searchTimer = window.setTimeout(() => {
    void loadEntries()
  }, 300)
})

onMounted(() => {
  void loadCategories()
  void loadEntries()
  if (period.value === 'month' || view.value !== 'list') void loadStats()
})

function switchView(next: 'calendar' | 'list' | 'chart'): void {
  if (next === 'calendar') {
    period.value = 'month'
    clearFilters()
    if (selectedDate.value.slice(0, 7) !== anchor.value.slice(0, 7)) selectedDate.value = anchor.value
  } else if (next === 'chart' && period.value === 'day') {
    period.value = 'month'
  }
  view.value = next
}

async function loadStats(): Promise<void> {
  const sequence = ++statsSequence
  statsLoading.value = true
  statsError.value = ''

  try {
    const result = await fetchEntryStats(period.value, anchor.value)
    if (sequence === statsSequence) stats.value = result
  } catch (error) {
    if (sequence !== statsSequence) return
    stats.value = null
    statsError.value = getApiErrorMessage(error, '图表数据暂时无法读取，请稍后重试。')
  } finally {
    if (sequence === statsSequence) statsLoading.value = false
  }

  if (sequence === statsSequence && stats.value && view.value === 'chart' && restorePosition?.view === 'chart') {
    const { scrollTop } = restorePosition
    restorePosition = undefined
    await nextTick()
    if (sequence === statsSequence && view.value === 'chart') {
      window.scrollTo({ top: scrollTop })
    }
  }
}

function selectStatsCategory(payload: { kind: EntryKind; category: string }): void {
  kindFilter.value = payload.kind
  categoryFilter.value = payload.category
  view.value = 'list'
}

async function loadCategories(): Promise<void> {
  try {
    categoryItems.value = await fetchCategories()
  } catch {
    categoryItems.value = []
  }
}

onBeforeUnmount(() => {
  window.clearTimeout(searchTimer)
  loadSequence++
  statsSequence++
})

async function loadEntries(append: boolean = false): Promise<void> {
  const sequence = ++loadSequence
  isLoading.value = true
  errorMessage.value = ''

  try {
    const query = {
      period: view.value === 'calendar' ? 'day' as const : period.value,
      anchor: view.value === 'calendar' ? selectedDate.value : anchor.value,
      kind: kindFilter.value === 'all' ? null : kindFilter.value,
      category: categoryFilter.value,
      search: search.value.trim()
    }
    const firstPage = append ? (summary.value?.page ?? 0) + 1 : 1
    if (!append) summary.value = null
    let result = await fetchEntryPeriod({ ...query, page: firstPage })
    if (sequence !== loadSequence) return

    const entries = append ? [...(summary.value?.entries ?? []), ...result.entries] : [...result.entries]
    let needsFocus = focusEntry && !entries.some(entry => entry.id === focusEntry)
    const maxFocusPages = Math.max(restorePages, 20)

    while (
      result.entries.length
      && entries.length < result.totalCount
      && (result.page < restorePages || (needsFocus && result.page < maxFocusPages))
    ) {
      result = await fetchEntryPeriod({ ...query, page: result.page + 1 })
      if (sequence !== loadSequence) return
      entries.push(...result.entries)
      needsFocus = focusEntry && !entries.some(entry => entry.id === focusEntry)
    }

    summary.value = { ...result, entries }
    restorePages = 1
    const target = focusEntry
    const position = view.value === 'chart' ? undefined : restorePosition
    focusEntry = ''
    if (position) restorePosition = undefined
    await nextTick()
    if (sequence !== loadSequence) return

    if (target) {
      const row = entryRows().find(element => element.dataset.entryId === target)
      if (row) {
        highlightedEntry.value = target
        row.scrollIntoView({ block: 'center' })
        row.focus({ preventScroll: true })
      }
    } else if (position) {
      const row = entryRows().find(element => element.dataset.entryId === position.visibleRow?.id)
      const top = row && position.visibleRow
        ? window.scrollY + row.getBoundingClientRect().top - position.visibleRow.top
        : position.scrollTop
      window.scrollTo({ top })
    }
  } catch (error) {
    if (sequence !== loadSequence) {
      return
    }

    summary.value = null

    if (error instanceof ApiError && error.status === 404) {
      await router.replace({ name: 'setup' })
      return
    }

    errorMessage.value = getApiErrorMessage(error, '账本加载失败，请重试。')
  } finally {
    if (sequence === loadSequence) {
      isLoading.value = false
    }
  }
}

function setKind(kind: 'all' | EntryKind): void {
  kindFilter.value = kind

  if (categoryFilter.value && !categoryOptions.value.includes(categoryFilter.value)) {
    categoryFilter.value = ''
  }
}

function clearFilters(): void {
  window.clearTimeout(searchTimer)
  kindFilter.value = 'all'
  categoryFilter.value = ''
  search.value = ''
}

function setPeriod(nextPeriod: EntryPeriod): void {
  period.value = nextPeriod
}

function shiftPeriod(direction: -1 | 1): void {
  if (direction === 1 && !canGoNext.value) {
    return
  }

  anchor.value = shiftAnchor(anchor.value, period.value, direction)
  if (view.value === 'calendar') {
    selectedDate.value = anchor.value.slice(0, 7) === today.slice(0, 7) ? today : anchor.value
  }
}

function formatEntryAmount(entry: LedgerEntry): string {
  if (amountsMasked.value) return '••••'
  return `${entry.kind === 'income' ? '+' : '−'}${formatMoney(entry.amount)}`
}

function openEntry(entryId?: string): void {
  void router.push({
    name: 'entry',
    query: entryId ? { id: entryId } : view.value === 'calendar' ? { date: selectedDate.value } : {}
  })
}

</script>

<template>
  <main ref="pageElement" class="page ledger-page with-nav">
    <header class="ledger-heading">
      <div class="period-nav">
        <button type="button" aria-label="上一周期" @click="shiftPeriod(-1)">‹</button>
        <strong>{{ periodLabel }}</strong>
        <button type="button" aria-label="下一周期" :disabled="!canGoNext" @click="shiftPeriod(1)">›</button>
      </div>
      <RouterLink class="round-tool ledger-settings" :to="{ name: 'categories' }" aria-label="分类设置"><AppIcon name="settings" /></RouterLink>
    </header>

    <section v-if="view !== 'chart'" class="ledger-hero">
      <LedgerMonthlyBudget v-if="period === 'month'"
        :month="anchor.slice(0, 7)" :today="today" :expense="displayExpense">
        <span class="ledger-hero-label">▣ 本月支出</span>
        <strong class="ledger-hero-amount">{{ maskMoney(displayExpense) }}</strong>
      </LedgerMonthlyBudget>
      <div v-else class="ledger-hero-copy">
        <span class="ledger-hero-label">▣ {{ period === 'day' ? '当天' : '本年' }}支出</span>
        <strong class="ledger-hero-amount">{{ maskMoney(displayExpense) }}</strong>
      </div>
    </section>

    <section v-if="view === 'chart'" class="chart-toolbar-card">
      <div class="period-tabs" role="group" aria-label="图表周期">
        <button v-for="item in chartPeriodOptions" :key="item.value" type="button"
          :class="{ active: period === item.value }" @click="setPeriod(item.value)">{{ item.label }}</button>
      </div>
      <div class="toolbar-views" role="group" aria-label="账单视图">
        <button type="button" class="view-pill" aria-label="收支日历" :aria-pressed="false" @click="switchView('calendar')"><AppIcon name="calendar" /><span>日历</span></button>
        <button type="button" class="view-pill" aria-label="账单列表" :aria-pressed="false" @click="switchView('list')"><AppIcon name="list" /><span>列表</span></button>
        <button type="button" class="view-pill" aria-label="收支图表" :aria-pressed="true" @click="switchView('chart')"><AppIcon name="chart" /><span>图表</span></button>
      </div>
    </section>

    <section v-else class="ledger-calendar-card">
      <header class="calendar-heading">
        <strong><AppIcon :name="view === 'list' ? 'list' : 'calendar'" />
          {{ view === 'list' ? '账单列表' : '收支日历' }}
        </strong>
        <div class="toolbar-views" role="group" aria-label="账单视图">
          <button type="button" class="view-pill" aria-label="收支日历" :aria-pressed="view === 'calendar'" @click="switchView('calendar')"><AppIcon name="calendar" /><span>日历</span></button>
          <button type="button" class="view-pill" aria-label="账单列表" :aria-pressed="view === 'list'" @click="switchView('list')"><AppIcon name="list" /><span>列表</span></button>
          <button type="button" class="view-pill" aria-label="收支图表" :aria-pressed="false" @click="switchView('chart')"><AppIcon name="chart" /><span>图表</span></button>
        </div>
      </header>
      <template v-if="view === 'calendar'">
        <p v-if="statsError" class="state-inline">{{ statsError }} <button type="button" @click="loadStats">重试</button></p>
        <LedgerCalendar :month="anchor" :selected="selectedDate" :today="today"
          :points="stats?.points ?? []" @select="selectedDate = $event" />
      </template>
      <div v-else class="period-tabs" role="group" aria-label="账本周期">
        <button v-for="item in periodOptions" :key="item.value" type="button"
          :class="{ active: period === item.value }" @click="setPeriod(item.value)">{{ item.label }}</button>
      </div>
    </section>

    <template v-if="view !== 'chart'">
    <details v-if="view === 'list'" class="filter-disclosure ui-disclosure" :open="hasFilters">
      <summary><span><AppIcon name="search" /> 搜索与筛选</span><span v-if="hasFilters" class="filter-label">{{ [
        kindFilter === 'all' ? '' : kindFilter === 'income' ? '收入' : '支出',
        categoryFilter, search.trim()
      ].filter(Boolean).join(' · ') }}</span></summary>
    <div class="filter-bar">
      <div class="kind-tabs" role="tablist" aria-label="收支类型">
        <button
          type="button"
          :class="{ active: kindFilter === 'all' }"
          @click="setKind('all')"
        >
          全部
        </button>
        <button
          type="button"
          :class="{ active: kindFilter === 'expense' }"
          @click="setKind('expense')"
        >
          支出
        </button>
        <button
          type="button"
          :class="{ active: kindFilter === 'income' }"
          @click="setKind('income')"
        >
          收入
        </button>
      </div>
      <div class="filter-row">
        <input v-model="search" type="search" placeholder="搜索分类或备注" aria-label="搜索分类或备注" />
        <select v-model="categoryFilter" aria-label="分类筛选">
          <option value="">全部分类</option>
          <option v-for="category in categoryOptions" :key="category" :value="category">
            {{ category }}
          </option>
        </select>
      </div>
    </div>

    <button v-if="hasFilters" type="button" class="back-link" @click="clearFilters">清除筛选</button>
    </details>

    <LoadingState v-if="isLoading && !summary" variant="card" label="正在读取记录" />
    <LoadingState v-if="isLoading && !summary" variant="list" :rows="3" label="正在读取记录列表" />

    <section v-else-if="errorMessage" class="state">
      <p>{{ errorMessage }}</p>
      <LoadingButton class="secondary-button" :loading="isLoading" loading-label="加载中…" @click="loadEntries()">重新加载</LoadingButton>
    </section>

    <template v-else>
      <section v-for="group in groupedEntries" :key="group.date" class="day-group">
        <header class="day-header">
          <span>{{ formatBusinessDateWithWeekday(group.date) }}</span>
          <span class="day-totals">
            <template v-if="summary && summary.entries.length < summary.totalCount">已显示小计 · </template>
            <template v-if="group.income">收入 {{ formatMoney(group.income) }}</template>
            <template v-if="group.expense">支出 {{ formatMoney(group.expense) }}</template>
          </span>
        </header>
        <div class="day-card">
          <button
            v-for="(entry, index) in group.entries"
            :key="entry.id"
            type="button"
            class="entry-row stagger-item"
            :class="{ 'entry-highlighted': highlightedEntry === entry.id }"
            :data-entry-id="entry.id"
            :style="{ '--stagger': Math.min(index, 8) }"
            @click="openEntry(entry.id)"
          >
            <span class="entry-mark"><CategoryIcon :name="entry.category" :icon-key="categoryIconsByKind.get(`${entry.kind}:${entry.category}`)" /></span>
            <span class="entry-copy">
              <span class="entry-title">{{ entry.note?.trim() || entry.category }}</span>
              <small v-if="(entry.note?.trim() && entry.note.trim() !== entry.category) || entry.source === 'automatic'">
                {{ entry.note?.trim() && entry.note.trim() !== entry.category ? entry.category : '' }}<template
                  v-if="entry.source === 'automatic'">{{ entry.note?.trim() && entry.note.trim() !== entry.category ? ' · ' : '' }}自动记账</template>
              </small>
            </span>
            <b :class="{ income: entry.kind === 'income' }">{{ formatEntryAmount(entry) }}</b>
          </button>
        </div>
      </section>

      <p v-if="view === 'calendar' && !groupedEntries.length" class="day-header">{{ formatBusinessDateWithWeekday(selectedDate) }}</p>
      <EmptyState
        v-if="!groupedEntries.length"
        :title="hasFilters ? '没有符合条件的记录' : view === 'calendar' ? '这天还没有账单' : '这个周期还没有记录'"
        :description="hasFilters ? '清除筛选，查看这段时间的全部收支。' : '记下第一笔收入或支出，让日常收支更清楚。'"
        :action-label="hasFilters ? '清除筛选' : '记一笔'"
        @action="hasFilters ? clearFilters() : openEntry()"
      />
    </template>
  <LoadingButton
    v-if="summary && summary.entries.length < summary.totalCount"
    class="secondary-button"
    :loading="isLoading"
    loading-label="加载中…"
    @click="loadEntries(true)"
  >加载更多（已显示 {{ summary.entries.length }} / {{ summary.totalCount }}）</LoadingButton>
    </template>

    <template v-else>
      <LoadingState v-if="statsLoading && !stats" variant="chart" compact label="正在读取图表" />
      <section v-else-if="statsError && !stats" class="state">
        <p>{{ statsError }}</p>
        <LoadingButton class="secondary-button" :loading="statsLoading" loading-label="加载中…" @click="loadStats">重新加载</LoadingButton>
      </section>

      <LedgerStatsPanel
        v-else-if="stats"
        v-model:mode="statsKind"
        v-model:by-group="statsByGroup"
        :stats="stats"
        :loading="statsLoading"
        :period="period"
        :anchor="anchor"
        :today="today"
        :period-label="periodLabel"
        :categories="categoryItems"
        :category-icons-by-kind="categoryIconsByKind"
        @open-entry="openEntry"
        @filter-list="selectStatsCategory"
      />
    </template>
    <AppNav />
  </main>
</template>

<style scoped>
.filter-label {
  max-width: 52%;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.ledger-page {
  --ledger-accent: var(--color-brand);
  background: var(--color-bg-page);
}
.ledger-heading, .calendar-heading { display: flex; align-items: center; gap: 8px; }
.ledger-heading { justify-content: space-between; margin-bottom: 14px; }
.ledger-heading .period-nav strong { font-size: var(--text-title3); }
.round-tool {
  display: inline-flex; align-items: center; justify-content: center; gap: 6px;
  min-width: var(--touch-target); min-height: var(--touch-target); border: 0;
  border-radius: var(--radius-pill); background: var(--color-bg-surface); color: var(--color-text-primary);
  text-decoration: none; font-weight: 650;
}
.round-tool .app-icon, .calendar-heading strong .app-icon { width: 17px; height: 17px; }
.ledger-settings { background: color-mix(in srgb, var(--color-bg-surface) 68%, transparent); }
.ledger-hero, .ledger-calendar-card, .chart-toolbar-card { background: var(--color-bg-surface); border-radius: 24px; padding: 16px; }
.ledger-hero { margin-bottom: 12px; }
.ledger-hero-copy { display: grid; gap: 5px; }
.ledger-hero-amount { font-size: clamp(1.5rem, 7vw, 2.15rem); line-height: 1.12; overflow-wrap: anywhere; }
.ledger-hero-label { font-weight: 650; }
.ledger-calendar-card { padding: 12px; margin-bottom: 16px; }
.chart-toolbar-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 10px 12px;
  margin-bottom: 14px;
  box-shadow: var(--shadow-card);
}
.toolbar-views { display: flex; align-items: center; gap: 4px; }
.view-pill {
  display: inline-flex; align-items: center; justify-content: center; gap: 4px;
  min-height: 36px; padding: 0 10px; border: 0; border-radius: var(--radius-pill);
  background: var(--color-bg-subtle); color: var(--color-text-secondary);
  font-size: var(--text-caption); font-weight: 600; white-space: nowrap;
}
.view-pill .app-icon { width: 14px; height: 14px; }
.view-pill[aria-pressed='true'] {
  background: color-mix(in srgb, var(--ledger-accent) 15%, var(--color-bg-surface));
  color: var(--ledger-accent);
}
.calendar-heading { margin-bottom: 8px; }
.calendar-heading strong { display: flex; gap: 6px; align-items: center; margin-right: auto; }
.calendar-heading .round-tool { background: var(--color-bg-subtle); }
.calendar-heading .round-tool[aria-pressed='true'] { background: color-mix(in srgb, var(--ledger-accent) 15%, var(--color-bg-surface)); }

.back-link {
  min-height: var(--touch-target);
  padding: 0 4px;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.filter-disclosure {
  margin-bottom: 16px;
  padding: 2px 20px;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.filter-disclosure summary .app-icon {
  width: 16px;
  height: 16px;
  margin-right: 6px;
}
.filter-disclosure > summary > span:first-child {
  display: inline-flex;
  align-items: center;
}
.filter-disclosure[open] {
  padding-bottom: 16px;
}
.ledger-history {
  display: flex;
  justify-content: center;
  margin-top: 24px;
  font-size: var(--text-footnote);
}

.ledger-page > .secondary-button {
  display: block;
  width: 100%;
  min-height: 48px;
  margin-top: 4px;
}

.state-inline {
  margin: 8px 0 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.7;
}

.period-panel {
  margin-bottom: 18px;
  padding: 20px;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.period-top {
  display: flex;
  flex-direction: row-reverse;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  margin-bottom: 18px;
}

.period-tabs {
  display: flex;
  gap: 2px;
  padding: 3px;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
}

.period-tabs button {
  min-height: 38px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.period-tabs button.active {
  background: var(--color-bg-surface);
  color: var(--color-brand);
  font-weight: 700;
  box-shadow: 0 2px 8px var(--color-shadow);
}

.period-nav {
  display: flex;
  align-items: center;
  gap: 4px;
}

.period-nav button {
  width: var(--touch-target);
  height: var(--touch-target);
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-brand);
  font-size: 1.125rem;
  line-height: 1;
}

.period-nav button:hover:not(:disabled) {
  background: var(--color-brand-soft);
}

.period-nav button:disabled {
  color: var(--color-text-disabled);
  cursor: not-allowed;
}

.period-nav strong {
  font-size: var(--text-footnote);
  font-weight: 700;
  letter-spacing: -0.2px;
  white-space: nowrap;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.summary-grid > div + div {
  padding-left: 14px;
  border-left: 1px solid color-mix(in srgb, var(--color-border-subtle) 55%, transparent);
}

.summary-grid span {
  display: block;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.summary-grid b {
  display: block;
  margin-top: 8px;
  font-size: clamp(1rem, 5vw, 1.375rem);
  font-weight: 700;
  letter-spacing: -0.03em;
  line-height: 1.15;
  font-variant-numeric: tabular-nums;
}

.income {
  color: var(--color-finance-income);
}

.filter-bar {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-bottom: 0;
}

.kind-tabs {
  display: flex;
  gap: 6px;
}

.kind-tabs button {
  min-height: 40px;
  padding: 0 16px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.kind-tabs button.active {
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-weight: 700;
}

.filter-row {
  display: flex;
  gap: 8px;
}

.filter-row input,
.filter-row select {
  box-sizing: border-box;
  min-height: 44px;
  padding: 0 14px;
  border: 1px solid transparent;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  font-size: var(--text-subhead);
}

.filter-row input:focus-visible,
.filter-row select:focus-visible {
  border-color: var(--color-border-focus);
  background: var(--color-bg-surface);
}

.filter-row input {
  flex: 1;
  min-width: 0;
}

.filter-row select {
  max-width: 118px;
}

.day-group {
  margin-bottom: 18px;
}

.day-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
  padding: 0 4px 10px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 500;
}

.day-totals {
  display: inline-flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 2px 10px;
  font-variant-numeric: tabular-nums;
}

.day-card {
  overflow: hidden;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.entry-row {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 14px 18px;
  border: 0;
  border-bottom: 1px solid var(--color-border-subtle);
  background: transparent;
  text-align: left;
}

.entry-row:last-child {
  border-bottom: 0;
}

.entry-row:hover,
.entry-row.entry-highlighted {
  background: var(--color-bg-hover);
}

.entry-mark {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  flex: 0 0 auto;
  width: 38px;
  height: 38px;
  border-radius: 50%;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.entry-copy {
  display: flex;
  flex-direction: column;
  gap: 3px;
  flex: 1;
  min-width: 0;
}

.entry-title {
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
  font-size: var(--text-subhead);
  font-weight: 600;
}

.entry-copy small {
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.entry-row b {
  flex: 0 0 auto;
  font-size: var(--text-body);
  font-weight: 700;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}
</style>
