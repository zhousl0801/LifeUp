import { describe, expect, test } from 'vitest'
import {
  classifyRouteMotion,
  commitRouteMotion,
  resolveSurfaceMode,
  setVisibleSheetCount,
  useRouteMotion
} from './routeMotion'

describe('routeMotion', () => {
  test('classifies tab, push, pop, modal-in, modal-out, and none transitions', () => {
    expect(classifyRouteMotion({ name: 'home' }, { name: undefined })).toBe('none')
    expect(classifyRouteMotion({ name: 'home' }, { name: 'home' })).toBe('none')
    expect(classifyRouteMotion({ name: 'ledger' }, { name: 'home' })).toBe('tab')
    expect(classifyRouteMotion({ name: 'capital' }, { name: 'plans' })).toBe('tab')
    expect(classifyRouteMotion({ name: 'cash' }, { name: 'home' })).toBe('push')
    expect(classifyRouteMotion({ name: 'funds' }, { name: 'assets' })).toBe('push')
    expect(classifyRouteMotion({ name: 'home' }, { name: 'cash' })).toBe('pop')
    expect(classifyRouteMotion({ name: 'mine' }, { name: 'backup' })).toBe('pop')
    expect(classifyRouteMotion({ name: 'entry' }, { name: 'home' })).toBe('modal-in')
    expect(classifyRouteMotion({ name: 'home' }, { name: 'entry' })).toBe('modal-out')
    expect(classifyRouteMotion({ name: 'home' }, { name: 'setup' }, true)).toBe('pop')
  })

  test('resolves surface mode for status bar and shell theming', () => {
    expect(resolveSurfaceMode('home')).toBe('home')
    expect(resolveSurfaceMode('entry')).toBe('entry')
    expect(resolveSurfaceMode('ledger')).toBe('default')
    expect(resolveSurfaceMode(undefined)).toBe('default')
  })

  test('commits reactive route motion, surface mode, and open sheet state', () => {
    const state = useRouteMotion()
    commitRouteMotion({ name: 'entry' }, { name: 'home' }, false)
    expect(state.routeMotion.value).toBe('modal-in')
    expect(state.currentSurface.value).toBe('entry')

    commitRouteMotion({ name: 'home' }, { name: 'entry' }, true)
    expect(state.routeMotion.value).toBe('modal-out')
    expect(state.currentSurface.value).toBe('home')

    setVisibleSheetCount(2)
    expect(state.visibleSheetCount.value).toBe(2)
    expect(state.hasOpenSheet.value).toBe(true)

    setVisibleSheetCount(0)
    expect(state.hasOpenSheet.value).toBe(false)
  })
})
