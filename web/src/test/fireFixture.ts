import type { FireOverview } from '../types/capital'

export const fireFixture: FireOverview = {
  goalMode: 'fire',
  activeMode: 'standard',
  safeWithdrawalRatePercent: 4,
  savingsRatePercent: 54.2,
  expenseWindowMonths: 12,
  currentPassiveMonthlyCashflow: 2310,
  currentPassiveAnnualCashflow: 27720,
  standard: {
    monthlyExpense: 6000, annualExpense: 72000, expenseSource: 'history', targetCapital: 1800000,
    monthlyCashflowGap: 3690, progressPercent: 38.5, status: 'onTrack', etaMonth: '2034-03', monthsRemaining: 88
  },
  lean: {
    monthlyExpense: 3900, annualExpense: 46800, expenseSource: 'history', targetCapital: 1170000,
    monthlyCashflowGap: 1590, progressPercent: 59.2, status: 'onTrack', etaMonth: '2031-01', monthsRemaining: 50
  },
  coast: {
    targetYear: 2051, targetYearSource: 'default', achievable: true, reached: true,
    thresholdCapital: 419400, progressPercent: 100, etaMonth: '2025-11', monthsRemaining: 0, note: null
  },
  milestones: [
    { key: 'emergency', label: '应急垫', thresholdAmount: 72000, progressPercent: 4, reached: true, date: '2024-08-01', description: '覆盖 1 年生活支出' },
    { key: 'coast', label: 'Coast', thresholdAmount: 419400, progressPercent: 23.3, reached: true, date: '2025-11-01', description: '2051 年自然自由' },
    { key: 'barista', label: '半退休', thresholdAmount: 900000, progressPercent: 50, reached: false, date: '2028-09-01', description: '被动现金流覆盖一半开销' }
  ],
  categories: [
    { name: '房租', groupName: '固定', monthlyAverage: 2200, isDefaultLean: true, isLean: true, isExcluded: false },
    { name: '旅行', groupName: null, monthlyAverage: 500, isDefaultLean: false, isLean: false, isExcluded: false }
  ],
  insights: {
    stage: '加速期',
    headline: '被动月薪 ¥2,310，已覆盖房租。',
    coveredCategories: ['房租'],
    suggestions: [{ badge: '支出杠杆', text: '目标本金减少 ¥15 万，自由时间提前 9 个月。' }]
  }
}
