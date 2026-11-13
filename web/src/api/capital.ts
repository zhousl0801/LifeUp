import type { CapitalGoalSettings, CapitalOverview } from '../types/capital'
import { apiRequest } from './client'

export function fetchCapital(): Promise<CapitalOverview> {
  return apiRequest<CapitalOverview>('/api/ledger/capital')
}

export function saveCapital(input: CapitalGoalSettings): Promise<CapitalOverview> {
  return apiRequest<CapitalOverview>('/api/ledger/capital', {
    method: 'PUT',
    body: JSON.stringify(input)
  })
}

export function previewCapital(input: CapitalGoalSettings): Promise<CapitalOverview> {
  return apiRequest<CapitalOverview>('/api/ledger/capital/preview', {
    method: 'POST',
    body: JSON.stringify(input)
  })
}
