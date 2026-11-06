import { expect, test } from 'vitest'
import { fundPlanDisplayName } from './plan'
import type { AutoPlan } from '../types/plan'

function plan(overrides: Partial<AutoPlan> = {}): AutoPlan {
  return {
    id: 'plan-1',
    type: 'fundSip',
    name: '基金定投 · 华夏移动互联灵活配置混合(QDII) A',
    amount: 100,
    cycle: 'tradingDay',
    dayOfMonth: null,
    dayOfWeek: null,
    time: '12:30:00',
    startDate: '2026-11-05',
    generateFrom: '2026-11-05',
    isActive: true,
    fundPositionId: 'position-1',
    fundPositionName: '华夏移动互联灵活配置混合(QDII) A',
    nextDueDate: '2026-11-06',
    version: 0,
    fundTarget: { name: '华夏移动互联灵活配置混合(QDII)', code: '002891', shareClass: 'A' },
    ...overrides
  }
}

test('fund plans display the linked fund name instead of the stored historical name', () => {
  expect(fundPlanDisplayName(plan())).toBe('华夏移动互联灵活配置混合(QDII)')
})

test('fund plan display strips legacy default prefixes and suffixes without a linked target', () => {
  expect(fundPlanDisplayName(plan({
    name: '基金定投 · 大成纳斯达克100ETF联接(QDII)A',
    fundPositionName: null,
    fundTarget: null
  }))).toBe('大成纳斯达克100ETF联接(QDII)A')

  expect(fundPlanDisplayName(plan({
    name: '大成纳斯达克100 每交易日定投',
    fundPositionName: null,
    fundTarget: null
  }))).toBe('大成纳斯达克100')

  expect(fundPlanDisplayName(plan({
    name: '建信新兴市场 定投',
    fundPositionName: null,
    fundTarget: null
  }))).toBe('建信新兴市场')
})
