import { readonly, ref } from 'vue'

export type MessageTone = 'success' | 'error' | 'info'

export interface MessageAction {
  label: string
  run: () => void
}

export interface MessageOptions {
  duration?: number
  action?: MessageAction
}

export interface MessageItem {
  id: number
  tone: MessageTone
  text: string
  duration: number
  action?: MessageAction
}

const maxVisible = 3
const defaultDuration: Record<MessageTone, number> = {
  success: 2800,
  error: 6000,
  info: 3600
}

const items = ref<MessageItem[]>([])
const timers = new Map<number, ReturnType<typeof setTimeout>>()
let sequence = 0

function dismiss(id: number): void {
  const timer = timers.get(id)

  if (timer) {
    clearTimeout(timer)
    timers.delete(id)
  }

  items.value = items.value.filter((item) => item.id !== id)
}

function schedule(item: MessageItem): void {
  if (item.duration <= 0) {
    return
  }

  timers.set(item.id, setTimeout(() => dismiss(item.id), item.duration))
}

function push(tone: MessageTone, text: string, options: MessageOptions = {}): number {
  const content = text.trim()

  if (!content) {
    return 0
  }

  const duration = options.duration ?? defaultDuration[tone]
  const existing = items.value.find((item) => item.tone === tone && item.text === content)

  if (existing) {
    const timer = timers.get(existing.id)

    if (timer) {
      clearTimeout(timer)
      timers.delete(existing.id)
    }

    existing.duration = duration
    existing.action = options.action
    schedule(existing)
    return existing.id
  }

  if (items.value.length >= maxVisible && items.value[0]) {
    dismiss(items.value[0].id)
  }

  const item: MessageItem = {
    id: ++sequence,
    tone,
    text: content,
    duration,
    action: options.action
  }

  items.value.push(item)
  schedule(item)
  return item.id
}

function clear(): void {
  for (const timer of timers.values()) {
    clearTimeout(timer)
  }

  timers.clear()
  items.value = []
}

export const toast = {
  success: (text: string, options?: MessageOptions) => push('success', text, options),
  error: (text: string, options?: MessageOptions) => push('error', text, options),
  info: (text: string, options?: MessageOptions) => push('info', text, options),
  dismiss,
  clear
}

export function useMessages() {
  return {
    messages: readonly(items),
    dismiss,
    clear
  }
}
