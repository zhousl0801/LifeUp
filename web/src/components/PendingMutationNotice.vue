<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { getApiErrorMessage, pendingMutationCount, readPendingMutations, recoverMutation } from '../api/client'
import { toast } from '../composables/useMessage'

const isChecking = ref(false)
const message = ref('')
const hasPending = computed(() => pendingMutationCount.value > 0)

function reloadPending(): void {
  readPendingMutations()
}

async function autoVerify(): Promise<void> {
  if (isChecking.value || !hasPending.value) return
  isChecking.value = true
  try {
    let resolved = 0
    for (const request of readPendingMutations()) {
      try {
        await recoverMutation(request, false)
        resolved++
      } catch {
        // 仍未查到回执时保留在本地，交由后续联网自动核对或用户手动重试。
      }
    }
    if (resolved > 0 && pendingMutationCount.value === 0) {
      message.value = ''
      toast.success('刚才的保存已自动确认完成')
    }
  } finally {
    isChecking.value = false
  }
}

function handleVisibilityOrOnline(): void {
  if (document.visibilityState === 'visible' && navigator.onLine !== false) {
    void autoVerify()
  }
}

watch(hasPending, (pending) => {
  if (pending && navigator.onLine !== false) {
    void autoVerify()
  }
})

onMounted(() => {
  reloadPending()
  window.addEventListener('storage', reloadPending)
  window.addEventListener('online', handleVisibilityOrOnline)
  document.addEventListener('visibilitychange', handleVisibilityOrOnline)
  if (hasPending.value && navigator.onLine !== false) {
    void autoVerify()
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('storage', reloadPending)
  window.removeEventListener('online', handleVisibilityOrOnline)
  document.removeEventListener('visibilitychange', handleVisibilityOrOnline)
})

async function check(retry: boolean): Promise<void> {
  if (isChecking.value) return
  isChecking.value = true
  message.value = ''

  try {
    for (const request of readPendingMutations()) {
      await recoverMutation(request, retry)
    }
    toast.success(retry ? '已重新保存成功' : '已确认保存成功')
  } catch (error) {
    message.value = getApiErrorMessage(error, '暂时还没查到结果，请联网后再试。')
  } finally {
    isChecking.value = false
  }
}
</script>

<template>
  <aside v-if="hasPending || message" class="pending-notice" role="status">
    <p>{{ isChecking ? '正在确认刚才那笔记录是否已保存…' : '刚才那笔记录还没确认是否保存，内容已保留，不会重复记账。' }}</p>
    <p v-if="message" class="pending-error">{{ message }}</p>
    <div class="pending-actions">
      <button type="button" :disabled="isChecking" @click="check(false)">立即确认</button>
      <button type="button" :disabled="isChecking" @click="check(true)">重新保存</button>
    </div>
  </aside>
</template>

<style scoped>
.pending-notice {
  position: relative;
  z-index: 60;
  max-width: 430px;
  box-sizing: border-box;
  margin: 0 auto;
  padding: 12px 16px;
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  border-bottom: 1px solid var(--color-border-default);
  font-size: 0.75rem;
}
.pending-notice button {
  min-height: var(--touch-target);
  margin-right: 12px;
  color: var(--color-brand);
}
</style>
