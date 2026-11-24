<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { fetchEntryPeriod } from '../api/entries'
import { getApiErrorMessage } from '../api/client'
import type { EntryCategory } from '../types/category'
import type {
  EntryKind,
  EntryPeriod,
  EntryStats,
  EntryStatsCategory,
  EntryStatsPoint,
  LedgerEntry
} from '../types/entry'
import { groupEntryStats } from '../utils/entryStatsGroups'
import { formatBusinessDateWithWeekday, formatMoney, sumAmounts } from '../utils/format'
import BottomSheet from './BottomSheet.vue'
import CategoryIcon from './CategoryIcon.vue'
import LedgerCategoryDonut, { type DonutSlice } from './LedgerCategoryDonut.vue'
import LedgerMonthlyBudget from './LedgerMonthlyBudget.vue'
import LedgerStatsChart, { type StatsChartMode } from './LedgerStatsChart.vue'
import LoadingState from './LoadingState.vue'
import MoneyAmount from './MoneyAmount.vue'

const palette = [
  '#4F46E5',
  '#0EA5E9',
  '#10B981',
  '#F59E0B',
  '#EC4899',
  '#8B5CF6',
  '#14B8A6',
  '#F97316'
]

const props = defineProps<{
  stats: EntryStats
  loading: boolean
  period: EntryPeriod
  anchor: string
  today: string
  periodLabel: string
  mode: StatsChartMode
  byGroup: boolean
  categories: EntryCategory[]
  categoryIconsByKind: Map<string, string | null | undefined>
}>()

const emit = defineEmits<{
  'update:mode': [mode: StatsChartMode]
  'update:byGroup': [byGroup: boolean]
  openEntry: [entryId: string]
  filterList: [payload: { kind: EntryKind; category: string }]
}>()

const selectedPoint = ref<EntryStatsPoint | null>(null)
const activeCategory = ref<string | null>(null)
const expandedGroup = ref<string | null>(null)

interface SheetContext {
  title: string
  subtitle: string
  period: EntryPeriod
  anchor: string
  kind: EntryKind | null
  category: string
}

const sheetOpen = ref(false)
const sheetContext = ref<SheetContext | null>(null)
const sheetSort = ref<'amountDesc' | 'dateDesc'>('amountDesc')
const sheetEntries = ref<LedgerEntry[]>([])
const sheetTotalCount = ref(0)
const sheetLoading = ref(false)
const sheetError = ref('')
let sheetSequence = 0

watch(() => [props.stats.from, props.stats.to, props.mode], () => {
  selectedPoint.value = null
  activeCategory.value = null
  expandedGroup.value = null
})

watch(() => props.byGroup, () => {
  activeCategory.value = null
  expandedGroup.value = null
})

const categoryKind = computed<EntryKind>(() => props.mode === 'income' ? 'income' : 'expense')
const surplus = computed(() => sumAmounts([props.stats.income, -props.stats.expense]))
const savingsRate = computed(() =>
  props.stats.income > 0 ? (surplus.value / props.stats.income) * 100 : null
)

const leafCategories = computed(() =>
  categoryKind.value === 'income' ? props.stats.incomeCategories : props.stats.expenseCategories
)

const statsCategories = computed<EntryStatsCategory[]>(() => {
  if (!props.byGroup) {
    return leafCategories.value
  }

  const groups = categoryKind.value === 'income' ? props.stats.incomeGroups : props.stats.expenseGroups
  return groups?.length
    ? groups
    : groupEntryStats(leafCategories.value, props.categories, categoryKind.value)
})

const categoryColors = computed(() => new Map(
  statsCategories.value.map((item, index) => [item.category, palette[index % palette.length]!])
))

const donutSlices = computed<DonutSlice[]>(() =>
  statsCategories.value.map((item) => ({
    category: item.category,
    amount: item.amount,
    share: item.share,
    color: categoryColors.value.get(item.category) ?? palette[0]!
  }))
)

const childrenByGroup = computed(() => {
  const map = new Map<string, EntryStatsCategory[]>()
  const matching = props.categories.filter((c) => c.kind === categoryKind.value)
  const parentNames = new Map(matching.filter((c) => c.isGroup).map((c) => [c.id, c.name]))
  const fallbackParent = new Map(
    matching
      .filter((c) => !c.isGroup)
      .map((c) => [c.name, parentNames.get(c.parentId ?? '') ?? '未归类'])
  )

  for (const leaf of leafCategories.value) {
    const groupName = leaf.parentGroup || fallbackParent.get(leaf.category) || '未归类'
    const list = map.get(groupName) ?? []
    list.push(leaf)
    map.set(groupName, list)
  }

  return map
})

const totalCount = computed(() =>
  leafCategories.value.reduce((sum, item) => sum + item.count, 0)
)

const activeTotal = computed(() => {
  if (props.mode === 'income') return props.stats.income
  if (props.mode === 'balance') return surplus.value
  return props.stats.expense
})

const categoryTotal = computed(() =>
  categoryKind.value === 'income' ? props.stats.income : props.stats.expense
)

const activeAverage = computed(() => {
  if (props.mode === 'income') {
    return props.period === 'year' && props.stats.points.length
      ? props.stats.income / props.stats.points.length
      : props.stats.incomeDailyAverage
  }

  if (props.mode === 'expense') {
    return props.period === 'year' && props.stats.points.length
      ? props.stats.expense / props.stats.points.length
      : props.stats.expenseDailyAverage
  }

  return 0
})

const peakLabel = computed(() => {
  if (props.mode === 'balance' || !props.stats.points.length) {
    return ''
  }

  const key = props.mode
  let peak: EntryStatsPoint | null = null
  for (const point of props.stats.points) {
    if (!peak || point[key] > peak[key]) {
      peak = point
    }
  }

  if (!peak || peak[key] <= 0) {
    return ''
  }

  return `峰值 ${peak.label} ¥${formatMoney(peak[key])}`
})

const comparisonBadge = computed(() => {
  const comp = props.stats.comparison
  if (!comp) return null

  if (!comp.comparable) {
    return { text: `${comp.label}无可比数据`, tone: 'muted' as const }
  }

  if (props.mode === 'expense') {
    const delta = comp.expenseDelta
    const pct = comp.expenseDeltaPercent
    if (delta === null) return null
    if (delta === 0) return { text: `${comp.label}持平`, tone: 'muted' as const }
    const sign = delta > 0 ? '+' : ''
    const pctText = pct === null ? '' : ` ${sign}${pct.toFixed(1)}%`
    return {
      text: `${comp.label}${pctText || ` ${sign}¥${formatMoney(delta)}`}`,
      tone: delta > 0 ? 'up-warn' as const : 'down-good' as const
    }
  }

  if (props.mode === 'income') {
    const delta = comp.incomeDelta
    const pct = comp.incomeDeltaPercent
    if (delta === null) return null
    if (delta === 0) return { text: `${comp.label}持平`, tone: 'muted' as const }
    const sign = delta > 0 ? '+' : ''
    const pctText = pct === null ? '' : ` ${sign}${pct.toFixed(1)}%`
    return {
      text: `${comp.label}${pctText || ` ${sign}¥${formatMoney(delta)}`}`,
      tone: delta > 0 ? 'down-good' as const : 'up-warn' as const
    }
  }

  const delta = comp.surplusDelta
  if (delta === null) return null
  if (delta === 0) return { text: `${comp.label}持平`, tone: 'muted' as const }
  const sign = delta > 0 ? '+' : '−'
  return {
    text: `${comp.label} ${sign}¥${formatMoney(Math.abs(delta))}`,
    tone: delta > 0 ? 'down-good' as const : 'up-warn' as const
  }
})

const topEntries = computed(() => {
  const list = categoryKind.value === 'income' ? props.stats.topIncomes : props.stats.topExpenses
  return list ?? []
})

function categoryDeltaLabel(item: EntryStatsCategory): { text: string; tone: 'up' | 'down' | 'muted' } | null {
  if (item.previousAmount === null || item.previousAmount === undefined) {
    return null
  }

  if (item.previousAmount <= 0) {
    return item.amount > 0 ? { text: '新增', tone: 'muted' } : null
  }

  const delta = item.amount - item.previousAmount
  if (delta === 0) {
    return { text: '持平', tone: 'muted' }
  }

  const pct = (delta / item.previousAmount) * 100
  const sign = pct > 0 ? '+' : ''
  const isGood = categoryKind.value === 'expense' ? delta < 0 : delta > 0
  return {
    text: `${sign}${Math.round(pct)}%`,
    tone: isGood ? 'down' : 'up'
  }
}

function handleSelectPoint(point: EntryStatsPoint): void {
  selectedPoint.value = selectedPoint.value?.date === point.date ? null : point
}

function handleDonutSelect(category: string): void {
  activeCategory.value = activeCategory.value === category ? null : category
  if (props.byGroup) {
    expandedGroup.value = activeCategory.value
  }
}

function handleRankClick(item: EntryStatsCategory): void {
  if (props.byGroup) {
    expandedGroup.value = expandedGroup.value === item.category ? null : item.category
    activeCategory.value = expandedGroup.value
    return
  }

  void openCategorySheet(item.category)
}

async function openCategorySheet(category: string): Promise<void> {
  sheetContext.value = {
    title: category,
    subtitle: `${props.periodLabel} · ${categoryKind.value === 'income' ? '收入' : '支出'}明细`,
    period: props.period,
    anchor: props.anchor,
    kind: categoryKind.value,
    category
  }
  sheetSort.value = 'amountDesc'
  sheetOpen.value = true
  await loadSheetEntries()
}

async function openPointSheet(point: EntryStatsPoint): Promise<void> {
  const pointPeriod: EntryPeriod = props.period === 'year' ? 'month' : 'day'
  const kind = props.mode === 'balance' ? null : props.mode
  sheetContext.value = {
    title: props.period === 'year' ? `${props.anchor.slice(0, 4)}年${point.label}` : formatBusinessDateWithWeekday(point.date),
    subtitle: kind ? `${kind === 'income' ? '收入' : '支出'}明细` : '全部收支明细',
    period: pointPeriod,
    anchor: point.date,
    kind,
    category: ''
  }
  sheetSort.value = 'amountDesc'
  sheetOpen.value = true
  await loadSheetEntries()
}

async function loadSheetEntries(): Promise<void> {
  const context = sheetContext.value
  if (!context) return
  const sequence = ++sheetSequence
  sheetLoading.value = true
  sheetError.value = ''

  try {
    const result = await fetchEntryPeriod({
      period: context.period,
      anchor: context.anchor,
      kind: context.kind,
      category: context.category || undefined,
      sort: sheetSort.value,
      page: 1,
      pageSize: 50
    })

    if (sequence !== sheetSequence) return
    sheetEntries.value = result.entries
    sheetTotalCount.value = result.totalCount
  } catch (error) {
    if (sequence !== sheetSequence) return
    sheetEntries.value = []
    sheetTotalCount.value = 0
    sheetError.value = getApiErrorMessage(error, '明细读取失败，请稍后重试。')
  } finally {
    if (sequence === sheetSequence) {
      sheetLoading.value = false
    }
  }
}

function toggleSheetSort(next: 'amountDesc' | 'dateDesc'): void {
  if (sheetSort.value === next) return
  sheetSort.value = next
  void loadSheetEntries()
}

function jumpToListFilter(): void {
  const context = sheetContext.value
  if (!context || !context.category || !context.kind) return
  sheetOpen.value = false
  emit('filterList', { kind: context.kind, category: context.category })
}

function openEntryFromSheet(entryId: string): void {
  sheetOpen.value = false
  emit('openEntry', entryId)
}

function resolveIconKey(kind: EntryKind, category: string, fallback?: string | null): string | null | undefined {
  return fallback ?? props.categoryIconsByKind.get(`${kind}:${category}`)
}
</script>

<template>
  <div class="stats-stack" :class="{ refreshing: loading }">
    <!-- ① 收支结余总览卡 -->
    <section class="stats-card stats-panel">
      <div class="mode-tabs" role="tablist" aria-label="图表视角">
        <button type="button" role="tab" :aria-selected="mode === 'expense'" :class="{ active: mode === 'expense' }" @click="emit('update:mode', 'expense')">支出</button>
        <button type="button" role="tab" :aria-selected="mode === 'income'" :class="{ active: mode === 'income' }" @click="emit('update:mode', 'income')">收入</button>
        <button type="button" role="tab" :aria-selected="mode === 'balance'" :class="{ active: mode === 'balance' }" @click="emit('update:mode', 'balance')">结余</button>
      </div>

      <div class="hero-metric">
        <div class="hero-metric-top">
          <span class="hero-metric-label">
            {{ periodLabel }}{{ mode === 'income' ? '收入' : mode === 'balance' ? '结余' : '支出' }}
          </span>
          <span v-if="comparisonBadge" class="delta-pill" :class="`tone-${comparisonBadge.tone}`">
            {{ comparisonBadge.text }}
          </span>
        </div>

        <strong class="hero-metric-value" :class="{ income: mode === 'income' || (mode === 'balance' && surplus > 0), negative: mode === 'balance' && surplus < 0 }">
          <template v-if="mode === 'balance' && surplus > 0">+</template>
          <template v-else-if="mode === 'balance' && surplus < 0">−</template>
          <MoneyAmount :value="Math.abs(activeTotal)" />
        </strong>

        <p class="stats-meta">
          <template v-if="mode === 'balance'">
            <template v-if="savingsRate !== null">储蓄率 {{ savingsRate.toFixed(1) }}% · </template>
            覆盖 {{ stats.coveredDays }} 天
          </template>
          <template v-else>
            {{ period === 'year' ? '月均' : '日均' }} ¥{{ formatMoney(activeAverage) }} · {{ totalCount }} 笔 · 覆盖 {{ stats.coveredDays }} 天
          </template>
        </p>
        <p v-if="stats.note" class="stats-note">{{ stats.note }}</p>
      </div>

      <LedgerMonthlyBudget
        v-if="period === 'month' && mode === 'expense'"
        compact
        :month="anchor.slice(0, 7)"
        :today="today"
        :expense="stats.expense"
      />

      <div class="overview-strip" role="group" aria-label="当期收支结余快览">
        <button type="button" :class="{ active: mode === 'expense' }" @click="emit('update:mode', 'expense')">
          <span>支出</span>
          <b>¥{{ formatMoney(stats.expense) }}</b>
        </button>
        <button type="button" :class="{ active: mode === 'income' }" @click="emit('update:mode', 'income')">
          <span>收入</span>
          <b class="income">¥{{ formatMoney(stats.income) }}</b>
        </button>
        <button type="button" :class="{ active: mode === 'balance' }" @click="emit('update:mode', 'balance')">
          <span>结余</span>
          <b :class="{ income: surplus > 0, negative: surplus < 0 }">{{ surplus > 0 ? '+' : surplus < 0 ? '−' : '' }}¥{{ formatMoney(Math.abs(surplus)) }}</b>
        </button>
      </div>
    </section>

    <!-- ② 趋势分析卡 -->
    <section class="stats-card chart-card">
      <div class="stats-head">
        <h2>{{ mode === 'income' ? '收入趋势' : mode === 'balance' ? '收支对比' : '支出趋势' }}</h2>
        <span class="tiny">{{ peakLabel || (period === 'year' ? '按月' : '按日') }}</span>
      </div>

      <LedgerStatsChart
        v-if="stats.points.length"
        :points="stats.points"
        :mode="mode"
        :period="period"
        :total="activeTotal"
        :average="activeAverage"
        :selected-date="selectedPoint?.date ?? null"
        @select-point="handleSelectPoint"
      />
      <p v-else class="state-inline">该周期还没有记录。</p>

      <button
        v-if="selectedPoint"
        type="button"
        class="point-callout"
        @click="openPointSheet(selectedPoint)"
      >
        <span class="point-callout-copy">
          <strong>{{ selectedPoint.label }}</strong>
          <template v-if="mode === 'balance'">
            收 ¥{{ formatMoney(selectedPoint.income) }} · 支 ¥{{ formatMoney(selectedPoint.expense) }}
          </template>
          <template v-else>
            {{ mode === 'income' ? '收入' : '支出' }} ¥{{ formatMoney(selectedPoint[mode]) }}
            <template v-if="period === 'month' && selectedPoint.category && selectedPoint[mode] > 0">（{{ selectedPoint.category }}等）</template>
          </template>
        </span>
        <span class="point-callout-action">看明细 ›</span>
      </button>
    </section>

    <!-- ③ 分类构成与排行卡 -->
    <section class="stats-card ranking-panel" :class="{ income: categoryKind === 'income' }">
      <div class="stats-head ranking-head">
        <div>
          <h2>{{ categoryKind === 'income' ? '收入构成' : '支出构成' }}</h2>
          <span class="tiny">{{ statsCategories.length }} 类</span>
        </div>
        <div class="mini-switch" role="group" aria-label="分类汇总层级">
          <button type="button" :class="{ active: byGroup }" @click="emit('update:byGroup', true)">一级</button>
          <button type="button" :class="{ active: !byGroup }" @click="emit('update:byGroup', false)">二级</button>
        </div>
      </div>

      <template v-if="statsCategories.length">
        <LedgerCategoryDonut
          :slices="donutSlices"
          :total="categoryTotal"
          :title="categoryKind === 'income' ? '总收入' : '总支出'"
          :active-category="activeCategory"
          @select="handleDonutSelect"
        />

        <div class="rank-list">
          <div v-for="category in statsCategories" :key="category.category" class="rank-block">
            <button
              type="button"
              class="rank-row"
              :class="{ highlighted: activeCategory === category.category }"
              @click="handleRankClick(category)"
            >
              <span class="rank-icon" :style="{ '--cat-color': categoryColors.get(category.category) }">
                <CategoryIcon
                  v-if="!byGroup"
                  :name="category.category"
                  :icon-key="resolveIconKey(categoryKind, category.category, category.iconKey)"
                  :size="18"
                />
                <i v-else class="group-dot" />
              </span>

              <span class="rank-body">
                <span class="rank-top">
                  <span class="rank-name">{{ category.category }}</span>
                  <span class="rank-right">
                    <small v-if="categoryDeltaLabel(category)" class="rank-delta" :class="`delta-${categoryDeltaLabel(category)!.tone}`">
                      {{ categoryDeltaLabel(category)!.text }}
                    </small>
                    <b>¥{{ formatMoney(category.amount) }}</b>
                  </span>
                </span>
                <span class="rank-bar">
                  <span :style="{ width: `${Math.max(category.share, 1)}%`, background: categoryColors.get(category.category) }" />
                </span>
                <span class="rank-sub">
                  <small>{{ category.count }} 笔 · {{ category.share.toFixed(1) }}%</small>
                  <small class="rank-arrow">{{ byGroup ? (expandedGroup === category.category ? '收起 ⌃' : '展开 ⌄') : '明细 ›' }}</small>
                </span>
              </span>
            </button>

            <div v-if="byGroup && expandedGroup === category.category" class="sub-list">
              <button
                v-for="child in childrenByGroup.get(category.category) ?? []"
                :key="child.category"
                type="button"
                class="sub-row"
                @click="openCategorySheet(child.category)"
              >
                <span class="sub-mark">
                  <CategoryIcon
                    :name="child.category"
                    :icon-key="resolveIconKey(categoryKind, child.category, child.iconKey)"
                    :size="15"
                  />
                </span>
                <span class="sub-name">{{ child.category }}</span>
                <span class="sub-meta">{{ child.count }} 笔 · {{ child.share.toFixed(1) }}%</span>
                <b class="sub-amount">¥{{ formatMoney(child.amount) }}</b>
              </button>
            </div>
          </div>
        </div>
      </template>
      <p v-else class="state-inline">该周期还没有{{ categoryKind === 'income' ? '收入' : '支出' }}记录。</p>
    </section>

    <!-- ④ 大额记录 Top 5 -->
    <section v-if="topEntries.length" class="stats-card top-panel">
      <div class="stats-head">
        <h2>{{ categoryKind === 'income' ? '大额收入' : '大额支出' }}</h2>
        <span class="tiny">Top {{ topEntries.length }}</span>
      </div>

      <div class="top-list">
        <button
          v-for="entry in topEntries"
          :key="entry.id"
          type="button"
          class="top-row"
          @click="emit('openEntry', entry.id)"
        >
          <span class="top-mark">
            <CategoryIcon
              :name="entry.category"
              :icon-key="resolveIconKey(entry.kind, entry.category, entry.iconKey)"
              :size="18"
            />
          </span>
          <span class="top-copy">
            <span class="top-title">{{ entry.note?.trim() || entry.category }}</span>
            <small>{{ entry.category }} · {{ entry.businessDate.slice(5) }}</small>
          </span>
          <b :class="{ income: entry.kind === 'income' }">
            {{ entry.kind === 'income' ? '+' : '−' }}¥{{ formatMoney(entry.amount) }}
          </b>
        </button>
      </div>
    </section>

    <!-- ⑤ 明细预览抽屉 -->
    <BottomSheet
      :open="sheetOpen"
      :title="sheetContext?.title ?? '明细'"
      :subtitle="sheetContext?.subtitle"
      close-icon
      @close="sheetOpen = false"
    >
      <div class="sheet-toolbar">
        <span class="tiny">共 {{ sheetTotalCount }} 笔</span>
        <div class="mini-switch" role="group" aria-label="明细排序">
          <button type="button" :class="{ active: sheetSort === 'amountDesc' }" @click="toggleSheetSort('amountDesc')">按金额</button>
          <button type="button" :class="{ active: sheetSort === 'dateDesc' }" @click="toggleSheetSort('dateDesc')">按时间</button>
        </div>
      </div>

      <LoadingState v-if="sheetLoading && !sheetEntries.length" variant="list" :rows="3" label="正在读取明细" />
      <p v-else-if="sheetError" class="state-inline">{{ sheetError }}</p>
      <div v-else-if="sheetEntries.length" class="sheet-list">
        <button
          v-for="entry in sheetEntries"
          :key="entry.id"
          type="button"
          class="top-row"
          @click="openEntryFromSheet(entry.id)"
        >
          <span class="top-mark">
            <CategoryIcon
              :name="entry.category"
              :icon-key="resolveIconKey(entry.kind, entry.category)"
              :size="18"
            />
          </span>
          <span class="top-copy">
            <span class="top-title">{{ entry.note?.trim() || entry.category }}</span>
            <small>{{ entry.businessDate }}{{ entry.note?.trim() && entry.note.trim() !== entry.category ? ` · ${entry.category}` : '' }}</small>
          </span>
          <b :class="{ income: entry.kind === 'income' }">
            {{ entry.kind === 'income' ? '+' : '−' }}¥{{ formatMoney(entry.amount) }}
          </b>
        </button>
      </div>
      <p v-else class="state-inline">没有符合条件的记录。</p>

      <button
        v-if="sheetContext?.category"
        type="button"
        class="secondary-button sheet-jump"
        @click="jumpToListFilter"
      >
        在账单列表中查看全部
      </button>
    </BottomSheet>
  </div>
</template>

<style scoped>
.stats-stack {
  transition: opacity var(--motion-fast) ease;
}

.stats-stack.refreshing {
  opacity: 0.68;
}

.stats-card {
  margin-bottom: 14px;
  padding: 18px 20px;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.mode-tabs {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 2px;
  padding: 3px;
  margin-bottom: 16px;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
}

.mode-tabs button {
  min-height: 36px;
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 600;
  transition: background var(--motion-fast) ease, color var(--motion-fast) ease;
}

.mode-tabs button.active {
  background: var(--color-bg-surface);
  color: var(--color-brand);
  font-weight: 700;
  box-shadow: 0 2px 8px var(--color-shadow);
}

.hero-metric-top {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.hero-metric-label {
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.delta-pill {
  padding: 3px 9px;
  border-radius: var(--radius-pill);
  font-size: 0.72rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.delta-pill.tone-up-warn {
  background: var(--color-danger-soft, rgba(217, 74, 74, 0.12));
  color: var(--color-danger);
}

.delta-pill.tone-down-good {
  background: var(--color-success-soft, rgba(13, 148, 136, 0.12));
  color: var(--color-finance-income);
}

.delta-pill.tone-muted {
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
}

.hero-metric-value {
  display: block;
  margin-top: 6px;
  font-size: clamp(1.6rem, 7.2vw, 2.15rem);
  font-weight: 750;
  letter-spacing: -0.03em;
  line-height: 1.15;
  font-variant-numeric: tabular-nums;
}

.hero-metric-value.income,
.income {
  color: var(--color-finance-income);
}

.hero-metric-value.negative,
.negative {
  color: var(--color-danger);
}

.stats-meta {
  margin: 6px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.6;
  font-variant-numeric: tabular-nums;
}

.stats-note {
  margin: 6px 0 0;
  color: var(--color-warning);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.overview-strip {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 6px;
  margin-top: 16px;
  padding-top: 14px;
  border-top: 1px solid var(--color-border-subtle);
}

.overview-strip button {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 3px;
  padding: 8px 10px;
  border: 0;
  border-radius: 14px;
  background: transparent;
  color: inherit;
  text-align: left;
  transition: background var(--motion-fast) ease;
}

.overview-strip button.active {
  background: var(--color-bg-subtle);
}

.overview-strip span {
  color: var(--color-text-secondary);
  font-size: 0.72rem;
}

.overview-strip b {
  font-size: 0.875rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.stats-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
  margin-bottom: 12px;
}

.ranking-head {
  align-items: center;
}

.ranking-head > div:first-child {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.stats-head h2 {
  margin: 0;
  font-size: 1.0625rem;
  font-weight: 700;
  letter-spacing: -0.3px;
}

.tiny {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.mini-switch {
  display: inline-flex;
  gap: 2px;
  padding: 2px;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
}

.mini-switch button {
  min-height: 30px;
  padding: 0 11px;
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 600;
}

.mini-switch button.active {
  background: var(--color-bg-surface);
  color: var(--color-brand);
  font-weight: 700;
  box-shadow: 0 1px 4px var(--color-shadow);
}

.point-callout {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  width: 100%;
  margin-top: 10px;
  padding: 10px 14px;
  border: 0;
  border-radius: 14px;
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  font-size: var(--text-caption);
  text-align: left;
}

.point-callout-copy {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 8px;
  font-variant-numeric: tabular-nums;
}

.point-callout-action {
  flex: 0 0 auto;
  color: var(--color-brand);
  font-weight: 600;
}

.rank-block + .rank-block {
  border-top: 1px solid var(--color-border-subtle);
}

.rank-row {
  display: grid;
  grid-template-columns: 36px minmax(0, 1fr);
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 12px 4px;
  border: 0;
  border-radius: 14px;
  background: transparent;
  color: inherit;
  text-align: left;
}

.rank-row:hover,
.rank-row.highlighted {
  background: var(--color-bg-hover);
}

.rank-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--cat-color, var(--color-brand)) 14%, var(--color-bg-surface));
  color: var(--cat-color, var(--color-brand));
}

.group-dot {
  width: 11px;
  height: 11px;
  border-radius: 50%;
  background: var(--cat-color, var(--color-brand));
}

.rank-body {
  display: block;
  min-width: 0;
}

.rank-top {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 8px;
  font-size: var(--text-footnote);
  font-weight: 600;
}

.rank-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rank-right {
  display: inline-flex;
  align-items: baseline;
  gap: 6px;
  flex-shrink: 0;
}

.rank-right b {
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.rank-delta {
  font-size: 0.7rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.rank-delta.delta-up { color: var(--color-danger); }
.rank-delta.delta-down { color: var(--color-finance-income); }
.rank-delta.delta-muted { color: var(--color-text-secondary); }

.rank-bar {
  display: block;
  height: 5px;
  margin: 6px 0 4px;
  overflow: hidden;
  border-radius: var(--radius-tag);
  background: var(--color-bg-subtle);
}

.rank-bar span {
  display: block;
  height: 100%;
  border-radius: var(--radius-tag);
}

.rank-sub {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-variant-numeric: tabular-nums;
}

.rank-arrow {
  color: var(--color-brand);
  font-weight: 500;
}

.sub-list {
  margin: 0 0 10px 44px;
  padding: 6px 10px;
  border-radius: 14px;
  background: var(--color-bg-subtle);
}

.sub-row {
  display: grid;
  grid-template-columns: 24px minmax(0, 1fr) auto auto;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 8px 2px;
  border: 0;
  background: transparent;
  color: inherit;
  font-size: var(--text-caption);
  text-align: left;
}

.sub-row + .sub-row {
  border-top: 1px solid var(--color-border-subtle);
}

.sub-mark {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--color-brand);
}

.sub-name {
  overflow: hidden;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sub-meta {
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}

.sub-amount {
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.top-list,
.sheet-list {
  display: grid;
}

.top-row {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 11px 2px;
  border: 0;
  background: transparent;
  color: inherit;
  text-align: left;
}

.top-row + .top-row {
  border-top: 1px solid var(--color-border-subtle);
}

.top-row:hover {
  background: var(--color-bg-hover);
}

.top-mark {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.top-copy {
  display: flex;
  flex-direction: column;
  gap: 2px;
  flex: 1;
  min-width: 0;
}

.top-title {
  overflow: hidden;
  font-size: var(--text-footnote);
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.top-copy small {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.top-row b {
  flex: 0 0 auto;
  font-size: var(--text-footnote);
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.sheet-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 10px;
}

.sheet-jump {
  width: 100%;
  min-height: 44px;
  margin-top: 12px;
}

.state-inline {
  margin: 8px 0 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.6;
}
</style>
