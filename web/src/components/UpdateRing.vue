<script setup lang="ts">
import { computed } from 'vue'
import type { AmountTone } from '../utils/format'

const props = withDefaults(defineProps<{
  value: number
  total: number
  size?: number
  indeterminate?: boolean
  profitTone?: AmountTone
}>(), {
  size: 18,
  indeterminate: false
})

const radius = 8
const circumference = 2 * Math.PI * radius
const safeTotal = computed(() => Math.max(0, props.total))
const safeValue = computed(() => Math.max(0, Math.min(props.value, safeTotal.value)))
const progress = computed(() => safeTotal.value > 0 ? safeValue.value / safeTotal.value : 0)
const dashOffset = computed(() => circumference * (1 - progress.value))
const profitTone = computed(() => props.indeterminate ? '' : props.profitTone)
const profitText = computed(() => {
  if (!profitTone.value) {
    return ''
  }

  return profitTone.value === 'amount-gain' ? '，收益为正' : '，收益为负'
})
const valueText = computed(() => props.indeterminate
  ? '正在更新'
  : `${safeValue.value}/${safeTotal.value}${profitText.value}`)
</script>

<template>
  <svg
    class="update-ring"
    :class="{ 'is-indeterminate': indeterminate }"
    :width="size"
    :height="size"
    viewBox="0 0 20 20"
    role="progressbar"
    aria-label="今日净值更新进度"
    :aria-valuenow="indeterminate ? undefined : safeValue"
    aria-valuemin="0"
    :aria-valuemax="safeTotal"
    :aria-valuetext="valueText"
    :aria-busy="indeterminate ? 'true' : undefined"
  >
    <g transform="rotate(-90 10 10)">
      <circle class="update-ring-track" cx="10" cy="10" :r="radius" />
      <circle
        class="update-ring-value"
        cx="10"
        cy="10"
        :r="radius"
        :style="indeterminate
          ? undefined
          : {
            strokeDasharray: `${circumference} ${circumference}`,
            strokeDashoffset: dashOffset
          }"
      />
    </g>
    <g v-if="profitTone" class="update-ring-face" :class="profitTone" aria-hidden="true">
      <path d="M7 8h.1M13 8h.1" />
      <path v-if="profitTone === 'amount-gain'" d="M7 11.5q3 4 6 0" />
      <path v-else d="M7 13.5q3-4 6 0M14.5 10.5v1" />
    </g>
  </svg>
</template>

<style scoped>
.update-ring {
  display: block;
  flex: none;
  color: var(--update-ring-color, currentColor);
}

.update-ring-track,
.update-ring-value {
  fill: none;
  stroke-width: 2;
}

.update-ring-face {
  fill: none;
  stroke: currentColor;
  stroke-width: 1.5;
  stroke-linecap: round;
}

.update-ring-track {
  stroke: currentColor;
  opacity: 0.22;
}

.update-ring-value {
  stroke: currentColor;
  stroke-linecap: round;
  transition: stroke-dashoffset var(--motion-base) var(--ease-standard);
}

.update-ring.is-indeterminate .update-ring-value {
  stroke-dasharray: 14 50;
  stroke-dashoffset: 0;
  transform-origin: 10px 10px;
  animation: update-ring-spin 900ms linear infinite;
}

@keyframes update-ring-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .update-ring-value {
    transition: none;
  }

  .update-ring.is-indeterminate .update-ring-value {
    animation: none;
  }
}
</style>
