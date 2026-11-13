import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { expect, test } from 'vitest'
import { nextTick } from 'vue'
import AppNav from './AppNav.vue'

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
      { path: '/salary', name: 'salary', component: Empty }
    ]
  })

  await router.push(path)
  await router.isReady()

  return router
}

test('renders 目标 instead of 计划 and highlights it on both routes', async () => {
  const router = await createTestRouter('/capital')
  const view = mount(AppNav, { global: { plugins: [router] } })
  const goal = () => view.findAll('.nav-item').find((item) => item.text() === '目标')!

  expect(view.findAll('.nav-item').map((item) => item.text())).toContain('目标')
  expect(view.findAll('.nav-item').map((item) => item.text())).not.toContain('计划')
  expect(goal().classes()).toContain('active')
  expect(goal().attributes('href')).toContain('capital')

  await router.push('/plans')
  await nextTick()
  expect(goal().classes()).toContain('active')

  await router.push('/')
  await nextTick()
  expect(goal().classes()).not.toContain('active')

  view.unmount()
})


test('highlights 我的 on the salary detail page', async () => {
  const router = await createTestRouter('/mine')
  const view = mount(AppNav, { global: { plugins: [router] } })
  const mineTab = () => view.findAll('.nav-item').find((item) => item.text() === '我的')!

  expect(mineTab().classes()).toContain('active')

  await router.push('/salary')
  await nextTick()

  expect(mineTab().classes()).toContain('active')
  expect(mineTab().attributes('href')).toContain('/mine')

  view.unmount()
})
