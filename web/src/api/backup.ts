import { apiDownload, apiRequest } from './client'

export interface BackupSummary {
  ledgerId: string
  startDate: string
  cashBalance: number
  providentBalance: number | null
  fundValue: number
  fundPending: number
  fundPendingArrival: number
  goldValue: number | null
  goldPendingArrival: number
  stockValue: number | null
  totalAssets: number | null
  isComplete: boolean
  entryCount: number
  assetTransactionCount: number
  planCount: number
  planOccurrenceCount: number
  mutationReceiptCount: number
  priceObservationCount: number
}

export interface BackupValidation {
  isValid: boolean
  willReplace: boolean
  validationToken: string | null
  summary: BackupSummary
  current: BackupSummary | null
  errors: string[]
  warnings: string[]
  duplicateRisks: string[]
}

export interface RestoreBackupResult {
  restored: boolean
  replacedExisting: boolean
  summary: BackupSummary
  restoredAt: string
  warnings: string[]
}

export function validateBackup(backup: unknown): Promise<BackupValidation> {
  return apiRequest<BackupValidation>('/api/ledger/backup/validate', {
    method: 'POST',
    body: JSON.stringify(backup),
    skipCacheInvalidation: true
  })
}

export function restoreBackup(
  backup: unknown,
  validationToken: string,
  replaceExisting: boolean
): Promise<RestoreBackupResult> {
  return apiRequest<RestoreBackupResult>('/api/ledger/backup/restore', {
    method: 'POST',
    body: JSON.stringify({ backup, validationToken, replaceExisting })
  })
}

export function downloadBackup(): Promise<void> {
  return apiDownload('/api/ledger/backup', 'lifeup-backup.json')
}

export function downloadDetails(from: string, to: string): Promise<void> {
  const params = new URLSearchParams()

  if (from) {
    params.set('from', from)
  }

  if (to) {
    params.set('to', to)
  }

  const suffix = params.toString() ? `?${params.toString()}` : ''
  return apiDownload(`/api/ledger/export/details.csv${suffix}`, 'lifeup-details.csv')
}
