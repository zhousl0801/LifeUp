import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, expect, test, vi } from 'vitest'
import MineView from './MineView.vue'

vi.mock('vue-router', () => ({
  RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
  useRoute: () => ({ name: 'mine', query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() })
}))
import { profileStorageKey } from '../utils/profile'
import { initializeTheme, setThemeMode } from '../utils/theme'

beforeEach(() => {
  localStorage.clear()
  initializeTheme()
  setThemeMode('system')
})

const mountOptions = {
  global: {
    stubs: {
      RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' }
    }
  }
}

test('salary profile entry opens the detail page', () => {
  const view = mount(MineView, mountOptions)

  const rows = view.findAll('.list-box .list-row')
  expect(rows[0]!.text()).toContain('头像与昵称')
  expect(rows[1]!.text()).toContain('工资档案')

  const link = view.findAll('a').find(item => item.text().includes('工资档案'))!
  expect(link.attributes('href')).toBe('/salary')
  expect(link.text()).toContain('查看')
  expect(view.find('.salary-card').exists()).toBe(false)
  view.unmount()
})

test('theme setting switches between system, light and dark', async () => {
  const view = mount(MineView, mountOptions)
  await flushPromises()

  const themeRow = view.findAll('button').find((button) => button.text().startsWith('主题配色'))!
  await themeRow.trigger('click')
  await flushPromises()

  const options = [...document.querySelectorAll<HTMLButtonElement>('.sheet .theme-option')]
  expect(options).toHaveLength(3)
  expect(options[0]!.getAttribute('aria-checked')).toBe('true')

  options[2]!.click()
  await flushPromises()

  expect(document.documentElement.dataset.colorScheme).toBe('dark')
  expect(document.querySelectorAll<HTMLButtonElement>('.sheet .theme-option')[2]!.getAttribute('aria-checked'))
    .toBe('true')

  view.unmount()
})

test('profile name is saved locally and shown in the settings row', async () => {
  const view = mount(MineView, mountOptions)
  await flushPromises()

  const profileRow = view.findAll('button').find((button) => button.text().includes('头像与昵称'))!
  expect(profileRow.text()).toContain('LifeUp')
  expect(profileRow.find('.default-avatar').exists()).toBe(true)

  await profileRow.trigger('click')
  await flushPromises()

  const input = document.querySelector<HTMLInputElement>('.sheet .profile-field input')!
  input.value = ' 小明 '
  input.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()

  document.querySelector<HTMLButtonElement>('.sheet .profile-save')!.click()
  await flushPromises()

  expect(JSON.parse(localStorage.getItem(profileStorageKey)!) as { name: string }).toMatchObject({ name: '小明' })
  expect(view.text()).toContain('小明')

  await view.findAll('button').find((button) => button.text().includes('头像与昵称'))!.trigger('click')
  await flushPromises()
  expect(document.querySelector<HTMLInputElement>('.sheet .profile-field input')!.value).toBe('小明')

  view.unmount()
})
