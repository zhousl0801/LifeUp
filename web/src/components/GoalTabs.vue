<script setup lang="ts">
import { computed, useTemplateRef } from 'vue'
import { useRoute } from 'vue-router'
import { useSlidingIndicator } from '../composables/useSlidingIndicator'

const route = useRoute()
const trackRef = useTemplateRef<HTMLElement>('track')
const isCapital = computed(() => route.name === 'capital')
const isPlans = computed(() => route.name === 'plans')
const activeTab = computed(() => (isPlans.value ? 'plans' : isCapital.value ? 'capital' : ''))

const { ready: pillReady, animated: pillAnimated, indicatorStyle } = useSlidingIndicator(
  trackRef,
  activeTab,
  { memoryKey: 'goal-tabs' }
)
</script>

<template>
  <nav ref="track" class="goal-tabs" :class="{ 'has-pill': pillReady }" aria-label="目标页分段">
    <span
      v-if="pillReady"
      class="goal-tabs-pill"
      :class="{ 'is-animated': pillAnimated }"
      :style="indicatorStyle"
      aria-hidden="true"
    />
    <RouterLink class="goal-tab" :class="{ active: isCapital }" to="/capital">资本积累</RouterLink>
    <RouterLink class="goal-tab" :class="{ active: isPlans }" to="/plans">自动记账</RouterLink>
  </nav>
</template>

<style scoped>
.goal-tabs {
  position: relative;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  margin-bottom: 14px;
  padding: 4px;
  border: 1px solid var(--color-surface-border);
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
}

.goal-tabs-pill {
  position: absolute;
  top: 4px;
  bottom: 4px;
  left: 0;
  border-radius: var(--radius-pill);
  background: var(--goal-pill-bg);
  box-shadow: var(--shadow-card);
  transform-origin: left center;
  pointer-events: none;
}

.goal-tabs-pill.is-animated {
  transition: transform var(--motion-segment) var(--ease-move);
}

.goal-tab {
  position: relative;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  border-radius: var(--radius-pill);
  color: var(--color-text-secondary);
  font-size: 14px;
  font-weight: 500;
  text-decoration: none;
  transition: color var(--motion-feedback) var(--ease-move);
}

.goal-tab.active {
  background: var(--goal-pill-bg);
  box-shadow: var(--shadow-card);
  color: var(--goal-pill-fg);
  font-weight: 600;
}

.goal-tabs.has-pill .goal-tab.active {
  background: transparent;
  box-shadow: none;
}

@media (prefers-reduced-motion: reduce) {
  .goal-tabs-pill.is-animated {
    transition: none;
  }

  .goal-tab {
    transition-duration: var(--motion-reduced-fade);
  }
}
</style>
