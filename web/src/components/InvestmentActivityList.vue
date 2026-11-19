<script setup lang="ts">
export interface InvestmentActivityRow {
  key: string
  label: string
  labelTone?: 'buy' | 'neutral'
  typeClass?: string
  title: string
  meta?: string
  time?: string
  amount: string
  amountTone?: string
  status?: string
  statusTone?: 'pending' | 'done' | 'reversed'
  isPending?: boolean
  clickable?: boolean
}

defineProps<{
  rows: InvestmentActivityRow[]
  stagger?: boolean
}>()

const emit = defineEmits<{
  select: [row: InvestmentActivityRow]
}>()
</script>

<template>
  <section class="activity-list">
    <div
      v-for="(row, index) in rows"
      :key="row.key"
      class="activity-row"
      :class="{
        'stagger-item': stagger,
        'is-pending': row.isPending,
        'is-clickable': row.clickable
      }"
      :style="stagger ? { '--stagger': Math.min(index, 8) } : undefined"
      :role="row.clickable ? 'button' : undefined"
      :tabindex="row.clickable ? 0 : undefined"
      @click="row.clickable && emit('select', row)"
      @keydown.enter="row.clickable && emit('select', row)"
    >
      <span class="activity-type" :class="[row.typeClass, { 'type-buy': row.labelTone === 'buy' }]">{{ row.label }}</span>
      <span class="activity-main">
        <strong>{{ row.title }}</strong>
        <small v-if="row.meta" class="activity-meta">{{ row.meta }}</small>
        <small v-if="row.time" class="activity-time">{{ row.time }}</small>
      </span>
      <span class="activity-side">
        <b :class="row.amountTone">{{ row.amount }}</b>
        <small
          v-if="row.status"
          :class="
            row.statusTone === 'pending'
              ? 'status-pending'
              : row.statusTone === 'reversed'
                ? 'status-reversed'
                : 'status-done'
          "
        >
          {{ row.status }}
        </small>
      </span>
    </div>
  </section>
</template>

<style scoped>
.activity-list {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.activity-row {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  box-sizing: border-box;
  width: 100%;
  padding: 14px 16px;
  border-bottom: 1px solid var(--color-border-subtle);
  text-align: left;
}

.activity-row:last-child {
  border-bottom: 0;
}

.activity-row.is-clickable {
  cursor: pointer;
}

.activity-row.is-clickable:active {
  background: var(--color-brand-soft);
}

.activity-type {
  flex: 0 0 34px;
  padding-top: 2px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.activity-type.type-sip,
.activity-type.type-buy {
  color: var(--color-finance-loss);
}

.activity-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
  min-width: 0;
}

.activity-main strong {
  font-size: 0.875rem;
  font-weight: 500;
  line-height: 1.45;
  overflow-wrap: anywhere;
}

.activity-main small {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
  font-variant-numeric: tabular-nums;
}

.activity-meta {
  overflow-wrap: anywhere;
}

.activity-time {
  white-space: nowrap;
}

.activity-side {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
  flex: 0 0 auto;
  padding-top: 1px;
}

.activity-side b {
  font-size: 0.875rem;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.activity-side small {
  font-size: var(--text-caption);
}

.status-pending {
  color: var(--color-brand);
}

.status-done {
  color: var(--color-text-secondary);
}

.status-reversed {
  color: var(--color-error);
}

.income {
  color: var(--color-finance-income);
}

.transfer {
  color: var(--color-text-primary);
}
</style>
