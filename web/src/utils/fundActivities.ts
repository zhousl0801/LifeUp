import type { InvestmentActivityRow } from '../components/InvestmentActivityList.vue'
import type {
  FundActivity,
  PendingFundArrival,
  PendingFundBuy,
  PendingFundSell
} from '../types/fund'
import { formatBusinessTime, formatMoney, formatQuantity } from './format'

export type ActivityKind = 'buy' | 'sip' | 'sell' | 'redeem' | 'arrival' | 'opening' | 'correction' | 'other'
export type ActivityFilter = 'all' | ActivityKind
export type ActivityStatusFilter = 'all' | 'pending' | 'done'
export type ActivityPendingAction = 'buy' | 'sell' | 'arrival'

export interface ActivityRow extends InvestmentActivityRow {
  kind: ActivityKind
  fundName: string
  timestamp: string
  amountText: string
  statusText: string
  statusTone: 'pending' | 'done' | 'reversed'
  isPending: boolean
  pendingAction: ActivityPendingAction | null
  pending: PendingFundBuy | PendingFundSell | PendingFundArrival | null
}

export const activityTypeOptions: { value: ActivityFilter; label: string }[] = [
  { value: 'all', label: '交易类型' },
  { value: 'sip', label: '定投' },
  { value: 'buy', label: '买入' },
  { value: 'sell', label: '卖出' },
  { value: 'redeem', label: '赎回' },
  { value: 'arrival', label: '到账' },
  { value: 'opening', label: '录入' },
  { value: 'correction', label: '核对' }
]

export function monthDayText(value: string): string {
  const parts = value.split('-')

  if (parts.length !== 3) {
    return value
  }

  return `${Number(parts[1])}月${Number(parts[2])}日`
}

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

function toRow(base: Omit<ActivityRow, 'title' | 'time' | 'amount' | 'status' | 'typeClass' | 'clickable'>): ActivityRow {
  return {
    ...base,
    typeClass: `type-${base.kind}`,
    title: `基金 | ${base.fundName}`,
    time: base.timestamp,
    amount: base.amountText,
    status: base.statusText,
    clickable: base.pendingAction !== null
  }
}

export function buildFundActivityRows(
  activities: FundActivity[],
  pendingBuys: PendingFundBuy[],
  sellApplications: PendingFundSell[],
  pendingArrivals: PendingFundArrival[]
): ActivityRow[] {
  const pendingBuyById = new Map(pendingBuys.map((item) => [item.transactionId, item]))
  const sellApplyById = new Map(sellApplications.map((item) => [item.transactionId, item]))
  const arrivalByConfirmId = new Map(pendingArrivals.map((item) => [item.transactionId, item]))
  const noteById = new Map(activities.map((item) => [item.id, item.note]))
  // 已走完后续环节的下单/申请记录不再重复列出：买入看确认，赎回申请看确认，确认看待到账。
  const settledIds = new Set<string>()

  for (const activity of activities) {
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

  for (const activity of activities) {
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

    rows.push(toRow({
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
    }))
  }

  for (const pending of pendingBuys) {
    if (seen.has(pending.transactionId)) {
      continue
    }

    const kind = isSipNote(pending.note) ? 'sip' : 'buy'
    rows.push(toRow({
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
    }))
  }

  for (const application of sellApplications) {
    if (seen.has(application.transactionId)) {
      continue
    }

    rows.push(toRow({
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
    }))
  }

  for (const arrival of pendingArrivals) {
    if (seen.has(arrival.transactionId)) {
      continue
    }

    rows.push(toRow({
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
    }))
  }

  // 未确认的交易置顶（还要盯），其余按时间倒序。
  return rows.sort((left, right) => {
    if (left.isPending !== right.isPending) {
      return left.isPending ? -1 : 1
    }

    return right.timestamp.localeCompare(left.timestamp)
  })
}
