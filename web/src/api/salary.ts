import type { SalaryProfileOverview, SaveSalaryProfileInput } from '../types/salary'
import { apiRequest } from './client'

export function fetchSalaryProfile(): Promise<SalaryProfileOverview> {
  return apiRequest<SalaryProfileOverview>('/api/ledger/salary-profile')
}

export function saveSalaryProfile(input: SaveSalaryProfileInput): Promise<SalaryProfileOverview> {
  return apiRequest<SalaryProfileOverview>('/api/ledger/salary-profile', {
    method: 'PUT',
    body: JSON.stringify(input)
  })
}

export function disableSalaryProfile(requestId: string): Promise<SalaryProfileOverview> {
  return apiRequest<SalaryProfileOverview>('/api/ledger/salary-profile/disable', {
    method: 'POST',
    body: JSON.stringify({ requestId })
  })
}
