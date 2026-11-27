<script setup lang="ts">
import { useRegisterSW } from 'virtual:pwa-register/vue'
import { activeWriteCount } from '../api/client'
import { toast } from '../composables/useMessage'
import { useRouteMotion } from '../utils/routeMotion'

const { needRefresh, updateServiceWorker } = useRegisterSW({ immediate: true })
const { hasOpenSheet } = useRouteMotion()

function applyUpdate(): void {
  if (activeWriteCount.value > 0 || hasOpenSheet.value) {
    toast.info('当前有正在保存或编辑的内容，完成后再更新更稳妥')
    return
  }
  void updateServiceWorker(true)
}
</script>

<template>
  <Transition name="banner-fade">
    <aside v-if="needRefresh" class="update-banner" role="status">
      <span>新版本已就绪</span>
      <button type="button" class="update-action" @click="applyUpdate">更新应用</button>
      <button type="button" class="update-dismiss" @click="needRefresh = false">稍后</button>
    </aside>
  </Transition>
</template>

<style scoped>
.update-banner {
  position: fixed;
  right: 0;
  bottom: calc(80px + env(safe-area-inset-bottom));
  left: 0;
  z-index: 45;
  display: flex;
  align-items: center;
  gap: 8px;
  box-sizing: border-box;
  width: min(100%, 430px);
  margin: 0 auto;
  padding: 10px 14px;
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: 0 8px 24px var(--color-shadow);
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.update-banner span {
  flex: 1;
  min-width: 0;
}

.update-action,
.update-dismiss {
  min-height: var(--touch-target);
  padding: 0 12px;
  border: 0;
  border-radius: var(--radius-pill);
  font-size: var(--text-footnote);
}

.update-action {
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-weight: 600;
}

.update-dismiss {
  background: transparent;
  color: var(--color-text-secondary);
}

@media (min-width: 600px) {
  .update-banner {
    bottom: calc(100px + env(safe-area-inset-bottom));
  }
}
</style>
