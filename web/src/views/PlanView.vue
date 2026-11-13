<script setup lang="ts">
import { navigateBack } from '../utils/navigation'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  deletePlan,
  fetchPlanCalendar,
  fetchPlanOccurrences,
  fetchPlanOverview,
  pausePlan,
  recordOccurrence,
  resumePlan,
  retryOccurrence,
  runPlans,
  savePlan,
  skipOccurrence
} from '../api/plans'
import { createFundPosition, fetchFundOverview, searchFunds } from '../api/funds'
import { ApiError, getApiErrorMessage } from '../api/client'
import { toast } from '../composables/useMessage'
import AppIcon from '../components/AppIcon.vue'
import AppNav from '../components/AppNav.vue'
import AnimatedMoney from '../components/AnimatedMoney.vue'
import BackButton from '../components/BackButton.vue'
import LoadingButton from '../components/LoadingButton.vue'
import BottomSheet from '../components/BottomSheet.vue'
import ConfirmSheet from '../components/ConfirmSheet.vue'
import GoalTabs from '../components/GoalTabs.vue'
import LoadingState from '../components/LoadingState.vue'
import PlanCalendar from '../components/PlanCalendar.vue'
import { createRequestId } from '../utils/request'
import { fundPlanDisplayName as fundDisplayName } from '../utils/plan'
import {
  formatBusinessDate,
  formatMoney,
  parseAmount,
  sumAmounts,
  todayInBusinessTimeZone
} from '../utils/format'
import type { FundPosition, FundSearchResult } from '../types/fund'
import type {
  AutoPlan,
  OccurrenceStatus,
  PlanCycle,
  PlanOccurrence,
  PlanOverview,
  PlanTradingScope,
  PlanType,
  SavePlanInput,
  TradingDay
} from '../types/plan'

const router = useRouter()
const route = useRoute()
const today = todayInBusinessTimeZone()
const overview = ref<PlanOverview | null>(null)
const positions = ref<FundPosition[]>([])
const positionsLoading = ref(false)
const positionsError = ref('')
const isLoading = ref(false)
const errorMessage = ref('')
const actionError = ref('')
const sheetOpen = ref(false)
const editingPlan = ref<AutoPlan | null>(null)
const isSaving = ref(false)
const formError = ref('')
const busyAction = ref('')
const busyKind = ref('')
const pendingPlanRemoval = ref<AutoPlan | null>(null)
const isDeletingPlan = ref(false)
const calendarYear = ref(Number(today.slice(0, 4)))
const calendarMonth = ref(Number(today.slice(5, 7)))
const calendarDays = ref<TradingDay[]>([])
const calendarLoading = ref(false)
const calendarError = ref('')
const calendarCache = new Map<string, TradingDay[]>()
let calendarSequence = 0
const occurrencePageSize = 10
const occurrencePage = ref(1)
const occurrenceItems = ref<PlanOccurrence[]>([])
const occurrenceTotal = ref(0)
const occurrenceLoading = ref(false)
const occurrenceError = ref('')
const occurrenceSection = ref<HTMLElement | null>(null)
let occurrenceSequence = 0
const expandedGroups = reactive<Record<PlanGroup['key'], boolean>>({
  fund: false,
  provident: false,
  income: false
})
const occurrencesExpanded = ref(false)

const form = reactive({
  type: 'salaryIncome' as PlanType,
  name: '',
  amount: '',
  fee: '0',
  cycle: 'monthly' as PlanCycle,
  tradingScope: 'threeMarkets' as PlanTradingScope,
  dayOfMonth: '15',
  dayOfWeek: '1',
  time: '09:00',
  startDate: today,
  fundPositionId: ''
})
const fundSearch = ref('')
const fundSearchResults = ref<FundSearchResult[]>([])
const fundSearching = ref(false)
const fundSearchError = ref('')
const selectedFundTarget = ref<FundSearchResult | null>(null)
let fundSearchTimer: ReturnType<typeof setTimeout> | null = null

const fundChoices = computed(() => {
  const choices = new Map<string, FundPosition & { sourceIds: string[] }>()

  for (const position of positions.value) {
    const normalize = (value: string | null) => (value ?? '').normalize('NFKC').replace(/\s/g, '').toUpperCase()
    const key = `${position.code ? `code:${normalize(position.code)}` : `name:${normalize(position.name)}`}|${normalize(position.shareClass)}`
    const existing = choices.get(key)

    if (existing) existing.sourceIds.push(position.id)
    else choices.set(key, { ...position, sourceIds: [position.id] })
  }

  return [...choices.values()]
})

const typeOptions: { value: PlanType; label: string }[] = [
  { value: 'salaryIncome', label: '工资到账' },
  { value: 'providentDeposit', label: '公积金缴存' },
  { value: 'providentWithdrawal', label: '公积金提取' },
  { value: 'fundSip', label: '基金定投' }
]

interface PlanGroup {
  key: 'fund' | 'provident' | 'income'
  title: string
  icon: string
  newType: PlanType
  plans: AutoPlan[]
  summary: string
}

interface FundPlanGroup {
  key: string
  name: string
  plans: AutoPlan[]
  single: AutoPlan | null
  codes: string[]
  shareClasses: string[]
  activeCount: number
  activeAmount: number
  nextDueDate: string | null
}

const planGroups = computed<PlanGroup[]>(() => {
  const plans = overview.value?.plans ?? []
  const fundPlans = plans.filter(plan => plan.type === 'fundSip')
  const providentPlans = plans.filter(plan => plan.type === 'providentDeposit' || plan.type === 'providentWithdrawal')
  const incomePlans = plans.filter(plan => plan.type === 'salaryIncome')

  return [
    {
      key: 'fund',
      title: '基金',
      icon: 'chart',
      newType: 'fundSip',
      plans: fundPlans,
      summary: groupSummary(fundPlans, '每期合计')
    },
    {
      key: 'provident',
      title: '公积金',
      icon: 'home',
      newType: 'providentDeposit',
      plans: providentPlans,
      summary: providentSummary(providentPlans)
    },
    {
      key: 'income',
      title: '定期收入',
      icon: 'wallet',
      newType: 'salaryIncome',
      plans: incomePlans,
      summary: groupSummary(incomePlans, '每期收入')
    }
  ]
})

const fundPlanGroups = computed<FundPlanGroup[]>(() => {
  const groups = new Map<string, FundPlanGroup>()
  const fundPlans = planGroups.value.find(group => group.key === 'fund')?.plans ?? []

  for (const plan of fundPlans) {
    const key = fundGroupKey(plan)
    const existing = groups.get(key)

    if (existing) {
      existing.plans.push(plan)
    } else {
      groups.set(key, {
        key,
        name: fundDisplayName(plan),
        plans: [plan],
        single: null,
        codes: [],
        shareClasses: [],
        activeCount: 0,
        activeAmount: 0,
        nextDueDate: null
      })
    }
  }

  return [...groups.values()]
    .map((group) => {
      const activePlans = group.plans.filter(plan => plan.isActive)
      const sortedPlans = [...group.plans]
        .sort((first, second) => shareClassLabel(first).localeCompare(shareClassLabel(second)))
      const nextDates = activePlans
        .map(plan => plan.nextDueDate)
        .filter((date): date is string => date !== null)
        .sort()

      return {
        ...group,
        plans: sortedPlans,
        single: sortedPlans.length === 1 ? sortedPlans[0]! : null,
        codes: [...new Set(sortedPlans.map(fundCode).filter((code): code is string => code !== null))],
        shareClasses: [...new Set(sortedPlans.map(shareClassLabel))],
        activeCount: activePlans.length,
        activeAmount: sumAmounts(activePlans.map(plan => plan.amount)),
        nextDueDate: nextDates[0] ?? null
      }
    })
    .sort((first, second) =>
      (first.nextDueDate ?? '9999-12-31').localeCompare(second.nextDueDate ?? '9999-12-31')
      || first.name.localeCompare(second.name))
})

const weekdayLabels = ['一', '二', '三', '四', '五', '六', '日']

// 新增从哪个分组进来，决定表单展示哪些字段：基金只填基金信息，公积金只能选缴存 / 提取。
const entryGroup = ref<PlanGroup['key'] | null>(null)

const sheetTitle = computed(() => {
  if (editingPlan.value) {
    return '编辑自动计划'
  }

  switch (entryGroup.value) {
    case 'fund':
      return '新建基金定投'
    case 'provident':
      return '新建公积金计划'
    case 'income':
      return '新建定期收入'
    default:
      return '新建自动计划'
  }
})

const showTypeField = computed(() => entryGroup.value === 'provident')

const typeFieldOptions = computed<{ value: PlanType; label: string }[]>(() =>
  entryGroup.value === 'provident'
    ? [
        { value: 'providentDeposit', label: '缴存' },
        { value: 'providentWithdrawal', label: '提取' }
      ]
    : [])

function groupKeyForType(type: PlanType): PlanGroup['key'] {
  if (type === 'fundSip') {
    return 'fund'
  }

  if (type === 'providentDeposit' || type === 'providentWithdrawal') {
    return 'provident'
  }

  return 'income'
}

function isGroupExpanded(key: PlanGroup['key']): boolean {
  return expandedGroups[key]
}

function toggleGroup(key: PlanGroup['key']): void {
  expandedGroups[key] = !expandedGroups[key]
}
const actionable = computed(() => overview.value?.pendingOccurrences ?? [])
const occurrenceTotalPages = computed(() =>
  Math.max(1, Math.ceil(occurrenceTotal.value / occurrencePageSize)))
const canSubmit = computed(() => {
  const amount = parseAmount(form.amount) ?? 0

  if (isSaving.value || amount <= 0) {
    return false
  }

  if (form.type === 'fundSip') {
    const fee = parseAmount(form.fee)

    if (fee === null || fee < 0 || fee >= amount) {
      return false
    }

    if (!selectedFundTarget.value && !fundChoices.value.some(item => item.id === form.fundPositionId)) {
      return false
    }
  }

  return form.time !== '' && form.startDate !== ''
})

watch(
  () => form.type,
  (type) => {
    if (type === 'fundSip') {
      void loadPositions()
    } else {
      resetFundSearch()
    }
  }
)

watch(
  () => form.cycle,
  (cycle) => {
    if (cycle === 'tradingDay' && editingPlan.value === null) {
      applyDefaultTradingScope()
    }
  }
)

function defaultTradingScope(): PlanTradingScope {
  if (selectedFundTarget.value) {
    return selectedFundTarget.value.isQdii ? 'threeMarkets' : 'china'
  }

  const position = fundChoices.value.find(item => item.id === form.fundPositionId)
  return position && (position.confirmationDelayDays ?? 1) >= 2 ? 'threeMarkets' : 'china'
}

function applyDefaultTradingScope(): void {
  form.tradingScope = defaultTradingScope()
}

watch(fundSearch, (value) => {
  fundSearchError.value = ''

  if (fundSearchTimer) {
    clearTimeout(fundSearchTimer)
    fundSearchTimer = null
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

onMounted(() => {
  if (route.query.new === 'providentDeposit' || route.query.new === 'salaryIncome') {
    openNew(route.query.new)
  }
  void load()
  void loadCalendar()
})

onBeforeUnmount(() => {
  if (fundSearchTimer) {
    clearTimeout(fundSearchTimer)
    fundSearchTimer = null
  }
})

async function load(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''

  try {
    overview.value = await fetchPlanOverview()
    await loadOccurrences()
  } catch (error) {
    overview.value = null

    if (error instanceof ApiError && error.status === 404) {
      await router.replace({ name: 'setup' })
      return
    }

    errorMessage.value = getApiErrorMessage(error, '自动记账计划加载失败，请重试。')
  } finally {
    isLoading.value = false
  }
}

async function loadPositions(): Promise<void> {
  if (positionsLoading.value) {
    return
  }

  positionsLoading.value = true
  positionsError.value = ''
  try {
    positions.value = (await fetchFundOverview()).positions
    const choice = fundChoices.value.find(item => item.sourceIds.includes(form.fundPositionId))
    if (choice) form.fundPositionId = choice.id
  } catch {
    positions.value = []
    positionsError.value = '基金类别加载失败，请重试。'
  } finally {
    positionsLoading.value = false
  }
}

async function loadOccurrences(page = occurrencePage.value): Promise<void> {
  const target = Math.max(1, page)
  const sequence = ++occurrenceSequence
  occurrenceLoading.value = true
  occurrenceError.value = ''

  try {
    const result = await fetchPlanOccurrences(target, occurrencePageSize)

    if (sequence !== occurrenceSequence) {
      return
    }

    const totalPages = Math.max(1, Math.ceil(result.totalCount / occurrencePageSize))

    if (target > totalPages) {
      void loadOccurrences(totalPages)
      return
    }

    occurrencePage.value = target
    occurrenceItems.value = result.items
    occurrenceTotal.value = result.totalCount
  } catch (error) {
    if (sequence !== occurrenceSequence) {
      return
    }

    occurrenceItems.value = []
    occurrenceTotal.value = 0
    occurrenceError.value = getApiErrorMessage(error, '执行记录加载失败，请重试。')
  } finally {
    if (sequence === occurrenceSequence) {
      occurrenceLoading.value = false
    }
  }
}

function goOccurrencePage(page: number): void {
  if (occurrenceLoading.value) {
    return
  }

  const target = Math.min(Math.max(1, page), occurrenceTotalPages.value)

  if (target !== occurrencePage.value) {
    void loadOccurrences(target).then(() => {
      occurrenceSection.value?.scrollIntoView?.({ block: 'start', behavior: 'smooth' })
    })
  }
}

function openNew(type: PlanType = 'salaryIncome'): void {
  editingPlan.value = null
  formError.value = ''
  form.type = type
  entryGroup.value = groupKeyForType(type)
  form.name = ''
  form.amount = ''
  form.fee = '0'
  form.cycle = 'monthly'
  form.tradingScope = 'threeMarkets'
  form.dayOfMonth = '15'
  form.dayOfWeek = '1'
  form.time = '09:00'
  form.startDate = today
  form.fundPositionId = ''
  resetFundSearch()
  sheetOpen.value = true

  if (type === 'fundSip') {
    void loadPositions()
  }
}

function openEdit(plan: AutoPlan): void {
  editingPlan.value = plan
  formError.value = ''
  form.type = plan.type
  entryGroup.value = groupKeyForType(plan.type)
  form.name = plan.name
  form.amount = String(plan.amount)
  form.fee = String(plan.fee ?? 0)
  form.cycle = plan.cycle
  form.tradingScope = plan.tradingScope ?? 'threeMarkets'
  form.dayOfMonth = plan.dayOfMonth ? String(plan.dayOfMonth) : '15'
  form.dayOfWeek = plan.dayOfWeek ? String(plan.dayOfWeek) : '1'
  form.time = plan.time.slice(0, 5)
  form.startDate = plan.startDate
  form.fundPositionId = plan.fundPositionId ?? ''
  resetFundSearch()
  sheetOpen.value = true

  if (plan.type === 'fundSip') {
    void loadPositions()
  }
}

function closeSheet(): void {
  if (!isSaving.value) {
    sheetOpen.value = false
  }
}

async function submit(): Promise<void> {
  const amount = parseAmount(form.amount)

  if (amount === null || amount <= 0) {
    formError.value = '请填写有效的计划金额。'
    return
  }

  const selectedChoice = fundChoices.value.find(item => item.id === form.fundPositionId)
  const target = selectedFundTarget.value
  const isFundSip = form.type === 'fundSip'
  const fee = isFundSip ? parseAmount(form.fee) ?? 0 : 0

  if (isFundSip && !target && !selectedChoice) {
    formError.value = positionsError.value || '请选择基金及份额类别，或搜索基金直接建立定投。'
    return
  }

  if (isFundSip && (fee < 0 || fee >= amount)) {
    formError.value = '每期申购费不能为负，且必须小于每期金额。'
    return
  }

  const fundTarget = isFundSip
    ? target
      ? { name: target.baseName, code: target.code, shareClass: target.shareClass }
      : selectedChoice
        ? { name: selectedChoice.name, code: selectedChoice.code, shareClass: selectedChoice.shareClass }
        : null
    : null

  const input: SavePlanInput = {
    id: editingPlan.value?.id ?? null,
    type: form.type,
    name: form.name.trim(),
    amount,
    fee,
    tradingScope: isFundSip && form.cycle === 'tradingDay' ? form.tradingScope : 'threeMarkets',
    cycle: isFundSip ? form.cycle : 'monthly',
    dayOfMonth: (isFundSip && form.cycle !== 'monthly') ? null : Number(form.dayOfMonth),
    dayOfWeek: (isFundSip && form.cycle === 'weekly') ? Number(form.dayOfWeek) : null,
    time: form.time.length === 5 ? `${form.time}:00` : form.time,
    startDate: form.startDate,
    fundPositionId: null,
    fundTarget,
    requestId: createRequestId(),
    expectedVersion: editingPlan.value?.version ?? null
  }

  isSaving.value = true
  formError.value = ''

  try {
    if (isFundSip && target) {
      await ensurePositionForTarget(target)
    }

    await savePlan(input)
    toast.success(editingPlan.value ? '计划已更新，后续期次按新配置生成。' : '计划已保存。')

    if (entryGroup.value) {
      expandedGroups[entryGroup.value] = true
    }

    sheetOpen.value = false
    await load()
  } catch (error) {
    formError.value = getApiErrorMessage(error, '保存计划失败，请重试。')
  } finally {
    isSaving.value = false
  }
}

function resetFundSearch(): void {
  if (fundSearchTimer) {
    clearTimeout(fundSearchTimer)
    fundSearchTimer = null
  }

  fundSearch.value = ''
  fundSearchResults.value = []
  fundSearchError.value = ''
  fundSearching.value = false
  selectedFundTarget.value = null
}

function chooseFundTarget(result: FundSearchResult): void {
  selectedFundTarget.value = result
  form.fundPositionId = ''
  fundSearch.value = ''
  fundSearchResults.value = []
  fundSearchError.value = ''

  if (form.cycle === 'tradingDay') {
    applyDefaultTradingScope()
  }
}

function onFundSelectChange(): void {
  selectedFundTarget.value = null

  if (form.cycle === 'tradingDay') {
    applyDefaultTradingScope()
  }
}

function clearFundTarget(): void {
  selectedFundTarget.value = null
  fundSearch.value = ''
  fundSearchResults.value = []
  fundSearchError.value = ''
}

function normalizeIdentity(value: string | null | undefined): string {
  return (value ?? '').normalize('NFKC').replace(/\s/g, '').toUpperCase()
}

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

async function ensurePositionForTarget(target: FundSearchResult): Promise<void> {
  const code = normalizeIdentity(target.code)
  const shareClass = normalizeIdentity(target.shareClass)
  const existing = positions.value.find((position) =>
    normalizeIdentity(position.code) === code && normalizeIdentity(position.shareClass) === shareClass)

  if (existing) {
    return
  }

  await createFundPosition({
    name: target.baseName,
    code: target.code,
    shareClass: target.shareClass ?? '',
    platform: '',
    shares: 0,
    cost: 0,
    note: '定投计划自动建立',
    requestId: createRequestId(),
    holdingDate: today,
    confirmationDelayDays: target.confirmationDelayDays
  })

  await loadPositions()
}

async function togglePlan(plan: AutoPlan): Promise<void> {
  busyAction.value = plan.id
  busyKind.value = 'toggle'
  actionError.value = ''

  try {
    if (plan.isActive) {
      await pausePlan(plan.id, createRequestId())
      toast.success('计划已暂停；已有期次保留，暂停期间不追补。')
    } else {
      await resumePlan(plan.id, createRequestId())
      toast.success('计划已恢复，从下一期继续。')
    }

    await load()
  } catch (error) {
    actionError.value = getApiErrorMessage(error, '操作失败，请重试。')
  } finally {
    busyAction.value = ''
    busyKind.value = ''
  }
}

function requestPlanRemoval(plan: AutoPlan): void {
  pendingPlanRemoval.value = plan
}

async function confirmPlanRemoval(): Promise<void> {
  const plan = pendingPlanRemoval.value

  if (!plan || isDeletingPlan.value) {
    return
  }

  isDeletingPlan.value = true
  actionError.value = ''

  try {
    await deletePlan(plan.id, createRequestId())
    pendingPlanRemoval.value = null
    toast.success('计划已删除；已记账的收支记录会保留。')
    await load()
  } catch (error) {
    actionError.value = getApiErrorMessage(error, '删除计划失败，请重试。')
  } finally {
    isDeletingPlan.value = false
  }
}

async function handleOccurrence(occurrenceId: string, action: 'retry' | 'skip'): Promise<void> {
  busyAction.value = occurrenceId
  busyKind.value = action
  actionError.value = ''

  try {
    if (action === 'retry') {
      await retryOccurrence(occurrenceId, createRequestId())
      toast.success('已重新排队；余额补足后点击“立即执行”或等待后台任务。')
    } else {
      await skipOccurrence(occurrenceId, createRequestId())
      toast.success('本期已跳过，不会再次生成。')
    }

    await load()
  } catch (error) {
    actionError.value = getApiErrorMessage(error, '操作失败，请重试。')
  } finally {
    busyAction.value = ''
    busyKind.value = ''
  }
}

async function recordCurrentOccurrence(occurrenceId: string): Promise<void> {
  busyAction.value = occurrenceId
  busyKind.value = 'record'
  actionError.value = ''

  try {
    const result = await recordOccurrence(occurrenceId, createRequestId())
    if (result.recorded) {
      toast.success('本期期次已补记到账本。')
    } else {
      actionError.value = result.error ?? '本期仍待处理，请检查账本余额和关联资料。'
    }
    await load()
  } catch (error) {
    actionError.value = getApiErrorMessage(error, '补记本期失败，请重试。')
  } finally {
    busyAction.value = ''
    busyKind.value = ''
  }
}

async function runNow(): Promise<void> {
  busyAction.value = 'run'
  busyKind.value = 'run'
  actionError.value = ''

  try {
    const result = await runPlans()
    const summary = `执行完成：新建 ${result.createdOccurrences}，已记账 ${result.completed}，待处理 ${result.waiting}，失败 ${result.failed}。`
    if (result.failed > 0) toast.info(summary)
    else toast.success(summary)
    await load()
  } catch (error) {
    actionError.value = getApiErrorMessage(error, '立即执行失败，请重试。')
  } finally {
    busyAction.value = ''
    busyKind.value = ''
  }
}

async function loadCalendar(year = calendarYear.value, month = calendarMonth.value): Promise<void> {
  const key = `${year}-${month}`
  const cached = calendarCache.get(key)

  calendarYear.value = year
  calendarMonth.value = month
  calendarError.value = ''

  if (cached) {
    calendarSequence++
    calendarDays.value = cached
    calendarLoading.value = false
    return
  }

  const sequence = ++calendarSequence
  calendarDays.value = []
  calendarLoading.value = true

  try {
    const result = await fetchPlanCalendar(year, month)

    if (sequence !== calendarSequence) {
      return
    }

    calendarCache.set(key, result.days)
    calendarDays.value = result.days
  } catch (error) {
    if (sequence !== calendarSequence) {
      return
    }

    calendarDays.value = []
    calendarError.value = getApiErrorMessage(error, '交易日历加载失败，请重试。')
  } finally {
    if (sequence === calendarSequence) {
      calendarLoading.value = false
    }
  }
}

function shiftCalendar(offset: number): void {
  const next = new Date(Date.UTC(calendarYear.value, calendarMonth.value - 1 + offset, 1))
  void loadCalendar(next.getUTCFullYear(), next.getUTCMonth() + 1)
}

function groupSummary(plans: AutoPlan[], amountLabel: string): string {
  if (!plans.length) {
    return '暂无计划'
  }

  const activePlans = plans.filter(plan => plan.isActive)

  if (!activePlans.length) {
    return `已全部暂停 · ${plans.length} 个计划`
  }

  const paused = plans.length - activePlans.length
  const amount = sumAmounts(activePlans.map(plan => plan.amount))

  return `${activePlans.length} 个启用 · ${amountLabel} ¥${formatMoney(amount)}${paused ? ` · ${paused} 个暂停` : ''}`
}

function providentSummary(plans: AutoPlan[]): string {
  if (!plans.length) {
    return '暂无计划'
  }

  const activePlans = plans.filter(plan => plan.isActive)
  const deposits = sumAmounts(activePlans.filter(plan => plan.type === 'providentDeposit').map(plan => plan.amount))
  const withdrawals = sumAmounts(activePlans.filter(plan => plan.type === 'providentWithdrawal').map(plan => plan.amount))
  const parts: string[] = []

  if (deposits > 0) {
    parts.push(`缴存 ¥${formatMoney(deposits)}`)
  }

  if (withdrawals > 0) {
    parts.push(`提取 ¥${formatMoney(withdrawals)}`)
  }

  const paused = plans.length - activePlans.length

  if (!parts.length) {
    return paused ? `已全部暂停 · ${paused} 个计划` : '缴存与提取'
  }

  return `${parts.join(' · ')}${paused ? ` · ${paused} 个暂停` : ''}`
}

function fundGroupKey(plan: AutoPlan): string {
  return fundDisplayName(plan).normalize('NFKC').replace(/\s/g, '').toUpperCase()
}

function fundCode(plan: AutoPlan): string | null {
  const code = plan.fundTarget?.code?.trim()
  return code ? code : null
}

function planDisplayName(planId: string, fallback: string): string {
  const plan = overview.value?.plans.find(item => item.id === planId)

  if (!plan) {
    return fallback
  }

  return plan.type === 'fundSip' ? fundDisplayName(plan) : fallback
}

function fundMetaLine(plan: AutoPlan): string {
  return [fundCode(plan), shareClassLabel(plan), scheduleText(plan)]
    .filter((part): part is string => Boolean(part))
    .join(' · ')
}

function fundGroupMetaLine(group: FundPlanGroup): string {
  return [
    group.codes.join(' / '),
    group.shareClasses.join(' / '),
    `${group.plans.length} 个计划`
  ].filter(Boolean).join(' · ')
}

function shareClassLabel(plan: AutoPlan): string {
  const shareClass = plan.fundTarget?.shareClass?.trim()

  if (shareClass) {
    return /^[A-Za-z]$/.test(shareClass) ? `${shareClass.toUpperCase()} 类` : shareClass
  }

  const match = (plan.fundPositionName ?? plan.name).match(/\s([A-Za-z])(?:\s*类)?(?:\s*[·]|$)/)
  return match ? `${match[1]!.toUpperCase()} 类` : '未标类别'
}

function nextDueText(plan: AutoPlan): string {
  if (!plan.isActive) {
    return '已暂停'
  }

  return plan.nextDueDate ? `下一期 ${shortDate(plan.nextDueDate)}` : '下一期待生成'
}

function shortDate(value: string): string {
  return value.startsWith(`${today.slice(0, 4)}-`)
    ? `${Number(value.slice(5, 7))}月${Number(value.slice(8, 10))}日`
    : formatBusinessDate(value)
}

function typeLabel(type: PlanType): string {
  return typeOptions.find((option) => option.value === type)?.label ?? type
}

function scheduleText(plan: AutoPlan): string {
  const time = plan.time.slice(0, 5)

  if (plan.cycle === 'tradingDay') {
    return `${plan.tradingScope === 'china' ? '中国交易日' : '三地交易日'} ${time}`
  }

  if (plan.cycle === 'weekly') {
    return `每周${weekdayLabels[(plan.dayOfWeek ?? 1) - 1]} ${time}`
  }

  return `每月 ${plan.dayOfMonth} 日 ${time}`
}

function statusLabel(status: OccurrenceStatus): string {
  switch (status) {
    case 'completed':
      return '已记账'
    case 'waiting':
      return '待处理'
    case 'failed':
      return '执行失败'
    case 'skipped':
      return '已跳过'
    default:
      return '待执行'
  }
}

function statusClass(status: OccurrenceStatus): string {
  return `status-${status}`
}

function goBack(): void {
  navigateBack(router)
}
</script>

<template>
  <main class="page plan-page with-nav">
    <GoalTabs />
    <header class="plan-header">
      <BackButton @back="goBack" />
      <h1>自动记账</h1>
      <button
        type="button"
        class="header-action"
        :disabled="busyAction === 'run'"
        :aria-label="busyAction === 'run' ? '正在执行' : '立即执行'"
        :title="busyAction === 'run' ? '正在执行' : '立即执行'"
        @click="runNow"
      >
        <AppIcon name="refresh" :class="{ spinning: busyAction === 'run' }" />
      </button>
    </header>

    <LoadingState v-if="isLoading && !overview" variant="card" label="正在读取自动计划" />
    <LoadingState v-if="isLoading && !overview" variant="list" :rows="3" label="正在读取计划列表" />

    <section v-else-if="errorMessage" class="state">
      <p>{{ errorMessage }}</p>
      <LoadingButton class="secondary-button" :loading="isLoading" loading-label="加载中…" @click="load">重新加载</LoadingButton>
    </section>

    <template v-else-if="overview">
      <p v-if="actionError" class="form-error" role="alert">{{ actionError }}</p>

      <div class="section-head">
        <h2>计划</h2>
        <span class="meta">{{ overview.plans.length }} 个 · {{ overview.activeCount }} 个启用</span>
      </div>

      <section
        v-for="group in planGroups"
        :key="group.key"
        class="plan-group"
        :class="`group-${group.key}`"
      >
        <header class="plan-group-head">
          <button
            type="button"
            class="group-toggle"
            :aria-expanded="isGroupExpanded(group.key)"
            @click="toggleGroup(group.key)"
          >
            <span class="group-icon"><AppIcon :name="group.icon" /></span>
            <span class="group-heading">
              <span class="group-title">{{ group.title }}</span>
              <span class="group-summary">{{ group.summary }}</span>
            </span>
            <span class="group-chevron" :class="{ open: isGroupExpanded(group.key) }" aria-hidden="true">⌄</span>
          </button>
          <button
            type="button"
            class="group-add"
            :aria-label="`新增${group.title}计划`"
            @click="openNew(group.newType)"
          >＋ 新增</button>
        </header>

        <div v-if="isGroupExpanded(group.key)" class="plan-group-body">
          <template v-if="group.key === 'fund'">
            <div v-if="fundPlanGroups.length" class="fund-plan-list">
              <article
                v-for="(fund, index) in fundPlanGroups"
                :key="fund.key"
                class="plan-card fund-plan-card stagger-item"
                :class="{ 'is-paused': fund.activeCount === 0 }"
                :style="{ '--stagger': Math.min(index, 8) }"
              >
                <details v-if="fund.plans.length > 1" class="fund-details">
                  <summary>
                    <span class="fund-summary-copy">
                      <strong>{{ fund.name }}</strong>
                      <small>
                        {{ fundGroupMetaLine(fund) }}
                        <template v-if="fund.plans.length - fund.activeCount"> · {{ fund.plans.length - fund.activeCount }} 个暂停</template>
                      </small>
                    </span>
                    <span class="fund-summary-amount">
                      <template v-if="fund.activeCount">
                        <b>¥{{ formatMoney(fund.activeAmount) }}</b>
                        <small>每期</small>
                      </template>
                      <small v-else class="paused-label">已暂停</small>
                    </span>
                  </summary>
                  <div class="fund-plan-rows">
                    <div
                      v-for="plan in fund.plans"
                      :key="plan.id"
                      class="fund-plan-row"
                      :class="{ paused: !plan.isActive }"
                    >
                      <span class="share-badge">{{ shareClassLabel(plan) }}</span>
                      <div class="fund-row-main">
                        <div class="fund-row-head">
                          <b>¥{{ formatMoney(plan.amount) }}</b>
                          <span class="fund-row-actions">
                            <button type="button" class="link-button" @click="openEdit(plan)">编辑</button>
                            <span class="action-sep" aria-hidden="true" />
                            <LoadingButton
                              class="link-button"
                              :loading="busyAction === plan.id && busyKind === 'toggle'"
                              loading-label="处理中…"
                              @click="togglePlan(plan)"
                            >{{ plan.isActive ? '暂停' : '恢复' }}</LoadingButton>
                            <span class="action-sep" aria-hidden="true" />
                            <button
                              type="button"
                              class="link-button danger-text"
                              :disabled="busyAction === plan.id"
                              @click="requestPlanRemoval(plan)"
                            >删除</button>
                          </span>
                        </div>
                        <small>
                          <template v-if="fundCode(plan)">{{ fundCode(plan) }} · </template>
                          {{ scheduleText(plan) }} · {{ nextDueText(plan) }}
                          <template v-if="(plan.fee ?? 0) > 0"> · 含申购费 ¥{{ formatMoney(plan.fee ?? 0) }}</template>
                        </small>
                      </div>
                    </div>
                  </div>
                </details>

                <template v-else-if="fund.single">
                  <header>
                    <div>
                      <strong>{{ fund.name }}</strong>
                      <small>{{ fundMetaLine(fund.single) }}</small>
                    </div>
                    <span class="plan-amount"><AnimatedMoney :value="fund.single.amount" /></span>
                  </header>
                  <p class="plan-meta">
                    开始 {{ formatBusinessDate(fund.single.startDate) }}
                    <template v-if="(fund.single.fee ?? 0) > 0"> · 含申购费 ¥{{ formatMoney(fund.single.fee ?? 0) }}</template>
                  </p>
                  <footer class="plan-card-foot">
                    <span class="plan-foot-note">{{ nextDueText(fund.single) }}</span>
                    <span class="plan-foot-actions">
                      <button type="button" class="link-button" @click="openEdit(fund.single)">编辑</button>
                      <span class="action-sep" aria-hidden="true" />
                      <LoadingButton
                        class="link-button"
                        :loading="busyAction === fund.single.id && busyKind === 'toggle'"
                        loading-label="处理中…"
                        @click="togglePlan(fund.single)"
                      >{{ fund.single.isActive ? '暂停' : '恢复' }}</LoadingButton>
                      <span class="action-sep" aria-hidden="true" />
                      <button
                        type="button"
                        class="link-button danger-text"
                        :disabled="busyAction === fund.single.id"
                        @click="requestPlanRemoval(fund.single)"
                      >删除</button>
                    </span>
                  </footer>
                </template>
              </article>
            </div>
          </template>

          <template v-else>
            <article
              v-for="(plan, index) in group.plans"
              :key="plan.id"
              class="plan-card stagger-item"
              :style="{ '--stagger': Math.min(index, 8) }"
            >
              <header>
                <div>
                  <strong>{{ plan.name }}</strong>
                  <small>{{ typeLabel(plan.type) }} · {{ scheduleText(plan) }}</small>
                </div>
                <span class="plan-amount"><AnimatedMoney :value="plan.amount" /></span>
              </header>
              <p class="plan-meta">
                开始 {{ formatBusinessDate(plan.startDate) }}
              </p>
              <p v-if="plan.type === 'providentWithdrawal'" class="plan-meta">提取后等额转入现金，不计入生活收入。</p>
              <footer class="plan-card-foot">
                <span class="plan-foot-note">{{ nextDueText(plan) }}</span>
                <span class="plan-foot-actions">
                  <button type="button" class="link-button" @click="openEdit(plan)">编辑</button>
                  <span class="action-sep" aria-hidden="true" />
                  <LoadingButton
                    class="link-button"
                    :loading="busyAction === plan.id && busyKind === 'toggle'"
                    loading-label="处理中…"
                    @click="togglePlan(plan)"
                  >{{ plan.isActive ? '暂停' : '恢复' }}</LoadingButton>
                  <span class="action-sep" aria-hidden="true" />
                  <button
                    type="button"
                    class="link-button danger-text"
                    :disabled="busyAction === plan.id"
                    @click="requestPlanRemoval(plan)"
                  >删除</button>
                </span>
              </footer>
            </article>
          </template>
        </div>
      </section>

      <template v-if="actionable.length">
        <div class="section-head">
          <h2>待处理期次</h2>
          <span class="meta">{{ actionable.length }} 笔</span>
        </div>
        <section class="occurrence-list">
          <article v-for="occurrence in actionable" :key="occurrence.id" class="occurrence-card">
            <div>
              <strong>{{ planDisplayName(occurrence.planId, occurrence.planName) }}</strong>
              <small>
                {{ occurrence.dueDate }} · ¥{{ formatMoney(occurrence.amount) }} ·
                <span :class="statusClass(occurrence.status)">{{ statusLabel(occurrence.status) }}</span>
              </small>
              <small v-if="occurrence.errorMessage" class="occurrence-error">{{ occurrence.errorMessage }}</small>
            </div>
            <div class="occurrence-actions">
              <LoadingButton
                class="small-button"
                :loading="busyAction === occurrence.id && busyKind === 'record'"
                loading-label="补记中…"
                @click="recordCurrentOccurrence(occurrence.id)"
              >
                补记本期
              </LoadingButton>
              <LoadingButton
                class="small-button"
                :loading="busyAction === occurrence.id && busyKind === 'retry'"
                loading-label="排队中…"
                @click="handleOccurrence(occurrence.id, 'retry')"
              >
                重新排队
              </LoadingButton>
              <LoadingButton
                class="small-button"
                :loading="busyAction === occurrence.id && busyKind === 'skip'"
                loading-label="跳过中…"
                @click="handleOccurrence(occurrence.id, 'skip')"
              >
                跳过
              </LoadingButton>
            </div>
          </article>
        </section>
      </template>

      <PlanCalendar
        :year="calendarYear"
        :month="calendarMonth"
        :days="calendarDays"
        :loading="calendarLoading"
        :error="calendarError"
        @prev="shiftCalendar(-1)"
        @next="shiftCalendar(1)"
        @retry="loadCalendar()"
      />

      <button
        ref="occurrenceSection"
        type="button"
        class="section-head occurrence-head section-toggle"
        :aria-expanded="occurrencesExpanded"
        @click="occurrencesExpanded = !occurrencesExpanded"
      >
        <span class="section-title">执行记录</span>
        <span class="section-toggle-meta">
          <span class="meta">共 {{ occurrenceTotal }} 条</span>
          <span class="section-chevron" :class="{ open: occurrencesExpanded }" aria-hidden="true">⌄</span>
        </span>
      </button>
      <template v-if="occurrencesExpanded">
        <div v-if="occurrenceError" class="occurrence-state">
          <span>{{ occurrenceError }}</span>
          <LoadingButton class="text-button" :loading="occurrenceLoading" loading-label="加载中…" @click="loadOccurrences()">重试</LoadingButton>
        </div>
        <template v-else>
          <section
            v-if="occurrenceItems.length"
            class="occurrence-list"
            :class="{ 'is-loading': occurrenceLoading }"
          >
            <article v-for="occurrence in occurrenceItems" :key="occurrence.id" class="occurrence-card">
              <div>
                <strong>{{ planDisplayName(occurrence.planId, occurrence.planName) }}</strong>
                <small>
                  {{ occurrence.dueDate }} {{ occurrence.dueTime.slice(0, 5) }} · ¥{{ formatMoney(occurrence.amount) }} ·
                  <span :class="statusClass(occurrence.status)">{{ statusLabel(occurrence.status) }}</span>
                </small>
                <small v-if="occurrence.errorMessage" class="occurrence-error">{{ occurrence.errorMessage }}</small>
              </div>
            </article>
          </section>
          <LoadingState v-else-if="occurrenceLoading" variant="list" :rows="3" compact label="正在读取执行记录" />
          <p v-else class="summary-note">还没有执行记录。</p>
          <nav v-if="occurrenceTotal > occurrencePageSize" class="occurrence-pagination" aria-label="执行记录分页">
            <button
              type="button"
              class="pagination-button"
              :disabled="occurrencePage <= 1 || occurrenceLoading"
              @click="goOccurrencePage(occurrencePage - 1)"
            >上一页</button>
            <span class="pagination-status">第 {{ occurrencePage }} / {{ occurrenceTotalPages }} 页</span>
            <button
              type="button"
              class="pagination-button"
              :disabled="occurrencePage >= occurrenceTotalPages || occurrenceLoading"
              @click="goOccurrencePage(occurrencePage + 1)"
            >下一页</button>
          </nav>
        </template>
      </template>
    </template>

    <BottomSheet
      :open="sheetOpen"
      :title="sheetTitle"
      subtitle="保存只影响尚未发生的期次"
      :close-disabled="isSaving"
      @close="closeSheet"
    >
      <form class="form-stack" @submit.prevent="submit">
        <label v-if="showTypeField" class="form-field">
          <span>计划类型</span>
          <select v-model="form.type" :disabled="editingPlan !== null">
            <option v-for="option in typeFieldOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
          </select>
        </label>
        <label v-if="form.type !== 'fundSip'" class="form-field">
          <span>名称（选填）</span>
          <input
            v-model="form.name"
            type="text"
            maxlength="50"
            :placeholder="form.type === 'salaryIncome' ? '例如：工资、奖金、年终奖' : '留空使用默认名称'"
          />
        </label>
        <label class="form-field">
          <span>每期金额（元）</span>
          <input v-model="form.amount" type="text" inputmode="decimal" placeholder="0.00" />
        </label>

        <label v-if="form.type === 'fundSip'" class="form-field">
          <span>基金及份额类别</span>
          <select
            v-model="form.fundPositionId"
            :disabled="positionsLoading || selectedFundTarget !== null"
            @change="onFundSelectChange"
          >
            <option value="">请选择已持有的基金</option>
            <option v-for="position in fundChoices" :key="position.id" :value="position.id">
              {{ position.name }} · {{ position.shareClass || '未标类别' }} · {{ position.code || '按名称匹配' }}
            </option>
          </select>
          <small v-if="!fundChoices.length">还没有基金持仓，可在下面搜索基金直接建立定投。</small>
        </label>

        <label v-if="form.type === 'fundSip'" class="form-field">
          <span>搜索基金（未持有也可直接定投）</span>
          <div v-if="selectedFundTarget" class="fund-selected">
            <div>
              <strong>{{ selectedFundTarget.name }}</strong>
              <small>
                {{ selectedFundTarget.code }} · {{ selectedFundTarget.shareClass ? `${selectedFundTarget.shareClass} 类` : '未区分类别' }}
                · {{ selectedFundTarget.isQdii ? 'QDII T+2' : '境内 T+1' }}
              </small>
            </div>
            <button type="button" class="text-button" @click="clearFundTarget">更换</button>
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
                @click="chooseFundTarget(result)"
              >
                <span>
                  <strong>{{ result.name }}</strong>
                  <small>{{ result.code }} · {{ result.fundType || '基金' }}</small>
                </span>
                <em v-if="result.isQdii">QDII</em>
              </button>
            </div>
            <small>选中后会在保存计划时自动建立 0 份额持仓，首期扣款后按确认份额入账。</small>
          </template>
        </label>

        <label v-if="form.type === 'fundSip'" class="form-field">
          <span>每期申购费（元）</span>
          <input v-model="form.fee" type="text" inputmode="decimal" placeholder="0.00" />
          <small>按平台实际扣费填写，份额按（扣款金额 − 申购费）÷ 确认净值自动折算。</small>
        </label>

        <p v-if="form.type === 'fundSip' && positionsError" class="form-error" role="alert">
          {{ positionsError }} <LoadingButton class="small-button" :loading="positionsLoading" loading-label="加载中…" @click="loadPositions">重新加载基金</LoadingButton>
        </p>
        <label v-if="form.type === 'fundSip'" class="form-field">
          <span>周期</span>
          <select v-model="form.cycle">
            <option value="monthly">每月</option>
            <option value="weekly">每周</option>
            <option value="tradingDay">交易日</option>
          </select>
        </label>

        <label v-if="form.type === 'fundSip' && form.cycle === 'tradingDay'" class="form-field">
          <span>交易日范围</span>
          <select v-model="form.tradingScope">
            <option value="china">中国交易日</option>
            <option value="threeMarkets">三地交易日（中、美、港均开市）</option>
          </select>
          <small>默认按基金是否 QDII 选择：QDII 用三地交易日，境内基金用中国交易日。</small>
        </label>

        <label v-if="form.type !== 'fundSip' || form.cycle === 'monthly'" class="form-field">
          <span>每月日期</span>
          <input v-model="form.dayOfMonth" type="number" min="1" max="31" />
          <small>当月没有该日期时按当月最后一天执行。</small>
        </label>

        <label v-else-if="form.cycle === 'weekly'" class="form-field">
          <span>星期</span>
          <select v-model="form.dayOfWeek">
            <option v-for="(label, index) in weekdayLabels" :key="label" :value="String(index + 1)">周{{ label }}</option>
          </select>
        </label>

        <p v-if="form.type === 'fundSip' && form.cycle === 'tradingDay'" class="form-note">
          北京时间 15:00 前创建且当天开市时从今天开始；15:00 后或今天休市时从下一个合格交易日开始，所选开始日期休市时顺延。
        </p>
        <div class="form-row">
          <label class="form-field">
            <span>执行时间（中国时区）</span>
            <input v-model="form.time" type="time" />
          </label>
          <label class="form-field">
            <span>开始日期</span>
            <input v-model="form.startDate" type="date" />
          </label>
        </div>

        <p class="form-note">
          保存不会立即扣款。基金定投到期时扣减账本现金并生成待确认买入，需自行核对实际份额和净值，不向销售平台下单。
        </p>
        <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>
        <div class="form-actions">
          <button type="button" class="secondary-button" :disabled="isSaving" @click="closeSheet">取消</button>
          <LoadingButton class="primary-button" type="submit" :loading="isSaving" :disabled="!canSubmit" loading-label="保存中…">
            保存
          </LoadingButton>
        </div>
      </form>
    </BottomSheet>
    <ConfirmSheet
      :open="pendingPlanRemoval !== null"
      title="删除计划"
      :message="pendingPlanRemoval ? `删除计划「${pendingPlanRemoval.name}」？该计划的待处理期次会一并删除，已记账的收支记录会保留。` : ''"
      confirm-label="删除"
      danger
      :busy="isDeletingPlan"
      @confirm="confirmPlanRemoval"
      @cancel="pendingPlanRemoval = null"
    />
    <AppNav />
  </main>
</template>

<style scoped>
.plan-page {
  padding-bottom: calc(97px + env(safe-area-inset-bottom));
}

.plan-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
}

.plan-header h1 {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 700;
  letter-spacing: -0.4px;
}

.header-action {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  width: var(--touch-target);
  height: var(--touch-target);
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: var(--color-bg-surface);
  color: var(--color-brand);
  box-shadow: var(--shadow-card);
  transition: background var(--motion-fast) ease, transform var(--motion-fast) ease;
}

.header-action:hover:not(:disabled) {
  background: var(--color-bg-hover);
}

.header-action:active:not(:disabled) {
  transform: scale(.94);
}

.header-action:disabled {
  color: var(--color-text-disabled);
}

.header-action :deep(.app-icon) {
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

.section-head .section-title {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
  letter-spacing: -0.3px;
}

.occurrence-head {
  scroll-margin-top: 12px;
}

.section-toggle {
  width: 100%;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  text-align: left;
}

.section-toggle-meta {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.section-chevron {
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  line-height: 1;
  transition: transform .15s ease;
}

.section-chevron.open {
  transform: rotate(180deg);
}

.meta {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.plan-group {
  --group-accent: var(--color-brand);
}

.group-fund {
  --group-accent: var(--color-chart-investment);
}

.group-provident {
  --group-accent: var(--color-chart-property);
}

.group-income {
  --group-accent: var(--color-finance-income);
}

.plan-group + .plan-group {
  margin-top: 18px;
}

.plan-group-head {
  display: flex;
  align-items: center;
  gap: 10px;
}

.group-toggle {
  display: flex;
  flex: 1;
  align-items: center;
  gap: 10px;
  min-width: 0;
  min-height: var(--touch-target);
  padding: 0;
  border: 0;
  background: transparent;
  color: inherit;
  text-align: left;
}

.group-chevron {
  flex: 0 0 auto;
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  line-height: 1;
  transition: transform .15s ease;
}

.group-chevron.open {
  transform: rotate(180deg);
}

.plan-group-body {
  margin-top: 9px;
}

.plan-group-body > .plan-card + .plan-card {
  margin-top: 9px;
}

.group-icon {
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  width: 30px;
  height: 30px;
  border-radius: var(--radius-control);
  background: color-mix(in srgb, var(--group-accent) 12%, var(--color-bg-surface));
  color: var(--group-accent);
}

.group-icon .app-icon {
  width: 17px;
  height: 17px;
}

.group-heading {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
}

.group-title {
  font-size: var(--text-subhead);
  font-weight: 700;
  letter-spacing: -0.2px;
}

.group-summary {
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.group-add {
  flex: 0 0 auto;
  min-height: 34px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: var(--text-caption);
  font-weight: 700;
}

.group-add:hover {
  background: var(--color-brand-soft-hover);
}

.fund-plan-list {
  display: flex;
  flex-direction: column;
  gap: 9px;
}

.text-button {
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-caption);
}

.plan-card,
.occurrence-card {
  padding: 20px;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.plan-card header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.plan-card header > div {
  min-width: 0;
}

.plan-card header strong {
  display: block;
  color: var(--color-text-primary);
  font-size: var(--text-subhead);
  font-weight: 700;
  letter-spacing: -0.2px;
}

.plan-card header small {
  display: block;
  margin-top: 5px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.plan-amount {
  color: var(--color-text-primary);
  font-size: 1.125rem;
  font-weight: 700;
  letter-spacing: -0.03em;
  font-variant-numeric: tabular-nums;
}

.plan-meta {
  margin: 10px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.6;
}

.plan-card footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-top: 14px;
  padding-top: 10px;
  border-top: 1px solid var(--color-border-subtle);
}

.plan-foot-note {
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.plan-foot-actions {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  gap: 2px;
}

.action-sep {
  width: 1px;
  height: 11px;
  background: var(--color-border-subtle);
}

.link-button {
  display: inline-flex;
  align-items: center;
  min-height: var(--touch-target);
  padding: 0 8px;
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-caption);
  font-weight: 600;
  white-space: nowrap;
}

.link-button:hover:not(:disabled) {
  background: var(--color-brand-soft);
}

.link-button:disabled {
  color: var(--color-text-disabled);
}

.link-button.danger-text {
  color: var(--color-danger);
}

.small-button {
  min-height: 40px;
  padding: 0 12px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: var(--text-caption);
  font-weight: 700;
  white-space: nowrap;
}

.small-button:hover:not(:disabled) {
  background: var(--color-brand-soft-hover);
}

.small-button:disabled {
  background: var(--color-bg-disabled);
  color: var(--color-text-disabled);
}

.fund-details > summary {
  display: flex;
  align-items: center;
  gap: 10px;
  padding-bottom: 4px;
  list-style: none;
  cursor: pointer;
}

.fund-details > summary::-webkit-details-marker {
  display: none;
}

.fund-details > summary::after {
  flex: 0 0 auto;
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  content: '⌄';
  transition: transform .15s ease;
}

.fund-details[open] > summary::after {
  transform: rotate(180deg);
}

.fund-summary-copy {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.fund-summary-copy strong {
  overflow: hidden;
  font-size: var(--text-subhead);
  font-weight: 700;
  letter-spacing: -0.2px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.fund-summary-copy small {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.fund-summary-amount {
  display: flex;
  flex: 0 0 auto;
  flex-direction: column;
  align-items: flex-end;
  gap: 3px;
}

.fund-summary-amount b {
  font-size: 1.125rem;
  font-weight: 700;
  letter-spacing: -0.03em;
  font-variant-numeric: tabular-nums;
}

.fund-summary-amount small {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.paused-label {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.fund-plan-rows {
  margin-top: 12px;
  padding-top: 4px;
  border-top: 1px solid var(--color-border-subtle);
}

.fund-plan-row {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: start;
  gap: 10px;
  padding: 12px 0;
  border-bottom: 1px solid var(--color-border-subtle);
}

.fund-plan-row:last-child {
  padding-bottom: 0;
  border-bottom: 0;
}

.fund-plan-row.paused {
  opacity: .58;
}

.share-badge {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  box-sizing: border-box;
  min-width: 36px;
  min-height: 24px;
  padding: 0 8px;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: var(--text-caption);
  font-weight: 700;
  white-space: nowrap;
}

.group-fund .share-badge {
  background: color-mix(in srgb, var(--color-chart-investment) 12%, var(--color-bg-surface));
  color: var(--color-chart-investment);
}

.fund-row-main {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.fund-row-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.fund-row-head b {
  font-size: 0.8125rem;
  font-weight: 700;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.fund-row-main small {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.fund-row-actions {
  display: flex;
  flex: 0 0 auto;
  align-items: center;
  gap: 2px;
}

.occurrence-list {
  display: flex;
  flex-direction: column;
  gap: 9px;
  transition: opacity .15s ease;
}

.occurrence-list.is-loading {
  opacity: .55;
}

.occurrence-state {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 8px;
  padding: 22px 0 18px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.occurrence-pagination {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 12px;
  margin-top: 12px;
}

.pagination-button {
  min-width: 76px;
  min-height: 44px;
  padding: 0 16px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-surface);
  color: var(--color-brand);
  font-size: var(--text-caption);
  font-weight: 600;
  box-shadow: var(--shadow-card);
}

.pagination-button:disabled {
  background: var(--color-bg-disabled);
  color: var(--color-text-disabled);
  box-shadow: none;
}

.pagination-status {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-variant-numeric: tabular-nums;
}

.occurrence-card {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: flex-start;
  padding: 16px 18px;
}

.occurrence-card strong {
  display: block;
  color: var(--color-text-primary);
  font-size: var(--text-footnote);
  font-weight: 700;
}

.occurrence-card small {
  display: block;
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.occurrence-card .occurrence-error {
  color: var(--color-finance-loss);
}

.occurrence-actions {
  display: flex;
  flex: 0 0 auto;
  gap: 6px;
}

.status-completed {
  color: var(--color-success);
}

.status-waiting,
.status-failed {
  color: var(--color-finance-loss);
}

.status-skipped {
  color: var(--color-text-secondary);
}

.summary-note {
  margin: 14px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
  text-align: center;
}

.plan-page :deep(.calendar-card) {
  border: 0;
  box-shadow: var(--shadow-card);
}

.fund-selected {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  min-height: var(--touch-target);
  padding: 10px 14px;
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
  border-radius: var(--radius-card);
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
</style>
