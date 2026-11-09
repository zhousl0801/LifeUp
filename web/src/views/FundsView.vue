<script setup lang="ts">
import { navigateBack } from '../utils/navigation'
import Decimal from 'decimal.js'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
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
  searchFunds,
  sellFund
} from '../api/funds'
import { fetchPlanOverview } from '../api/plans'
import { ApiError, getApiErrorMessage } from '../api/client'
import { toast } from '../composables/useMessage'
import AppNav from '../components/AppNav.vue'
import AppIcon from '../components/AppIcon.vue'
import AnimatedMoney from '../components/AnimatedMoney.vue'
import EmptyState from '../components/EmptyState.vue'
import LoadingState from '../components/LoadingState.vue'
import BackButton from '../components/BackButton.vue'
import LoadingButton from '../components/LoadingButton.vue'
import BottomSheet from '../components/BottomSheet.vue'
import ConfirmSheet from '../components/ConfirmSheet.vue'
import FundReturnsPanel from '../components/FundReturnsPanel.vue'
import {
  amountTone,
  formatBusinessDate,
  formatBusinessTime,
  addAmounts,
  formatMoney,
  formatQuantity,
  formatSignedMoney,
  parseAmount,
  parseNonNegativeQuantity,
  parseSignedAmount,
  parsePrice,
  parseQuantity,
  sumAmounts,
  todayInBusinessTimeZone
} from '../utils/format'
import { createRequestId } from '../utils/request'
import { fundPlanDisplayName } from '../utils/plan'
import type {
  FundActivity,
  FundOverview,
  FundPosition,
  FundSearchResult,
  FundShareClass,
  FundSummary,
  PendingFundArrival,
  PendingFundBuy,
  PendingFundSell
} from '../types/fund'
import type { AutoPlan } from '../types/plan'

type SheetKind = 'opening' | 'buy' | 'buyConfirm' | 'shares' | 'sell' | 'sellConfirm' | 'sellArrival'
type PortfolioView = 'positions' | 'returns' | 'activities' | 'plans'
type GroupScope = 'all' | 'ungrouped' | string
type FundMetricKey = 'daily' | 'value' | 'profit' | 'cumulative' | 'rate' | 'cost' | 'weight' | 'pending'

const metricStorageKey = 'lifeup.fund.metrics.v2'
const defaultMetrics: FundMetricKey[] = ['daily', 'profit', 'cumulative']
const metricOptions: { key: FundMetricKey; label: string }[] = [
  { key: 'daily', label: '日收益' },
  { key: 'profit', label: '持有收益' },
  { key: 'cumulative', label: '累计收益' },
  { key: 'rate', label: '收益率' },
  { key: 'value', label: '持仓价值' },
  { key: 'cost', label: '持仓成本' },
  { key: 'weight', label: '占比' },
  { key: 'pending', label: '待确认' }
]
const platformOptions = ['券商直销', '支付宝', '天天基金', '银行']

const route = useRoute()
const router = useRouter()
const portfolioViews: PortfolioView[] = ['positions', 'returns', 'activities', 'plans']

function normalizeView(value: unknown): PortfolioView {
  const candidate = Array.isArray(value) ? value[0] : value
  return portfolioViews.includes(candidate as PortfolioView) ? candidate as PortfolioView : 'positions'
}

const today = todayInBusinessTimeZone()
const overview = ref<FundOverview | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')
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
const expandedFund = ref<string | null>(null)
const searchText = ref('')
const metricKeys = ref<FundMetricKey[]>(loadMetricKeys())
const metricNotice = ref('')
const groupSheetOpen = ref(false)
const settingsOpen = ref(false)
const pendingGroupRemoval = ref<{ id: string; name: string } | null>(null)
const groupDraft = ref('')
const groupError = ref('')
const groupBusy = ref(false)
const cancellingPendingId = ref<string | null>(null)
const groupBusyKey = ref('')
const groupDetailId = ref<string | null>(null)
const editingGroupId = ref<string | null>(null)
const editingGroupName = ref('')
const plans = ref<AutoPlan[]>([])
const plansLoaded = ref(false)
const plansLoading = ref(false)
const plansError = ref('')
const fundSearch = ref('')
const fundSearchResults = ref<FundSearchResult[]>([])
const fundSearching = ref(false)
const fundSearchError = ref('')
const selectedFund = ref<FundSearchResult | null>(null)
let fundSearchTimer: ReturnType<typeof setTimeout> | null = null
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
  note: ''
})

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

const managedGroupFunds = computed(() => {
  const groupId = managedGroup.value?.id

  return groupId ? allFunds.value.filter((fund) => fund.groupId === groupId) : []
})

const managedGroupValue = computed(() =>
  sumAmounts(managedGroupFunds.value.map((fund) => fund.assetValue)))

const groupAssetFunds = computed(() =>
  [...allFunds.value].sort((left, right) =>
    new Decimal(right.assetValue).comparedTo(left.assetValue)
      || left.name.localeCompare(right.name, 'zh-Hans-CN')))

const scopedFunds = computed(() => {
  const search = searchText.value.trim().toLowerCase()

  return allFunds.value.filter((fund) => {
    if (activeGroup.value === 'ungrouped' && fund.groupId) {
      return false
    }

    if (activeGroup.value !== 'all' && activeGroup.value !== 'ungrouped' && fund.groupId !== activeGroup.value) {
      return false
    }

    if (!search) {
      return true
    }

    return fund.name.toLowerCase().includes(search)
      || fund.classes.some((fundClass) => fundClass.platforms.some((platform) =>
        (platform.code ?? '').toLowerCase().includes(search)))
  })
})

const hasScopeFilter = computed(() => activeGroup.value !== 'all' || searchText.value.trim() !== '')
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

type ActivityKind = 'buy' | 'sip' | 'sell' | 'redeem' | 'arrival' | 'opening' | 'correction' | 'other'
type ActivityFilter = 'all' | ActivityKind
type ActivityStatusFilter = 'all' | 'pending' | 'done'
type ActivityPendingAction = 'buy' | 'sell' | 'arrival'

interface ActivityRow {
  key: string
  kind: ActivityKind
  label: string
  fundName: string
  timestamp: string
  amountText: string
  statusText: string
  statusTone: 'pending' | 'done' | 'reversed'
  isPending: boolean
  pendingAction: ActivityPendingAction | null
  pending: PendingFundBuy | PendingFundSell | PendingFundArrival | null
}

const activityTypeFilter = ref<ActivityFilter>('all')
const activityStatusFilter = ref<ActivityStatusFilter>('all')

const activityTypeOptions: { value: ActivityFilter; label: string }[] = [
  { value: 'all', label: '交易类型' },
  { value: 'sip', label: '定投' },
  { value: 'buy', label: '买入' },
  { value: 'sell', label: '卖出' },
  { value: 'redeem', label: '赎回' },
  { value: 'arrival', label: '到账' },
  { value: 'opening', label: '录入' },
  { value: 'correction', label: '核对' }
]

function isSipNote(note: string | null | undefined): boolean {
  return (note ?? '').includes('定投')
}

function activityKindOf(type: FundActivity['type'], isSip: boolean): ActivityKind {
  switch (type) {
    case 'buy':
    case 'buyPending':
    case 'buyConfirmed':
      return isSip ? 'sip' : 'buy'
    case 'sell':
      return 'sell'
    case 'sellApply':
    case 'sellConfirm':
      return 'redeem'
    case 'sellArrival':
      return 'arrival'
    case 'opening':
      return 'opening'
    case 'correction':
      return 'correction'
    default:
      return 'other'
  }
}

function monthDayText(value: string): string {
  const parts = value.split('-')

  if (parts.length !== 3) {
    return value
  }

  return `${Number(parts[1])}月${Number(parts[2])}日`
}

function activityLabelOf(kind: ActivityKind): string {
  switch (kind) {
    case 'sip':
      return '定投'
    case 'buy':
      return '买入'
    case 'sell':
      return '卖出'
    case 'redeem':
      return '赎回'
    case 'arrival':
      return '到账'
    case 'opening':
      return '录入'
    case 'correction':
      return '核对'
    default:
      return '其他'
  }
}

function moneyText(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${formatMoney(value)}元`
}

function activityAmountTextOf(
  activity: FundActivity,
  pendingBuy: PendingFundBuy | undefined
): string {
  if (activity.type === 'sellApply') {
    return activity.shares === null ? '—' : `${formatQuantity(activity.shares)}份`
  }

  if (activity.type === 'buyPending') {
    return moneyText(pendingBuy?.amount ?? activity.amount)
  }

  return moneyText(activity.amount)
}

function activityStatusOf(type: FundActivity['type']): string {
  switch (type) {
    case 'buyPending':
      return '交易进行中'
    case 'buyConfirmed':
      return '已确认'
    case 'sellApply':
      return '赎回申请中'
    case 'sellConfirm':
      return '待到账'
    case 'sellArrival':
      return '已到账'
    case 'sell':
      return '已完成'
    case 'opening':
      return '已录入'
    case 'correction':
      return '已核对'
    default:
      return ''
  }
}

const activityRows = computed<ActivityRow[]>(() => {
  const pendingBuyById = new Map(scopedPendingBuys.value.map((item) => [item.transactionId, item]))
  const sellApplyById = new Map(scopedSellApplications.value.map((item) => [item.transactionId, item]))
  const arrivalByConfirmId = new Map(scopedPendingArrivals.value.map((item) => [item.transactionId, item]))
  const noteById = new Map(scopedActivities.value.map((item) => [item.id, item.note]))
  // 已走完后续环节的下单/申请记录不再重复列出：买入看确认，赎回申请看确认，确认看待到账。
  const settledIds = new Set<string>()

  for (const activity of scopedActivities.value) {
    if (activity.type === 'buyConfirmed' && activity.linkedTransactionId) {
      settledIds.add(activity.linkedTransactionId)
    }

    if (activity.type === 'sellConfirm' && activity.linkedTransactionId) {
      settledIds.add(activity.linkedTransactionId)
    }

    if (activity.type === 'sellArrival' && activity.linkedTransactionId) {
      settledIds.add(activity.linkedTransactionId)
    }
  }

  const rows: ActivityRow[] = []
  const seen = new Set<string>()

  for (const activity of scopedActivities.value) {
    if (settledIds.has(activity.id)) {
      continue
    }

    seen.add(activity.id)
    const pendingBuy = pendingBuyById.get(activity.id)
    const sellApply = sellApplyById.get(activity.id)
    const arrival = arrivalByConfirmId.get(activity.id)
    const linkedNote = activity.linkedTransactionId ? noteById.get(activity.linkedTransactionId) : null
    const kind = activityKindOf(activity.type, isSipNote(activity.note) || isSipNote(linkedNote))
    const isPending = activity.type === 'buyPending'
      || activity.type === 'sellApply'
      || activity.type === 'sellConfirm'
    const pending = pendingBuy ?? sellApply ?? arrival ?? null
    const pendingAction: ActivityPendingAction | null = activity.isReversed
      ? null
      : activity.type === 'buyPending' && pendingBuy
        ? 'buy'
        : activity.type === 'sellApply' && sellApply
          ? 'sell'
          : activity.type === 'sellConfirm' && arrival
            ? 'arrival'
            : null

    rows.push({
      key: activity.id,
      kind,
      label: activityLabelOf(kind),
      fundName: activity.positionName,
      timestamp: [activity.businessDate, formatBusinessTime(activity.createdAt)].filter(Boolean).join(' '),
      amountText: activityAmountTextOf(activity, pendingBuy),
      statusText: activity.isReversed
        ? '已撤销'
        : pendingBuy
          ? `待确认 · ${monthDayText(pendingBuy.confirmationDate)}自动确认`
          : activityStatusOf(activity.type),
      statusTone: activity.isReversed ? 'reversed' : isPending ? 'pending' : 'done',
      isPending: isPending && !activity.isReversed,
      pendingAction,
      pending
    })
  }

  for (const pending of scopedPendingBuys.value) {
    if (seen.has(pending.transactionId)) {
      continue
    }

    const kind = isSipNote(pending.note) ? 'sip' : 'buy'
    rows.push({
      key: pending.transactionId,
      kind,
      label: activityLabelOf(kind),
      fundName: pending.positionName,
      timestamp: pending.businessDate,
      amountText: moneyText(pending.amount),
      statusText: `待确认 · ${monthDayText(pending.confirmationDate)}自动确认`,
      statusTone: 'pending',
      isPending: true,
      pendingAction: 'buy',
      pending
    })
  }

  for (const application of scopedSellApplications.value) {
    if (seen.has(application.transactionId)) {
      continue
    }

    rows.push({
      key: application.transactionId,
      kind: 'redeem',
      label: activityLabelOf('redeem'),
      fundName: application.positionName,
      timestamp: application.businessDate,
      amountText: `${formatQuantity(application.shares)}份`,
      statusText: '赎回申请中',
      statusTone: 'pending',
      isPending: true,
      pendingAction: 'sell',
      pending: application
    })
  }

  for (const arrival of scopedPendingArrivals.value) {
    if (seen.has(arrival.transactionId)) {
      continue
    }

    rows.push({
      key: arrival.transactionId,
      kind: 'arrival',
      label: activityLabelOf('arrival'),
      fundName: arrival.positionName,
      timestamp: arrival.businessDate,
      amountText: moneyText(arrival.amount),
      statusText: '待到账',
      statusTone: 'pending',
      isPending: true,
      pendingAction: 'arrival',
      pending: arrival
    })
  }

  // 未确认的交易置顶（还要盯），其余按时间倒序。
  return rows.sort((left, right) => {
    if (left.isPending !== right.isPending) {
      return left.isPending ? -1 : 1
    }

    return right.timestamp.localeCompare(left.timestamp)
  })
})

const filteredActivityRows = computed(() => activityRows.value.filter((row) => {
  if (activityStatusFilter.value === 'pending' && !row.isPending) {
    return false
  }

  if (activityStatusFilter.value === 'done' && row.isPending) {
    return false
  }

  return activityTypeFilter.value === 'all' || row.kind === activityTypeFilter.value
}))

function openActivityRow(row: ActivityRow): void {
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
    confirmedValue,
    cost,
    holdingProfit,
    realizedProfit,
    pendingBuyAmount,
    pendingArrivalAmount,
    assetValue: addAmounts(confirmedValue, pendingBuyAmount, pendingArrivalAmount),
    rate: cost > 0 ? holdingProfit / cost * 100 : null,
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

const todayProfit = computed(() => {
  const funds = scopedFunds.value
  const quoted = funds.filter((fund) => fund.hasEffectiveQuotes)
  const latestDate = funds
    .map((fund) => fund.latestNetValueDate)
    .filter((date): date is string => Boolean(date))
    .sort()
    .at(-1) ?? null

  return {
    value: sumAmounts(funds.map((fund) => fund.dailyProfit)),
    hasValue: quoted.length > 0,
    complete: quoted.length === funds.length,
    latestDate,
    isToday: latestDate === today
  }
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

    if (!form.positionId && overview.value.positions.length > 0) {
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

function loadMetricKeys(): FundMetricKey[] {
  try {
    const stored = localStorage.getItem(metricStorageKey)

    if (stored) {
      const keys = JSON.parse(stored) as unknown

      if (Array.isArray(keys)) {
        const valid = keys.filter((key): key is FundMetricKey =>
          metricOptions.some((option) => option.key === key)).slice(0, 3)

        if (valid.length) {
          return valid
        }
      }
    }
  } catch {
    // 浏览器存储不可用时使用默认三项指标。
  }

  return [...defaultMetrics]
}

function persistMetricKeys(): void {
  try {
    localStorage.setItem(metricStorageKey, JSON.stringify(metricKeys.value))
  } catch {
    // 指标只是展示设置，保存失败不影响账本。
  }
}

function toggleMetric(key: FundMetricKey): void {
  metricNotice.value = ''

  if (metricKeys.value.includes(key)) {
    if (metricKeys.value.length <= 1) {
      metricNotice.value = '至少保留一项指标。'
      return
    }

    metricKeys.value = metricKeys.value.filter((item) => item !== key)
    persistMetricKeys()
    return
  }

  if (metricKeys.value.length >= 3) {
    metricNotice.value = '最多同时显示 3 项指标，请先取消一项。'
    return
  }

  metricKeys.value = [...metricKeys.value, key]
  persistMetricKeys()
}

function metricDisplay(fund: FundSummary): { key: FundMetricKey; label: string; value: string; sub?: string; tone: string }[] {
  const total = scope.value.assetValue

  return metricKeys.value.map((key) => {
    switch (key) {
      case 'daily':
        return {
          key,
          label: '日收益',
          value: fund.hasEffectiveQuotes ? formatSignedMoney(fund.dailyProfit) : '—',
          sub: fund.hasEffectiveQuotes && fund.dailyProfitRate !== null
            ? `${formatSignedMoney(fund.dailyProfitRate)}%`
            : undefined,
          tone: amountTone(fund.hasEffectiveQuotes ? fund.dailyProfit : null)
        }
      case 'profit':
        return {
          key,
          label: '持有收益',
          value: fund.hasEffectiveQuotes ? formatSignedMoney(fund.holdingProfit) : '行情待补',
          tone: amountTone(fund.hasEffectiveQuotes ? fund.holdingProfit : null)
        }
      case 'cumulative':
        return {
          key,
          label: '累计收益',
          value: fund.historyConfirmed ? formatSignedMoney(cumulativeProfit(fund)) : '待核对',
          tone: amountTone(fund.historyConfirmed ? cumulativeProfit(fund) : null)
        }
      case 'rate':
        return {
          key,
          label: '收益率',
          value: fund.hasEffectiveQuotes && fund.cost > 0
            ? `${formatSignedMoney(fund.holdingProfit / fund.cost * 100)}%`
            : '—',
          tone: amountTone(fund.hasEffectiveQuotes && fund.cost > 0 ? fund.holdingProfit / fund.cost : null)
        }
      case 'cost':
        return { key, label: '成本', value: `¥${formatMoney(fund.cost)}`, tone: '' }
      case 'weight':
        return {
          key,
          label: '占比',
          value: total > 0 ? `${formatMoney(fund.assetValue / total * 100)}%` : '—',
          tone: ''
        }
      case 'pending':
        return {
          key,
          label: '在途',
          value: addAmounts(fund.pendingBuyAmount, fund.pendingArrivalAmount) > 0
            ? `¥${formatMoney(addAmounts(fund.pendingBuyAmount, fund.pendingArrivalAmount))}`
            : '—',
          tone: ''
        }
      default:
        return { key, label: '持仓价值', value: `¥${formatMoney(fund.value)}`, tone: '' }
    }
  })
}

function weightOf(fund: FundSummary): string {
  const total = scope.value.assetValue
  return total > 0 ? `${formatMoney(fund.assetValue / total * 100)}%` : '—'
}

// 待确认买入在确认净值日就定了份额，只是还没确认，所以卡片资产要把它算进去，
// 否则各只基金加起来和顶部总额对不上。
function pendingOf(fund: FundSummary): number {
  return addAmounts(fund.pendingBuyAmount, fund.pendingArrivalAmount)
}

function buildFallbackFunds(
  positions: FundPosition[],
  pendingBuys: PendingFundBuy[],
  pendingArrivals: PendingFundArrival[]
): FundSummary[] {
  const byName = new Map<string, FundPosition[]>()

  for (const position of positions) {
    const members = byName.get(position.name) ?? []
    members.push(position)
    byName.set(position.name, members)
  }

  return Array.from(byName, ([name, members]) => {
    const classes: FundShareClass[] = []
    const byClass = new Map<string, FundPosition[]>()

    for (const member of members) {
      const key = `${member.code?.trim() || member.name.normalize('NFKC').replace(/\s/g, '')}|${member.shareClass?.trim().toUpperCase() ?? ''}`
      const classMembers = byClass.get(key) ?? []
      classMembers.push(member)
      byClass.set(key, classMembers)
    }

    for (const [key, classMembers] of byClass) {
      const netValues = [...new Set(classMembers.map((member) => member.netValue))]
      const netValueDates = [...new Set(classMembers.map((member) => member.netValueDate))]

      classes.push({
        key,
        code: classMembers[0]?.code ?? null,
        shareClass: classMembers[0]?.shareClass || null,
        shares: sumAmounts(classMembers.map(member => member.shares)),
        cost: sumAmounts(classMembers.map(member => member.cost)),
        value: sumAmounts(classMembers.map(member => member.value)),
        holdingProfit: sumAmounts(classMembers.map(member => member.holdingProfit)),
        realizedProfit: sumAmounts(classMembers.map(member => member.realizedProfit)),
        netValue: netValues.length === 1 ? netValues[0]! : null,
        netValueDate: netValueDates.length === 1 ? netValueDates[0]! : null,
        dailyProfit: 0,
        dailyProfitRate: null,
        platforms: classMembers.map((member) => ({
          positionId: member.id,
          platform: member.platform,
          name: member.name,
          code: member.code,
          shareClass: member.shareClass,
          shares: member.shares,
          cost: member.cost,
          netValue: member.netValue,
          netValueDate: member.netValueDate,
          value: member.value,
          holdingProfit: member.holdingProfit,
          realizedProfit: member.realizedProfit,
          hasEffectiveQuote: member.hasEffectiveQuote,
          confirmationDelayDays: member.confirmationDelayDays,
          dailyProfit: member.dailyProfit ?? 0,
          dailyProfitRate: member.dailyProfitRate ?? null
        }))
      })
    }

    const memberIds = new Set(members.map((member) => member.id))
    const pendingBuyAmount = sumAmounts(pendingBuys
      .filter((item) => memberIds.has(item.positionId))
      .map(item => item.amount))
    const pendingArrivalAmount = sumAmounts(pendingArrivals
      .filter((item) => memberIds.has(item.positionId))
      .map(item => item.amount))
    const value = sumAmounts(members.map(member => member.value))
    const dailyProfit = sumAmounts(members.map(member => member.dailyProfit ?? 0))
    const groupId = members.find((member) => member.groupId)?.groupId ?? null

    return {
      key: name,
      name,
      groupId,
      groupName: members.find((member) => member.groupName)?.groupName ?? null,
      value,
      cost: sumAmounts(members.map(member => member.cost)),
      holdingProfit: sumAmounts(members.map(member => member.holdingProfit)),
      realizedProfit: sumAmounts(members.map(member => member.realizedProfit)),
      pendingBuyAmount,
      pendingArrivalAmount,
      assetValue: sumAmounts([value, pendingBuyAmount, pendingArrivalAmount]),
      platformCount: new Set(members.map((member) => member.platform).filter(Boolean)).size,
      latestNetValueDate: members.map((member) => member.netValueDate).sort().at(-1) ?? null,
      historyConfirmed: members.every((member) => member.historyConfirmed),
      hasEffectiveQuotes: members.every((member) => member.hasEffectiveQuote),
      classes,
      dailyProfit,
      dailyProfitRate: value - dailyProfit > 0 ? dailyProfit / (value - dailyProfit) * 100 : null
    } satisfies FundSummary
  }).sort((left, right) => left.name.localeCompare(right.name, 'zh-Hans-CN'))
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
  settingsOpen.value = false
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
    toast.success(`已新增投资分组「${name}」，可以继续添加资产。`)
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
    toast.success(`分组已更名为「${name}」。`)
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

    toast.success(`已删除分组「${target.name}」，组内基金回到未分组。`)
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

    toast.success(isMember
      ? `「${fund.name}」已移出「${group.name}」。`
      : `「${fund.name}」已加入「${group.name}」。`)
    await loadOverview()
  } catch (error) {
    groupError.value = getApiErrorMessage(error, '调整分组资产失败，请重试。')
    await loadOverview()
  } finally {
    groupBusy.value = false
    groupBusyKey.value = ''
  }
}

function planCycleLabel(plan: AutoPlan): string {
  if (plan.cycle === 'tradingDay') {
    return plan.tradingScope === 'china' ? '中国交易日' : '三地交易日'
  }

  if (plan.cycle === 'weekly') {
    const weekdays = ['', '周一', '周二', '周三', '周四', '周五', '周六', '周日']
    return `每周${weekdays[plan.dayOfWeek ?? 0] ?? ''}`
  }

  return `每月 ${plan.dayOfMonth ?? 1} 日`
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
  form.shares = String(application.shares)
  form.netValue = ''
  form.netValueDate = application.businessDate
  form.businessDate = today
  form.note = application.note ?? ''
}

function openBuyConfirmSheet(pending: PendingFundBuy): void {
  openSheet('buyConfirm', pending.positionId)
  confirmingBuyId.value = pending.transactionId
}

function estimateSellNetAmount(): void {
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

function openArrivalSheet(arrival: PendingFundArrival): void {
  openSheet('sellArrival', arrival.positionId)
  arrivingConfirmId.value = arrival.transactionId
  form.netAmount = String(arrival.amount)
  form.businessDate = today
  form.note = arrival.note ?? ''
}

function closeSheet(): void {
  if (!isSaving.value) {
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

async function submitOpening(forceAdd = false): Promise<void> {
  const shares = parseNonNegativeQuantity(form.shares)
  const cost = parseAmount(form.cost)
  const priorRealizedProfit = form.priorProfit.trim() ? parseSignedAmount(form.priorProfit) : 0

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
  toast.success(`已录入基金「${form.name.trim()}」。`)
  sheet.value = null
  await loadOverview()
}

function normalizePositionKey(value: string | null | undefined): string {
  return (value ?? '').normalize('NFKC').replace(/\s/g, '').toUpperCase()
}

function normalizePlatformKey(value: string | null | undefined): string {
  const key = normalizePositionKey(value)
  return key === '直销' ? '券商直销' : key
}

function findDuplicatePosition(): FundPosition | null {
  const code = normalizePositionKey(form.code)
  const shareClass = normalizePositionKey(form.shareClass)
  const platform = normalizePlatformKey(form.platform)

  if (!code) {
    return null
  }

  return overview.value?.positions.find((position) => {
    if (normalizePositionKey(position.code) !== code
      || normalizePositionKey(position.shareClass) !== shareClass) {
      return false
    }

    const existingPlatform = normalizePlatformKey(position.platform)

    return existingPlatform === platform || existingPlatform === '' || platform === ''
  }) ?? null
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
      requestId: createRequestId()
    })
    toast.success(`已更新「${prompt.position.name}」持仓为 ${formatQuantity(prompt.shares)} 份、成本 ¥${formatMoney(prompt.cost)}。`)
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
  duplicatePrompt.value = null
  await submitOpening(true)
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
  const result = await buyFund({
    positionId: position.id,
    amount,
    fee,
    businessDate: todayInBusinessTimeZone(),
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success(`已记录买入待确认 ¥${formatMoney(result.activity.amount)}，当前现金 ¥${formatMoney(result.cashBalance)}。`)
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
  toast.success(`已更新「${position.name}」持仓份额。`)
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
  const result = await sellFund({
    positionId: position.id,
    shares,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success(`已申请赎回 ${formatQuantity(result.activity.shares)} 份，确认后转为待到账。`)
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
  const result = await confirmFundSell(application.transactionId, {
    shares,
    netValue,
    netValueDate: form.netValueDate,
    fee,
    netAmount,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success(`已确认赎回 ${formatQuantity(result.activity.shares)} 份，待到账 ¥${formatMoney(result.activity.amount)}。`)
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
  const result = await arriveFundSell(arrival.transactionId, {
    amount,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success(`赎回到账 ¥${formatMoney(result.activity.amount)}，当前现金 ¥${formatMoney(result.cashBalance)}。`)
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


watch(fundSearch, (value) => {
  fundSearchError.value = ''

  if (fundSearchTimer) {
    clearTimeout(fundSearchTimer)
    fundSearchTimer = null
  }

  if (selectedFund.value) {
    if (!value.trim()) {
      return
    }

    selectedFund.value = null
  }

  const query = value.trim()

  if (!query) {
    fundSearchResults.value = []
    return
  }

  fundSearchTimer = setTimeout(() => {
    void runFundSearch(query)
  }, 300)
})

onBeforeUnmount(() => {
  if (fundSearchTimer) {
    clearTimeout(fundSearchTimer)
  }

})

async function runFundSearch(query: string): Promise<void> {
  fundSearching.value = true

  try {
    const results = await searchFunds(query)

    if (fundSearch.value.trim() === query) {
      fundSearchResults.value = results
      fundSearchError.value = results.length ? '' : '没有找到匹配的基金，请换一个名称或代码。'
    }
  } catch (error) {
    if (fundSearch.value.trim() === query) {
      fundSearchResults.value = []
      fundSearchError.value = getApiErrorMessage(error, '基金搜索暂时不可用，请稍后重试。')
    }
  } finally {
    fundSearching.value = false
  }
}

function chooseFund(result: FundSearchResult): void {
  selectedFund.value = result
  form.name = result.baseName
  form.code = result.code
  form.shareClass = result.shareClass ?? ''
  form.confirmationDelayDays = String(result.confirmationDelayDays)
  fundSearchResults.value = []
  fundSearchError.value = ''
  fundSearch.value = ''
}

function clearSelectedFund(): void {
  selectedFund.value = null
  form.name = ''
  form.code = ''
  form.shareClass = ''
  form.confirmationDelayDays = '1'
  fundSearch.value = ''
  fundSearchResults.value = []
  fundSearchError.value = ''
}

async function cancelPending(transactionId: string): Promise<void> {
  if (cancellingPendingId.value) {
    return
  }

  cancellingPendingId.value = transactionId

  try {
    await cancelFundPending(transactionId, createRequestId())
    toast.success('待处理基金交易已撤销。')
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
    <header class="funds-header">
      <BackButton @back="goBack" />
      <h1>投资</h1>
      <div class="funds-header-actions">
        <button
          type="button"
          class="icon-button"
          aria-label="搜索与指标"
          :class="{ active: searchText.trim() }"
          @click="settingsOpen = true"
        >
          <AppIcon name="filter" />
        </button>
        <details class="investment-actions">
          <summary class="icon-button" aria-label="记录投资"><AppIcon name="plus" /></summary>
          <div class="funds-actions" @click="($event.currentTarget as HTMLElement).closest('details')?.removeAttribute('open')">
            <button type="button" class="action-secondary" @click="openSheet('opening')">录入持仓</button>
            <button type="button" class="action-primary" :disabled="!overview?.positions.length" @click="openSheet('buy')">买入</button>
            <button type="button" class="action-secondary" :disabled="!overview?.positions.length" @click="openSheet('sell')">卖出</button>
          </div>
        </details>
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
      <section class="funds-hero">
        <p class="hero-label">
          总资产
          <span v-if="searchText.trim()" class="hero-search" :title="searchText">· 搜索「{{ searchText.trim() }}」</span>
          <span class="hero-tag">{{ scope.historyConfirmed ? '已确认持仓' : '持仓日期待确认' }}</span>
        </p>
        <h2 class="hero-balance">
          <template v-if="scope.hasEffectiveQuotes"><span>¥</span><AnimatedMoney :value="scope.assetValue" prefix="" /></template>
          <template v-else>行情待补</template>
        </h2>
        <div class="hero-today">
          <span class="hero-today-label">今日收益</span>
          <b :class="amountTone(todayProfit.hasValue ? todayProfit.value : null)">
            {{ todayProfit.hasValue ? formatSignedMoney(todayProfit.value) : '—' }}
          </b>
          <small v-if="!todayProfit.hasValue">行情待补</small>
          <small v-else-if="!todayProfit.isToday || !todayProfit.complete">
            <template v-if="!todayProfit.isToday && todayProfit.latestDate">
              按 {{ formatBusinessDate(todayProfit.latestDate) }} 净值
            </template>
            <template v-if="!todayProfit.complete"> · 部分持仓行情待补</template>
          </small>
        </div>

        <div class="funds-metrics" role="group" aria-label="收益概览">
          <div class="metrics-row">
            <div class="metric">
              <span class="metric-label">持有收益</span>
              <div class="metric-value">
                <b :class="amountTone(scope.hasEffectiveQuotes ? scope.holdingProfit : null)">
                  {{ scope.hasEffectiveQuotes ? formatSignedMoney(scope.holdingProfit) : '行情待补' }}
                </b>
                <small
                  v-if="scope.hasEffectiveQuotes && scope.rate !== null"
                  class="metric-rate"
                  :class="amountTone(scope.rate)"
                >{{ formatSignedMoney(scope.rate) }}%</small>
              </div>
            </div>
            <div class="metric metric-end">
              <span class="metric-label">已实现收益</span>
              <div class="metric-value">
                <b class="metric-secondary" :class="amountTone(scope.historyConfirmed ? scope.realizedProfit : null)">
                  {{ scope.historyConfirmed ? formatSignedMoney(scope.realizedProfit) : '待核对' }}
                </b>
              </div>
            </div>
          </div>
          <p class="metrics-note">
            {{ scopedFunds.length }} 只基金 · 持仓成本 <AnimatedMoney :value="scope.cost" />
            <template v-if="scope.pendingBuyAmount > 0"> · 含待确认 <AnimatedMoney :value="scope.pendingBuyAmount" /></template>
            <template v-if="scope.pendingArrivalAmount > 0"> · 含待到账 <AnimatedMoney :value="scope.pendingArrivalAmount" /></template>
          </p>
        </div>
      </section>

      <div class="view-tabs ui-segmented" role="tablist" aria-label="投资视图">
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
      <div v-if="searchText.trim()" class="active-search">
        <span>当前仅显示搜索结果</span>
        <button type="button" class="back-link" @click="searchText = ''">清除搜索</button>
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
          <span class="meta">可用现金 ¥{{ formatMoney(overview.cashBalance) }}</span>
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
                  <b><AnimatedMoney :value="fund.assetValue" /></b>
                  <small>
                    占比 {{ weightOf(fund) }}
                    <template v-if="pendingOf(fund) > 0"> · 含待确认 <AnimatedMoney :value="pendingOf(fund)" /></template>
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

            <div v-if="expandedFund === fund.key" class="fund-detail">
              <section
                v-for="(fundClass, index) in fund.classes"
                :key="fundClass.key ?? index"
                class="fund-class"
              >
                <header class="fund-class-head">
                  <strong>{{ fundClass.shareClass ? `${fundClass.shareClass} 类` : '未标类别' }}</strong>
                  <span>{{ formatQuantity(fundClass.shares) }} 份 · 成本 ¥{{ formatMoney(fundClass.cost) }}</span>
                </header>
                <div class="fund-class-values">
                  <div>
                    <span>估值</span>
                    <b>¥{{ formatMoney(fundClass.value) }}</b>
                  </div>
                  <div>
                    <span>持有盈亏</span>
                    <b :class="amountTone(fundClass.holdingProfit)">
                      {{ formatSignedMoney(fundClass.holdingProfit) }}
                    </b>
                  </div>
                  <div>
                    <span>已实现盈亏</span>
                    <b :class="amountTone(fundClass.realizedProfit)">
                      {{ formatSignedMoney(fundClass.realizedProfit) }}
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
          </article>
        </section>

        <EmptyState
          v-else
          icon="chart"
          :title="allFunds.length ? '没有找到基金' : '从第一笔持仓开始'"
          :description="allFunds.length ? '试试其他分组，或清除搜索条件。' : '录入已有基金，查看持仓价值与收益。'"
          :action-label="allFunds.length ? '查看全部基金' : '录入已有持仓'"
          @action="allFunds.length ? (selectGroup('all'), searchText = '') : openSheet('opening')"
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

        <section v-if="filteredActivityRows.length" class="activity-list">
          <div
            v-for="(row, index) in filteredActivityRows"
            :key="row.key"
            class="activity-row stagger-item"
            :class="{ 'is-pending': row.isPending, 'is-clickable': row.pendingAction }"
            :style="{ '--stagger': Math.min(index, 8) }"
            :role="row.pendingAction ? 'button' : undefined"
            :tabindex="row.pendingAction ? 0 : undefined"
            @click="openActivityRow(row)"
            @keydown.enter="openActivityRow(row)"
          >
            <span class="activity-type" :class="`type-${row.kind}`">{{ row.label }}</span>
            <span class="activity-main">
              <strong>基金 | {{ row.fundName }}</strong>
              <small>{{ row.timestamp }}</small>
            </span>
            <span class="activity-side">
              <b>{{ row.amountText }}</b>
              <small :class="`status-${row.statusTone}`">{{ row.statusText }}</small>
            </span>
          </div>
        </section>

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

    <BottomSheet
      :open="settingsOpen"
      title="搜索与指标"
      subtitle="只影响当前查看，不改变持仓"
      @close="settingsOpen = false"
    >
      <div class="portfolio-toolbar">
        <input v-model="searchText" type="search" placeholder="搜索基金名称或代码" aria-label="搜索基金" />
      </div>

      <div class="metric-picker" aria-label="持仓指标">
        <span>显示指标</span>
        <button
          v-for="option in metricOptions"
          :key="option.key"
          type="button"
          :class="{ active: metricKeys.includes(option.key) }"
          @click="toggleMetric(option.key)"
        >
          {{ option.label }}
        </button>
      </div>
      <p v-if="metricNotice" class="summary-note">{{ metricNotice }}</p>
    </BottomSheet>

    <BottomSheet
      :open="groupSheetOpen"
      title="管理分组"
      :subtitle="managedGroup ? '勾选资产加入分组，取消勾选即移出' : '新增、改名或删除分组'"
      :close-disabled="groupBusy"
      @close="closeGroupSheet"
    >
      <div v-if="managedGroup" class="group-detail">
        <button type="button" class="group-back" @click="backToGroupList">
          <AppIcon name="chevron-back" />
          全部分组
        </button>

        <div class="group-detail-head">
          <template v-if="editingGroupId === managedGroup.id">
            <input
              v-model="editingGroupName"
              type="text"
              maxlength="20"
              aria-label="分组名称"
              @keydown.enter.prevent="saveGroupName(managedGroup.id)"
            />
            <LoadingButton class="confirm-button" :loading="groupBusy" loading-label="保存中…" @click="saveGroupName(managedGroup.id)">保存</LoadingButton>
            <button type="button" class="text-button" :disabled="groupBusy" @click="editingGroupId = null">取消</button>
          </template>
          <template v-else>
            <div class="group-detail-copy">
              <strong>{{ managedGroup.name }}</strong>
              <small>{{ managedGroupFunds.length }} 只基金 · {{ managedGroup.positionCount }} 个持仓</small>
            </div>
            <button type="button" class="text-button" @click="startRenameGroup(managedGroup.id, managedGroup.name)">改名</button>
            <button
              type="button"
              class="text-button danger"
              :disabled="groupBusy"
              @click="removeGroup(managedGroup.id, managedGroup.name)"
            >删除</button>
          </template>
        </div>

        <div class="group-summary">
          <div>
            <span>已选资产（元）</span>
            <b>¥{{ formatMoney(managedGroupValue) }}</b>
          </div>
          <div>
            <span>已选资产数</span>
            <b>{{ managedGroupFunds.length }}</b>
          </div>
        </div>

        <p class="group-asset-title">选择资产</p>
        <p v-if="!groupAssetFunds.length" class="summary-note">还没有基金持仓，先录入持仓后再添加。</p>
        <section v-else class="group-asset-list">
          <button
            v-for="fund in groupAssetFunds"
            :key="fund.key"
            type="button"
            class="group-asset-row"
            :class="{ selected: fund.groupId === managedGroup.id }"
            :disabled="groupBusy"
            :aria-busy="groupBusyKey === fund.key"
            @click="toggleGroupFund(fund)"
          >
            <span class="group-asset-copy">
              <strong>{{ fund.name }}</strong>
              <small>当前持仓 ¥{{ formatMoney(fund.assetValue) }} · {{ fund.classes.length }} 个类别</small>
              <small v-if="fund.groupId && fund.groupId !== managedGroup.id" class="group-asset-hint">
                该产品已放置「{{ fund.groupName }}」中
              </small>
            </span>
            <span class="group-check" :class="{ checked: fund.groupId === managedGroup.id }">
              <span v-if="groupBusyKey === fund.key" class="group-check-spinner" aria-hidden="true" />
              <AppIcon v-else-if="fund.groupId === managedGroup.id" name="check" />
            </span>
          </button>
        </section>

        <p class="summary-note">同一基金的 A/C 类会一起调整；取消勾选后回到「未分组」。</p>
      </div>

      <div v-else class="group-list">
        <section v-if="groups.length" class="group-manage-list">
          <button
            v-for="group in groups"
            :key="group.id"
            type="button"
            class="group-manage-row"
            @click="openGroupDetail(group.id)"
          >
            <span>
              <strong>{{ group.name }}</strong>
              <small>{{ group.fundCount }} 只基金 · ¥{{ formatMoney(group.confirmedValue) }}</small>
            </span>
            <AppIcon name="chevron" />
          </button>
        </section>
        <p v-else class="summary-note">还没有分组，先在下方新增一个。</p>

        <form class="form-stack" @submit.prevent="submitGroup">
          <label class="form-field">
            <span>新增分组</span>
            <div class="group-create-row">
              <input v-model="groupDraft" type="text" maxlength="20" placeholder="例如：纳斯达克指数" />
              <LoadingButton class="primary-button" type="submit" :loading="groupBusy" loading-label="添加中…">添加</LoadingButton>
            </div>
          </label>
        </form>

        <p class="summary-note">分组只改变查看范围，不影响持仓、成本和收益。</p>
      </div>

      <p v-if="groupError" class="form-error" role="alert">{{ groupError }}</p>
    </BottomSheet>

    <BottomSheet
      :open="sheet !== null"
      :title="sheetTitle"
      :subtitle="sheetSubtitle"
      :close-disabled="isSaving"
      @close="closeSheet"
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

          <label class="form-field">
            <span>建账前已实现收益（元）</span>
            <input v-model="form.priorProfit" type="text" inputmode="decimal" placeholder="选填，可为负数" />
            <small>
              按平台上「累计收益 − 持有收益」填写，不填按 0 处理。只影响累计收益，不改持仓成本、份额和现金。
            </small>
          </label>
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
          <button type="button" class="text-button estimate-button" @click="estimateSellNetAmount">
            按 份额 × 净值 − 费用 试算净到账
          </button>
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
            <span>净到账金额（元）</span>
            <input v-model="form.netAmount" type="text" inputmode="decimal" placeholder="0.00" />
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
          <button type="button" class="secondary-button" :disabled="isSaving" @click="closeSheet">
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

.funds-header-actions .icon-button.active {
  background: var(--color-brand-soft);
}

.back-link {
  min-height: var(--touch-target);
  padding: 0 4px;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: 0.75rem;
}

.header-spacer {
  width: 46px;
}

.funds-hero {
  margin-bottom: 16px;
  padding: var(--card-padding);
  border-radius: var(--radius-card);
  background: linear-gradient(180deg, var(--color-hero-bg) 0%, var(--color-hero-bg-end) 100%);
  box-shadow: var(--shadow-hero);
  color: var(--color-hero-text);
}

.hero-label {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 7px;
  margin: 0;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-footnote);
}

.hero-tag {
  padding: 3px 9px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-hero-surface);
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.hero-balance {
  margin: 10px 0 8px;
  color: var(--color-hero-text);
  font-size: 2.5rem;
  font-weight: 700;
  letter-spacing: -0.04em;
  line-height: 1.15;
  font-variant-numeric: tabular-nums;
}

.hero-balance span {
  margin-right: 5px;
  color: var(--color-hero-text-secondary);
  font-size: 1.25rem;
  font-weight: 500;
  letter-spacing: 0;
}

.hero-today {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: 2px 12px;
  margin-top: 18px;
  padding: 12px 14px;
  border-radius: var(--radius-control);
  background: var(--color-hero-surface);
}

.hero-today-label {
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.hero-today b {
  color: var(--color-hero-text);
  font-size: 1.125rem;
  font-weight: 600;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.hero-today small {
  grid-column: 1 / -1;
  color: var(--color-hero-text-muted);
  font-size: var(--text-caption);
  line-height: 1.4;
}

.funds-metrics {
  margin-top: 18px;
  padding-top: 16px;
  border-top: 1px solid var(--color-hero-border);
}

.metrics-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: end;
  gap: 16px;
}

.metric-label {
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.metric-value {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-top: 6px;
}

.metric-value b {
  color: var(--color-hero-text);
  font-size: 1.0625rem;
  font-weight: 600;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.metric-value .metric-secondary {
  font-size: var(--text-subhead);
}

.metric-rate {
  padding: 2px 7px;
  border-radius: var(--radius-pill);
  background: var(--color-hero-surface);
  font-size: var(--text-caption);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.metric-end {
  text-align: right;
}

.metric-end .metric-value {
  justify-content: flex-end;
}

.metrics-note {
  margin: 10px 0 0;
  color: var(--color-hero-text-muted);
  font-size: var(--text-caption);
  line-height: 1.6;
}

.hero-search {
  overflow: hidden;
  max-width: 170px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.active-search {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin: -8px 0 12px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
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
}
.funds-actions button {
  min-height: 44px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  font-size: 0.875rem;
  font-weight: 600;
  text-align: center;
}
.action-primary {
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand);
  color: var(--color-on-brand);
}

.action-primary:hover:not(:disabled) {
  background: var(--color-brand-hover);
}

.action-secondary {
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.action-secondary:hover:not(:disabled) {
  background: var(--color-brand-soft-hover);
}

.funds-actions button:disabled {
  background: var(--color-bg-disabled);
  color: var(--color-text-disabled);
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
}

.group-manage .app-icon {
  width: 13px;
  height: 13px;
}

.portfolio-toolbar input {
  width: 100%;
  min-height: var(--touch-target);
  padding: 0 14px;
  border: 1px solid transparent;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  font-size: var(--text-body);
}

.portfolio-toolbar input:focus-visible {
  border-color: var(--color-border-focus);
  background: var(--color-bg-surface);
}

.metric-picker {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  margin-top: 10px;
}

.metric-picker > span {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.metric-picker button {
  min-height: 40px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.metric-picker button.active {
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-weight: 600;
}

.view-tabs {
  display: flex;
  gap: 2px;
  margin-bottom: 10px;
  padding: 4px;
  border-bottom: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-surface);
}

.view-tabs button {
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

.view-tabs button:hover:not(.active) {
  color: var(--color-text-primary);
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
}
.fund-card > header:hover {
  background: var(--color-bg-hover);
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
  min-height: 34px;
  padding: 0 12px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: var(--text-caption);
  font-weight: 600;
  white-space: nowrap;
}

.holding-action-row button:active {
  background: var(--color-brand-soft-hover);
}

@media (max-width: 340px) {
  .holding-action-row {
    flex-wrap: wrap;
  }

  .holding-buttons {
    width: 100%;
  }
}

.fund-platform-row {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  margin-top: 9px;
  padding-top: 9px;
  border-top: 1px dashed var(--color-border-subtle);
}

.fund-platform-row strong {
  display: block;
  font-size: var(--text-caption);
  font-weight: 500;
}

.fund-platform-row small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.fund-platform-value {
  text-align: right;
}

.fund-platform-value b {
  font-size: var(--text-caption);
  font-weight: 500;
  font-variant-numeric: tabular-nums;
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
}

.fund-search-result:last-child {
  border-bottom: 0;
}

.fund-search-result:active {
  background: var(--color-brand-soft);
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
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
}

.group-create-row {
  display: flex;
  gap: 8px;
}

.group-create-row input {
  flex: 1;
  min-width: 0;
}

.group-create-row button {
  flex: 0 0 auto;
  min-height: 48px;
  padding: 0 18px;
  border-radius: var(--radius-pill);
  font-size: var(--text-footnote);
}

.group-detail,
.group-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.group-back {
  display: inline-flex;
  align-items: center;
  align-self: flex-start;
  gap: 2px;
  min-height: var(--touch-target);
  padding: 0 4px 0 0;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-footnote);
}

.group-back .app-icon {
  width: 16px;
  height: 16px;
}

.group-detail-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
}

.group-detail-copy {
  flex: 1;
  min-width: 0;
}

.group-detail-copy strong {
  display: block;
  font-size: 1rem;
  font-weight: 600;
}

.group-detail-copy small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.group-detail-head input {
  flex: 1;
  min-width: 0;
  min-height: var(--touch-target);
  padding: 0 12px;
  border: 1px solid transparent;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: var(--text-footnote);
}

.group-summary {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  padding: 12px;
  border-radius: var(--radius-control);
  background: var(--color-brand-soft);
}

.group-summary span {
  display: block;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.group-summary b {
  display: block;
  margin-top: 5px;
  font-size: 1rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.group-summary > div:last-child {
  text-align: right;
}

.group-asset-title {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.group-asset-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.group-asset-row {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 14px;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  text-align: left;
}

.group-asset-row.selected {
  background: var(--color-brand-soft);
}

.group-asset-copy {
  flex: 1;
  min-width: 0;
}

.group-asset-copy strong {
  display: block;
  font-size: var(--text-footnote);
  font-weight: 600;
  line-height: 1.45;
  overflow-wrap: anywhere;
}

.group-asset-copy small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.group-asset-copy .group-asset-hint {
  color: var(--color-brand);
}

.group-check-spinner {
  width: 14px;
  height: 14px;
  border: 2px solid var(--color-brand);
  border-right-color: transparent;
  border-radius: 50%;
  animation: group-check-spin 0.7s linear infinite;
}

.group-check.checked .group-check-spinner {
  border-color: var(--color-on-brand);
  border-right-color: transparent;
}

@keyframes group-check-spin {
  to {
    transform: rotate(360deg);
  }
}

.group-check {
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  width: 22px;
  height: 22px;
  border: 1.5px solid var(--color-border-default);
  border-radius: 50%;
  color: var(--color-on-brand);
}

.group-check.checked {
  border-color: var(--color-brand);
  background: var(--color-brand);
}

.group-check .app-icon {
  width: 14px;
  height: 14px;
}

.group-manage-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.group-manage-row {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  min-height: 52px;
  padding: 12px 14px;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  text-align: left;
}

.group-manage-row:hover {
  background: var(--color-bg-hover);
}

.group-manage-row > span {
  flex: 1;
  min-width: 0;
}

.group-manage-row strong {
  display: block;
  font-size: var(--text-footnote);
  font-weight: 600;
}

.group-manage-row small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.group-manage-row .app-icon {
  width: 16px;
  height: 16px;
  color: var(--color-text-secondary);
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

.text-button.danger {
  color: var(--color-finance-loss);
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

.confirm-button {
  min-height: 36px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.position-list {
  display: flex;
  flex-direction: column;
  gap: 9px;
}

.position-card {
  padding: 14px;
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
}

.position-card header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
}

.position-card header strong {
  display: block;
  font-size: 0.8125rem;
  font-weight: 600;
}

.position-card header small {
  display: block;
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.position-card header > b {
  font-size: 1rem;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.position-metrics {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  margin-top: 13px;
  padding-top: 11px;
  border-top: 1px solid var(--color-border-subtle);
}

.position-metrics > div + div {
  padding-left: 10px;
  border-left: 1px solid var(--color-border-subtle);
}

.position-metrics span {
  display: block;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.position-metrics b {
  display: block;
  margin-top: 5px;
  font-size: 0.75rem;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.position-card footer {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  margin-top: 11px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.quote-error {
  display: block;
  margin-top: 7px;
  color: var(--color-finance-loss);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.empty-state {
  padding: 34px 16px;
  border: 1px dashed var(--color-border-default);
  border-radius: var(--radius-card);
  text-align: center;
}

.empty-state p {
  margin: 0 0 14px;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.empty-state .primary-button {
  width: 100%;
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

.activity-list {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.activity-row {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  box-sizing: border-box;
  width: 100%;
  padding: 14px 16px;
  border-bottom: 1px solid var(--color-border-subtle);
  text-align: left;
}

.activity-row:last-child {
  border-bottom: 0;
}

.activity-row.is-clickable {
  cursor: pointer;
}

.activity-row.is-clickable:active {
  background: var(--color-brand-soft);
}

.activity-type {
  flex: 0 0 34px;
  padding-top: 2px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.activity-type.type-sip,
.activity-type.type-buy {
  color: var(--color-finance-loss);
}

.activity-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
  min-width: 0;
}

.activity-main strong {
  font-size: 0.875rem;
  font-weight: 500;
  line-height: 1.45;
  overflow-wrap: anywhere;
}

.activity-main small {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.activity-side {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
  flex: 0 0 auto;
  padding-top: 1px;
}

.activity-side b {
  font-size: 0.875rem;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.activity-side small {
  font-size: var(--text-caption);
}

.status-pending {
  color: var(--color-brand);
}

.status-done {
  color: var(--color-text-secondary);
}

.status-reversed {
  color: var(--color-finance-loss);
}

.danger-text {
  align-self: flex-start;
  color: var(--color-finance-loss);
}

.summary-note {
  margin: 14px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
  text-align: center;
}

.estimate-button {
  align-self: flex-start;
  min-height: 32px;
  padding: 0;
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
</style>
