import type { AutoPlan, OccurrenceStatus } from '../types/plan'
import { formatBusinessDate, formatMoney, sumAmounts } from './format'
import { fundPlanDisplayName } from './plan'

export const weekdayLabels = ['一', '二', '三', '四', '五', '六', '日']

export interface FundPlanGroup {
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

export function groupSummary(plans: AutoPlan[], amountLabel: string): string {
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

export function incomeSummary(plans: AutoPlan[]): string {
  if (!plans.length) {
    return '暂无计划'
  }

  const activePlans = plans.filter(plan => plan.isActive)

  if (!activePlans.length) {
    return `已全部暂停 · ${plans.length} 个计划`
  }

  const salary = sumAmounts(activePlans.filter(plan => plan.type === 'salaryIncome').map(plan => plan.amount))
  const bonus = sumAmounts(activePlans.filter(plan => plan.type === 'bonusIncome').map(plan => plan.amount))
  const parts: string[] = []

  if (salary > 0) {
    parts.push(`工资 ¥${formatMoney(salary)}/月`)
  }

  if (bonus > 0) {
    parts.push(`年终奖 ¥${formatMoney(bonus)}/年`)
  }

  const paused = plans.length - activePlans.length

  if (!parts.length) {
    return paused ? `已全部暂停 · ${paused} 个计划` : '暂无启用计划'
  }

  return `${parts.join(' · ')}${paused ? ` · ${paused} 个暂停` : ''}`
}

export function providentSummary(plans: AutoPlan[]): string {
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

export function fundGroupKey(plan: AutoPlan): string {
  return fundPlanDisplayName(plan).normalize('NFKC').replace(/\s/g, '').toUpperCase()
}

export function fundCode(plan: AutoPlan): string | null {
  const code = plan.fundTarget?.code?.trim()
  return code ? code : null
}

export function shareClassLabel(plan: AutoPlan): string {
  const shareClass = plan.fundTarget?.shareClass?.trim()

  if (shareClass) {
    return /^[A-Za-z]$/.test(shareClass) ? `${shareClass.toUpperCase()} 类` : shareClass
  }

  const match = (plan.fundPositionName ?? plan.name).match(/\s([A-Za-z])(?:\s*类)?(?:\s*[·]|$)/)
  return match ? `${match[1]!.toUpperCase()} 类` : '未标类别'
}

export function planCycleLabel(plan: AutoPlan): string {
  if (plan.cycle === 'tradingDay') {
    return plan.tradingScope === 'china' ? '中国交易日' : '三地交易日'
  }

  if (plan.cycle === 'weekly') {
    return `每周${weekdayLabels[(plan.dayOfWeek ?? 1) - 1] ?? ''}`
  }

  if (plan.cycle === 'yearly') {
    return `每年 ${plan.monthOfYear ?? 1} 月 ${plan.dayOfMonth} 日`
  }

  return `每月 ${plan.dayOfMonth ?? 1} 日`
}

export function scheduleText(plan: AutoPlan): string {
  return `${planCycleLabel(plan)} ${plan.time.slice(0, 5)}`
}

export function fundMetaLine(plan: AutoPlan): string {
  return [fundCode(plan), shareClassLabel(plan), scheduleText(plan)]
    .filter((part): part is string => Boolean(part))
    .join(' · ')
}

export function fundGroupMetaLine(group: FundPlanGroup): string {
  return [
    group.codes.join(' / '),
    group.shareClasses.join(' / '),
    `${group.plans.length} 个计划`
  ].filter(Boolean).join(' · ')
}

export function shortDate(value: string, today: string): string {
  return value.startsWith(`${today.slice(0, 4)}-`)
    ? `${Number(value.slice(5, 7))}月${Number(value.slice(8, 10))}日`
    : formatBusinessDate(value)
}

export function nextDueText(plan: AutoPlan, today: string): string {
  if (!plan.isActive) {
    return '已暂停'
  }

  return plan.nextDueDate ? `下一期 ${shortDate(plan.nextDueDate, today)}` : '下一期待生成'
}

export function statusLabel(status: OccurrenceStatus): string {
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

export function statusClass(status: OccurrenceStatus): string {
  return `status-${status}`
}
