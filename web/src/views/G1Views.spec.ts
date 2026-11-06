import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, expect, test, vi } from 'vitest'
import CashAdjustmentLink from '../components/CashAdjustmentLink.vue'

const mocks = vi.hoisted(() => ({ history: vi.fn() }))
vi.mock('../api/history', () => ({ fetchHistory: mocks.history }))
const history = {
  ledgerVersion: 1,
  cashOpening: { id: 'cash-id', source: 'cashOpening', date: '2026-10-01', amount: 1000, sequence: 0, version: 1, quantity: null, price: null, priceDate: null, isReversed: false, needsDateConfirmation: false },
  facts: [],
  prices: [],
  audit: [],
  snapshot: { asOfDate: '2026-10-21', cashBalance: 1000, providentBalance: null, funds: [], stocks: [], gold: null, adjustments: [], issues: [], knownTotalAssets: 1000, totalAssets: null }
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.history.mockResolvedValue(structuredClone(history))
})

test('cash links load on demand and show only matching direction and dates', async () => {
  mocks.history.mockResolvedValue({ ...history, facts: [
    { id: 'match', source: 'adjustment', date: '2026-10-21', recordedDifference: -50, note: '漏记' },
    { id: 'old', source: 'adjustment', date: '2026-10-01', recordedDifference: -50 },
    { id: 'income', source: 'adjustment', date: '2026-10-21', recordedDifference: 50 }
  ] })
  const view = mount(CashAdjustmentLink, { props: { businessDate: '2026-10-20', cashEffect: -50 } })
  expect(mocks.history).not.toHaveBeenCalled()
  await view.find('button').trigger('click')
  await flushPromises()
  expect(view.findAll('option')).toHaveLength(2)
  await view.find('select').setValue('match')
  expect(view.emitted('update:modelValue')![0]).toEqual(['match'])
  view.unmount()
})
