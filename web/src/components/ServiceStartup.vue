<script setup lang="ts">
import { onBeforeUnmount, onMounted, shallowRef } from 'vue'
import { ApiError, readOnlyRequest } from '../api/client'

const emit = defineEmits<{ ready: [] }>()
const state = shallowRef<'checking' | 'waiting' | 'unavailable'>('checking')
const waitingDelayMs = 650
const retryDelayMs = 4_000
const connectionDeadlineMs = 120_000

let connection: AbortController | undefined
let waitingTimer: number | undefined
let retryTimer: number | undefined
let deadlineTimer: number | undefined

function stopChecking(): void {
  window.clearTimeout(waitingTimer)
  window.clearTimeout(retryTimer)
  window.clearTimeout(deadlineTimer)
  connection?.abort()
  connection = undefined
}

function showUnavailable(): void {
  stopChecking()
  state.value = 'unavailable'
}

async function checkService(signal: AbortSignal): Promise<void> {
  try {
    const health = await readOnlyRequest<{ status: string }>('/api/health', {
      headers: { Accept: 'application/json' },
      cache: 'no-store',
      signal
    })
    if (signal.aborted) return
    if (health?.status === 'ok') {
      stopChecking()
      emit('ready')
      return
    }
  } catch (error) {
    if (signal.aborted) return
    if (error instanceof ApiError && error.status >= 400 && error.status < 500
      && error.status !== 408 && error.status !== 429) {
      showUnavailable()
      return
    }
  }

  retryTimer = window.setTimeout(() => void checkService(signal), retryDelayMs)
}

function connect(): void {
  if (connection) return

  connection = new AbortController()
  if (state.value === 'checking') {
    waitingTimer = window.setTimeout(() => { state.value = 'waiting' }, waitingDelayMs)
  } else {
    state.value = 'waiting'
  }
  deadlineTimer = window.setTimeout(showUnavailable, connectionDeadlineMs)
  void checkService(connection.signal)
}

onMounted(connect)
onBeforeUnmount(stopChecking)
</script>

<template>
  <Transition name="content-fade">
    <main v-if="state !== 'checking'" class="page service-startup">
      <Transition name="content-fade" mode="out-in">
        <div :key="state" class="startup-status">
          <div role="status">
            <span v-if="state === 'waiting'" class="loading-orbit startup-orbit" aria-hidden="true" />
            <h1 class="startup-title">{{ state === 'waiting' ? '正在连接服务' : '暂时无法连接' }}</h1>
            <p class="startup-detail">
              {{ state === 'waiting' ? '连接后会自动加载。' : '请检查网络，或稍后再试。' }}
            </p>
          </div>
          <button v-if="state === 'unavailable'" class="secondary-button" type="button" @click="connect">
            重新连接
          </button>
        </div>
      </Transition>
    </main>
  </Transition>
</template>

<style scoped>
.service-startup {
  display: grid;
  place-items: center;
  text-align: center;
}

.startup-status {
  display: grid;
  justify-items: center;
  gap: var(--space-4);
}

.startup-orbit {
  display: block;
  width: 36px;
  height: 36px;
  margin: 0 auto var(--space-5);
}

.startup-title {
  margin: 0;
  font-size: var(--text-title3);
  font-weight: 600;
}

.startup-detail {
  max-width: 22em;
  margin: var(--space-2) auto 0;
  color: var(--color-text-secondary);
  font-size: var(--text-subhead);
}
</style>
