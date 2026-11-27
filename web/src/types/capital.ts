export type CapitalStatus =
  | 'reached'
  | 'onTrack'
  | 'unreachable'
  | 'beyondHorizon'
  | 'depleted'
  | 'noTarget'

export type CapitalInputSource = 'user' | 'salary' | 'history' | 'default'

export interface CapitalEffectiveValue {
  value: number
  source: CapitalInputSource
}

export interface CapitalEffectiveInputs {
  monthlyIncome: CapitalEffectiveValue
  monthlyExpense: CapitalEffectiveValue
  monthlyProvidentDeposit: CapitalEffectiveValue
  incomeGrowthPercent: CapitalEffectiveValue
  expenseGrowthPercent: CapitalEffectiveValue
  investmentAnnualReturnPercent: CapitalEffectiveValue
  providentAnnualReturnPercent: CapitalEffectiveValue
  annualBonus: CapitalEffectiveValue
  bonusPayoutMonth: CapitalEffectiveValue
}

export interface CapitalGoalSettings {
  targetAmount: number | null
  monthlyIncome: number | null
  monthlyExpense: number | null
  monthlyProvidentDeposit: number | null
  incomeGrowthPercent: number | null
  expenseGrowthPercent: number | null
  investmentAnnualReturnPercent: number | null
  providentAnnualReturnPercent: number | null
  annualBonus: number | null
  bonusPayoutMonth: number | null
  useSalaryProfile: boolean
  goalMode?: FireGoalMode
  fireMode?: FireTrackMode
  safeWithdrawalRatePercent?: number | null
  excludedExpenseCategories?: string[] | null
  leanExpenseCategories?: string[] | null
  fireMonthlyExpenseOverride?: number | null
  leanMonthlyExpenseOverride?: number | null
  coastTargetYear?: number | null
}

export interface CapitalCurrent {
  asOfDate: string
  total: number
  cash: number
  investment: number
  provident: number
  coverageComplete: boolean
  note: string | null
}

export interface CapitalProjectionPoint {
  month: string
  capital: number
  netAccumulation: number
  return: number
  passiveAnnualCashflow?: number
  expectedAnnualExpense?: number
}

export interface CapitalYearRow {
  year: number
  yearEndCapital: number
  netSavings: number
  providentDeposit: number
  return: number
  total: number
  isGoalYear: boolean
  passiveMonthlyCashflow?: number
  freedomPercent?: number
}

export interface CapitalMilestone {
  thresholdAmount: number
  progressPercent: number
  date: string
  title: string
}

export interface CapitalProjection {
  status: CapitalStatus
  targetAmount: number | null
  remaining: number | null
  progressPercent: number
  etaMonth: string | null
  monthsRemaining: number | null
  monthlyNetAccumulation: number
  blendedAnnualReturnPercent: number
  points: CapitalProjectionPoint[]
  years: CapitalYearRow[]
  milestones: CapitalMilestone[]
  notes: string[]
}

export interface CapitalActualMonth {
  month: string
  comparableChange: number | null
  netDeposit: number | null
  marketValueChange: number | null
  expectedNetDeposit: number
  isComplete: boolean
  actualExpense?: number | null
  note: string | null
}

export interface CapitalOverview {
  settings: CapitalGoalSettings
  effective: CapitalEffectiveInputs
  current: CapitalCurrent
  projection: CapitalProjection
  actualMonth: CapitalActualMonth
  hasSalaryProfile: boolean
  fire?: FireOverview
}

export interface CapitalYearLabel {
  label: string
  leftPercent: number
}

export interface CapitalSuggestions {
  income: number | null
  expense: number | null
  providentDeposit: number | null
}

export type FireGoalMode = 'fire' | 'custom'
export type FireTrackMode = 'standard' | 'lean'
export type CapitalChartView = 'cashflow' | 'capital' | 'monthly'

export interface FireModeSummary {
  monthlyExpense: number
  expenseSource: CapitalInputSource
  annualExpense: number
  targetCapital: number
  monthlyCashflowGap: number
  progressPercent: number
  status: CapitalStatus
  etaMonth: string | null
  monthsRemaining: number | null
}

export interface FireCoastSummary {
  targetYear: number
  targetYearSource: 'user' | 'default'
  achievable: boolean
  reached: boolean
  thresholdCapital: number | null
  progressPercent: number
  etaMonth: string | null
  monthsRemaining: number | null
  note: string | null
}

export type FireMilestoneKey = 'emergency' | 'coast' | 'barista' | 'lean'

export interface FireMilestone {
  key: FireMilestoneKey
  label: string
  thresholdAmount: number
  progressPercent: number
  reached: boolean
  date: string | null
  description: string
}

export interface FireExpenseCategory {
  name: string
  groupName: string | null
  monthlyAverage: number
  isDefaultLean: boolean
  isLean: boolean
  isExcluded: boolean
}

export interface FireSuggestion {
  badge: string
  text: string
}

export interface FireInsights {
  stage: string
  headline: string
  coveredCategories: string[]
  suggestions: FireSuggestion[]
}

export interface FireOverview {
  goalMode: FireGoalMode
  activeMode: FireTrackMode
  safeWithdrawalRatePercent: number
  savingsRatePercent: number
  expenseWindowMonths: number
  currentPassiveMonthlyCashflow: number
  currentPassiveAnnualCashflow: number
  standard: FireModeSummary
  lean: FireModeSummary
  coast: FireCoastSummary
  milestones: FireMilestone[]
  categories: FireExpenseCategory[]
  insights: FireInsights
}
