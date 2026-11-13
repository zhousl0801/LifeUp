<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const isCapital = computed(() => route.name === 'capital')
const isPlans = computed(() => route.name === 'plans')
</script>

<template>
  <nav class="goal-tabs" aria-label="目标页分段">
    <RouterLink class="goal-tab" :class="{ active: isCapital }" to="/capital">资本积累</RouterLink>
    <RouterLink class="goal-tab" :class="{ active: isPlans }" to="/plans">自动计划</RouterLink>
  </nav>
</template>

<style scoped>
.goal-tabs {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  margin-bottom: 14px;
  padding: 4px;
  border-radius: var(--radius-pill);
  background: var(--color-border-subtle);
}

.goal-tab {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  border-radius: var(--radius-pill);
  color: var(--color-text-secondary);
  font-size: 14px;
  font-weight: 500;
  text-decoration: none;
}

.goal-tab.active {
  background: var(--color-bg-surface);
  box-shadow: 0 2px 8px rgba(30, 27, 56, 0.08);
  color: var(--color-brand);
  font-weight: 600;
}
</style>
