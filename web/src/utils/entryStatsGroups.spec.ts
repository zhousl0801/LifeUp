import { expect, test } from 'vitest'
import type { EntryCategory } from '../types/category'
import { groupEntryStats } from './entryStatsGroups'

const dining = { category: '餐饮', amount: 35.60, count: 2, share: 0 }
const transport = { category: '交通', amount: 9.90, count: 1, share: 0 }

test('falls back to the original expense group rules for flat category data', () => {
  expect(groupEntryStats([dining, transport], [], 'expense')).toEqual([
    { category: '必要', amount: 45.50, count: 3, share: 100 }
  ])
})

test('uses the current parent after a category move and marks unknown categories unassigned', () => {
  const categories: EntryCategory[] = [
    { id: 'custom', kind: 'expense', name: '外食', isGroup: true, isActive: false, sortOrder: 0, systemKey: null },
    { id: 'dining', kind: 'expense', name: '餐饮', parentId: 'custom', isActive: false, sortOrder: 0, systemKey: null }
  ]

  expect(groupEntryStats([dining, transport], categories, 'expense')).toEqual([
    { category: '外食', amount: 35.60, count: 2, share: 78.24 },
    { category: '未归类', amount: 9.90, count: 1, share: 21.76 }
  ])
})
