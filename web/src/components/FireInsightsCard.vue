<script setup lang="ts">
import type { FireInsights } from '../types/capital'
import { maskInsightAmounts } from '../utils/capital'

defineProps<{
  insights: FireInsights
  expenseWindowMonths: number
  amountsMasked: boolean
}>()

const stageTones: Record<string, string> = {
  起步期: 'sky',
  筑基期: 'violet',
  加速期: 'amber',
  半自由期: 'emerald',
  冲刺期: 'emerald',
  已自由: 'emerald'
}

const suggestionTones: Record<string, string> = {
  支出杠杆: 'amber',
  阶段捷径: 'emerald',
  'Coast 捷径': 'sky',
  收益试算: 'violet',
  高储蓄率: 'emerald'
}
</script>

<template>
  <section class="fire-insights" aria-label="自由加速建议">
    <header>
      <span class="badge" :data-tone="stageTones[insights.stage]">{{ insights.stage }}</span>
      <span class="caption">{{ expenseWindowMonths > 0 ? `近 ${expenseWindowMonths} 月账单` : '基于当前假设' }}</span>
    </header>
    <p class="headline">{{ maskInsightAmounts(insights.headline, amountsMasked) }}</p>
    <div v-if="insights.coveredCategories.length" class="covered">
      <span class="caption">已覆盖：</span>
      <span v-for="category in insights.coveredCategories" :key="category" class="badge" data-tone="emerald">{{ category }}</span>
    </div>
    <ul v-if="insights.suggestions.length" class="suggestions">
      <li v-for="(suggestion, index) in insights.suggestions.slice(0, 3)" :key="`${suggestion.badge}-${index}`">
        <span class="badge" :data-tone="suggestionTones[suggestion.badge]">{{ suggestion.badge }}</span>
        <span>{{ maskInsightAmounts(suggestion.text, amountsMasked) }}</span>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.fire-insights {
  margin-top: 14px;
  padding: var(--card-padding);
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
  font-variant-numeric: tabular-nums;
}

header,
.covered {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

header {
  justify-content: space-between;
}

.caption {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.headline {
  margin: 10px 0 0;
  font-size: 0.875rem;
  line-height: 1.6;
}

.covered {
  margin-top: 10px;
}

.badge {
  display: inline-block;
  padding: 3px 8px;
  border-radius: var(--radius-tag);
  background: var(--badge-bg, var(--color-brand-soft));
  color: var(--badge-fg, var(--color-brand));
  font-size: var(--text-caption);
  font-weight: 600;
  white-space: nowrap;
}

[data-tone='sky'] {
  --badge-bg: var(--color-tint-sky-bg);
  --badge-fg: var(--color-tint-sky-fg);
}

[data-tone='violet'] {
  --badge-bg: var(--color-tint-violet-bg);
  --badge-fg: var(--color-tint-violet-fg);
}

[data-tone='amber'] {
  --badge-bg: var(--color-tint-amber-bg);
  --badge-fg: var(--color-tint-amber-fg);
}

[data-tone='emerald'] {
  --badge-bg: var(--color-tint-emerald-bg);
  --badge-fg: var(--color-tint-emerald-fg);
}

.suggestions {
  display: grid;
  gap: 8px;
  margin: 12px 0 0;
  padding: 0;
  list-style: none;
}

.suggestions li {
  display: flex;
  align-items: start;
  gap: 8px;
  padding: 10px 12px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  font-size: var(--text-footnote);
  line-height: 1.5;
}
</style>
