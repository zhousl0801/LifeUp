import { apiRequest } from './client'
import type { HistoryOverview } from '../types/history'

export function fetchHistory(asOf?: string): Promise<HistoryOverview> {
  return apiRequest(`/api/ledger/history${asOf ? `?asOf=${encodeURIComponent(asOf)}` : ''}`)
}
