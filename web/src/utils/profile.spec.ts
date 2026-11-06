import { beforeEach, expect, test } from 'vitest'
import { loadProfile, normalizeProfileName, profileStorageKey, saveProfile } from './profile'

beforeEach(() => {
  localStorage.clear()
})

test('profile name is trimmed and limited to 20 characters', () => {
  expect(normalizeProfileName('  小明  ')).toBe('小明')
  expect(normalizeProfileName('一二三四五六七八九十一二三四五六七八九十一')).toHaveLength(20)
  expect(normalizeProfileName(undefined)).toBe('')
})

test('saved profile is read back and ignores invalid avatar values', () => {
  expect(saveProfile({ name: '小明', avatar: 'https://example.com/a.png' })).toBe(true)
  expect(loadProfile()).toEqual({ name: '小明', avatar: null })

  localStorage.setItem(profileStorageKey, JSON.stringify({ name: '小红', avatar: 'data:image/png;base64,abc' }))
  expect(loadProfile()).toEqual({ name: '小红', avatar: 'data:image/png;base64,abc' })
})
