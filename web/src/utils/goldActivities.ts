import Decimal from 'decimal.js'
import type { InvestmentActivityRow } from '../components/InvestmentActivityList.vue'
import type { GoldActivity } from '../types/gold'
import {
  formatBusinessDate,
  formatBusinessTime,
  formatMoney,
  formatQuantity,
  formatSignedMoney,
  formatUnitPrice
} from './format'

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

export function toGoldActivityRow(activity: GoldActivity): InvestmentActivityRow {
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
