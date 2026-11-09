import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { toast, useMessages } from './useMessage'

const { messages, clear } = useMessages()

beforeEach(() => {
  clear()
  vi.useFakeTimers()
})

afterEach(() => {
  clear()
  vi.useRealTimers()
})

test('a success message dismisses after its default duration', () => {
  toast.success('已保存')

  expect(messages.value).toHaveLength(1)
  vi.advanceTimersByTime(2799)
  expect(messages.value).toHaveLength(1)
  vi.advanceTimersByTime(1)
  expect(messages.value).toHaveLength(0)
})

test('repeating the same message restarts its timer without stacking', () => {
  toast.success('已保存')
  vi.advanceTimersByTime(2000)
  toast.success('已保存')

  expect(messages.value).toHaveLength(1)
  vi.advanceTimersByTime(2000)
  expect(messages.value).toHaveLength(1)
  vi.advanceTimersByTime(800)
  expect(messages.value).toHaveLength(0)
})

test('keeps at most three visible messages', () => {
  toast.success('第一条')
  toast.success('第二条')
  toast.success('第三条')
  toast.success('第四条')

  expect(messages.value.map((item) => item.text)).toEqual(['第二条', '第三条', '第四条'])
})

test('a message can be dismissed immediately', () => {
  const id = toast.success('已保存')
  toast.dismiss(id)

  expect(messages.value).toHaveLength(0)
})
