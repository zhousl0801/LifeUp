import type { InitializeLedgerInput, LedgerOverview, MonthChange, UpdateAssetCoverageInput } from '../types/ledger'
import type { AssetTrend, TrendRange } from '../types/trend'
import { apiRequest } from './client'

export function fetchLedger(): Promise<LedgerOverview> {
  return apiRequest<LedgerOverview>('/api/ledger')
}

export function fetchAssetTrend(range: TrendRange): Promise<AssetTrend> {
  return apiRequest<AssetTrend>(`/api/ledger/trend?range=${encodeURIComponent(range)}`)
}

export function fetchDayChange(): Promise<MonthChange> {
  return apiRequest<MonthChange>('/api/ledger/day-change')
}

export function fetchMonthChange(): Promise<MonthChange> {
  return apiRequest<MonthChange>('/api/ledger/month-change')
}

export function updateAssetCoverage(input: UpdateAssetCoverageInput): Promise<LedgerOverview> {
  return apiRequest<LedgerOverview>('/api/ledger/coverage', {
    method: 'PUT',
    body: JSON.stringify(input)
  })
}

export function initializeLedger(input: InitializeLedgerInput): Promise<LedgerOverview> {
  return apiRequest<LedgerOverview>('/api/ledger/initialize', {
    method: 'POST',
    body: JSON.stringify(input)
  })
}

interface MonthlyBudget {
  month: string
  amount: number | null
}

export function fetchMonthlyBudget(month: string): Promise<MonthlyBudget> {
  return apiRequest<MonthlyBudget>(`/api/ledger/monthly-budget?month=${encodeURIComponent(month)}`)
}

export function updateMonthlyBudget(month: string, amount: number | null): Promise<MonthlyBudget> {
  return apiRequest<MonthlyBudget>(`/api/ledger/monthly-budget?month=${encodeURIComponent(month)}`, {
    method: 'PUT',
    body: JSON.stringify({ amount })
  })
}
