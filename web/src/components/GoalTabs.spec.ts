import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { expect, test } from 'vitest'
import { nextTick } from 'vue'
import GoalTabs from './GoalTabs.vue'

const Empty = { template: '<div />' }

async function createTestRouter(path: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/capital', name: 'capital', component: Empty },
      { path: '/plans', name: 'plans', component: Empty },
      { path: '/', name: 'home', component: Empty },
      { path: '/ledger', name: 'ledger', component: Empty },
      { path: '/mine', name: 'mine', component: Empty },
      { path: '/assets', name: 'assets', component: Empty }
    ]
  })

  await router.push(path)
  await router.isReady()

  return router
}

test('highlights the active segment on both routes', async () => {
  const router = await createTestRouter('/capital')
  const view = mount(GoalTabs, { global: { plugins: [router] } })

  expect(view.findAll('.goal-tab')[0]!.classes()).toContain('active')
  expect(view.findAll('.goal-tab')[1]!.classes()).not.toContain('active')

  await router.push('/plans')
  await nextTick()

  expect(view.findAll('.goal-tab')[0]!.classes()).not.toContain('active')
  expect(view.findAll('.goal-tab')[1]!.classes()).toContain('active')

  view.unmount()
})
