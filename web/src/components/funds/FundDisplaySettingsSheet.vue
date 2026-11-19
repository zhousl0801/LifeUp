<script setup lang="ts">
import BottomSheet from '../BottomSheet.vue'

export type FundMetricKey = 'daily' | 'value' | 'cumulative' | 'cumulativeRate' | 'cost' | 'weight' | 'pending'

defineProps<{
  open: boolean
  searchText: string
  metricKeys: FundMetricKey[]
  metricOptions: { key: FundMetricKey; label: string }[]
  metricNotice: string
}>()

const emit = defineEmits<{
  close: []
  'update:searchText': [value: string]
  toggleMetric: [key: FundMetricKey]
}>()
</script>

<template>
  <BottomSheet
    :open="open"
    title="搜索与指标"
    subtitle="只影响当前查看，不改变持仓"
    @close="emit('close')"
  >
    <div class="portfolio-toolbar">
      <input
        :value="searchText"
        type="search"
        placeholder="搜索基金名称或代码"
        aria-label="搜索基金"
        @input="emit('update:searchText', ($event.target as HTMLInputElement).value)"
      />
    </div>

    <div class="metric-picker" aria-label="持仓指标">
      <span>显示指标</span>
      <button
        v-for="option in metricOptions"
        :key="option.key"
        type="button"
        :class="{ active: metricKeys.includes(option.key) }"
        @click="emit('toggleMetric', option.key)"
      >
        {{ option.label }}
      </button>
    </div>
    <p v-if="metricNotice" class="summary-note">{{ metricNotice }}</p>
  </BottomSheet>
</template>

<style scoped>
.portfolio-toolbar input {
  width: 100%;
  min-height: var(--touch-target);
  padding: 0 14px;
  border: 1px solid transparent;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  font-size: var(--text-body);
}

.portfolio-toolbar input:focus-visible {
  border-color: var(--color-border-focus);
  background: var(--color-bg-surface);
}

.metric-picker {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  margin-top: 10px;
}

.metric-picker > span {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.metric-picker button {
  min-height: 40px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.metric-picker button.active {
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-weight: 600;
}

.summary-note {
  margin: 14px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
  text-align: center;
}
</style>
