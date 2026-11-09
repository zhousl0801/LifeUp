import type { EntryCategory } from '../types/category'
import type { EntryKind, EntryStatsCategory } from '../types/entry'
import { sumAmounts } from './format'

const necessary = new Set(['餐饮', '日用', '交通', '居家', '服饰', '通讯', '医疗'])

function legacyGroup(name: string, kind: EntryKind): string {
  if (kind === 'income') return name === '工资' ? '经常收入' : '其他收入'
  if (name === '房租' || name === '订阅') return '固定'
  if (necessary.has(name)) return '必要'
  if (name === '娱乐' || name === '旅行') return '弹性'
  return '其他'
}

/** Reconstructs group totals when the API only returns the leaf ranking. */
export function groupEntryStats(
  leaves: readonly EntryStatsCategory[],
  categories: readonly EntryCategory[],
  kind: EntryKind
): EntryStatsCategory[] {
  const matching = categories.filter(category => category.kind === kind)
  const parents = new Map(matching.filter(category => category.isGroup).map(category => [category.id, category.name]))
  const parentByName = new Map(matching.filter(category => !category.isGroup).map(category => [
    category.name, parents.get(category.parentId ?? '')
  ]))
  const byGroup = new Map<string, { amounts: number[]; count: number }>()

  for (const leaf of leaves) {
    const name = parents.size ? parentByName.get(leaf.category) ?? '未归类' : legacyGroup(leaf.category, kind)
    const group = byGroup.get(name) ?? { amounts: [], count: 0 }
    group.amounts.push(leaf.amount)
    group.count += leaf.count
    byGroup.set(name, group)
  }

  const total = sumAmounts(leaves.map(leaf => leaf.amount))
  return Array.from(byGroup, ([category, group]) => {
    const amount = sumAmounts(group.amounts)
    return { category, amount, count: group.count, share: total ? Number((amount / total * 100).toFixed(2)) : 0 }
  }).sort((a, b) => b.amount - a.amount || a.category.localeCompare(b.category, 'zh-CN'))
}
