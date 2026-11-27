<script setup lang="ts">
import { computed, shallowRef, useId } from 'vue'
import type { FireExpenseCategory, FireOverview } from '../types/capital'
import { formatCapitalAmount } from '../utils/capital'

const props = defineProps<{
  fire?: FireOverview | null
  categories: FireExpenseCategory[]
  leanCategories: string[] | null
  excludedCategories: string[]
  amountsMasked: boolean
  standardError?: string
  leanError?: string
}>()

const standardOverride = defineModel<string>('standardOverride', { required: true })
const leanOverride = defineModel<string>('leanOverride', { required: true })
const emit = defineEmits<{
  cycle: [category: FireExpenseCategory]
  reset: []
}>()

const open = shallowRef(false)
const bodyId = useId()
const manualOverride = computed(() => Boolean(standardOverride.value.trim() || leanOverride.value.trim()))
const leanNames = computed(() => new Set(props.leanCategories ?? props.categories
  .filter((category) => category.isDefaultLean)
  .map((category) => category.name)))
const excludedNames = computed(() => new Set(props.excludedCategories))
const stateLabels = { lean: 'Lean 刚性', standard: '仅标准', excluded: '已排除' }
const categories = computed(() => props.categories.map((category) => {
  const state: keyof typeof stateLabels = excludedNames.value.has(category.name)
    ? 'excluded'
    : leanNames.value.has(category.name) ? 'lean' : 'standard'
  return { category, state }
}))

function money(value: number): string {
  return formatCapitalAmount(value, props.amountsMasked)
}
</script>

<template>
  <section class="calibration">
    <button
      type="button"
      class="accordion"
      :aria-expanded="open"
      :aria-controls="bodyId"
      @click="open = !open"
    >
      <span>支出分类校准</span>
      <span v-if="manualOverride" class="summary badge">手动覆写</span>
      <span v-else-if="fire" class="summary">标准 {{ money(fire.standard.monthlyExpense) }}/月 · Lean {{ money(fire.lean.monthlyExpense) }}/月</span>
      <svg class="chevron" viewBox="0 0 16 16" width="16" height="16" aria-hidden="true">
        <path d="m4 6 4 4 4-4" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
      </svg>
    </button>

    <div v-if="open" :id="bodyId" class="body">
      <div class="legend" aria-label="支出分类状态">
        <span data-state="lean">绿：Lean 刚性</span>
        <span data-state="standard">灰：仅标准</span>
        <span data-state="excluded">划线：已排除</span>
      </div>
      <div v-if="categories.length" class="category-grid">
        <button
          v-for="{ category, state } in categories"
          :key="category.name"
          type="button"
          class="category"
          :data-state="state"
          :aria-label="`${category.name}，${stateLabels[state]}`"
          @click="emit('cycle', category)"
        >
          {{ category.name }}
          <span v-if="category.monthlyAverage > 0">{{ money(category.monthlyAverage) }}</span>
        </button>
      </div>
      <p v-else class="hint">记账后可按分类调整支出基准。</p>
      <button type="button" class="reset" @click="emit('reset')">恢复按固定+必要分组</button>

      <label class="override">
        <span>标准退休月支出（选填）</span>
        <input
          v-model="standardOverride" :class="{ 'private-amount': amountsMasked }"
          aria-label="标准退休月支出" :aria-invalid="Boolean(standardError)"
          inputmode="decimal" placeholder="按账单" autocomplete="off"
        />
      </label>
      <label class="override">
        <span>Lean 退休月支出（选填）</span>
        <input
          v-model="leanOverride" :class="{ 'private-amount': amountsMasked }"
          aria-label="Lean 退休月支出" :aria-invalid="Boolean(leanError)"
          inputmode="decimal" placeholder="按账单" autocomplete="off"
        />
      </label>
    </div>
    <p v-if="standardError" class="field-error">标准退休月支出：{{ standardError }}</p>
    <p v-if="leanError" class="field-error">Lean 退休月支出：{{ leanError }}</p>
  </section>
</template>

<style scoped>
.calibration {
  margin-bottom: 10px;
  border-radius: 16px;
  background: var(--color-bg-surface);
}

.accordion {
  display: grid;
  grid-template-columns: auto 1fr auto;
  align-items: center;
  gap: 6px;
  width: 100%;
  min-height: var(--touch-target);
  padding: 10px 14px;
  border: 0;
  border-radius: inherit;
  background: transparent;
  color: var(--color-text-primary);
  font-size: var(--text-footnote);
  font-weight: 600;
  text-align: left;
}

.summary {
  justify-self: end;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 400;
  text-align: right;
}

.summary.badge {
  padding: 3px 6px;
  border-radius: var(--radius-tag);
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.chevron {
  transition: transform var(--motion-feedback) var(--ease-move);
}

.accordion[aria-expanded='true'] .chevron {
  transform: rotate(180deg);
}

.body {
  padding: 10px 14px 4px;
  border-top: 1px solid var(--color-border-subtle);
}

.legend,
.category-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  font-size: var(--text-caption);
}

.legend {
  margin-bottom: 8px;
}

.legend span,
.category {
  padding: 4px 8px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
}

.category {
  min-height: var(--touch-target);
  font-size: var(--text-footnote);
}

[data-state='lean'] {
  background: var(--color-tint-emerald-bg);
  color: var(--color-tint-emerald-fg);
}

[data-state='excluded'] {
  color: var(--color-text-secondary);
  text-decoration: line-through;
}

.reset {
  min-height: var(--touch-target);
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-caption);
}

.override {
  display: grid;
  grid-template-columns: minmax(0, auto) minmax(0, 1fr);
  align-items: center;
  gap: 8px;
  min-height: var(--touch-target);
  border-top: 1px solid var(--color-border-subtle);
  font-size: var(--text-footnote);
}

.override input {
  min-width: 0;
  width: 100%;
  min-height: var(--touch-target);
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  font: inherit;
  font-variant-numeric: tabular-nums;
  text-align: right;
}

.override .private-amount:not(:focus) {
  -webkit-text-security: disc;
}

.hint,
.field-error {
  font-size: var(--text-caption);
}

.hint {
  color: var(--color-text-secondary);
}

.field-error {
  margin: 0;
  padding: 0 14px 8px;
  color: var(--color-error);
}

@media (prefers-reduced-motion: reduce) {
  .chevron {
    transition: none;
  }
}
</style>
