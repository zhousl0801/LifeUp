import type { EntryKind } from './entry'

export interface EntryCategory {
  id: string
  kind: EntryKind
  name: string
  sortOrder: number
  isActive: boolean
  systemKey: string | null
  parentId?: string | null
  isGroup?: boolean
}

export interface CreateCategoryInput {
  kind: EntryKind
  name: string
  parentId?: string
  isGroup?: boolean
}
