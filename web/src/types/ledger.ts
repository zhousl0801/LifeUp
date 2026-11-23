export interface AssetOverview {
  isConfigured: boolean
  amount: number | null
  note: string | null
  notHeld: boolean
}

export interface MonthSummary {
  income: number
  expense: number
  surplus: number
  hasRecords: boolean
  providentDeposit?: number
  providentWithdrawal?: number
}

export interface LedgerOverview {
  id: string
  currency: string
  timeZone: string
  startDate: string
  asOfDate: string
  isComplete: boolean
  totalAssets: number
  cash: AssetOverview
  fund: AssetOverview
  providentFund: AssetOverview
  gold: AssetOverview
  stock: AssetOverview
  month: MonthSummary
  fundNotHeld: boolean
  providentFundNotHeld: boolean
  goldNotHeld: boolean
  stockNotHeld: boolean
  coverageComplete: boolean
}

export type AssetKey = 'cash' | 'providentFund' | 'fund' | 'gold' | 'stock'

export interface AssetChange {
  assetType: AssetKey
  change: number | null
  isNew: boolean
  baselineAmount?: number | null
}

export interface PriorFundDisclosure {
  date: string
  profit: number
  covered: number
  total: number
  complete: boolean
}

export interface MonthChange {
  from: string
  to: string
  totalChange: number | null
  livingSurplus: number
  providentDeposit: number
  investmentProfit: number | null
  reconciliationDifference: number
  isComplete: boolean
  notes: string[]
  assetChanges: AssetChange[]
  comparableChange: number | null
  comparableBase: number | null
  addedRecords: number | null
  marketValueChange: number | null
  netDeposit: number | null
  hasComparableData: boolean
  dayBreakdown?: DayChangeBreakdown | null
  priorFundDisclosure?: PriorFundDisclosure | null
}

export interface DayChangeBreakdown {
  cashIncome: number
  cashExpense: number
  providentDeposit: number
  fund: DayInvestmentChange | null
  stock: DayInvestmentChange | null
  gold: DayInvestmentChange | null
  cashAdjustment: number
  holdingAdjustment: number
  otherDifference: number
}

export interface DayInvestmentChange {
  profit: number
  previousValue: number
  currentValue: number
  cashTransfer: number
  openingValue: number
  adjustment: number
}

export interface UpdateAssetCoverageInput {
  fundNotHeld: boolean
  providentFundNotHeld: boolean
  goldNotHeld: boolean
  stockNotHeld: boolean
}

export interface InitializeLedgerInput {
  cashAsOfDate?: string
  startDate: string
  cashBalance: number
}
