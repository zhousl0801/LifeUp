import type { LedgerEntry } from '../types/entry'

interface LedgerReturn {
  entry: LedgerEntry
  action: 'create' | 'update' | 'reverse'
}

let pendingReturn: LedgerReturn | undefined

export function returnToLedger(change: LedgerReturn): void {
  pendingReturn = change
}

export function takeLedgerReturn(): LedgerReturn | undefined {
  const change = pendingReturn
  pendingReturn = undefined
  return change
}
