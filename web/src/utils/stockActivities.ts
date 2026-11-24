import Decimal from 'decimal.js'
import type { InvestmentActivityRow } from '../components/InvestmentActivityList.vue'
import type { StockActivity } from '../types/stock'
import {
  formatBusinessDate,
  formatBusinessTime,
  formatMoney,
  formatQuantity,
  formatSignedMoney,
  formatUnitPrice
} from './format'

export function formatShares(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return '—'
  }

  return formatQuantity(value).replace(/\.?0+$/, '') || '0'
}

function activityLabel(activity: StockActivity): string {
  switch (activity.type) {
    case 'buy':
      return '买入'
    case 'sell':
      return '卖出'
    case 'opening':
      return '建仓'
    case 'correction':
      return '核对'
    default:
      return '行情'
  }
}

function activityStatus(activity: StockActivity): string {
  switch (activity.type) {
    case 'buy':
    case 'sell':
      return '已成交'
    case 'opening':
      return '已录入'
    case 'correction':
      return '已核对'
    default:
      return '仅更新估值'
  }
}

function activityTime(activity: StockActivity): string {
  return [formatBusinessDate(activity.businessDate), formatBusinessTime(activity.createdAt)]
    .filter(Boolean)
    .join(' ')
}

function activityCostPerShare(activity: StockActivity): number | null {
  if (activity.shares === null || activity.shares <= 0) {
    return null
  }

  return new Decimal(activity.amount)
    .div(activity.shares)
    .toDecimalPlaces(3, Decimal.ROUND_HALF_UP)
    .toNumber()
}

function activityMeta(activity: StockActivity): string {
  const quantity = activity.shares === null ? '' : `${formatShares(activity.shares)} 股`

  if (activity.type === 'buy' || activity.type === 'sell') {
    const price = activity.price === null
      ? ''
      : `${activity.type === 'buy' ? '成本' : '成交价'} ${formatUnitPrice(activity.price)}/股`
    return [quantity, price].filter(Boolean).join(' · ')
  }

  if (activity.type === 'correction' || activity.type === 'opening') {
    const cost = activityCostPerShare(activity)
    return [
      quantity,
      cost === null ? `成本 ${formatMoney(activity.amount)}` : `成本 ${formatUnitPrice(cost)}/股`
    ].filter(Boolean).join(' · ')
  }

  return ''
}

function activityAmount(activity: StockActivity): string {
  if (activity.type === 'buy' || activity.type === 'sell') {
    return `${formatSignedMoney(activity.cashEffect)}元`
  }

  if (activity.type === 'correction' || activity.type === 'opening') {
    return `${formatMoney(activity.amount)}元`
  }

  return activity.price === null ? '—' : `${formatUnitPrice(activity.price)}元/股`
}

function activityAmountTone(activity: StockActivity): string {
  if (activity.type === 'buy') {
    return 'transfer'
  }

  return activity.type === 'sell' ? 'income' : ''
}

export function toStockActivityRow(activity: StockActivity): InvestmentActivityRow {
  return {
    key: activity.id,
    label: activityLabel(activity),
    labelTone: activity.type === 'buy' ? 'buy' : 'neutral',
    title: `股票 | ${activity.positionName}`,
    meta: activityMeta(activity),
    time: activityTime(activity),
    amount: activityAmount(activity),
    amountTone: activityAmountTone(activity),
    status: activityStatus(activity),
    statusTone: 'done'
  }
}
