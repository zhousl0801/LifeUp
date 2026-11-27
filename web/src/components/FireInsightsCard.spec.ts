import { mount } from '@vue/test-utils'
import { expect, test } from 'vitest'
import { fireFixture } from '../test/fireFixture'
import FireInsightsCard from './FireInsightsCard.vue'

test('shows stage, covered categories and masked suggestions', async () => {
  const view = mount(FireInsightsCard, {
    props: { insights: fireFixture.insights, expenseWindowMonths: 12, amountsMasked: false }
  })
  expect(view.text()).toContain('近 12 月账单')
  expect(view.text()).toContain('加速期')
  expect(view.text()).toContain('房租')
  expect(view.text()).toContain('¥15 万')
  await view.setProps({ amountsMasked: true })
  expect(view.text()).toContain('目标本金减少 ••••，自由时间提前 9 个月')
  expect(view.text()).not.toContain('¥2,310')
})
