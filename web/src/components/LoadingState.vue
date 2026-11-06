<script setup lang="ts">
withDefaults(defineProps<{
  variant?: 'hero' | 'card' | 'list' | 'chart' | 'text' | 'inline'
  rows?: number
  label?: string
  compact?: boolean
}>(), {
  variant: 'card',
  rows: 3,
  label: '正在加载',
  compact: false
})
</script>

<template>
  <div
    class="loading-state"
    :class="[`is-${variant}`, { 'is-compact': compact }]"
    role="status"
    aria-busy="true"
    :aria-label="label"
  >
    <template v-if="variant === 'hero'">
      <span class="loading-block loading-line short" />
      <span class="loading-block loading-amount" />
      <span class="loading-block loading-chart" />
    </template>

    <template v-else-if="variant === 'chart'">
      <span class="loading-block loading-chart" />
    </template>

    <template v-else-if="variant === 'text'">
      <span class="loading-block loading-line" />
    </template>

    <template v-else-if="variant === 'inline'">
      <span class="loading-orbit" aria-hidden="true" />
      <span class="loading-block loading-line inline" />
    </template>

    <template v-else-if="variant === 'list'">
      <span v-for="index in rows" :key="index" class="loading-block loading-row" />
    </template>

    <template v-else>
      <span class="loading-block loading-card">
        <span class="loading-line short" />
        <span class="loading-line amount" />
        <span class="loading-line" />
      </span>
    </template>
  </div>
</template>
