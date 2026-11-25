<script setup lang="ts">
import { navigateBack } from '../utils/navigation'
import Decimal from 'decimal.js'
import { computed, onBeforeUnmount, onMounted, reactive, ref, useTemplateRef, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSlidingIndicator } from '../composables/useSlidingIndicator'
import {
  arriveFundSell,
  assignFundGroup,
  buyFund,
  cancelFundPending,
  confirmFundSell,
  correctFundShares,
  createFundGroup,
  createFundPosition,
  deleteFundGroup,
  fetchFundOverview,
  renameFundGroup,
  sellFund
} from '../api/funds'
import { fetchPlanOverview } from '../api/plans'
import { fetchQuoteStatus, refreshQuotes, type QuoteStatus } from '../api/quotes'
import { ApiError, getApiErrorMessage } from '../api/client'
import { useFundSearch } from '../composables/useFundSearch'
import { useAmountPrivacy } from '../composables/useAmountPrivacy'
import { toast } from '../composables/useMessage'
import AmountVisibilityButton from '../components/AmountVisibilityButton.vue'
import AppNav from '../components/AppNav.vue'
import AppIcon from '../components/AppIcon.vue'
import MoneyAmount from '../components/MoneyAmount.vue'
import EmptyState from '../components/EmptyState.vue'
import LoadingState from '../components/LoadingState.vue'
import BackButton from '../components/BackButton.vue'
import QuoteRefreshButton from '../components/QuoteRefreshButton.vue'
import InvestmentHeroMetrics from '../components/InvestmentHeroMetrics.vue'
import LoadingButton from '../components/LoadingButton.vue'
import BottomSheet from '../components/BottomSheet.vue'
import { useUnsavedSheet } from '../composables/useUnsavedSheet'
import ConfirmSheet from '../components/ConfirmSheet.vue'
import FundReturnsPanel from '../components/FundReturnsPanel.vue'
import InvestmentActivityList, { type InvestmentActivityRow } from '../components/InvestmentActivityList.vue'
import UpdateRing from '../components/UpdateRing.vue'
import FundGroupManagerSheet from '../components/funds/FundGroupManagerSheet.vue'
import {
  amountTone,
  calculatePercent,
  formatBusinessDate,
  addAmounts,
  formatMoney,
  formatQuantity,
  formatSignedMoney,
  parseAmount,
  parseNonNegativeQuantity,
  parseSignedAmount,
  parsePrice,
  parseQuantity,
  subtractAmounts,
  sumAmounts,
  todayInBusinessTimeZone
} from '../utils/format'
import {
  activityTypeOptions,
  buildFundActivityRows,
  monthDayText,
  type ActivityFilter,
  type ActivityRow,
  type ActivityStatusFilter
} from '../utils/fundActivities'
import { buildFallbackFunds, findDuplicateFundPosition } from '../utils/fundAggregation'
import { createRequestId } from '../utils/request'
import { latestFundDailyProfit } from '../utils/fundDailyProfit'
import { fundPlanDisplayName } from '../utils/plan'
import { planCycleLabel } from '../utils/planFormatting'
import type {
  FundOverview,
  FundPosition,
  FundSearchResult,
  FundSummary,
  PendingFundArrival,
  PendingFundBuy,
  PendingFundSell
} from '../types/fund'
import type { AutoPlan } from '../types/plan'

type SheetKind = 'opening' | 'buy' | 'buyConfirm' | 'shares' | 'sell' | 'sellConfirm' | 'sellArrival'
type PortfolioView = 'positions' | 'returns' | 'activities' | 'plans'
type GroupScope = 'all' | 'ungrouped' | string

const platformOptions = ['券商直销', '支付宝', '天天基金', '银行']

const route = useRoute()
const router = useRouter()
const portfolioViews: PortfolioView[] = ['positions', 'returns', 'activities', 'plans']

function normalizeView(value: unknown): PortfolioView {
  const candidate = Array.isArray(value) ? value[0] : value
  return portfolioViews.includes(candidate as PortfolioView) ? candidate as PortfolioView : 'positions'
}

const today = todayInBusinessTimeZone()
const { amountsMasked, maskMoney, maskSignedMoney } = useAmountPrivacy()
const overview = ref<FundOverview | null>(null)
const sellNetAmountManual = ref(false)
const isLoading = ref(false)
const errorMessage = ref('')
const quoteRefreshing = ref(false)
const quoteStatus = ref<QuoteStatus | null>(null)
let quoteStatusTimer: ReturnType<typeof setInterval> | null = null
let unmounted = false
const sheet = ref<SheetKind | null>(null)
const requestId = ref(createRequestId())
const isSaving = ref(false)
const formError = ref('')
const confirmingSellId = ref<string | null>(null)
const arrivingConfirmId = ref<string | null>(null)
const confirmingBuyId = ref<string | null>(null)
const duplicatePrompt = ref<{ position: FundPosition; shares: number; cost: number } | null>(null)
const activeGroup = ref<GroupScope>('all')
const activeView = ref<PortfolioView>(normalizeView(route.params.view))
const viewTabsRef = useTemplateRef<HTMLElement>('viewTabs')
const {
  ready: viewPillReady,
  animated: viewPillAnimated,
  indicatorStyle: viewPillStyle
} = useSlidingIndicator(viewTabsRef, activeView, {
  activeSelector: 'button.active'
})
const expandedFund = ref<string | null>(null)
const groupSheetOpen = ref(false)
const pendingGroupRemoval = ref<{ id: string; name: string } | null>(null)
const groupDraft = ref('')
const groupError = ref('')
const groupBusy = ref(false)
const cancellingPendingId = ref<string | null>(null)
let pendingCancelRequest: { transactionId: string; requestId: string } | null = null
const groupBusyKey = ref('')
const groupDetailId = ref<string | null>(null)
const editingGroupId = ref<string | null>(null)
const editingGroupName = ref('')
const plans = ref<AutoPlan[]>([])
const plansLoaded = ref(false)
const plansLoading = ref(false)
const plansError = ref('')
const selectedFund = ref<FundSearchResult | null>(null)
const {
  fundSearch,
  fundSearchResults,
  fundSearching,
  fundSearchError,
  cancelFundSearch,
  resetFundSearch
} = useFundSearch({
  onNonEmptyInput: () => {
    selectedFund.value = null
  }
})
const form = reactive({
  positionId: '',
  name: '',
  code: '',
  shareClass: '',
  platform: '',
  shares: '',
  cost: '',
  amount: '',
  netAmount: '',
  netValue: '',
  netValueDate: today,
  fee: '0',
  confirmationDelayDays: '2',
  businessDate: today,
  priorProfit: '',
  priorProfitSign: 1 as 1 | -1,
  note: ''
})

const { discardOpen, requestClose, resolveDiscard } = useUnsavedSheet(
  () => requestId.value,
  () => sheet.value !== null && sheet.value !== 'buyConfirm',
  () => isSaving.value,
  () => ({ ...form, search: fundSearch.value }),
  closeSheet
)

const selectedPosition = computed<FundPosition | null>(() =>
  overview.value?.positions.find((position) => position.id === form.positionId) ?? null)
const selectedSellApplication = computed<PendingFundSell | null>(() =>
  overview.value?.sellApplications.find((pending) => pending.transactionId === confirmingSellId.value) ?? null)
const selectedPendingBuy = computed<PendingFundBuy | null>(() =>
  overview.value?.pendingBuys.find((pending) => pending.transactionId === confirmingBuyId.value) ?? null)
const selectedPendingArrival = computed<PendingFundArrival | null>(() =>
  overview.value?.pendingArrivals.find((pending) => pending.transactionId === arrivingConfirmId.value) ?? null)

const allFunds = computed<FundSummary[]>(() => {
  const loaded = overview.value

  if (!loaded) {
    return []
  }

  if (loaded.funds?.length) {
    return loaded.funds
  }

  return buildFallbackFunds(loaded.positions, loaded.pendingBuys, loaded.pendingArrivals)
})

const groups = computed(() => overview.value?.groups ?? [])

const groupChips = computed(() => {
  const all = allFunds.value
  const ungroupedFunds = all.filter((fund) => !fund.groupId)
  const chips = [{
    key: 'all' as GroupScope,
    label: '全部',
    count: all.length,
    value: sumAmounts(all.map(fund => fund.assetValue))
  }, ...groups.value.map((group) => ({
    key: group.id as GroupScope,
    label: group.name,
    count: all.filter((fund) => fund.groupId === group.id).length,
    value: sumAmounts(all.filter((fund) => fund.groupId === group.id).map(fund => fund.assetValue))
  }))]

  if (ungroupedFunds.length) {
    chips.push({
      key: 'ungrouped' as GroupScope,
      label: '未分组',
      count: ungroupedFunds.length,
      value: sumAmounts(ungroupedFunds.map(fund => fund.assetValue))
    })
  }

  return chips
})

const managedGroup = computed(() =>
  groups.value.find((group) => group.id === groupDetailId.value) ?? null)

const scopedFunds = computed(() => {
  if (activeGroup.value === 'all') return allFunds.value
  if (activeGroup.value === 'ungrouped') return allFunds.value.filter(fund => !fund.groupId)
  return allFunds.value.filter(fund => fund.groupId === activeGroup.value)
})

const hasScopeFilter = computed(() => activeGroup.value !== 'all')
const returnsGroupId = computed(() =>
  activeGroup.value !== 'all' && activeGroup.value !== 'ungrouped' ? activeGroup.value : null)
const returnsUngrouped = computed(() => activeGroup.value === 'ungrouped')

const scopedPositionIds = computed(() => new Set(
  scopedFunds.value.flatMap((fund) => fund.classes.flatMap((fundClass) =>
    fundClass.platforms.map((platform) => platform.positionId)))
))

const scopedPendingBuys = computed(() => {
  if (!hasScopeFilter.value) {
    return overview.value?.pendingBuys ?? []
  }

  return (overview.value?.pendingBuys ?? []).filter((item) => scopedPositionIds.value.has(item.positionId))
})

const scopedSellApplications = computed(() => {
  if (!hasScopeFilter.value) {
    return overview.value?.sellApplications ?? []
  }

  return (overview.value?.sellApplications ?? []).filter((item) => scopedPositionIds.value.has(item.positionId))
})

const scopedPendingArrivals = computed(() => {
  if (!hasScopeFilter.value) {
    return overview.value?.pendingArrivals ?? []
  }

  return (overview.value?.pendingArrivals ?? []).filter((item) => scopedPositionIds.value.has(item.positionId))
})

const pendingCount = computed(() =>
  scopedPendingBuys.value.length + scopedSellApplications.value.length + scopedPendingArrivals.value.length)

const scopedActivities = computed(() => {
  const trades = (overview.value?.activities ?? []).filter((item) => item.type !== 'quote')

  if (!hasScopeFilter.value) {
    return trades
  }

  return trades.filter((item) => scopedPositionIds.value.has(item.positionId))
})

const activityTypeFilter = ref<ActivityFilter>('all')
const activityStatusFilter = ref<ActivityStatusFilter>('all')

const activityRows = computed<ActivityRow[]>(() =>
  buildFundActivityRows(
    scopedActivities.value,
    scopedPendingBuys.value,
    scopedSellApplications.value,
    scopedPendingArrivals.value
  )
)

const filteredActivityRows = computed(() => activityRows.value.filter((row) => {
  if (activityStatusFilter.value === 'pending' && !row.isPending) {
    return false
  }

  if (activityStatusFilter.value === 'done' && row.isPending) {
    return false
  }

  return activityTypeFilter.value === 'all' || row.kind === activityTypeFilter.value
}))

function openActivityRow(item: InvestmentActivityRow): void {
  const row = item as ActivityRow
  if (!row.pending) {
    return
  }

  if (row.pendingAction === 'buy') {
    openBuyConfirmSheet(row.pending as PendingFundBuy)
  } else if (row.pendingAction === 'sell') {
    openSellConfirmSheet(row.pending as PendingFundSell)
  } else if (row.pendingAction === 'arrival') {
    openArrivalSheet(row.pending as PendingFundArrival)
  }
}

const scope = computed(() => {
  const confirmedValue = sumAmounts(scopedFunds.value.map(fund => fund.value))
  const cost = sumAmounts(scopedFunds.value.map(fund => fund.cost))
  const holdingProfit = sumAmounts(scopedFunds.value.map(fund => fund.holdingProfit))
  const realizedProfit = sumAmounts(scopedFunds.value.map(fund => fund.realizedProfit))
  const pendingBuyAmount = sumAmounts(scopedPendingBuys.value.map(item => item.amount))
  const pendingArrivalAmount = sumAmounts(scopedPendingArrivals.value.map(item => item.amount))

  return {
    cost,
    totalProfit: addAmounts(holdingProfit, realizedProfit),
    pendingBuyAmount,
    pendingArrivalAmount,
    assetValue: addAmounts(confirmedValue, pendingBuyAmount, pendingArrivalAmount),
    hasEffectiveQuotes: scopedFunds.value.every((fund) => fund.hasEffectiveQuotes),
    historyConfirmed: scopedFunds.value.every((fund) => fund.historyConfirmed)
  }
})

const sortedFunds = computed(() => {
  return [...scopedFunds.value].sort((left, right) =>
    new Decimal(right.assetValue).comparedTo(left.assetValue)
      || left.name.localeCompare(right.name, 'zh-Hans-CN'))
})

function cumulativeProfit(fund: FundSummary): number {
  return addAmounts(fund.holdingProfit, fund.realizedProfit)
}

const todayProfit = computed(() => latestFundDailyProfit(scopedFunds.value, today))

const todayProfitRate = computed(() => {
  const { latestDate, value } = todayProfit.value
  if (!latestDate) return null

  const disclosedValue = sumAmounts(scopedFunds.value
    .filter(fund => fund.hasDailyProfit && fund.dailyProfitDate === latestDate)
    .map(fund => fund.value))
  return calculatePercent(value, subtractAmounts(disclosedValue, value))
})
const totalUnavailable = computed(() => {
  if (!scope.value.hasEffectiveQuotes) return '行情待补'
  if (!scope.value.historyConfirmed) return '待核对'
  return undefined
})
const totalProfit = computed(() => totalUnavailable.value ? null : scope.value.totalProfit)
const totalRate = computed(() => calculatePercent(totalProfit.value, scope.value.cost))

const isUpdating = computed(() => isLoading.value || quoteRefreshing.value || (quoteStatus.value?.isRunning ?? false))
const hasQuoteError = computed(() => {
  const run = quoteStatus.value?.lastRun
  return !isUpdating.value && !!run && run.fundAttempted > 0 && run.fundFailed > 0
})
const todayProfitTone = computed(() => amountTone(todayProfit.value.hasValue ? todayProfit.value.value : null))
const quoteProfitTone = computed(() =>
  quoteStatus.value && todayProfit.value.complete && !hasQuoteError.value ? todayProfitTone.value : ''
)
const todayProfitStatus = computed(() => {
  if (!todayProfit.value.hasValue) {
    return '行情待补'
  }

  const parts: string[] = []

  if (hasQuoteError.value) {
    parts.push('更新失败')
  }

  if (!todayProfit.value.isToday && todayProfit.value.latestDate) {
    parts.push(`${monthDayText(todayProfit.value.latestDate)}净值`)
  }

  if (!todayProfit.value.complete) {
    parts.push(`${todayProfit.value.covered}/${todayProfit.value.total} 只已披露`)
  }

  return parts.join(' · ')
})

const scopedPlans = computed(() => {
  if (!hasScopeFilter.value) {
    return plans.value
  }

  return plans.value.filter((plan) => plan.fundPositionId !== null && scopedPositionIds.value.has(plan.fundPositionId))
})

const sheetTitle = computed(() => {
  switch (sheet.value) {
    case 'opening':
      return '录入基金持仓'
    case 'buy':
      return '记录基金买入'
    case 'buyConfirm':
      return '待确认交易'
    case 'shares':
      return '调整基金份额'
    case 'sell':
      return '申请赎回基金'
    case 'sellConfirm':
      return '确认基金赎回'
    case 'sellArrival':
      return '登记赎回到账'
    default:
      return ''
  }
})
const sheetSubtitle = computed(() => {
  switch (sheet.value) {
    case 'opening':
      return '用于已有持仓，不扣现金'
    case 'buy':
      return `现金减少，T+${selectedPosition.value?.confirmationDelayDays ?? 2} 自动确认份额`
    case 'buyConfirm':
      return '买入后按确认净值自动结转份额'
    case 'shares':
      return '生成份额核对记录，保留原有流水'
    case 'sell':
      return '份额冻结，仍在持仓中估值'
    case 'sellConfirm':
      return '份额减少，净回款转为待到账'
    case 'sellArrival':
      return '待到账转入现金，不重复计算收益'
    default:
      return ''
  }
})
const sheetNote = computed(() => {
  switch (sheet.value) {
    case 'opening':
      return '录入已有持仓不会从现金扣除购买成本，也不产生生活收支。'
    case 'buy':
      return '按提交时间所在交易日的基金净值确认份额，15:00 前可撤销；份额由行情数据源自动计算。'
    case 'shares':
      return '只修改持仓份额，净值必须由行情数据源获取。'
    case 'sell':
      return '申请后份额仍计入持仓估值，但会冻结，不能重复申请超过可售份额。'
    case 'sellConfirm':
      return '按确认份额比例结转成本；净回款转为待到账，与成本差额计入已实现收益。'
    case 'sellArrival':
      return '到账金额必须与待到账金额一致，不再次确认收益；如有差额，请先更正赎回确认。'
    default:
      return ''
  }
})
const canSubmit = computed(() => {
  if (isSaving.value) {
    return false
  }

  switch (sheet.value) {
    case 'opening':
      return form.name.trim() !== ''
        && form.code.trim() !== ''
        && parseNonNegativeQuantity(form.shares) !== null
        && parseAmount(form.cost) !== null
        && form.businessDate !== ''
    case 'buy':
      return form.positionId !== '' && !!selectedPosition.value?.code && (parseAmount(form.amount) ?? 0) > 0
        && (parseAmount(form.fee) ?? -1) >= 0
        && (parseAmount(form.fee) ?? Number.POSITIVE_INFINITY) < (parseAmount(form.amount) ?? 0)
    case 'shares':
      return form.positionId !== '' && parseNonNegativeQuantity(form.shares) !== null
    case 'sell':
      return form.positionId !== '' && parseQuantity(form.shares) !== null && form.businessDate !== ''
    case 'sellConfirm':
      return confirmingSellId.value !== null
        && parseQuantity(form.shares) !== null
        && parsePrice(form.netValue) !== null
        && (parseAmount(form.netAmount) ?? 0) > 0
        && parseAmount(form.fee) !== null
        && form.businessDate !== ''
    case 'sellArrival':
      return arrivingConfirmId.value !== null && (parseAmount(form.netAmount) ?? 0) > 0
        && form.businessDate !== ''
    default:
      return false
  }
})

watch(
  () => form.positionId,
  (positionId) => {
    const position = overview.value?.positions.find((item) => item.id === positionId)

    if (!position || sheet.value !== 'shares') {
      return
    }

    form.shares = String(position.shares)
  }
)

onMounted(async () => {
  await loadOverview()
  if (unmounted) {
    return
  }

  void loadQuoteStatus()
  startQuoteStatusPolling()

  if (activeView.value === 'plans') {
    await loadPlans()
  }
})

watch(
  () => route.params.view,
  (value) => {
    activeView.value = normalizeView(value)

    if (activeView.value === 'plans') {
      void loadPlans()
    }
  }
)

async function loadOverview(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''

  try {
    overview.value = await fetchFundOverview()
    ensureActiveGroup()

    if (!form.positionId && sheet.value !== 'opening' && overview.value.positions.length > 0) {
      form.positionId = overview.value.positions[0]!.id
    }
  } catch (error) {
    overview.value = null

    if (error instanceof ApiError && error.status === 404) {
      await router.replace({ name: 'setup' })
      return
    }

    errorMessage.value = getApiErrorMessage(error, '基金数据加载失败，请重试。')
  } finally {
    isLoading.value = false
  }
}

async function loadQuoteStatus(reloadOnCompletion = true): Promise<void> {
  try {
    const wasUpdating = quoteStatus.value?.isRunning ?? false
    const status = await fetchQuoteStatus()
    quoteStatus.value = status

    if (reloadOnCompletion && wasUpdating && !status.isRunning) {
      await loadOverview()
    }
  } catch {
    // 行情状态只是辅助信息，读取失败不阻塞基金页。
  }
}

async function syncQuotes(): Promise<void> {
  if (isUpdating.value || !overview.value) return

  quoteRefreshing.value = true
  try {
    const run = await refreshQuotes('funds')
    await Promise.all([loadOverview(), loadQuoteStatus(false)])
    if (!overview.value) return

    if (run.fundFailed > 0) {
      toast.info(`刷新完成：成功 ${run.fundSucceeded}，失败 ${run.fundFailed}`)
    } else if (run.fundSucceeded === 0) {
      toast.info('暂无可刷新行情')
    } else if (run.updated === 0) {
      toast.info('行情已是最新')
    } else {
      toast.success(`已更新 ${run.updated} 项行情`)
    }
  } catch (error) {
    toast.error(getApiErrorMessage(error, '行情刷新失败'))
  } finally {
    quoteRefreshing.value = false
  }
}

function startQuoteStatusPolling(): void {
  if (unmounted || quoteStatusTimer !== null) {
    return
  }

  quoteStatusTimer = setInterval(() => {
    if (document.visibilityState === 'hidden' || quoteRefreshing.value) {
      return
    }

    void loadQuoteStatus()
  }, 15000)
}

function stopQuoteStatusPolling(): void {
  if (quoteStatusTimer !== null) {
    clearInterval(quoteStatusTimer)
    quoteStatusTimer = null
  }
}

function ensureActiveGroup(): void {
  if (activeGroup.value === 'all') {
    return
  }

  if (activeGroup.value === 'ungrouped') {
    if (!allFunds.value.some((fund) => !fund.groupId)) {
      activeGroup.value = 'all'
    }

    return
  }

  if (!groups.value.some((group) => group.id === activeGroup.value)) {
    activeGroup.value = 'all'
  }
}

function metricDisplay(fund: FundSummary) {
  const cumulative = cumulativeProfit(fund)
  const rate = calculatePercent(cumulative, fund.cost)
  return [
    {
      key: 'daily',
      label: '日收益',
      value: fund.hasDailyProfit ? maskSignedMoney(fund.dailyProfit) : '—',
      sub: fund.hasDailyProfit && fund.dailyProfitRate !== null && !amountsMasked.value
        ? `${formatSignedMoney(fund.dailyProfitRate)}%`
        : undefined,
      tone: amountsMasked.value ? '' : amountTone(fund.hasDailyProfit ? fund.dailyProfit : null)
    },
    {
      key: 'cumulative',
      label: '累计收益',
      value: fund.historyConfirmed ? maskSignedMoney(cumulative) : '待核对',
      tone: amountsMasked.value ? '' : amountTone(fund.historyConfirmed ? cumulative : null)
    },
    {
      key: 'cumulativeRate',
      label: '累计收益率',
      value: !fund.historyConfirmed
        ? '待核对'
        : !fund.hasEffectiveQuotes
          ? '行情待补'
          : rate === null ? '—' : (amountsMasked.value ? '••••' : `${formatSignedMoney(rate)}%`),
      tone: amountsMasked.value ? '' : amountTone(fund.historyConfirmed && fund.hasEffectiveQuotes ? rate : null)
    }
  ]
}

function weightOf(fund: FundSummary): string {
  const weight = calculatePercent(fund.assetValue, scope.value.assetValue)
  return weight !== null ? `${formatMoney(weight)}%` : '—'
}

// 待确认买入在确认净值日就定了份额，只是还没确认，所以卡片资产要把它算进去，
// 否则各只基金加起来和顶部总额对不上。
function pendingOf(fund: FundSummary): number {
  return addAmounts(fund.pendingBuyAmount, fund.pendingArrivalAmount)
}

function toggleFund(key: string): void {
  expandedFund.value = expandedFund.value === key ? null : key
}

function selectGroup(key: GroupScope): void {
  activeGroup.value = key
  expandedFund.value = null
}

async function setView(view: PortfolioView): Promise<void> {
  activeView.value = view
  const target = view === 'positions' ? '/funds' : `/funds/${view}`

  if (route.path !== target) {
    await router.replace(target)
  }

  if (view === 'plans') {
    await loadPlans()
  }
}

async function loadPlans(): Promise<void> {
  if (plansLoaded.value || plansLoading.value) {
    return
  }

  plansLoading.value = true
  plansError.value = ''

  try {
    const result = await fetchPlanOverview()
    plans.value = result.plans.filter((plan) => plan.type === 'fundSip')
    plansLoaded.value = true
  } catch (error) {
    plansError.value = getApiErrorMessage(error, '定投计划暂时无法读取。')
  } finally {
    plansLoading.value = false
  }
}

function openGroupSheet(): void {
  groupSheetOpen.value = true
  groupDetailId.value = null
  groupDraft.value = ''
  groupError.value = ''
  editingGroupId.value = null
  editingGroupName.value = ''
}

function closeGroupSheet(): void {
  if (!groupBusy.value) {
    groupSheetOpen.value = false
    groupDetailId.value = null
    editingGroupId.value = null
  }
}

function openGroupDetail(groupId: string): void {
  groupDetailId.value = groupId
  groupError.value = ''
  editingGroupId.value = null
  editingGroupName.value = ''
}

function backToGroupList(): void {
  groupDetailId.value = null
  groupError.value = ''
  editingGroupId.value = null
  editingGroupName.value = ''
}

async function submitGroup(): Promise<void> {
  const name = groupDraft.value.trim()

  if (!name) {
    groupError.value = '请输入分组名称。'
    return
  }

  groupBusy.value = true
  groupError.value = ''

  try {
    const created = await createFundGroup(name)
    groupDraft.value = ''
    toast.success('分组已新增')
    await loadOverview()
    groupDetailId.value = created.id
  } catch (error) {
    groupError.value = getApiErrorMessage(error, '新增分组失败，请重试。')
  } finally {
    groupBusy.value = false
  }
}

function startRenameGroup(groupId: string, name: string): void {
  editingGroupId.value = groupId
  editingGroupName.value = name
  groupError.value = ''
}

async function saveGroupName(groupId: string): Promise<void> {
  const name = editingGroupName.value.trim()

  if (!name) {
    groupError.value = '分组名称不能为空。'
    return
  }

  groupBusy.value = true
  groupError.value = ''

  try {
    await renameFundGroup(groupId, name)
    editingGroupId.value = null
    toast.success('分组已更名')
    await loadOverview()
  } catch (error) {
    groupError.value = getApiErrorMessage(error, '分组改名失败，请重试。')
  } finally {
    groupBusy.value = false
  }
}

function removeGroup(groupId: string, name: string): void {
  pendingGroupRemoval.value = { id: groupId, name }
}

async function confirmRemoveGroup(): Promise<void> {
  const target = pendingGroupRemoval.value

  if (!target || groupBusy.value) {
    return
  }

  groupBusy.value = true
  groupError.value = ''

  try {
    await deleteFundGroup(target.id)

    if (activeGroup.value === target.id) {
      activeGroup.value = 'all'
    }

    if (groupDetailId.value === target.id) {
      groupDetailId.value = null
    }

    toast.success('分组已删除')
    pendingGroupRemoval.value = null
    await loadOverview()
  } catch (error) {
    groupError.value = getApiErrorMessage(error, '删除分组失败，请重试。')
    pendingGroupRemoval.value = null
  } finally {
    groupBusy.value = false
  }
}

async function toggleGroupFund(fund: FundSummary): Promise<void> {
  const group = managedGroup.value
  const positionIds = Array.from(new Set(
    fund.classes.flatMap((fundClass) => fundClass.platforms).map((platform) => platform.positionId)))

  if (!group || positionIds.length === 0 || groupBusy.value) {
    return
  }

  const isMember = fund.groupId === group.id
  groupBusy.value = true
  groupBusyKey.value = fund.key
  groupError.value = ''

  try {
    // 同一基金可能有多个平台持仓；逐个移动，避免并发请求争抢账本版本。
    for (const positionId of positionIds) {
      await assignFundGroup(positionId, isMember ? null : group.id)
    }

    toast.success(isMember ? '已移出分组' : '已加入分组')
    await loadOverview()
  } catch (error) {
    groupError.value = getApiErrorMessage(error, '调整分组资产失败，请重试。')
    await loadOverview()
  } finally {
    groupBusy.value = false
    groupBusyKey.value = ''
  }
}

function openSheet(kind: SheetKind, positionId?: string): void {
  sheet.value = kind
  confirmingSellId.value = null
  arrivingConfirmId.value = null
  confirmingBuyId.value = null
  requestId.value = createRequestId()
  formError.value = ''
  form.note = ''
  form.businessDate = today
  form.priorProfit = ''
  form.priorProfitSign = 1
  form.shares = ''
  form.cost = ''
  form.amount = ''
  form.netAmount = ''
  form.fee = '0'
  form.name = ''
  form.code = ''
  form.shareClass = ''
  form.platform = '券商直销'
  form.confirmationDelayDays = '1'
  selectedFund.value = null
  fundSearch.value = ''
  fundSearchResults.value = []
  fundSearchError.value = ''
  fundSearching.value = false

  if (kind === 'opening') {
    form.positionId = ''
    form.netValue = ''
    form.netValueDate = today
    return
  }

  form.positionId = positionId ?? overview.value?.positions[0]?.id ?? ''
  form.platform = ''

  const position = selectedPosition.value
  if (kind === 'shares' && position) {
    form.shares = String(position.shares)
  }
}

function openSellConfirmSheet(application: PendingFundSell): void {
  openSheet('sellConfirm', application.positionId)
  confirmingSellId.value = application.transactionId
  sellNetAmountManual.value = false
  form.shares = String(application.shares)
  form.netValue = ''
  form.netValueDate = application.businessDate
  form.businessDate = today
  form.note = application.note ?? ''
}

watch(
  () => [sheet.value, form.shares, form.netValue, form.fee] as const,
  ([currentSheet]) => {
    if (currentSheet !== 'sellConfirm' || sellNetAmountManual.value) return
    const shares = parseQuantity(form.shares)
    const netValue = parsePrice(form.netValue)
    const fee = parseAmount(form.fee)
    if (shares === null || netValue === null || netValue <= 0 || fee === null || fee < 0) {
      form.netAmount = ''
      return
    }
    const gross = new Decimal(shares).mul(netValue).minus(fee)
    form.netAmount = gross.gt(0) ? gross.toFixed(2, Decimal.ROUND_DOWN) : ''
  }
)

function onSellNetAmountInput(): void {
  sellNetAmountManual.value = true
  formError.value = ''
}

function estimateSellNetAmount(): void {
  sellNetAmountManual.value = false
  const shares = parseQuantity(form.shares)
  const netValue = parsePrice(form.netValue)
  const fee = parseAmount(form.fee)

  if (shares === null || netValue === null || netValue <= 0 || fee === null || fee < 0) {
    formError.value = '请先填写有效的确认份额、成交净值和费用。'
    return
  }

  const gross = new Decimal(shares).mul(netValue).minus(fee)

  if (gross.lte(0)) {
    formError.value = '扣除费用后的净回款必须大于 0。'
    return
  }

  formError.value = ''
  form.netAmount = gross.toFixed(2, Decimal.ROUND_DOWN)
}

function openBuyConfirmSheet(pending: PendingFundBuy): void {
  openSheet('buyConfirm', pending.positionId)
  confirmingBuyId.value = pending.transactionId
}

function openArrivalSheet(arrival: PendingFundArrival): void {
  openSheet('sellArrival', arrival.positionId)
  arrivingConfirmId.value = arrival.transactionId
  form.netAmount = String(arrival.amount)
  form.businessDate = today
  form.note = arrival.note ?? ''
}

function closeSheet(): void {
  if (!isSaving.value) {
    cancelFundSearch()
    sheet.value = null
  }
}

async function submit(): Promise<void> {
  if (!sheet.value || !overview.value) {
    return
  }

  formError.value = ''

  try {
    if (sheet.value === 'opening') {
      await submitOpening()
    } else if (sheet.value === 'buy') {
      await submitBuy()
    } else if (sheet.value === 'shares') {
      await submitShareCorrection()
    } else if (sheet.value === 'sell') {
      await submitSell()
    } else if (sheet.value === 'sellConfirm') {
      await submitSellConfirm()
    } else if (sheet.value === 'sellArrival') {
      await submitSellArrival()
    }
  } catch (error) {
    formError.value = getApiErrorMessage(error, '保存失败，请重试。')
  } finally {
    isSaving.value = false
  }
}

function setPriorProfitSign(sign: 1 | -1): void {
  form.priorProfitSign = sign
  form.priorProfit = form.priorProfit.replace(/^[+-]/, '')
}

function handlePriorProfitInput(): void {
  const trimmed = form.priorProfit.trim()
  if (trimmed.startsWith('-')) {
    form.priorProfitSign = -1
    form.priorProfit = trimmed.slice(1)
  } else if (trimmed.startsWith('+')) {
    form.priorProfitSign = 1
    form.priorProfit = trimmed.slice(1)
  }
}

function parsePriorProfit(): number | null {
  const raw = form.priorProfit.trim()
  if (!raw) return 0
  const signed = raw.startsWith('-') || raw.startsWith('+')
    ? raw
    : `${form.priorProfitSign < 0 ? '-' : ''}${raw}`
  const parsed = parseSignedAmount(signed)
  return parsed === 0 ? 0 : parsed
}

async function submitOpening(forceAdd = false): Promise<void> {
  const shares = parseNonNegativeQuantity(form.shares)
  const cost = parseAmount(form.cost)
  const priorRealizedProfit = parsePriorProfit()

  if (!form.name.trim()) {
    formError.value = '请输入基金名称。'
    return
  }

  if (shares === null || cost === null) {
    formError.value = '请填写有效的份额和成本。'
    return
  }

  if (priorRealizedProfit === null) {
    formError.value = '建账前已实现收益请填写有效金额，可为负数，最多两位小数；不填按 0 处理。'
    return
  }

  if (!form.businessDate) {
    formError.value = '请选择持仓截至日期。'
    return
  }

  if (shares === 0 && cost !== 0) {
    formError.value = '份额为 0 时，持仓成本也应为 0。'
    return
  }

  if (!forceAdd) {
    const duplicate = findDuplicatePosition()

    if (duplicate) {
      duplicatePrompt.value = { position: duplicate, shares, cost }
      return
    }
  }

  isSaving.value = true
  await createFundPosition({
    name: form.name.trim(),
    code: form.code.trim(),
    shareClass: form.shareClass.trim(),
    platform: form.platform.trim(),
    shares,
    cost,
    confirmationDelayDays: Number(form.confirmationDelayDays),
    note: form.note.trim(),
    holdingDate: form.businessDate,
    priorRealizedProfit,
    requestId: requestId.value
  })
  toast.success('基金已录入')
  sheet.value = null
  await loadOverview()
}

function findDuplicatePosition(): FundPosition | null {
  return findDuplicateFundPosition(
    overview.value?.positions,
    form.code,
    form.shareClass,
    form.platform
  )
}

async function updateDuplicatePosition(): Promise<void> {
  const prompt = duplicatePrompt.value

  if (!prompt || isSaving.value) {
    return
  }

  isSaving.value = true
  formError.value = ''

  try {
    await correctFundShares({
      positionId: prompt.position.id,
      shares: prompt.shares,
      cost: prompt.cost,
      note: form.note.trim() || '按新录入核对已有持仓',
      requestId: requestId.value
    })
    toast.success('持仓已更新')
    duplicatePrompt.value = null
    sheet.value = null
    await loadOverview()
  } catch (error) {
    formError.value = getApiErrorMessage(error, '更新已有持仓失败，请重试。')
  } finally {
    isSaving.value = false
  }
}

async function addDuplicatePosition(): Promise<void> {
  if (isSaving.value) {
    return
  }

  duplicatePrompt.value = null
  formError.value = ''

  try {
    await submitOpening(true)
  } catch (error) {
    formError.value = getApiErrorMessage(error, '保存失败，请重试。')
  } finally {
    isSaving.value = false
  }
}

async function submitBuy(): Promise<void> {
  const position = selectedPosition.value
  const amount = parseAmount(form.amount)
  const fee = parseAmount(form.fee)

  if (!position) {
    formError.value = '请选择基金持仓。'
    return
  }

  if (amount === null || amount <= 0 || fee === null || fee < 0 || fee >= amount) {
    formError.value = '请填写有效的扣款金额和小于扣款金额的申购费。'
    return
  }

  isSaving.value = true
  await buyFund({
    positionId: position.id,
    amount,
    fee,
    businessDate: todayInBusinessTimeZone(),
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success('买入已记录')
  sheet.value = null
  await loadOverview()
}

async function submitShareCorrection(): Promise<void> {
  const position = selectedPosition.value
  const shares = parseNonNegativeQuantity(form.shares)
  if (!position || shares === null) {
    formError.value = '请填写有效的持仓份额。'
    return
  }

  isSaving.value = true
  await correctFundShares({
    positionId: position.id,
    shares,
    note: form.note.trim() || '持仓份额核对',
    requestId: requestId.value
  })
  toast.success('持仓已更新')
  sheet.value = null
  await loadOverview()
}

async function submitSell(): Promise<void> {
  const position = selectedPosition.value
  const shares = parseQuantity(form.shares)

  if (!position) {
    formError.value = '请选择基金持仓。'
    return
  }

  if (shares === null) {
    formError.value = '请填写有效的申请赎回份额。'
    return
  }

  isSaving.value = true
  await sellFund({
    positionId: position.id,
    shares,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success('赎回已申请')
  sheet.value = null
  await loadOverview()
}

async function submitSellConfirm(): Promise<void> {
  const application = selectedSellApplication.value
  const shares = parseQuantity(form.shares)
  const netValue = parsePrice(form.netValue)
  const netAmount = parseAmount(form.netAmount)
  const fee = parseAmount(form.fee)

  if (!application) {
    formError.value = '赎回申请不存在，请刷新后重试。'
    return
  }

  if (shares === null || netValue === null || netAmount === null || netAmount <= 0 || fee === null || fee < 0) {
    formError.value = '请填写有效的确认份额、净值、净回款和费用。'
    return
  }

  if (shares > application.shares) {
    formError.value = '确认份额不能超过申请赎回份额。'
    return
  }

  if (fee > netAmount) {
    formError.value = '费用不能超过净回款金额。'
    return
  }

  if (form.businessDate < application.businessDate) {
    formError.value = '确认日期不能早于申请日期。'
    return
  }

  const valueDateError = validateValueDate(form.netValueDate, '净值日期')

  if (valueDateError) {
    formError.value = valueDateError
    return
  }

  if (form.netValueDate > form.businessDate) {
    formError.value = '净值日期不能晚于确认日期。'
    return
  }

  isSaving.value = true
  await confirmFundSell(application.transactionId, {
    shares,
    netValue,
    netValueDate: form.netValueDate,
    fee,
    netAmount,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success('赎回已确认')
  sheet.value = null
  await loadOverview()
}

async function submitSellArrival(): Promise<void> {
  const arrival = selectedPendingArrival.value
  const amount = parseAmount(form.netAmount)

  if (!arrival) {
    formError.value = '待到账记录不存在，请刷新后重试。'
    return
  }

  if (amount === null || amount <= 0) {
    formError.value = '请填写有效的到账金额。'
    return
  }

  if (amount !== arrival.amount) {
    formError.value = '到账金额必须与待到账金额一致；如有差额，请先更正赎回确认。'
    return
  }

  if (form.businessDate < arrival.businessDate) {
    formError.value = '到账日期不能早于确认日期。'
    return
  }

  isSaving.value = true
  await arriveFundSell(arrival.transactionId, {
    amount,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success('赎回已到账')
  sheet.value = null
  await loadOverview()
}

function validateValueDate(value: string, label: string): string | null {
  if (!value) {
    return `请选择${label}。`
  }

  if (value > today) {
    return `${label}不能晚于今天。`
  }

  return null
}

function positionLabel(position: FundPosition): string {
  return position.shareClass ? `${position.name} ${position.shareClass}` : position.name
}

onBeforeUnmount(() => {
  unmounted = true
  stopQuoteStatusPolling()
})

function chooseFund(result: FundSearchResult): void {
  selectedFund.value = result
  form.name = result.baseName
  form.code = result.code
  form.shareClass = result.shareClass ?? ''
  form.confirmationDelayDays = String(result.confirmationDelayDays)
  resetFundSearch()
}

function clearSelectedFund(): void {
  selectedFund.value = null
  form.name = ''
  form.code = ''
  form.shareClass = ''
  form.confirmationDelayDays = '1'
  resetFundSearch()
}

async function cancelPending(transactionId: string): Promise<void> {
  if (cancellingPendingId.value) {
    return
  }

  cancellingPendingId.value = transactionId
  if (pendingCancelRequest?.transactionId !== transactionId) {
    pendingCancelRequest = { transactionId, requestId: createRequestId() }
  }

  try {
    await cancelFundPending(transactionId, pendingCancelRequest.requestId)
    pendingCancelRequest = null
    toast.success('交易已撤销')
    sheet.value = null
    await loadOverview()
  } catch (error) {
    formError.value = getApiErrorMessage(error, '撤销失败，请刷新后重试。')
  } finally {
    cancellingPendingId.value = null
  }
}

function goBack(): void {
  navigateBack(router)
}
</script>

<template>
  <main class="page funds-page with-nav">
    <header class="funds-header asset-page-header">
      <BackButton @back="goBack" />
      <h1>基金</h1>
      <div class="funds-header-actions">
        <details class="investment-actions">
          <summary class="icon-button" aria-label="记录投资"><AppIcon name="plus" /></summary>
          <div class="funds-actions" @click="($event.currentTarget as HTMLElement).closest('details')?.removeAttribute('open')">
            <button type="button" class="action-secondary" @click="openSheet('opening')">录入持仓</button>
            <button type="button" class="action-primary" :disabled="!overview?.positions.length" @click="openSheet('buy')">买入</button>
            <button type="button" class="action-secondary" :disabled="!overview?.positions.length" @click="openSheet('sell')">卖出</button>
          </div>
        </details>
        <QuoteRefreshButton :refreshing="isUpdating" :disabled="!overview" @refresh="syncQuotes" />
      </div>
    </header>

    <template v-if="isLoading && !overview">
      <LoadingState variant="hero" label="正在读取基金" />
      <LoadingState variant="list" :rows="4" label="正在读取基金列表" />
    </template>

    <section v-else-if="errorMessage" class="state">
      <p>{{ errorMessage }}</p>
      <LoadingButton class="secondary-button" :loading="isLoading" loading-label="加载中…" @click="loadOverview">重新加载</LoadingButton>
    </section>

    <template v-else-if="overview">
      <section class="funds-hero investment-hero">
        <p class="hero-label">
          总资产
          <span class="hero-tag">{{ scope.historyConfirmed ? '已确认持仓' : '持仓日期待确认' }}</span>
          <AmountVisibilityButton />
        </p>
        <h2 class="hero-balance">
          <template v-if="scope.hasEffectiveQuotes"><span class="hero-currency">¥</span><MoneyAmount :value="scope.assetValue" prefix="" /></template>
          <template v-else>行情待补</template>
        </h2>
        <div class="funds-metrics">
          <InvestmentHeroMetrics
            :daily-profit="todayProfit.hasValue ? todayProfit.value : null"
            :daily-rate="todayProfitRate"
            :total-profit="totalProfit"
            :total-rate="totalRate"
            :total-unavailable="totalUnavailable"
          />
          <small v-if="isUpdating" class="hero-update is-updating">
            <UpdateRing :value="todayProfit.covered" :total="todayProfit.total" indeterminate />
            <span>
              更新中…
              <template v-if="todayProfit.total > 0"> · {{ todayProfit.covered }}/{{ todayProfit.total }}</template>
            </span>
          </small>
          <small v-else-if="!todayProfit.hasValue" class="hero-update">
            <UpdateRing :value="0" :total="todayProfit.total" />
            <span>行情待补</span>
          </small>
          <small v-else-if="todayProfitStatus || quoteProfitTone" class="hero-update" :class="{ 'is-warning': hasQuoteError }">
            <UpdateRing :value="todayProfit.covered" :total="todayProfit.total" :profit-tone="quoteProfitTone" />
            <span>{{ todayProfitStatus || '净值已更新' }}</span>
          </small>
          <p class="hero-foot">
            {{ scopedFunds.length }} 只基金 · 持仓成本 <MoneyAmount :value="scope.cost" />
            <template v-if="scope.pendingBuyAmount > 0"> · 含待确认 <MoneyAmount :value="scope.pendingBuyAmount" /></template>
            <template v-if="scope.pendingArrivalAmount > 0"> · 含待到账 <MoneyAmount :value="scope.pendingArrivalAmount" /></template>
          </p>
        </div>
      </section>

      <div ref="viewTabs" class="view-tabs ui-segmented" :class="{ 'has-pill': viewPillReady }" role="tablist" aria-label="投资视图">
        <span
          v-if="viewPillReady"
          class="view-tabs-pill"
          :class="{ 'is-animated': viewPillAnimated }"
          :style="viewPillStyle"
          aria-hidden="true"
        />
        <button type="button" role="tab" :aria-selected="activeView === 'positions'" :class="{ active: activeView === 'positions' }" @click="setView('positions')">持仓明细</button>
        <button type="button" role="tab" :aria-selected="activeView === 'returns'" :class="{ active: activeView === 'returns' }" @click="setView('returns')">收益日历</button>
        <button
          type="button"
          role="tab"
          :aria-selected="activeView === 'activities'"
          :aria-label="pendingCount ? `交易记录，${pendingCount} 笔待处理` : '交易记录'"
          :class="{ active: activeView === 'activities' }"
          @click="setView('activities')"
        >
          交易记录
          <span v-if="pendingCount" class="tab-badge" aria-hidden="true">{{ pendingCount }}</span>
        </button>
        <button type="button" role="tab" :aria-selected="activeView === 'plans'" :class="{ active: activeView === 'plans' }" @click="setView('plans')">我的定投</button>
      </div>

      <div class="group-filter">
        <div class="group-pills" role="tablist" aria-label="筛选投资分组">
          <button
            v-for="chip in groupChips"
            :key="chip.key"
            type="button"
            role="tab"
            class="group-pill"
            :class="{ active: activeGroup === chip.key }"
            :aria-selected="activeGroup === chip.key"
            @click="selectGroup(chip.key)"
          >
            {{ chip.label }}
          </button>
        </div>
        <button type="button" class="group-manage" @click="openGroupSheet">
          管理
          <AppIcon name="chevron" />
        </button>
      </div>
      <template v-if="activeView === 'returns'">
        <FundReturnsPanel
          :group-id="returnsGroupId"
          :ungrouped="returnsUngrouped"
        />
      </template>

      <template v-else-if="activeView === 'positions'">
        <div class="section-head">
          <h2>持仓明细</h2>
          <span class="meta">可用现金 {{ maskMoney(overview.cashBalance) }}</span>
        </div>

        <section v-if="sortedFunds.length" class="fund-list">
          <article
            v-for="(fund, index) in sortedFunds"
            :key="fund.key"
            class="fund-card stagger-item"
            :style="{ '--stagger': Math.min(index, 8) }"
          >
            <header
              role="button"
              tabindex="0"
              @click="toggleFund(fund.key)"
              @keydown.enter="toggleFund(fund.key)"
              @keydown.space.prevent="toggleFund(fund.key)"
              :aria-expanded="expandedFund === fund.key"
            >
              <div class="fund-heading">
                <div class="fund-title">
                  <strong>{{ fund.name }}</strong>
                  <small>
                    {{ fund.groupName ?? '未分组' }} · {{ fund.classes.length }} 个类别
                  </small>
                </div>
                <AppIcon class="expand-mark" :class="{ expanded: expandedFund === fund.key }" name="chevron" />
              </div>
              <div class="fund-values">
                <div class="fund-asset">
                  <b><MoneyAmount :value="fund.assetValue" /></b>
                  <small>
                    占比 {{ weightOf(fund) }}
                    <template v-if="pendingOf(fund) > 0"> · 含待确认 <MoneyAmount :value="pendingOf(fund)" /></template>
                  </small>
                </div>
                <div v-for="metric in metricDisplay(fund)" :key="metric.key" class="fund-metric">
                  <span class="fund-metric-label">
                    {{ metric.label }}
                    <small v-if="metric.sub" class="fund-metric-sub" :class="metric.tone">{{ metric.sub }}</small>
                  </span>
                  <div class="fund-metric-value">
                    <b :class="metric.tone">{{ metric.value }}</b>
                  </div>
                </div>
              </div>
            </header>

            <Transition name="collapse-reveal">
              <div v-if="expandedFund === fund.key" class="fund-detail">
                <section
                  v-for="(fundClass, index) in fund.classes"
                  :key="fundClass.key ?? index"
                  class="fund-class"
                >
                  <header class="fund-class-head">
                    <strong>{{ fundClass.shareClass ? `${fundClass.shareClass} 类` : '未标类别' }}</strong>
                    <span>{{ formatQuantity(fundClass.shares) }} 份 · 成本 {{ maskMoney(fundClass.cost) }}</span>
                  </header>
                  <div class="fund-class-values">
                    <div>
                      <span>估值</span>
                      <b>{{ maskMoney(fundClass.value) }}</b>
                    </div>
                    <div>
                      <span>持有盈亏</span>
                      <b :class="amountsMasked ? '' : amountTone(fundClass.holdingProfit)">
                        {{ maskSignedMoney(fundClass.holdingProfit) }}
                      </b>
                    </div>
                    <div>
                      <span>已实现盈亏</span>
                      <b :class="amountsMasked ? '' : amountTone(fundClass.realizedProfit)">
                        {{ maskSignedMoney(fundClass.realizedProfit) }}
                      </b>
                    </div>
                  </div>
                  <p class="fund-class-meta">
                    {{ fundClass.netValue === null ? '净值来源不一致' : `净值 ${formatQuantity(fundClass.netValue)}` }}
                  </p>
                  <div class="holding-actions">
                    <div v-for="holding in fundClass.platforms" :key="holding.positionId" class="holding-action-row">
                      <div class="holding-copy">
                        <strong>{{ holding.platform || '未标平台' }}</strong>
                        <small v-if="fundClass.platforms.length > 1">{{ formatQuantity(holding.shares) }} 份</small>
                      </div>
                      <div class="holding-buttons">
                        <button type="button" @click="openSheet('buy', holding.positionId)">买入</button>
                        <button type="button" @click="openSheet('sell', holding.positionId)">卖出</button>
                        <button type="button" @click="openSheet('shares', holding.positionId)">调整份额</button>
                      </div>
                    </div>
                  </div>
                  <p class="fund-class-code">
                    {{ fundClass.code ?? fundClass.platforms[0]?.code ?? '未填代码' }} ·
                    {{ fundClass.identityBasis ?? (fundClass.platforms[0]?.code ? '按基金代码和类别匹配' : '按规范化基金名称和类别匹配') }}
                  </p>
                </section>

                <p v-if="fund.classes.length > 1" class="summary-note">
                  同一基金的 A/C 类分别列示份额和净值，不合并份额，也不平均净值。
                  <template v-if="!fund.hasEffectiveQuotes"> 部分持仓行情依据不足，估值仅供参考。</template>
                </p>
                <p v-else-if="!fund.hasEffectiveQuotes" class="summary-note">部分持仓行情依据不足，估值仅供参考。</p>
              </div>
            </Transition>
          </article>
        </section>

        <EmptyState
          v-else
          icon="chart"
          :title="allFunds.length ? '没有找到基金' : '从第一笔持仓开始'"
          :description="allFunds.length ? '试试其他分组。' : '录入已有基金，查看持仓价值与收益。'"
          :action-label="allFunds.length ? '查看全部基金' : '录入已有持仓'"
          @action="allFunds.length ? selectGroup('all') : openSheet('opening')"
        />

        <p v-if="!scope.historyConfirmed || !scope.hasEffectiveQuotes" class="summary-note">
          部分持仓日期或行情依据不足，当前估值可能不完整。
        </p>
      </template>

      <template v-else-if="activeView === 'activities'">
        <div class="activity-toolbar">
          <h2>交易记录</h2>
          <select v-model="activityStatusFilter" class="activity-status-filter" aria-label="交易状态">
            <option value="all">全部</option>
            <option value="pending">进行中</option>
            <option value="done">已完成</option>
          </select>
          <select v-model="activityTypeFilter" class="activity-type-filter" aria-label="交易类型">
            <option v-for="option in activityTypeOptions" :key="option.value" :value="option.value">
              {{ option.label }}
            </option>
          </select>
        </div>

        <InvestmentActivityList
          v-if="filteredActivityRows.length"
          :rows="filteredActivityRows"
          stagger
          @select="openActivityRow"
        />

        <EmptyState
          v-else
          compact
          icon="ledger"
          :title="activityRows.length ? '没有符合条件的交易' : '还没有交易记录'"
          :description="activityRows.length ? '试试其他交易类型或状态。' : '记录买入、赎回等投资变动后，可在这里查看。'"
        />
      </template>

      <template v-else>
        <div class="section-head">
          <h2>定投计划</h2>
          <span class="meta">与自动记账共用同一计划</span>
        </div>

        <LoadingState v-if="plansLoading" variant="list" :rows="3" compact label="正在读取定投计划" />
        <section v-else-if="plansError" class="state">
          <p>{{ plansError }}</p>
          <LoadingButton class="secondary-button" :loading="plansLoading" loading-label="加载中…" @click="plansLoaded = false; plansError = ''; loadPlans()">重新加载</LoadingButton>
        </section>
        <section v-else-if="scopedPlans.length" class="fund-plan-list">
          <article v-for="plan in scopedPlans" :key="plan.id" class="fund-plan-row">
            <div>
              <strong>{{ fundPlanDisplayName(plan) }}</strong>
              <small>
                {{ planCycleLabel(plan) }} · {{ plan.isActive ? '启用' : '已暂停' }}
                · 下期 {{ plan.nextDueDate ? formatBusinessDate(plan.nextDueDate) : '待定' }}
              </small>
            </div>
            <b>¥{{ formatMoney(plan.amount) }}</b>
            <RouterLink to="/plans">管理</RouterLink>
          </article>
        </section>
        <EmptyState v-else compact icon="clock" title="还没有定投计划" description="为固定投入设置计划，到期后按计划记账。" action-label="设置定投计划" @action="router.push('/plans')" />
      </template>

    </template>

    <FundGroupManagerSheet
      v-model:group-draft="groupDraft"
      v-model:editing-group-name="editingGroupName"
      :open="groupSheetOpen"
      :groups="groups"
      :all-funds="allFunds"
      :detail-id="groupDetailId"
      :editing-group-id="editingGroupId"
      :busy="groupBusy"
      :busy-key="groupBusyKey"
      :error="groupError"
      @close="closeGroupSheet"
      @open-detail="openGroupDetail"
      @back-to-list="backToGroupList"
      @submit-group="submitGroup"
      @start-rename="startRenameGroup"
      @cancel-rename="editingGroupId = null"
      @save-group-name="saveGroupName"
      @remove-group="removeGroup"
      @toggle-fund="toggleGroupFund"
    />

    <BottomSheet
      :open="sheet !== null"
      :title="sheetTitle"
      :subtitle="sheetSubtitle"
      :close-disabled="isSaving"
      @close="requestClose"
    >
      <div class="sheet-summary">
        <div v-if="sheet !== 'buyConfirm'" class="summary-line">
          <span>可用现金</span>
          <b>¥{{ formatMoney(overview?.cashBalance) }}</b>
        </div>
        <div v-if="selectedPosition && sheet !== 'buyConfirm'" class="summary-line">
          <span>当前持仓</span>
          <b>{{ formatQuantity(selectedPosition.shares) }} 份</b>
        </div>
        <div v-if="selectedPendingBuy" class="summary-line">
          <span>基金</span>
          <b>{{ selectedPendingBuy.positionName }}</b>
        </div>
        <div v-if="selectedPendingBuy" class="summary-line">
          <span>买入日期</span>
          <b>{{ formatBusinessDate(selectedPendingBuy.businessDate) }}</b>
        </div>
        <div v-if="selectedPendingBuy" class="summary-line">
          <span>待确认扣款</span>
          <b>¥{{ formatMoney(selectedPendingBuy.amount) }} · 申购费 ¥{{ formatMoney(selectedPendingBuy.fee) }}</b>
        </div>
        <div v-if="selectedPendingBuy" class="summary-line">
          <span>确认净值日</span>
          <b>{{ formatBusinessDate(selectedPendingBuy.tradeDate) }} · {{ monthDayText(selectedPendingBuy.confirmationDate) }}自动确认份额</b>
        </div>
        <div v-if="selectedSellApplication" class="summary-line">
          <span>申请赎回</span>
          <b>{{ formatQuantity(selectedSellApplication.shares) }} 份</b>
        </div>
        <div v-if="selectedPendingArrival" class="summary-line">
          <span>待到账</span>
          <b>¥{{ formatMoney(selectedPendingArrival.amount) }}</b>
        </div>
      </div>

      <div v-if="sheet === 'buyConfirm' && selectedPendingBuy" class="form-stack">
        <p v-if="selectedPendingBuy.note" class="form-note">备注：{{ selectedPendingBuy.note }}</p>
        <p class="form-note">{{ selectedPendingBuy.canCancel ? '可在撤销截止时间前撤销这笔交易。' : '已超过撤销时间，等待自动确认。' }}</p>
        <LoadingButton
          v-if="selectedPendingBuy.canCancel"
          class="secondary-button danger-text"
          :loading="cancellingPendingId === selectedPendingBuy.transactionId"
          loading-label="撤销中…"
          @click="cancelPending(selectedPendingBuy.transactionId)"
        >
          撤销这笔待确认
        </LoadingButton>
      </div>

      <form v-else class="form-stack" @submit.prevent="submit">
        <template v-if="sheet === 'opening'">
          <label class="form-field">
            <span>基金名称 / 代码</span>

            <div v-if="selectedFund" class="fund-selected">
              <div>
                <strong>{{ selectedFund.name }}</strong>
                <small>{{ selectedFund.code }} · {{ selectedFund.fundType || '基金' }}<template v-if="selectedFund.isQdii"> · QDII</template></small>
              </div>
              <button type="button" class="text-button" @click="clearSelectedFund">更换</button>
            </div>

            <template v-else>
              <input
                v-model="fundSearch"
                type="search"
                autocomplete="off"
                placeholder="输入名称或代码，如 000834、纳斯达克"
                aria-label="搜索基金名称或代码"
              />
              <p v-if="fundSearching" class="form-note">正在搜索基金…</p>
              <p v-else-if="fundSearchError" class="form-note" role="status">{{ fundSearchError }}</p>
              <div v-if="fundSearchResults.length" class="fund-search-results" role="listbox" aria-label="基金搜索结果">
                <button
                  v-for="result in fundSearchResults"
                  :key="result.code"
                  type="button"
                  role="option"
                  class="fund-search-result"
                  @click="chooseFund(result)"
                >
                  <span>
                    <strong>{{ result.name }}</strong>
                    <small>{{ result.code }} · {{ result.fundType || '基金' }}</small>
                  </span>
                  <em v-if="result.isQdii">QDII</em>
                </button>
              </div>
            </template>

            <small>支持按基金名称或代码模糊搜索，选中后自动识别份额类别和确认周期。</small>
          </label>

          <label class="form-field">
            <span>平台</span>
            <select v-model="form.platform">
              <option v-for="platform in platformOptions" :key="platform" :value="platform">{{ platform }}</option>
            </select>
          </label>

          <div class="form-row">
            <label class="form-field">
              <span>持有份额</span>
              <input v-model="form.shares" type="text" inputmode="decimal" placeholder="0.0000" />
            </label>
            <label class="form-field">
              <span>持仓成本（元）</span>
              <input v-model="form.cost" type="text" inputmode="decimal" placeholder="0.00" />
            </label>
          </div>

          <label class="form-field">
            <span>截至日期</span>
            <input v-model="form.businessDate" type="date" :max="today" />
            <small>这笔持仓在这一天成立；补录历史持仓时请选择实际日期。</small>
          </label>

          <div class="form-field">
            <span>建账前已实现收益（元）</span>
            <div class="signed-input-row">
              <div class="sign-toggle" role="group" aria-label="收益正负">
                <button type="button" :aria-pressed="form.priorProfitSign === 1" @click="setPriorProfitSign(1)">盈 +</button>
                <button type="button" :aria-pressed="form.priorProfitSign === -1" @click="setPriorProfitSign(-1)">亏 −</button>
              </div>
              <input
                v-model="form.priorProfit"
                type="text"
                inputmode="decimal"
                aria-label="建账前已实现收益（元）"
                placeholder="选填，默认 0"
                @input="handlePriorProfitInput"
              />
            </div>
            <small>
              按平台上「累计收益 − 持有收益」填写，不填按 0 处理。只影响累计收益，不改持仓成本、份额和现金。
            </small>
          </div>
        </template>

        <template v-else-if="sheet === 'buy'">
          <label class="form-field">
            <span>基金持仓</span>
            <select v-model="form.positionId">
              <option v-for="position in overview?.positions ?? []" :key="position.id" :value="position.id">
                {{ positionLabel(position) }}
              </option>
            </select>
          </label>
          <label class="form-field">
            <span>扣款金额（元）</span>
            <input v-model="form.amount" type="text" inputmode="decimal" placeholder="0.00" />
          </label>
          <label class="form-field">
            <span>实际申购费（元）</span>
            <input v-model="form.fee" type="text" inputmode="decimal" placeholder="0.00" />
          </label>
          <small v-if="!selectedPosition?.code" class="form-note">请先为该持仓设置基金代码，才能从数据源自动确认份额。</small>
        </template>

        <template v-else-if="sheet === 'shares'">
          <label class="form-field">
            <span>核对后实际份额</span>
            <input v-model="form.shares" type="text" inputmode="decimal" placeholder="0.0000" />
            <small>份额调整会生成当天的持仓核对记录，成本保持不变。</small>
          </label>
        </template>

        <template v-else-if="sheet === 'sell'">
          <label class="form-field">
            <span>基金持仓</span>
            <select v-model="form.positionId">
              <option v-for="position in overview?.positions ?? []" :key="position.id" :value="position.id">
                {{ positionLabel(position) }}
              </option>
            </select>
          </label>
          <label class="form-field">
            <span>申请赎回份额</span>
            <input v-model="form.shares" type="text" inputmode="decimal" placeholder="0.0000" />
            <small>申请后份额冻结，仍计入持仓估值；确认时才减少份额并转为待到账。</small>
          </label>
          <label class="form-field">
            <span>申请日期</span>
            <input v-model="form.businessDate" type="date" :max="today" />
          </label>
        </template>

        <template v-else-if="sheet === 'sellConfirm'">
          <div class="form-row">
            <label class="form-field">
              <span>确认赎回份额</span>
              <input v-model="form.shares" type="text" inputmode="decimal" placeholder="0.0000" />
            </label>
            <label class="form-field">
              <span>成交净值</span>
              <input v-model="form.netValue" type="text" inputmode="decimal" placeholder="0.0000" />
            </label>
          </div>
          <div class="form-row">
            <label class="form-field">
              <span>净值日期</span>
              <input v-model="form.netValueDate" type="date" :max="today" />
            </label>
            <label class="form-field">
              <span>费用（元）</span>
              <input v-model="form.fee" type="text" inputmode="decimal" placeholder="0.00" />
            </label>
          </div>
          <label class="form-field">
            <span>预计到账金额（元）</span>
            <input v-model="form.netAmount" type="text" inputmode="decimal" placeholder="输完份额、净值和费用自动计算" @input="onSellNetAmountInput" />
            <small v-if="sellNetAmountManual">
              已按平台确认单修改 ·
              <button type="button" class="text-button inline-recalc" @click="estimateSellNetAmount">恢复自动计算</button>
            </small>
            <small v-else-if="form.netAmount">已自动按「份额 × 净值 − 费用」算出，如有尾差可直接修改。</small>
          </label>
          <label class="form-field">
            <span>确认日期</span>
            <input
              v-model="form.businessDate"
              type="date"
              :min="selectedSellApplication?.businessDate"
              :max="today"
            />
          </label>
          <p class="form-note">确认份额不能超过申请份额；净值日期默认取申请日，可按实际成交调整。</p>
          <LoadingButton
            v-if="selectedSellApplication?.canCancel"
            class="text-button danger-text"
            :loading="cancellingPendingId === selectedSellApplication.transactionId"
            loading-label="撤销中…"
            @click="cancelPending(selectedSellApplication.transactionId)"
          >撤销赎回申请</LoadingButton>
        </template>

        <template v-else-if="sheet === 'sellArrival'">
          <label class="form-field">
            <span>到账金额（元）</span>
            <input v-model="form.netAmount" type="text" inputmode="decimal" placeholder="0.00" />
            <small>必须与待到账金额一致；如有差额，请先更正赎回确认。</small>
          </label>
          <label class="form-field">
            <span>到账日期</span>
            <input
              v-model="form.businessDate"
              type="date"
              :min="selectedPendingArrival?.businessDate"
              :max="today"
            />
          </label>
        </template>

        <label class="form-field">
          <span>备注</span>
          <input v-model="form.note" type="text" maxlength="200" placeholder="选填" />
        </label>

        <p class="form-note">{{ sheetNote }}</p>
        <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

        <div class="form-actions">
          <button type="button" class="secondary-button" :disabled="isSaving" @click="requestClose">
            取消
          </button>
          <LoadingButton class="primary-button" type="submit" :loading="isSaving" :disabled="!canSubmit" loading-label="保存中…">
            保存
          </LoadingButton>
        </div>
      </form>
    </BottomSheet>

    <BottomSheet
      :open="duplicatePrompt !== null"
      title="已有相同持仓"
      subtitle="同一基金、类别和平台已存在记录"
      :close-disabled="isSaving"
      @close="duplicatePrompt = null"
    >
      <template v-if="duplicatePrompt">
        <div class="sheet-summary">
          <div class="summary-line">
            <span>已有</span>
            <b>{{ formatQuantity(duplicatePrompt.position.shares) }} 份 · 成本 ¥{{ formatMoney(duplicatePrompt.position.cost) }}</b>
          </div>
          <div class="summary-line">
            <span>本次录入</span>
            <b>{{ formatQuantity(duplicatePrompt.shares) }} 份 · 成本 ¥{{ formatMoney(duplicatePrompt.cost) }}</b>
          </div>
        </div>

        <p class="summary-note duplicate-note">
          更新已有持仓会用本次份额和成本覆盖原记录，并生成一条可追溯的核对记录；新增独立持仓会再建一笔记录，两者都会计入同一基金类别。
        </p>
        <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

        <div class="duplicate-actions">
          <LoadingButton class="primary-button" :loading="isSaving" loading-label="更新中…" @click="updateDuplicatePosition">
            更新已有持仓
          </LoadingButton>
          <LoadingButton class="secondary-button" :loading="isSaving" loading-label="新增中…" @click="addDuplicatePosition">
            新增独立持仓
          </LoadingButton>
          <button type="button" class="text-button" :disabled="isSaving" @click="duplicatePrompt = null">取消</button>
        </div>
      </template>
    </BottomSheet>

    <ConfirmSheet
      :open="pendingGroupRemoval !== null"
      title="删除分组"
      :message="pendingGroupRemoval ? `删除分组「${pendingGroupRemoval.name}」？组内基金会回到未分组，持仓和收益不变。` : ''"
      confirm-label="删除"
      danger
      :busy="groupBusy"
      @confirm="confirmRemoveGroup"
      @cancel="pendingGroupRemoval = null"
    />
    <ConfirmSheet
      :open="discardOpen"
      title="放弃修改？"
      message="离开后，本次填写的内容会丢失。"
      confirm-label="放弃修改"
      cancel-label="继续编辑"
      danger
      @confirm="resolveDiscard(true)"
      @cancel="resolveDiscard(false)"
    />
    <AppNav />
  </main>
</template>

<style scoped>
.funds-page {
  padding-bottom: calc(97px + env(safe-area-inset-bottom));
}

.funds-header {
  display: grid;
  grid-template-columns: var(--touch-target) 1fr auto;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
}

.funds-header h1 {
  margin: 0;
  font-size: var(--text-title3);
  font-weight: 700;
  letter-spacing: -0.3px;
}

.funds-header-actions {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 2px;
}

.funds-hero {
  margin-bottom: var(--space-4);
}

.hero-update {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin-top: var(--space-3);
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.4;
  --update-ring-color: var(--color-hero-line);
}

.hero-update.is-warning {
  color: var(--color-warning);
  --update-ring-color: var(--color-warning);
}

.funds-header .investment-actions {
  margin-left: 0;
}

.investment-actions {
  position: relative;
}
.investment-actions > summary {
  list-style: none;
  cursor: pointer;
}
.investment-actions > summary::-webkit-details-marker {
  display: none;
}
.funds-actions {
  position: absolute;
  top: 43px;
  right: 0;
  z-index: 30;
  display: grid;
  gap: 6px;
  width: 180px;
  padding: 10px;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-float);
  transform-origin: top right;
}

.investment-actions[open] > .funds-actions {
  animation: funds-popover-in var(--motion-content-in) var(--ease-enter);
}

@keyframes funds-popover-in {
  from {
    opacity: 0;
    transform: translate3d(0, -4px, 0) scale(0.96);
  }
  to {
    opacity: 1;
    transform: translate3d(0, 0, 0) scale(1);
  }
}

.funds-actions button {
  min-height: 44px;
  padding: 0 14px;
  font-size: 0.875rem;
  font-weight: 600;
  text-align: center;
}

.group-filter {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 14px;
}

.group-pills {
  display: flex;
  flex: 1;
  gap: 6px;
  min-width: 0;
  overflow-x: auto;
  scrollbar-width: none;
}

.group-pills::-webkit-scrollbar {
  display: none;
}

.group-pill {
  flex: 0 0 auto;
  min-height: 38px;
  padding: 0 15px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-surface);
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
  white-space: nowrap;
  transition:
    background-color var(--motion-feedback) var(--ease-move),
    color var(--motion-feedback) var(--ease-move),
    transform var(--motion-press-out) var(--ease-move);
}

@media (hover: hover) and (pointer: fine) {
  .group-pill:hover:not(.active) {
    background: var(--color-bg-hover);
    color: var(--color-text-primary);
  }
}

.group-pill:active {
  transform: scale(0.97);
  transition-duration: var(--motion-press-in);
}

.group-pill.active {
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-weight: 600;
}

.group-manage {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  gap: 1px;
  min-height: 36px;
  padding: 0 2px 0 8px;
  border: 0;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  white-space: nowrap;
  transition:
    color var(--motion-feedback) var(--ease-move),
    opacity var(--motion-press-out) var(--ease-move);
}

.group-manage:active {
  opacity: 0.72;
  transition-duration: var(--motion-press-in);
}

.group-manage .app-icon {
  width: 13px;
  height: 13px;
}

.view-tabs {
  position: relative;
  display: flex;
  gap: 2px;
  margin-bottom: 10px;
  padding: 4px;
  border-bottom: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-surface);
}

.view-tabs-pill {
  position: absolute;
  top: 4px;
  bottom: 4px;
  left: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  transform-origin: left center;
  pointer-events: none;
}

.view-tabs-pill.is-animated {
  transition: transform var(--motion-segment) var(--ease-move);
}

.view-tabs button {
  position: relative;
  z-index: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 2px;
  min-width: 0;
  min-height: 40px;
  padding: 0 4px;
  border-radius: var(--radius-pill);
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
  white-space: nowrap;
  transition:
    color var(--motion-feedback) var(--ease-move),
    background-color var(--motion-feedback) var(--ease-move);
}

.tab-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 15px;
  height: 15px;
  padding: 0 3px;
  border-radius: var(--radius-pill);
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-size: 0.625rem;
  font-weight: 600;
  line-height: 1;
  font-variant-numeric: tabular-nums;
}

.view-tabs button.active {
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-weight: 600;
}

.view-tabs.has-pill button.active {
  background: transparent;
}

@media (hover: hover) and (pointer: fine) {
  .view-tabs button:hover:not(.active) {
    color: var(--color-text-primary);
  }
}

.view-tabs button.active::after {
  content: none;
}

.fund-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.fund-card {
  overflow: hidden;
  border-radius: 20px;
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}
.fund-card > header {
  display: grid;
  gap: 10px;
  padding: 16px;
  cursor: pointer;
  transition: background-color var(--motion-feedback) var(--ease-move);
}
@media (hover: hover) and (pointer: fine) {
  .fund-card > header:hover {
    background: var(--color-bg-hover);
  }
}
.fund-card > header:active {
  background: var(--color-bg-hover);
  transition-duration: var(--motion-press-in);
}
.fund-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
}
.fund-title {
  min-width: 0;
}
.fund-title strong {
  display: block;
  color: var(--color-text-primary);
  font-size: 1rem;
  font-weight: 600;
  letter-spacing: -0.01em;
  line-height: 1.5;
  overflow-wrap: anywhere;
}
.fund-title small {
  display: block;
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.6;
}
.fund-values {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.fund-asset {
  grid-column: 1 / -1;
  min-width: 0;
  text-align: left;
}

.fund-metric {
  min-width: 0;
  text-align: left;
}

.fund-asset b {
  display: block;
  color: var(--color-text-primary);
  font-size: 1.25rem;
  font-weight: 600;
  letter-spacing: -0.03em;
  line-height: 1.15;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.fund-asset small {
  display: block;
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.fund-metric-label {
  display: flex;
  align-items: baseline;
  gap: 4px;
  margin-bottom: 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.35;
}

.fund-metric-sub {
  font-size: var(--text-caption);
  font-weight: 500;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.fund-metric-value b {
  display: block;
  font-size: 0.8125rem;
  font-weight: 600;
  line-height: 1.4;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.expand-mark {
  flex: 0 0 auto;
  width: 13px;
  height: 17px;
  margin-top: 3px;
  color: var(--color-text-secondary);
  transition: transform var(--motion-feedback) var(--ease-move);
}
.expand-mark.expanded {
  transform: rotate(90deg);
}

@media (max-width: 360px) {
  .view-tabs button {
    padding: 0 4px;
    font-size: 0.75rem;
    white-space: nowrap;
  }

  .fund-card > header {
    padding: 14px;
  }

  .fund-values {
    gap: 10px 6px;
  }

  .fund-metric-value b {
    font-size: 0.75rem;
  }
}

.fund-detail {
  padding: 4px 18px 18px;
  border-top: 1px solid var(--color-border-subtle);
}

.fund-class {
  margin-top: 12px;
  padding: 14px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
}

.fund-class-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
  font-size: var(--text-footnote);
}

.fund-class-head strong {
  font-size: 0.875rem;
  font-weight: 600;
}

.fund-class-head span {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  text-align: right;
}

.fund-class-values {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px solid var(--color-border-subtle);
}

.fund-class-values span {
  display: block;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.fund-class-values b {
  display: block;
  margin-top: 4px;
  font-size: 0.8125rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.fund-class-values > div:nth-child(2),
.fund-class-values > div:nth-child(3) {
  text-align: right;
}

.fund-class-meta {
  margin: 7px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.65;
}

.fund-class-code {
  margin: 10px 0 0;
  padding-top: 9px;
  border-top: 1px solid var(--color-border-subtle);
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.holding-actions {
  display: grid;
  gap: 8px;
  margin-top: 10px;
}

.holding-action-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding-top: 9px;
  border-top: 1px solid var(--color-border-subtle);
}

.holding-copy {
  min-width: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.holding-copy strong {
  display: block;
  color: var(--color-text-primary);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.holding-copy small {
  display: block;
  margin-top: 2px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.holding-buttons {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 6px;
  flex: 0 0 auto;
}

.holding-action-row button {
  --glossy-light: 1;
  position: relative;
  min-height: 34px;
  padding: 0 12px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-btn-soft-bg);
  color: var(--color-btn-soft-fg);
  box-shadow:
    0 2px 5px var(--color-btn-soft-shadow),
    0 0 0 1px var(--color-btn-soft-ring);
  font-size: var(--text-caption);
  font-weight: 600;
  white-space: nowrap;
  transition:
    transform var(--motion-press-out) var(--ease-move),
    background-color var(--motion-feedback) var(--ease-move);
}

.holding-action-row button::before,
.holding-action-row button::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  pointer-events: none;
}

.holding-action-row button::before {
  padding: 1px;
  background: linear-gradient(
    to bottom,
    rgba(255, 255, 255, var(--color-btn-soft-stroke)),
    rgba(255, 255, 255, 0)
  );
  -webkit-mask: linear-gradient(#000 0 0) content-box, linear-gradient(#000 0 0);
  -webkit-mask-composite: xor;
  mask-composite: exclude;
}

.holding-action-row button::after {
  box-shadow: inset 0 0 4px 1px rgba(255, 255, 255, var(--color-btn-soft-glow));
  opacity: var(--glossy-light);
  transition: opacity var(--motion-feedback) var(--ease-move);
}

@media (hover: hover) and (pointer: fine) {
  .holding-action-row button:hover:not(:disabled) {
    background: var(--color-btn-soft-bg-hover);
  }
}

.holding-action-row button:active:not(:disabled) {
  --glossy-light: 0.56;
  background: var(--color-btn-soft-bg-hover);
  transform: scale(0.97);
  transition-duration: var(--motion-press-in);
}

@media (max-width: 340px) {
  .holding-action-row {
    flex-wrap: wrap;
  }

  .holding-buttons {
    width: 100%;
  }
}

.fund-plan-list {
  display: flex;
  flex-direction: column;
  gap: 9px;
}

.fund-plan-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 16px;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.fund-plan-row > div {
  flex: 1;
  min-width: 0;
}

.fund-plan-row strong {
  display: block;
  font-size: 0.75rem;
  font-weight: 600;
}

.fund-plan-row small {
  display: block;
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.fund-plan-row > b {
  font-size: 0.75rem;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.fund-plan-row a {
  color: var(--color-brand);
  font-size: var(--text-caption);
  text-decoration: none;
}

.fund-selected {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  min-height: var(--touch-target);
  padding: 10px 12px;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
}

.fund-selected strong {
  display: block;
  font-size: var(--text-footnote);
  font-weight: 600;
  line-height: 1.5;
  overflow-wrap: anywhere;
}

.fund-selected small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.fund-search-results {
  display: flex;
  flex-direction: column;
  max-height: 240px;
  margin-top: 6px;
  overflow-y: auto;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-float);
}

.fund-search-result {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  min-height: var(--touch-target);
  padding: 8px 10px;
  border: 0;
  border-bottom: 1px solid var(--color-border-subtle);
  background: transparent;
  color: var(--color-text-primary);
  text-align: left;
  transition: background-color var(--motion-feedback) var(--ease-move);
}

.fund-search-result:last-child {
  border-bottom: 0;
}

@media (hover: hover) and (pointer: fine) {
  .fund-search-result:hover {
    background: var(--color-bg-hover);
  }
}

.fund-search-result:active {
  background: var(--color-brand-soft);
  transition-duration: var(--motion-press-in);
}

.fund-search-result span {
  min-width: 0;
}

.fund-search-result strong {
  display: block;
  font-size: var(--text-footnote);
  font-weight: 500;
  line-height: 1.45;
  overflow-wrap: anywhere;
}

.fund-search-result small {
  display: block;
  margin-top: 2px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.fund-search-result em {
  flex: 0 0 auto;
  padding: 2px 5px;
  border-radius: var(--radius-tag);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: var(--text-caption);
  font-style: normal;
}

.form-field input[readonly] {
  background: transparent;
  color: var(--color-text-secondary);
}

.signed-input-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.signed-input-row input {
  flex: 1;
  min-width: 0;
}

.sign-toggle {
  display: inline-flex;
  flex: 0 0 auto;
  padding: 2px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
}

.sign-toggle button {
  min-height: 36px;
  padding: 0 10px;
  border: 0;
  border-radius: calc(var(--radius-control) - 2px);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 600;
  white-space: nowrap;
  transition:
    background-color var(--motion-feedback) var(--ease-move),
    color var(--motion-feedback) var(--ease-move),
    box-shadow var(--motion-feedback) var(--ease-move);
}

.sign-toggle button[aria-pressed='true'] {
  background: var(--color-bg-surface);
  color: var(--color-brand);
  box-shadow: 0 1px 2px var(--color-shadow);
}

.text-button {
  flex: 0 0 auto;
  min-height: var(--touch-target);
  padding: 0 4px;
  border: 0;
  background: transparent;
  color: var(--color-brand);
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

.meta {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.activity-toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.activity-toolbar h2 {
  margin: 0 auto 0 0;
  font-size: 0.9375rem;
  font-weight: 600;
}

.activity-status-filter,
.activity-type-filter {
  min-height: 32px;
  padding: 0 8px;
  border: 0;
  border-radius: var(--radius-tag);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.activity-status-filter {
  color: var(--color-brand);
}

.danger-text {
  align-self: flex-start;
  color: var(--color-danger);
}

.summary-note {
  margin: 14px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
  text-align: center;
}

.estimate-button,
.inline-recalc {
  align-self: flex-start;
  min-height: 28px;
  padding: 0;
  color: var(--color-brand);
  font-size: var(--text-caption);
}

.duplicate-note {
  margin-top: 0;
  text-align: left;
}

.duplicate-actions {
  display: grid;
  gap: 8px;
  margin-top: 4px;
}

.duplicate-actions button {
  min-height: 48px;
  border-radius: var(--radius-pill);
  font-size: 0.9375rem;
}

@media (prefers-reduced-motion: reduce) {
  .investment-actions[open] > .funds-actions {
    animation: none;
  }

  .view-tabs-pill.is-animated,
  .expand-mark {
    transition: none;
  }

  .group-pill:active,
  .holding-action-row button:active:not(:disabled) {
    transform: none;
  }
}
</style>
