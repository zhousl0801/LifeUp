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
}

export interface CapitalYearRow {
  year: number
  yearEndCapital: number
  netSavings: number
  providentDeposit: number
  return: number
  total: number
  isGoalYear: boolean
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
  note: string | null
}

export interface CapitalOverview {
  settings: CapitalGoalSettings
  effective: CapitalEffectiveInputs
  current: CapitalCurrent
  projection: CapitalProjection
  actualMonth: CapitalActualMonth
  hasSalaryProfile: boolean
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
