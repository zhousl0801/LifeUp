import { expect, test, vi } from 'vitest'
import type { Router } from 'vue-router'
import { navigateBack } from './navigation'

test('returns to the source when history exists and falls back for direct links', () => {
  const router = { options: { history: { state: { back: '/assets' as string | null } } }, back: vi.fn(), push: vi.fn() }
  navigateBack(router as unknown as Router)
  expect(router.back).toHaveBeenCalledOnce()
  router.options.history.state.back = null
  navigateBack(router as unknown as Router, { name: 'mine' })
  expect(router.push).toHaveBeenCalledWith({ name: 'mine' })
})
