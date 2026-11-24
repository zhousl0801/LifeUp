<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import Decimal from 'decimal.js'
import { fetchMonthlyBudget, updateMonthlyBudget } from '../api/ledger'
import { getApiErrorMessage } from '../api/client'
import { toast } from '../composables/useMessage'
import { formatMoney, parseAmount, sumAmounts } from '../utils/format'
import BottomSheet from './BottomSheet.vue'
import LoadingButton from './LoadingButton.vue'

const props = defineProps<{ month: string; today: string; expense: number | null; compact?: boolean }>()
const budget = ref<number | null>(null)
const loading = ref(true)
const saving = ref(false)
const loadError = ref('')
const formError = ref('')
const open = ref(false)
const draft = ref('')
const parsed = computed(() => parseAmount(draft.value))
const monthLabel = computed(() => `${props.month.slice(0, 4)}年${Number(props.month.slice(5))}月`)
const remaining = computed(() => budget.value !== null && props.expense !== null
  ? sumAmounts([budget.value, -props.expense])
  : null)
const percentage = computed(() => budget.value && props.expense !== null ? props.expense / budget.value * 100 : null)
const progressLabel = computed(() => percentage.value === null ? '' : `${Math.round(percentage.value)}%`)
const dailyAllowance = computed(() => {
  if (remaining.value === null || !props.today.startsWith(props.month + '-')) return null

  const [year, month] = props.month.split('-').map(Number)
  const lastDay = new Date(Date.UTC(year!, month!, 0)).getUTCDate()
  const daysLeft = lastDay - Number(props.today.slice(-2)) + 1
  return new Decimal(Math.max(remaining.value, 0)).div(daysLeft)
})
const budgetAmount = computed(() => '¥' + formatMoney(budget.value))
const compactMoney = new Intl.NumberFormat('zh-CN', { notation: 'compact', maximumFractionDigits: 2 })
const ringAmount = computed(() => budget.value !== null && budget.value >= 100000
  ? '¥' + compactMoney.format(budget.value)
  : budgetAmount.value)
let loadSequence = 0

async function load(): Promise<void> {
  const sequence = ++loadSequence
  loading.value = true
  loadError.value = ''
  try {
    const result = await fetchMonthlyBudget(props.month)
    if (sequence === loadSequence) {
      budget.value = result.amount
    }
  } catch (error) {
    if (sequence === loadSequence) {
      loadError.value = getApiErrorMessage(error, '预算暂时无法读取。')
    }
  } finally {
    if (sequence === loadSequence) {
      loading.value = false
    }
  }
}

function edit(): void {
  draft.value = budget.value?.toFixed(2) ?? ''
  formError.value = ''
  open.value = true
}

async function save(amount: number | null): Promise<void> {
  if (saving.value) return
  saving.value = true
  formError.value = ''
  try {
    budget.value = (await updateMonthlyBudget(props.month, amount)).amount
    open.value = false
    toast.success(amount === null ? '月度预算已取消' : '月度预算已保存')
  } catch (error) {
    formError.value = getApiErrorMessage(error, '预算保存失败，请重试。')
  } finally {
    saving.value = false
  }
}

function submit(): void {
  if (parsed.value === null || parsed.value <= 0) {
    formError.value = '请输入大于零、最多两位小数的预算金额。'
    return
  }

  void save(parsed.value)
}

watch(
  () => props.month,
  () => {
    void load()
  },
  { immediate: true }
)

onBeforeUnmount(() => {
  loadSequence++
})
</script>

<template>
  <div v-if="compact" class="monthly-budget-compact">
    <div v-if="loadError" class="budget-compact-error">
      <small>{{ loadError }}</small>
      <button type="button" class="budget-compact-link" @click="load">重试</button>
    </div>
    <template v-else-if="!loading">
      <button
        v-if="budget !== null"
        type="button"
        class="budget-compact-bar"
        :class="{ over: remaining !== null && remaining < 0 }"
        :aria-label="`更改月度预算，当前 ${budgetAmount}${progressLabel ? '，已用 ' + progressLabel : ''}`"
        @click="edit"
      >
        <span class="budget-compact-track">
          <span class="budget-compact-fill" :style="{ width: `${Math.min(percentage ?? 0, 100)}%` }" />
        </span>
        <span class="budget-compact-text">
          <span>预算已用 {{ progressLabel || '0%' }}</span>
          <small v-if="remaining !== null">{{ remaining < 0 ? '超支' : '剩' }} ¥{{ formatMoney(Math.abs(remaining)) }} ›</small>
        </span>
      </button>
      <button v-else type="button" class="budget-compact-set" @click="edit">设置本月预算 ›</button>
    </template>

    <BottomSheet :open="open" :title="monthLabel + '预算'" subtitle="只应用于这个月。"
      close-icon :close-disabled="saving" @close="open = false">
      <form class="budget-form" @submit.prevent="submit">
        <label for="monthly-budget-amount">预算金额（元）</label>
        <input id="monthly-budget-amount" v-model="draft" type="text" inputmode="decimal"
          maxlength="18" placeholder="输入这个月的预算" :disabled="saving" />
        <p v-if="formError" role="alert">{{ formError }}</p>
        <div class="budget-form-actions" :class="{ 'can-clear': budget !== null }">
          <button v-if="budget !== null" type="button" class="secondary-button" :disabled="saving" @click="save(null)">取消预算</button>
          <LoadingButton type="submit" class="primary-button" :loading="saving">保存</LoadingButton>
        </div>
      </form>
    </BottomSheet>
  </div>

  <div v-else class="monthly-budget">
    <div class="budget-copy">
      <slot />
      <small v-if="remaining !== null" class="budget-remaining" :class="{ over: remaining < 0 }">
        {{ remaining < 0 ? '超出预算' : '剩余预算' }} ¥{{ formatMoney(Math.abs(remaining)) }}
      </small>
      <small v-if="remaining !== null" class="budget-daily">
        每日可花 {{ dailyAllowance === null ? '—' : '¥' + formatMoney(dailyAllowance) }}
      </small>
    </div>
    <div v-if="loadError" class="budget-retry">
      <small>{{ loadError }}</small>
      <button type="button" class="budget-action" @click="load">重试预算</button>
    </div>
    <template v-else-if="!loading">
      <button v-if="budget !== null" type="button" class="budget-ring"
        :class="{ over: remaining !== null && remaining < 0 }"
        :style="{ '--progress': Math.min(percentage ?? 0, 100) + '%' }"
        :aria-label="`更改月度预算，当前 ${budgetAmount}${progressLabel ? '，已用 ' + progressLabel : ''}`" @click="edit">
        <span class="budget-ring-face">
          <span>月度预算</span>
          <b>{{ ringAmount }}</b>
          <span v-if="progressLabel">已用 {{ progressLabel }}</span>
        </span>
      </button>
      <button v-else type="button" class="budget-action" @click="edit">设置月度预算</button>
    </template>

    <BottomSheet :open="open" :title="monthLabel + '预算'" subtitle="只应用于这个月。"
      close-icon :close-disabled="saving" @close="open = false">
      <form class="budget-form" @submit.prevent="submit">
        <label for="monthly-budget-amount">预算金额（元）</label>
        <input id="monthly-budget-amount" v-model="draft" type="text" inputmode="decimal"
          maxlength="18" placeholder="输入这个月的预算" :disabled="saving" />
        <p v-if="formError" role="alert">{{ formError }}</p>
        <div class="budget-form-actions" :class="{ 'can-clear': budget !== null }">
          <button v-if="budget !== null" type="button" class="secondary-button" :disabled="saving" @click="save(null)">取消预算</button>
          <LoadingButton type="submit" class="primary-button" :loading="saving">保存</LoadingButton>
        </div>
      </form>
    </BottomSheet>
  </div>
</template>

<style scoped>
.monthly-budget {
  display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: 12px;
}
.budget-copy { display: grid; min-width: 0; gap: 4px; }
.budget-retry { display: grid; justify-items: end; max-width: 110px; text-align: right; }
.budget-ring {
  display: grid; place-items: center; width: clamp(94px, 27vw, 112px); aspect-ratio: 1;
  padding: 7px; border: 0; border-radius: 50%; color: var(--color-text-primary);
  background: conic-gradient(var(--ledger-accent, var(--color-brand)) var(--progress), var(--color-bg-subtle) 0);
}
.budget-ring.over {
  background: conic-gradient(var(--color-danger) var(--progress), var(--color-bg-subtle) 0);
}
.budget-ring-face {
  display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px;
  width: 100%; height: 100%; border-radius: 50%; background: var(--color-bg-surface);
  color: var(--color-text-secondary); font-size: var(--text-caption); line-height: 1.15;
}
.budget-ring-face b {
  color: var(--color-text-primary); font-size: clamp(.7rem, 3.3vw, .875rem);
  font-variant-numeric: tabular-nums; white-space: nowrap;
}
.budget-remaining, .budget-daily, .budget-retry small { color: var(--color-text-secondary); font-size: var(--text-caption); }
.budget-remaining.over { color: var(--color-danger); }
.budget-action {
  min-height: var(--touch-target); padding: 0 6px; border: 0; border-radius: var(--radius-control);
  color: var(--color-brand); background: transparent; font-size: var(--text-footnote);
}
.budget-form {
  display: grid; grid-template-columns: minmax(0, auto) minmax(0, 1fr); align-items: center; gap: 10px 12px;
}
.budget-form label {
  padding: 12px 0 12px 14px; border-radius: 16px 0 0 16px; background: var(--color-bg-surface);
  color: var(--color-text-primary); font-size: 0.875rem; font-weight: 500;
}
.budget-form input {
  appearance: none; -webkit-appearance: none; box-sizing: border-box; width: 100%; min-height: 46px;
  margin-left: -12px; padding: 0 14px 0 12px; border: 0; border-radius: 0 16px 16px 0;
  background: var(--color-bg-surface); color: var(--color-text-primary);
  font-size: 0.9375rem; font-weight: 600; font-variant-numeric: tabular-nums; text-align: right;
}
.budget-form input:focus-visible { outline: none; box-shadow: none; }
.budget-form p { grid-column: 1 / -1; margin: 0; color: var(--color-danger); font-size: var(--text-footnote); }
.budget-form-actions { grid-column: 1 / -1; display: grid; gap: 8px; }
.budget-form-actions.can-clear { grid-column: 1 / -1; grid-template-columns: repeat(2, minmax(0, 1fr)); }

.monthly-budget-compact { margin-top: 12px; }
.budget-compact-error {
  display: flex; align-items: center; justify-content: space-between; gap: 8px;
  color: var(--color-text-secondary); font-size: var(--text-caption);
}
.budget-compact-link, .budget-compact-set {
  border: 0; padding: 4px 0; background: transparent;
  color: var(--color-brand); font-size: var(--text-caption); font-weight: 600;
}
.budget-compact-bar {
  display: grid; gap: 6px; width: 100%; padding: 0; border: 0; background: transparent; text-align: left;
}
.budget-compact-track {
  display: block; width: 100%; height: 6px; overflow: hidden;
  border-radius: var(--radius-pill); background: var(--color-bg-subtle);
}
.budget-compact-fill {
  display: block; height: 100%; border-radius: var(--radius-pill);
  background: var(--ledger-accent, var(--color-brand));
  transition: width var(--motion-base) var(--ease-standard);
}
.budget-compact-bar.over .budget-compact-fill { background: var(--color-danger); }
.budget-compact-text {
  display: flex; align-items: center; justify-content: space-between; gap: 8px;
  color: var(--color-text-secondary); font-size: var(--text-caption); font-variant-numeric: tabular-nums;
}
.budget-compact-bar.over .budget-compact-text small { color: var(--color-danger); font-weight: 600; }
</style>
