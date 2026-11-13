<script setup lang="ts">
import { navigateBack } from '../utils/navigation'
import Decimal from 'decimal.js'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { arriveGoldSell, buyGold, fetchGoldOverview, refreshGoldQuote, saveGoldHolding, sellGold } from '../api/gold'
import { ApiError, getApiErrorMessage } from '../api/client'
import { fetchLedger, updateAssetCoverage } from '../api/ledger'
import { toast } from '../composables/useMessage'
import AppNav from '../components/AppNav.vue'
import AnimatedMoney from '../components/AnimatedMoney.vue'
import BackButton from '../components/BackButton.vue'
import LoadingButton from '../components/LoadingButton.vue'
import LoadingState from '../components/LoadingState.vue'
import InvestmentActivityList, { type InvestmentActivityRow } from '../components/InvestmentActivityList.vue'
import BottomSheet from '../components/BottomSheet.vue'
import { useUnsavedSheet } from '../composables/useUnsavedSheet'
import ConfirmSheet from '../components/ConfirmSheet.vue'
import {
  amountTone,
  formatBusinessDate,
  formatBusinessTime,
  formatMoney,
  formatQuantity,
  formatSignedMoney,
  formatUnitPrice,
  parseAmount,
  parseNonNegativeQuantity,
  parsePrice,
  parseQuantity,
  parseUnitPrice,
  todayInBusinessTimeZone
} from '../utils/format'
import { createRequestId } from '../utils/request'
import type { GoldActivity, GoldOverview, PendingGoldArrival } from '../types/gold'

type SheetKind = 'buy' | 'sell' | 'sellArrival' | 'holding' | 'detail'

const router = useRouter()
const today = todayInBusinessTimeZone()
const overview = ref<GoldOverview | null>(null)
const isLoading = ref(false)
const isRefreshing = ref(false)
const errorMessage = ref('')
const sheet = ref<SheetKind | null>(null)
const requestId = ref(createRequestId())
const isSaving = ref(false)
const expectedVersion = ref(0)
const formError = ref('')
const arrivingPendingId = ref<string | null>(null)
const form = reactive({
  grams: '',
  amount: '',
  netAmount: '',
  costPrice: '',
  quote: '',
  businessDate: today,
  note: ''
})

const { discardOpen, requestClose, resolveDiscard } = useUnsavedSheet(
  () => requestId.value,
  () => sheet.value !== null && sheet.value !== 'detail',
  () => isSaving.value,
  () => ({ ...form }),
  closeSheet
)

const quoteLabel = computed(() => {
  switch (overview.value?.quoteSource) {
    case 'sge_delayed': return '上金所延时参考价'
    case 'eastmoney_sge': return 'Au99.99 现货参考价'
    case 'icbc': return '工行积存金报价'
    default: return '报价'
  }
})

const hasHolding = computed(() => (overview.value?.grams ?? 0) > 0)

// 总收益按持有收益加已实现收益展示，口径与基金累计收益一致。
const totalProfit = computed<number | null>(() => {
  const data = overview.value

  if (!data || !data.hasEffectiveQuote) {
    return null
  }

  return data.holdingProfit + data.realizedProfit
})

const totalRate = computed<number | null>(() => {
  const data = overview.value

  if (!data || !data.hasEffectiveQuote || data.cost <= 0) {
    return null
  }

  return (data.holdingProfit + data.realizedProfit) / data.cost * 100
})

const selectedPendingArrival = computed<PendingGoldArrival | null>(() =>
  overview.value?.pendingArrivals.find((pending) => pending.transactionId === arrivingPendingId.value) ?? null)

const sheetTitle = computed(() => {
  switch (sheet.value) {
    case 'detail':
      return '积存金'
    case 'buy':
      return '记录积存金买入'
    case 'sell':
      return '记录积存金卖出'
    case 'sellArrival':
      return '登记积存金卖到账'
    case 'holding':
      return overview.value?.isConfigured ? '核对持仓' : '录入积存金持仓'
    default:
      return ''
  }
})
const sheetSubtitle = computed(() => {
  switch (sheet.value) {
    case 'detail':
      return overview.value?.hasEffectiveQuote
        ? `积存金 · ${formatMoney(overview.value.quote)} 元/克`
        : '积存金 · 行情待补'
    case 'buy':
      return '从统一现金扣款，增加克数与成本'
    case 'sell':
      return '按克数和估值报价计算到账，直接转入现金'
    case 'sellArrival':
      return '待到账转入现金，不重复计算盈亏'
    case 'holding':
      return '用于已有持仓或更正，不扣现金'
    default:
      return ''
  }
})
const sheetNote = computed(() => {
  switch (sheet.value) {
    case 'buy':
      return '报价自动使用当前行情；行情暂不可用时按实际扣款和克数估算。'
    case 'sell':
      return '按克数 × 金估值报价计算到账金额，卖出默认直接到账，不再单独确认。'
    case 'sellArrival':
      return '到账金额必须与待到账金额一致，不再次确认盈亏；如有差额，请先更正卖出记录。'
    case 'holding':
      return '报价自动使用当前行情；成本按元/克填写，克数为 0 时持仓成本也为 0。此操作不改变现金。'
    default:
      return ''
  }
})
const canSubmit = computed(() => {
  if (isSaving.value) {
    return false
  }

  switch (sheet.value) {
    case 'buy':
      return parseQuantity(form.grams) !== null && (parseAmount(form.amount) ?? 0) > 0
    case 'sell':
      return parseQuantity(form.grams) !== null && parsePrice(form.quote) !== null
    case 'sellArrival':
      return arrivingPendingId.value !== null && (parseAmount(form.netAmount) ?? 0) > 0
    case 'holding':
      return parseNonNegativeQuantity(form.grams) !== null && parseUnitPrice(form.costPrice) !== null
    default:
      return false
  }
})
const sellProceeds = computed<number | null>(() => {
  const grams = parseQuantity(form.grams)
  const quote = parsePrice(form.quote)

  if (grams === null || quote === null) {
    return null
  }

  return new Decimal(grams).times(quote).toDecimalPlaces(2, Decimal.ROUND_HALF_UP).toNumber()
})
const holdingCostTotal = computed<number | null>(() => {
  const grams = parseNonNegativeQuantity(form.grams)
  const costPrice = parseUnitPrice(form.costPrice)

  if (grams === null || costPrice === null || grams <= 0) {
    return null
  }

  return new Decimal(grams).times(costPrice).toDecimalPlaces(2, Decimal.ROUND_HALF_UP).toNumber()
})
const parseGramInput = computed<number>(() => parseNonNegativeQuantity(form.grams) ?? 0)

onMounted(async () => {
  await loadOverview()
  await refreshQuoteOnEnter()
})

async function refreshQuoteOnEnter(): Promise<void> {
  if (!overview.value || isRefreshing.value) {
    return
  }

  // 自动刷新和手动刷新共用同一个进行中状态，避免两次请求同时改账本版本。
  isRefreshing.value = true

  try {
    try {
      // 后台定时刷新为每小时一次；进入贵金属页面时再取一次最新报价。
      await refreshGoldQuote()
    } catch {
      // 失败原因由服务端记录在持仓状态中，下面重新读取以显示失败提示。
    }
    await loadOverview()
  } finally {
    isRefreshing.value = false
  }
}

async function loadOverview(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''

  try {
    overview.value = await fetchGoldOverview()
  } catch (error) {
    overview.value = null

    if (error instanceof ApiError && error.status === 404) {
      await router.replace({ name: 'setup' })
      return
    }

    errorMessage.value = getApiErrorMessage(error, '贵金属数据加载失败，请重试。')
  } finally {
    isLoading.value = false
  }
}

async function refreshQuote(): Promise<void> {
  if (isRefreshing.value) {
    return
  }

  isRefreshing.value = true
  errorMessage.value = ''

  try {
    const result = await refreshGoldQuote()
    await loadOverview()
    if (!overview.value) return

    const summary = result.updated
      ? `报价已更新：¥${formatMoney(result.quote)} / 克（${formatBusinessDate(result.quoteDate)}）。`
      : `报价已是最新：¥${formatMoney(result.quote)} / 克。`
    if (result.updated) toast.success(summary)
    else toast.info(summary)
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      await loadOverview()
      errorMessage.value = '报价正在更新，请稍后查看最新报价。'
      return
    }

    errorMessage.value = '报价刷新失败，请稍后重试。'
  } finally {
    isRefreshing.value = false
  }
}

function openSheet(kind: SheetKind): void {
  expectedVersion.value = overview.value?.version ?? 0
  sheet.value = kind
  arrivingPendingId.value = null
  requestId.value = createRequestId()
  formError.value = ''
  form.note = ''
  form.businessDate = today
  form.quote = overview.value?.hasEffectiveQuote ? String(overview.value.quote) : ''
  form.grams = ''
  form.amount = ''
  form.netAmount = ''
  form.costPrice = ''

  if (kind === 'holding' && overview.value?.isConfigured) {
    form.grams = String(overview.value.grams)
    // 成本按元/克编辑；用总额除以克数还原单价，避免四舍五入后再乘回去产生总额漂移。
    form.costPrice = overview.value.grams > 0
      ? String(Number((overview.value.cost / overview.value.grams).toFixed(4)))
      : ''
  }
}

function openDetail(): void {
  openSheet('detail')
}

function openArrivalSheet(pending: PendingGoldArrival): void {
  openSheet('sellArrival')
  arrivingPendingId.value = pending.transactionId
  form.netAmount = String(pending.amount)
  form.note = pending.note ?? ''
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
    if (sheet.value === 'buy') {
      await submitBuy()
    } else if (sheet.value === 'sell') {
      await submitSell()
    } else if (sheet.value === 'sellArrival') {
      await submitSellArrival()
    } else if (sheet.value === 'holding') {
      await submitHolding()
    }
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      await loadOverview()
      expectedVersion.value = overview.value?.version ?? expectedVersion.value
      requestId.value = createRequestId()
    }
    formError.value = getApiErrorMessage(error, '保存失败，请重试。')
  } finally {
    isSaving.value = false
  }
}

async function submitBuy(): Promise<void> {
  const grams = parseQuantity(form.grams)
  const amount = parseAmount(form.amount)

  if (grams === null || amount === null || amount <= 0) {
    formError.value = '请填写大于 0 的克数和实际扣款金额。'
    return
  }

  const quote = overview.value?.hasEffectiveQuote && overview.value.quote > 0
    ? overview.value.quote
    : new Decimal(amount).div(grams).toDecimalPlaces(4, Decimal.ROUND_HALF_UP).toNumber()
  const quoteDate = overview.value?.hasEffectiveQuote && overview.value.quoteDate
    ? overview.value.quoteDate
    : form.businessDate

  isSaving.value = true
  const result = await buyGold({
    grams,
    amount,
    quote,
    quoteDate,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success(`已买入 ${formatQuantity(result.activity.grams)} 克，当前现金 ¥${formatMoney(result.cashBalance)}。`)
  sheet.value = null
  await loadOverview()
}

async function submitSell(): Promise<void> {
  const grams = parseQuantity(form.grams)
  const quote = parsePrice(form.quote)
  const netAmount = sellProceeds.value

  if (grams === null || quote === null || netAmount === null || netAmount <= 0) {
    formError.value = '请填写大于 0 的卖出克数和金估值报价。'
    return
  }

  isSaving.value = true
  const result = await sellGold({
    grams,
    netAmount,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success(`已卖出 ${formatQuantity(result.activity.grams)} 克，当前现金 ¥${formatMoney(result.cashBalance)}。`)
  sheet.value = null
  await loadOverview()
}

async function submitSellArrival(): Promise<void> {
  const pending = selectedPendingArrival.value
  const amount = parseAmount(form.netAmount)

  if (!pending) {
    formError.value = '积存金卖出待到账记录不存在，请刷新后重试。'
    return
  }

  if (amount === null || amount <= 0) {
    formError.value = '请填写有效的到账金额。'
    return
  }

  if (amount !== pending.amount) {
    formError.value = '到账金额必须与待到账金额一致；如有差额，请先更正卖出记录。'
    return
  }

  if (form.businessDate < pending.businessDate) {
    formError.value = '到账日期不能早于卖出日期。'
    return
  }

  isSaving.value = true
  const result = await arriveGoldSell(pending.transactionId, {
    amount,
    businessDate: form.businessDate,
    note: form.note.trim(),
    requestId: requestId.value
  })
  toast.success(`积存金卖到账 ¥${formatMoney(result.activity.amount)}，当前现金 ¥${formatMoney(result.cashBalance)}。`)
  sheet.value = null
  await loadOverview()
}

async function submitHolding(): Promise<void> {
  const grams = parseNonNegativeQuantity(form.grams)
  const costPrice = parseUnitPrice(form.costPrice)

  if (grams === null || costPrice === null) {
    formError.value = '请填写有效的克数和成本均价。'
    return
  }

  const cost = grams === 0
    ? 0
    : new Decimal(grams).times(costPrice).toDecimalPlaces(2, Decimal.ROUND_HALF_UP).toNumber()

  if (!overview.value?.hasEffectiveQuote || overview.value.quote <= 0 || !overview.value.quoteDate) {
    formError.value = '行情暂不可用，请稍后自动获取报价后再保存。'
    return
  }

  isSaving.value = true
  await saveGoldHolding({
    expectedVersion: expectedVersion.value,
    grams,
    cost,
    quote: overview.value.quote,
    quoteDate: overview.value.quoteDate,
    note: form.note.trim(),
    holdingDate: form.businessDate,
    requestId: requestId.value
  })
  toast.success('积存金持仓已保存。')
  sheet.value = null
  await loadOverview()
}

async function markNotHeld(): Promise<void> {
  try {
    isSaving.value = true
    const ledger = await fetchLedger()
    await updateAssetCoverage({
      fundNotHeld: ledger.fundNotHeld,
      providentFundNotHeld: ledger.providentFundNotHeld,
      goldNotHeld: true,
      stockNotHeld: ledger.stockNotHeld
    })
    await router.push({ name: 'assets' })
  } catch (error) {
    errorMessage.value = getApiErrorMessage(error, '标记失败，请稍后重试。')
  } finally {
    isSaving.value = false
  }
}

function formatPercent(value: number | null): string {
  if (value === null) {
    return '—'
  }

  const sign = value > 0 ? '+' : value < 0 ? '−' : ''

  return `${sign}${Math.abs(value).toFixed(2)}%`
}

function profitText(value: number | null): string {
  return value === null ? '行情待补' : formatSignedMoney(value)
}

function activityLabel(activity: GoldActivity): string {
  switch (activity.type) {
    case 'buy':
      return '买入'
    case 'sell':
      return '卖出'
    case 'sellPending':
      return '待到账'
    case 'sellArrival':
      return '到账'
    case 'correction':
      return '核对'
    default:
      return '行情'
  }
}

function activityStatus(activity: GoldActivity): string {
  switch (activity.type) {
    case 'sellPending':
      return '待到账'
    case 'sellArrival':
      return '已到账'
    case 'correction':
      return '已核对'
    case 'quote':
      return '仅更新估值'
    default:
      return '已成交'
  }
}

function activityTime(activity: GoldActivity): string {
  return [formatBusinessDate(activity.businessDate), formatBusinessTime(activity.createdAt)]
    .filter(Boolean)
    .join(' ')
}

function activityCostPerGram(activity: GoldActivity): number | null {
  if (activity.grams === null || activity.grams <= 0) {
    return null
  }

  return new Decimal(activity.amount)
    .div(activity.grams)
    .toDecimalPlaces(3, Decimal.ROUND_HALF_UP)
    .toNumber()
}

function activityMeta(activity: GoldActivity): string {
  if (activity.type === 'sellArrival') {
    return `到账 ${formatMoney(activity.amount)}`
  }

  if (activity.type === 'buy' || activity.type === 'sell') {
    const price = activity.quote === null
      ? ''
      : `${activity.type === 'buy' ? '成本' : '成交价'} ${formatUnitPrice(activity.quote)}/克`
    return [`${formatQuantity(activity.grams)} 克`, price].filter(Boolean).join(' · ')
  }

  if (activity.type === 'sellPending') {
    return `${formatQuantity(activity.grams)} 克 · 待到账 ${formatMoney(activity.amount)}`
  }

  if (activity.type === 'quote') {
    return `报价 ${formatUnitPrice(activity.quote)}/克`
  }

  const cost = activityCostPerGram(activity)
  return [
    `${formatQuantity(activity.grams)} 克`,
    cost === null ? `成本 ${formatMoney(activity.amount)}` : `成本 ${formatUnitPrice(cost)}/克`
  ].filter(Boolean).join(' · ')
}

function activityAmount(activity: GoldActivity): string {
  if (activity.type === 'buy' || activity.type === 'sell' || activity.type === 'sellArrival') {
    return `${formatSignedMoney(activity.cashEffect)}元`
  }

  if (activity.type === 'quote') {
    return `${formatUnitPrice(activity.quote)}元/克`
  }

  return `${formatMoney(activity.amount)}元`
}

function activityAmountTone(activity: GoldActivity): string {
  if (activity.cashEffect > 0) {
    return 'income'
  }

  return activity.cashEffect < 0 ? 'transfer' : ''
}

function toActivityRow(activity: GoldActivity): InvestmentActivityRow {
  return {
    key: activity.id,
    label: activityLabel(activity),
    labelTone: activity.type === 'buy' ? 'buy' : 'neutral',
    title: '积存金',
    meta: activityMeta(activity),
    time: activityTime(activity),
    amount: activityAmount(activity),
    amountTone: activityAmountTone(activity),
    status: activityStatus(activity),
    statusTone: activity.type === 'sellPending' ? 'pending' : 'done'
  }
}

const activityRows = computed(() => (overview.value?.activities ?? []).map(toActivityRow))

function goBack(): void {
  navigateBack(router)
}
</script>

<template>
  <main class="page gold-page with-nav">
    <header class="gold-header">
      <BackButton @back="goBack" />
      <h1>贵金属</h1>
    </header>

    <LoadingState v-if="isLoading && !overview" variant="hero" label="正在读取贵金属" />
    <LoadingState v-if="isLoading && !overview" variant="list" :rows="3" label="正在读取贵金属明细" />

    <section v-else-if="errorMessage && !overview" class="state">
      <p>{{ errorMessage }}</p>
      <LoadingButton class="secondary-button" :loading="isLoading" loading-label="加载中…" @click="loadOverview">重新加载</LoadingButton>
    </section>

    <template v-else-if="overview">
      <section class="gold-hero investment-hero">
        <p class="hero-label">
          贵金属资产
          <span class="hero-tag">积存金 · 人民币</span>
        </p>
        <h2 class="hero-balance">
          <template v-if="overview.hasEffectiveQuote"><span class="hero-currency">¥</span><AnimatedMoney :value="overview.value" prefix="" /></template>
          <template v-else>行情待补</template>
        </h2>
        <p class="hero-meta">
          持有 {{ formatQuantity(overview.grams) }} 克 ·
          {{ quoteLabel }} {{ overview.hasEffectiveQuote ? formatMoney(overview.quote) : '行情待补' }} 元/克
          <template v-if="overview.hasEffectiveQuote && overview.quoteDate"> · 行情 {{ formatBusinessDate(overview.quoteDate) }}</template>
          <template v-if="overview.pendingArrivalAmount > 0"> · 含待到账 ¥{{ formatMoney(overview.pendingArrivalAmount) }}</template>
        </p>
        <div class="hero-metrics">
          <div>
            <span>总收益</span>
            <b :class="amountTone(totalProfit)">{{ profitText(totalProfit) }}</b>
          </div>
          <div>
            <span>总收益率</span>
            <b :class="amountTone(totalRate)">{{ formatPercent(totalRate) }}</b>
          </div>
          <div>
            <span>成本均价</span>
            <b>{{ overview.averageCost === null ? '—' : `¥${formatMoney(overview.averageCost)} / 克` }}</b>
          </div>
        </div>
        <p class="hero-foot">可用现金 ¥{{ formatMoney(overview.cashBalance) }}</p>
      </section>

      <div v-if="overview.isConfigured" class="gold-actions">
        <button type="button" class="action-primary" @click="openSheet('buy')">买入</button>
        <button type="button" class="action-secondary" @click="openSheet('holding')">录入 / 核对</button>
        <LoadingButton class="action-secondary" :loading="isRefreshing" loading-label="刷新中…" @click="refreshQuote">
          刷新报价
        </LoadingButton>
      </div>

      <p v-if="errorMessage" class="form-error" role="alert">{{ errorMessage }}</p>

      <section v-if="!overview.isConfigured" class="empty-stock">
        <p>还没有贵金属持仓。可以录入已有积存金持仓，也可以直接记录买入。</p>
        <div class="empty-actions">
          <button type="button" class="primary-button" @click="openSheet('buy')">买入第一笔积存金</button>
          <button type="button" class="secondary-button" @click="openSheet('holding')">录入已有持仓</button>
        </div>
        <LoadingButton class="text-button empty-link" :loading="isSaving" loading-label="处理中…" @click="markNotHeld">
          没有贵金属，标记为不持有
        </LoadingButton>
      </section>

      <template v-if="overview.pendingArrivals.length">
        <div class="section-head">
          <h2>卖出待到账</h2>
          <span class="meta">{{ overview.pendingArrivals.length }} 笔 · ¥{{ formatMoney(overview.pendingArrivalAmount) }}</span>
        </div>
        <section class="pending-list">
          <article v-for="pending in overview.pendingArrivals" :key="pending.transactionId" class="pending-card">
            <div class="pending-copy">
              <strong>{{ formatQuantity(pending.grams) }} 克</strong>
              <small>
                {{ formatBusinessDate(pending.businessDate) }}
                <template v-if="pending.note"> · {{ pending.note }}</template>
              </small>
            </div>
            <div class="pending-side">
              <b>¥{{ formatMoney(pending.amount) }}</b>
              <button type="button" class="confirm-button" @click="openArrivalSheet(pending)">登记到账</button>
            </div>
          </article>
        </section>
      </template>

      <section v-if="hasHolding" class="market-group">
        <div class="section-head">
          <h2>持仓</h2>
          <span class="meta">1 项 · ¥{{ formatMoney(overview.value) }}</span>
        </div>
        <div class="position-list">
          <button type="button" class="position-row" @click="openDetail">
            <span class="position-main">
              <span class="position-name">
                <strong>积存金</strong>
              </span>
              <small class="position-code-line">
                {{ formatQuantity(overview.grams) }} 克 · 成本 {{ overview.averageCost === null ? '—' : `¥${formatMoney(overview.averageCost)} / 克` }}
              </small>
              <small>
                报价 {{ overview.hasEffectiveQuote ? `${formatMoney(overview.quote)} 元/克` : '行情待补' }}
                <template v-if="overview.quoteDate"> · {{ formatBusinessDate(overview.quoteDate) }}</template>
              </small>
            </span>
            <span class="position-side">
              <b>¥{{ formatMoney(overview.value) }}</b>
              <small :class="amountTone(totalProfit)">
                {{ profitText(totalProfit) }}
                <em v-if="totalRate !== null" class="position-percent">{{ formatPercent(totalRate) }}</em>
              </small>
            </span>
          </button>
        </div>
      </section>

      <details v-else-if="overview.isConfigured" class="closed-block">
        <summary>已清仓</summary>
        <div class="position-list closed-list">
          <button type="button" class="position-row" @click="openDetail">
            <span class="position-main">
              <span class="position-name">
                <strong>积存金</strong>
              </span>
              <small>已清仓 · 行情 {{ overview.quoteDate ? formatBusinessDate(overview.quoteDate) : '—' }}</small>
            </span>
            <span class="position-side">
              <b :class="amountTone(overview.realizedProfit)">{{ formatSignedMoney(overview.realizedProfit) }}</b>
              <small>已实现收益</small>
            </span>
          </button>
        </div>
      </details>

      <p v-if="!overview.historyConfirmed || !overview.hasEffectiveQuote" class="hint">持仓日期或行情依据不足，当前估值可能不完整。</p>
      <p v-if="overview.lastQuoteError" class="hint">报价暂时更新失败，当前显示上次有效报价，请稍后重试。</p>

      <template v-if="activityRows.length">
        <div class="section-head activities-head">
          <h2>交易记录</h2>
          <span class="meta">按业务日期</span>
        </div>

        <InvestmentActivityList :rows="activityRows" />
      </template>

      <p v-else-if="overview.isConfigured" class="hint">还没有贵金属交易记录，买入或核对持仓后会出现在这里。</p>
    </template>

    <BottomSheet
      :open="sheet === 'detail'"
      :title="sheetTitle"
      :subtitle="sheetSubtitle"
      @close="requestClose"
    >
      <template v-if="overview">
        <div class="detail-metrics">
          <div>
            <span>市值</span>
            <b>¥{{ formatMoney(overview.value) }}</b>
          </div>
          <div>
            <span>总收益</span>
            <b :class="amountTone(totalProfit)">{{ profitText(totalProfit) }}</b>
          </div>
          <div>
            <span>总收益率</span>
            <b :class="amountTone(totalRate)">{{ formatPercent(totalRate) }}</b>
          </div>
          <div>
            <span>已实现收益</span>
            <b :class="amountTone(overview.realizedProfit)">{{ formatSignedMoney(overview.realizedProfit) }}</b>
          </div>
        </div>
        <div class="detail-lines">
          <div><span>持有数量</span><b>{{ formatQuantity(overview.grams) }} 克</b></div>
          <div><span>持仓成本</span><b>¥{{ formatMoney(overview.cost) }}</b></div>
          <div><span>成本均价</span><b>{{ overview.averageCost === null ? '—' : `¥${formatMoney(overview.averageCost)} / 克` }}</b></div>
          <div><span>估值报价</span><b>{{ overview.hasEffectiveQuote ? `¥${formatMoney(overview.quote)} / 克` : '行情待补' }}</b></div>
          <div><span>行情日期</span><b>{{ overview.hasEffectiveQuote && overview.quoteDate ? formatBusinessDate(overview.quoteDate) : '—' }}</b></div>
          <div v-if="overview.pendingArrivalAmount > 0"><span>卖出待到账</span><b>¥{{ formatMoney(overview.pendingArrivalAmount) }}</b></div>
        </div>
        <div class="detail-actions">
          <button type="button" class="action-primary" @click="openSheet('buy')">买入</button>
          <button type="button" class="action-secondary" :disabled="!hasHolding" @click="openSheet('sell')">卖出</button>
          <button type="button" class="action-secondary" @click="openSheet('holding')">核对持仓</button>
        </div>
        <p v-if="overview.lastQuoteError" class="hint">最近报价刷新失败：{{ overview.lastQuoteError }}</p>
        <div class="section-head"><h2>积存金记录</h2></div>
        <InvestmentActivityList v-if="activityRows.length" :rows="activityRows" />
        <p v-else class="hint">还没有积存金交易记录。</p>
      </template>
    </BottomSheet>

    <BottomSheet
      :open="sheet === 'buy' || sheet === 'sell' || sheet === 'sellArrival' || sheet === 'holding'"
      :title="sheetTitle"
      :subtitle="sheetSubtitle"
      :close-disabled="isSaving"
      @close="requestClose"
    >
      <div class="sheet-summary">
        <div class="summary-line">
          <span>可用现金</span>
          <b>¥{{ formatMoney(overview?.cashBalance) }}</b>
        </div>
        <div class="summary-line">
          <span>当前持有</span>
          <b>{{ formatQuantity(overview?.grams) }} 克</b>
        </div>
        <div v-if="selectedPendingArrival" class="summary-line">
          <span>卖出待到账</span>
          <b>¥{{ formatMoney(selectedPendingArrival.amount) }}</b>
        </div>
      </div>

      <form class="form-stack" @submit.prevent="submit">
        <template v-if="sheet === 'buy'">
          <label class="form-field">
            <span>买入克数</span>
            <input
              v-model="form.grams"
              type="text"
              inputmode="decimal"
              autocomplete="off"
              placeholder="0.0000"
              @input="formError = ''"
            />
          </label>
          <label class="form-field">
            <span>实际扣款金额（元，含费用）</span>
            <input
              v-model="form.amount"
              type="text"
              inputmode="decimal"
              autocomplete="off"
              placeholder="0.00"
              @input="formError = ''"
            />
          </label>
        </template>

        <template v-else-if="sheet === 'sell'">
          <label class="form-field">
            <span>卖出克数</span>
            <input
              v-model="form.grams"
              type="text"
              inputmode="decimal"
              autocomplete="off"
              placeholder="0.0000"
              @input="formError = ''"
            />
          </label>
          <label class="form-field">
            <span>金估值报价（元/克）</span>
            <input
              v-model="form.quote"
              type="text"
              inputmode="decimal"
              autocomplete="off"
              placeholder="0.0000"
              @input="formError = ''"
            />
          </label>
          <p class="form-note sell-preview">
            预计到账 <b>{{ sellProceeds === null ? '—' : `¥${formatMoney(sellProceeds)}` }}</b>
            <span>按克数 × 报价计算，默认直接到账</span>
          </p>
        </template>

        <template v-else-if="sheet === 'sellArrival'">
          <label class="form-field">
            <span>到账金额（元）</span>
            <input
              v-model="form.netAmount"
              type="text"
              inputmode="decimal"
              autocomplete="off"
              placeholder="0.00"
              @input="formError = ''"
            />
            <small>必须与待到账金额一致；如有差额，请先更正卖出记录。</small>
          </label>
        </template>

        <template v-else-if="sheet === 'holding'">
          <label class="form-field">
            <span>实际持有克数</span>
            <input
              v-model="form.grams"
              type="text"
              inputmode="decimal"
              autocomplete="off"
              placeholder="0.0000"
              @input="formError = ''"
            />
          </label>
          <label class="form-field">
            <span>成本均价（元 / 克）</span>
            <input
              v-model="form.costPrice"
              type="text"
              inputmode="decimal"
              autocomplete="off"
              placeholder="0.00"
              @input="formError = ''"
            />
            <small v-if="holdingCostTotal !== null">按 {{ formatQuantity(parseGramInput) }} 克计算，持仓成本合计 ¥{{ formatMoney(holdingCostTotal) }}</small>
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
.gold-page {
  padding-bottom: calc(97px + env(safe-area-inset-bottom));
}

.gold-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
}

.gold-header h1 {
  margin: 0;
  font-size: var(--text-title3);
  font-weight: 700;
  letter-spacing: -0.4px;
}

.position-percent {
  margin-left: 4px;
  font-size: var(--text-caption);
  font-style: normal;
  font-weight: 500;
  opacity: 0.85;
}

.gold-actions {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin-top: 14px;
}

.gold-actions button,
.detail-actions button {
  min-height: 48px;
  border-radius: var(--radius-pill);
  font-size: 0.875rem;
  font-weight: 600;
}

.action-primary {
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand);
  color: var(--color-on-brand);
}

.action-primary:hover {
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

.action-secondary:disabled {
  background: var(--color-bg-disabled);
  color: var(--color-text-disabled);
}

.form-error {
  margin: 12px 0 0;
  padding: 9px 11px;
  border-radius: var(--radius-tag);
  background: var(--color-error-bg);
  color: var(--color-error);
  font-size: var(--text-caption);
  line-height: 1.6;
}

.hint {
  margin: 14px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
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

.activities-head {
  margin-top: 26px;
}

.market-group {
  margin-top: 2px;
}

.position-list {
  overflow: hidden;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.position-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 16px 18px;
  border: 0;
  border-bottom: 1px solid var(--color-border-subtle);
  background: transparent;
  color: inherit;
  text-align: left;
}

.position-row:last-child {
  border-bottom: 0;
}

.position-row:hover {
  background: var(--color-bg-subtle);
}

.position-main,
.position-side {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.position-main {
  flex: 1;
}

.position-side {
  flex: 0 0 auto;
  align-items: flex-end;
}

.position-name {
  display: flex;
  align-items: baseline;
  gap: 7px;
  min-width: 0;
}

.position-name strong {
  flex: 1;
  overflow: hidden;
  color: var(--color-text-primary);
  font-size: 0.9375rem;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.position-code-line {
  font-variant-numeric: tabular-nums;
}

.position-main > small,
.position-side small {
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.position-side b {
  color: var(--color-text-primary);
  font-size: 1rem;
  font-weight: 700;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.closed-block {
  margin-top: 20px;
  padding: 4px 18px 16px;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.closed-block summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 48px;
  padding: 6px 0;
  color: var(--color-text-primary);
  font-size: var(--text-footnote);
  font-weight: 600;
  cursor: pointer;
  list-style: none;
}

.closed-block summary::-webkit-details-marker {
  display: none;
}

.closed-block summary::after {
  color: var(--color-text-secondary);
  font-size: 1rem;
  content: '⌄';
}

.closed-block[open] summary::after {
  transform: rotate(180deg);
}

.closed-list {
  margin-top: 4px;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
}

.closed-list .position-row {
  padding-right: 0;
  padding-left: 0;
}

.closed-list .position-side b {
  font-size: 0.8125rem;
}

.empty-stock {
  margin-top: 20px;
  padding: 26px 18px;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
  text-align: center;
}

.empty-stock p {
  margin: 0 0 14px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.6;
}

.empty-actions {
  display: grid;
  gap: 8px;
}

.empty-actions button {
  min-height: var(--touch-target);
  border-radius: var(--radius-control);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.empty-stock .empty-link {
  margin-top: 12px;
  min-height: 32px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.pending-list {
  display: flex;
  flex-direction: column;
  gap: 9px;
}

.pending-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 16px 18px;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.pending-copy {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.pending-copy strong {
  color: var(--color-text-primary);
  font-size: 0.9375rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.pending-copy small {
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pending-side {
  display: flex;
  flex: 0 0 auto;
  align-items: center;
  gap: 10px;
}

.pending-side b {
  color: var(--color-text-primary);
  font-size: 1rem;
  font-weight: 700;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
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

.detail-metrics {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
  padding: 16px;
  border-radius: var(--radius-card);
  background: var(--color-bg-subtle);
}

.detail-metrics span,
.detail-lines span {
  display: block;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.detail-metrics b {
  display: block;
  margin-top: 5px;
  font-size: 0.9375rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.detail-lines {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 14px;
}

.detail-lines > div {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  padding-bottom: 9px;
  border-bottom: 1px solid var(--color-border-subtle);
}

.detail-lines > div:last-child {
  border-bottom: 0;
  padding-bottom: 0;
}

.detail-lines b {
  font-size: 0.8125rem;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
  text-align: right;
}

.detail-actions {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin-top: 16px;
}

.sheet-summary {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.summary-line {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  padding: 11px 14px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.summary-line b {
  color: var(--color-text-primary);
  font-size: var(--text-footnote);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.sell-preview {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 8px;
  padding: 0;
  background: transparent;
}

.sell-preview b {
  color: var(--color-brand);
  font-size: 0.9375rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.sell-preview span {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}
</style>
