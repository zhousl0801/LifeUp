<script setup lang="ts">
import { computed, useTemplateRef } from 'vue'
import { useRoute } from 'vue-router'
import { useSlidingIndicator } from '../composables/useSlidingIndicator'
import AppIcon from './AppIcon.vue'

const route = useRoute()
const tabsRef = useTemplateRef<HTMLElement>('tabs')
const isGoal = computed(() => route.name === 'capital' || route.name === 'plans')
const isMine = computed(() => route.name === 'mine' || route.name === 'salary')
const activeTab = computed(() => {
  if (route.name === 'ledger') return 'ledger'
  if (route.name === 'home') return 'home'
  if (isGoal.value) return 'goal'
  if (isMine.value) return 'mine'
  return ''
})

const { ready: pillReady, animated: pillAnimated, indicatorStyle } = useSlidingIndicator(
  tabsRef,
  activeTab,
  { memoryKey: 'app-nav' }
)
</script>

<template>
  <nav class="app-nav" aria-label="主导航">
    <div ref="tabs" class="nav-tabs" :class="{ 'has-pill': pillReady }">
      <span
        v-if="pillReady"
        class="nav-pill"
        :class="{ 'is-animated': pillAnimated }"
        :style="indicatorStyle"
        aria-hidden="true"
      />
      <RouterLink class="nav-item" active-class="active" to="/ledger">
        <span class="nav-icon-slot" aria-hidden="true">
          <svg class="nav-icon nav-icon-outline" viewBox="0 0 24 24"><rect x="5" y="3" width="15" height="18" rx="2" /><path d="M3 7h5M3 12h5M3 17h5M11 8h6M11 12h6" /></svg>
          <svg class="nav-icon nav-icon-filled" viewBox="0 0 24 24"><path d="M6 3h12a3 3 0 0 1 3 3v12a3 3 0 0 1-3 3H6a3 3 0 0 1-3-3V6a3 3 0 0 1 3-3Z" fill="currentColor" /><path class="knockout-line" d="M7.5 8h9M7.5 12h9M7.5 16h5.5" /></svg>
        </span>
        <span>账单</span>
      </RouterLink>
      <RouterLink class="nav-item" exact-active-class="active" to="/">
        <span class="nav-icon-slot" aria-hidden="true">
          <svg class="nav-icon nav-icon-outline" viewBox="0 0 24 24"><path d="m3 10 9-7 9 7v10H3Z" /><path d="M9 20v-7h6v7" /></svg>
          <svg class="nav-icon nav-icon-filled" viewBox="0 0 24 24"><path d="M12 3 2.5 10.5V21h7v-6h5v6h7V10.5Z" fill="currentColor" /><path class="knockout" d="M10 15h4v6h-4z" /></svg>
        </span>
        <span>总览</span>
      </RouterLink>
      <RouterLink class="nav-item" :class="{ active: isGoal }" to="/capital">
        <span class="nav-icon-slot" aria-hidden="true">
          <svg class="nav-icon" viewBox="0 0 24 24"><circle cx="12" cy="12" r="8" /><circle cx="12" cy="12" r="4" /><circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" /></svg>
        </span>
        <span>目标</span>
      </RouterLink>
      <RouterLink class="nav-item" :class="{ active: isMine }" to="/mine">
        <span class="nav-icon-slot" aria-hidden="true">
          <svg class="nav-icon nav-icon-outline" viewBox="0 0 24 24"><circle cx="12" cy="8" r="4" /><path d="M4 21v-2a8 8 0 0 1 16 0v2" /></svg>
          <svg class="nav-icon nav-icon-filled" viewBox="0 0 24 24"><circle cx="12" cy="8" r="4.25" fill="currentColor" /><path d="M4 21v-1.6a8 8 0 0 1 16 0V21Z" fill="currentColor" /></svg>
        </span>
        <span>我的</span>
      </RouterLink>
    </div>
    <RouterLink class="nav-add" to="/entry" aria-label="记一笔" title="记一笔">
      <AppIcon name="plus" />
    </RouterLink>
  </nav>
</template>

<style scoped>
.app-nav {
  position: fixed;
  bottom: calc(9px + env(safe-area-inset-bottom));
  left: 50%;
  z-index: 20;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 58px;
  gap: 8px;
  width: min(calc(100% - 24px), 406px);
  transform: translateX(-50%);
  pointer-events: none;
}

.nav-tabs,
.nav-add {
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-pill);
  background: linear-gradient(160deg,
    color-mix(in srgb, var(--color-bg-surface) 96%, transparent),
    color-mix(in srgb, var(--color-bg-surface) 88%, transparent));
  box-shadow: inset 0 1px 0 color-mix(in srgb, #ffffff 18%, transparent), var(--shadow-card);
  pointer-events: auto;
}

.nav-tabs {
  position: relative;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  padding: 4px;
}

.nav-pill {
  position: absolute;
  top: 4px;
  bottom: 4px;
  left: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  transform-origin: left center;
  pointer-events: none;
}

.nav-pill.is-animated {
  transition: transform var(--motion-segment) var(--ease-move);
}

.nav-item {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  gap: 4px;
  min-width: 0;
  min-height: 48px;
  padding: 0;
  border-radius: var(--radius-pill);
  color: var(--color-text-secondary);
  font-size: 0.6875rem;
  font-weight: 500;
  text-decoration: none;
  transition:
    color var(--motion-feedback) var(--ease-move),
    transform var(--motion-press-out) var(--ease-settle);
}

.nav-item:active {
  transform: scale(0.96);
  transition-duration: var(--motion-press-in);
}

.nav-icon-slot {
  position: relative;
  display: grid;
  place-items: center;
  width: 23px;
  height: 23px;
}

.nav-icon {
  grid-area: 1 / 1;
  width: 23px;
  height: 23px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.7;
  stroke-linecap: round;
  stroke-linejoin: round;
  transition: opacity var(--motion-feedback) var(--ease-move);
}

.nav-icon-filled {
  opacity: 0;
  stroke: none;
}

.nav-icon-filled .knockout {
  fill: var(--color-bg-surface);
}

.nav-icon-filled .knockout-line {
  fill: none;
  stroke: var(--color-bg-surface);
  stroke-width: 2;
  stroke-linecap: round;
  stroke-linejoin: round;
}

@media (hover: hover) and (pointer: fine) {
  .nav-item:hover {
    color: var(--color-brand);
  }
}

.nav-item.active {
  color: var(--color-brand);
  font-weight: 600;
  background: var(--color-brand-soft);
}

.nav-tabs.has-pill .nav-item.active {
  background: transparent;
}

.nav-item.active .nav-icon-outline {
  opacity: 0;
}

.nav-item.active .nav-icon-filled {
  opacity: 1;
}

.nav-add {
  display: grid;
  place-items: center;
  border-color: transparent;
  background: linear-gradient(145deg, var(--color-accent-mint), var(--color-accent-mint-strong));
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.45), 0 10px 24px color-mix(in srgb, var(--color-accent-mint-strong) 28%, transparent);
  color: var(--color-on-accent-mint);
  transition:
    transform var(--motion-press-out) var(--ease-settle),
    box-shadow var(--motion-feedback) var(--ease-move);
}

.nav-add :deep(.app-icon) {
  width: 30px;
  height: 30px;
}

@media (hover: hover) and (pointer: fine) {
  .nav-add:hover {
    box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.55), 0 12px 26px color-mix(in srgb, var(--color-accent-mint-strong) 34%, transparent);
  }
}

.nav-add:active {
  transform: scale(0.95);
  transition-duration: var(--motion-press-in);
}

@media (prefers-reduced-motion: reduce) {
  .nav-pill.is-animated {
    transition: none;
  }

  .nav-item,
  .nav-add {
    transition: color var(--motion-reduced-fade) var(--ease-linear);
  }

  .nav-item:active,
  .nav-add:active {
    transform: none;
  }

  .nav-icon {
    transition-duration: var(--motion-reduced-fade);
  }
}
</style>
