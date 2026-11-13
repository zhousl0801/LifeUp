<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import type {
  CapitalEffectiveInputs,
  CapitalEffectiveValue,
  CapitalGoalSettings,
  CapitalOverview,
  CapitalSuggestions
} from '../types/capital'
import { useUnsavedSheet } from '../composables/useUnsavedSheet'
import { formatMoney, formatSignedMoney, parseAmount, parseSignedAmount } from '../utils/format'
import BottomSheet from './BottomSheet.vue'
import ConfirmSheet from './ConfirmSheet.vue'
import LoadingButton from './LoadingButton.vue'

type FieldKey =
  | 'targetAmount'
  | 'monthlyIncome'
  | 'monthlyExpense'
  | 'monthlyProvidentDeposit'
  | 'incomeGrowthPercent'
  | 'expenseGrowthPercent'
  | 'investmentAnnualReturnPercent'
  | 'providentAnnualReturnPercent'

const amountFields: FieldKey[] = ['monthlyIncome', 'monthlyExpense', 'monthlyProvidentDeposit']
const percentFields: FieldKey[] = [
  'incomeGrowthPercent',
  'expenseGrowthPercent',
  'investmentAnnualReturnPercent',
  'providentAnnualReturnPercent'
]
const fieldKeys: FieldKey[] = ['targetAmount', ...amountFields, ...percentFields]
const investmentPresets = [
  { label: '纳指100 20年 ≈13%', value: 13 },
  { label: '标普500 20年 ≈10%', value: 10 }
]

const props = defineProps<{
  open: boolean
  settings: CapitalGoalSettings
  effective: CapitalEffectiveInputs
  suggestions: CapitalSuggestions
  preview: CapitalOverview | null
  saving: boolean
  serverError: string | null
}>()

const emit = defineEmits<{
  close: []
  preview: [settings: CapitalGoalSettings]
  save: [settings: CapitalGoalSettings]
}>()

const fields = reactive<Record<FieldKey, string>>({
  targetAmount: '',
  monthlyIncome: '',
  monthlyExpense: '',
  monthlyProvidentDeposit: '',
  incomeGrowthPercent: '',
  expenseGrowthPercent: '',
  investmentAnnualReturnPercent: '',
  providentAnnualReturnPercent: ''
})
const errors = reactive<Partial<Record<FieldKey, string>>>({})
const sessionKey = ref(0)
let previewTimer: ReturnType<typeof setTimeout> | null = null

const { discardOpen, requestClose, resolveDiscard } = useUnsavedSheet(
  () => String(sessionKey.value),
  () => props.open,
  () => props.saving,
  () => ({ ...fields }),
  () => emit('close')
)

const computedNet = computed(() => {
  const income = parseInput(fields.monthlyIncome, parseAmount)
  const expense = parseInput(fields.monthlyExpense, parseAmount)
  const provident = parseInput(fields.monthlyProvidentDeposit, parseAmount)

  if (income === null || expense === null || provident === null) {
    return null
  }

  return income - expense + provident
})

const previewNet = computed(() => props.preview?.projection.monthlyNetAccumulation ?? computedNet.value)
const previewBlended = computed(() => props.preview?.projection.blendedAnnualReturnPercent ?? null)

watch(
  () => props.open,
  (open) => {
    if (open) {
      Object.assign(fields, toForm(props.settings))
      clearErrors()
      sessionKey.value++
    } else if (previewTimer) {
      clearTimeout(previewTimer)
      previewTimer = null
    }
  },
  { immediate: true }
)

watch(
  fields,
  () => {
    if (!props.open) {
      return
    }

    validateFields()
    schedulePreview()
  },
  { deep: true }
)

onBeforeUnmount(() => {
  if (previewTimer) {
    clearTimeout(previewTimer)
  }
})

function toForm(settings: CapitalGoalSettings): Record<FieldKey, string> {
  const value = (number: number | null) => (number === null ? '' : String(number))

  return {
    targetAmount: formatWan(settings.targetAmount),
    monthlyIncome: value(settings.monthlyIncome),
    monthlyExpense: value(settings.monthlyExpense),
    monthlyProvidentDeposit: value(settings.monthlyProvidentDeposit),
    incomeGrowthPercent: value(settings.incomeGrowthPercent),
    expenseGrowthPercent: value(settings.expenseGrowthPercent),
    investmentAnnualReturnPercent: value(settings.investmentAnnualReturnPercent),
    providentAnnualReturnPercent: value(settings.providentAnnualReturnPercent)
  }
}

function validateFields(): void {
  clearErrors()

  const targetText = fields.targetAmount.trim()

  if (targetText && parseTarget(targetText) === null) {
    errors.targetAmount = '目标金额必须大于零，最多四位小数（万元）。'
  }

  for (const key of amountFields) {
    const text = fields[key].trim()

    if (text && parseAmount(text) === null) {
      errors[key] = '金额必须不小于零，最多两位小数。'
    }
  }

  for (const key of percentFields) {
    const text = fields[key].trim()

    if (!text) {
      continue
    }

    const value = parseSignedAmount(text)

    if (value === null || value <= -100 || value > 100) {
      errors[key] = '百分比必须在 -100 到 100 之间，最多两位小数。'
    }
  }
}

function clearErrors(): void {
  for (const key of fieldKeys) {
    delete errors[key]
  }
}

function hasErrors(): boolean {
  return Object.keys(errors).length > 0
}

function schedulePreview(): void {
  if (previewTimer) {
    clearTimeout(previewTimer)
  }

  previewTimer = setTimeout(() => {
    if (props.open && !hasErrors()) {
      emit('preview', toSettings())
    }
  }, 300)
}

function toSettings(): CapitalGoalSettings {
  return {
    targetAmount: parseInput(fields.targetAmount, parseTarget),
    monthlyIncome: parseInput(fields.monthlyIncome, parseAmount),
    monthlyExpense: parseInput(fields.monthlyExpense, parseAmount),
    monthlyProvidentDeposit: parseInput(fields.monthlyProvidentDeposit, parseAmount),
    incomeGrowthPercent: parseInput(fields.incomeGrowthPercent, parseSignedAmount),
    expenseGrowthPercent: parseInput(fields.expenseGrowthPercent, parseSignedAmount),
    investmentAnnualReturnPercent: parseInput(fields.investmentAnnualReturnPercent, parseSignedAmount),
    providentAnnualReturnPercent: parseInput(fields.providentAnnualReturnPercent, parseSignedAmount)
  }
}

function parseInput(value: string, parser: (input: string) => number | null): number | null {
  const text = value.trim()

  return text ? parser(text) : null
}

function parseTarget(value: string): number | null {
  const text = value.trim()

  if (!/^\d+(?:\.\d{1,4})?$/.test(text)) {
    return null
  }

  const wan = Number(text)

  if (!Number.isFinite(wan) || wan <= 0) {
    return null
  }

  const yuan = Math.round(wan * 10_000 * 100) / 100

  return yuan <= 1_000_000_000 ? yuan : null
}

function formatWan(value: number | null): string {
  if (value === null) {
    return ''
  }

  return String(Number((value / 10_000).toFixed(4)))
}

function save(): void {
  if (props.saving) {
    return
  }

  validateFields()

  if (hasErrors()) {
    return
  }

  emit('save', toSettings())
}

function resetAssumptions(): void {
  fields.monthlyIncome = ''
  fields.monthlyExpense = ''
  fields.monthlyProvidentDeposit = ''
  fields.incomeGrowthPercent = ''
  fields.expenseGrowthPercent = ''
  fields.investmentAnnualReturnPercent = ''
  fields.providentAnnualReturnPercent = ''
  validateFields()

  if (!hasErrors()) {
    emit('preview', toSettings())
  }
}

function applySuggestion(
  field: 'monthlyIncome' | 'monthlyExpense' | 'monthlyProvidentDeposit',
  value: number | null
): void {
  if (value === null) {
    return
  }

  fields[field] = String(value)
}

function applyPreset(value: number): void {
  fields.investmentAnnualReturnPercent = String(value)
}

function amountHint(value: CapitalEffectiveValue): string {
  if (value.source === 'user') {
    return '已设定'
  }

  if (value.source === 'history') {
    return `最近均值 ${formatMoney(value.value)}`
  }

  return '暂无历史，按 0'
}

function percentHint(value: CapitalEffectiveValue): string {
  if (value.source === 'user') {
    return '已设定'
  }

  return `默认 ${formatRate(value.value)}`
}

function formatRate(value: number | null): string {
  return value === null ? '—' : `${value.toFixed(1)}%`
}

function formatNet(value: number | null): string {
  return value === null ? '—' : formatSignedMoney(value)
}
</script>

<template>
  <BottomSheet
    :open="open"
    title="调整假设"
    subtitle="目标按万元填写；留空的假设按最近 6 个月自动估算"
    :close-disabled="saving"
    @close="requestClose"
  >
    <form class="form" @submit.prevent="save">
      <div class="field full">
        <label class="field-control">
          <span>目标金额（万元）</span>
          <span class="input-unit">
            <input
              v-model="fields.targetAmount"
              aria-label="目标金额"
              inputmode="decimal"
              placeholder="例如 100"
              autocomplete="off"
            />
            <i class="unit">万元</i>
          </span>
        </label>
        <small v-if="errors.targetAmount" class="field-error">{{ errors.targetAmount }}</small>
        <small v-else class="hint">例如 100 表示 100 万；留空则清除目标</small>
      </div>

      <div class="field">
        <label class="field-control">
          <span>月收入</span>
          <input v-model="fields.monthlyIncome" aria-label="月收入" inputmode="decimal" placeholder="现金到账" autocomplete="off" />
        </label>
        <small v-if="errors.monthlyIncome" class="field-error">{{ errors.monthlyIncome }}</small>
        <small v-else-if="!fields.monthlyIncome" class="hint">{{ amountHint(effective.monthlyIncome) }}</small>
        <button
          v-if="suggestions.income !== null"
          type="button"
          class="quick-fill"
          @click.prevent="applySuggestion('monthlyIncome', suggestions.income)"
        >
          用计划收入 {{ formatMoney(suggestions.income) }}
        </button>
      </div>

      <div class="field">
        <label class="field-control">
          <span>月支出</span>
          <input v-model="fields.monthlyExpense" aria-label="月支出" inputmode="decimal" placeholder="日常支出" autocomplete="off" />
        </label>
        <small v-if="errors.monthlyExpense" class="field-error">{{ errors.monthlyExpense }}</small>
        <small v-else-if="!fields.monthlyExpense" class="hint">{{ amountHint(effective.monthlyExpense) }}</small>
        <button
          v-if="suggestions.expense !== null"
          type="button"
          class="quick-fill"
          @click.prevent="applySuggestion('monthlyExpense', suggestions.expense)"
        >
          用账单预设 {{ formatMoney(suggestions.expense) }}
        </button>
      </div>

      <div class="field">
        <label class="field-control">
          <span>公积金月缴存</span>
          <input
            v-model="fields.monthlyProvidentDeposit"
            aria-label="公积金月缴存"
            inputmode="decimal"
            placeholder="0"
            autocomplete="off"
          />
        </label>
        <small v-if="errors.monthlyProvidentDeposit" class="field-error">{{ errors.monthlyProvidentDeposit }}</small>
        <small v-else-if="!fields.monthlyProvidentDeposit" class="hint">{{ amountHint(effective.monthlyProvidentDeposit) }}</small>
        <button
          v-if="suggestions.providentDeposit !== null"
          type="button"
          class="quick-fill"
          @click.prevent="applySuggestion('monthlyProvidentDeposit', suggestions.providentDeposit)"
        >
          用缴存设置 {{ formatMoney(suggestions.providentDeposit) }}
        </button>
      </div>

      <div class="field">
        <label class="field-control">
          <span>公积金年化</span>
          <input
            v-model="fields.providentAnnualReturnPercent"
            aria-label="公积金年化"
            inputmode="decimal"
            placeholder="1.5"
            autocomplete="off"
          />
        </label>
        <small v-if="errors.providentAnnualReturnPercent" class="field-error">{{ errors.providentAnnualReturnPercent }}</small>
        <small v-else-if="!fields.providentAnnualReturnPercent" class="hint">{{ percentHint(effective.providentAnnualReturnPercent) }}</small>
      </div>

      <div class="field">
        <label class="field-control">
          <span>收入年增长</span>
          <input
            v-model="fields.incomeGrowthPercent"
            aria-label="收入年增长"
            inputmode="decimal"
            placeholder="0"
            autocomplete="off"
          />
        </label>
        <small v-if="errors.incomeGrowthPercent" class="field-error">{{ errors.incomeGrowthPercent }}</small>
        <small v-else-if="!fields.incomeGrowthPercent" class="hint">{{ percentHint(effective.incomeGrowthPercent) }}</small>
      </div>

      <div class="field">
        <label class="field-control">
          <span>支出年增长</span>
          <input
            v-model="fields.expenseGrowthPercent"
            aria-label="支出年增长"
            inputmode="decimal"
            placeholder="0"
            autocomplete="off"
          />
        </label>
        <small v-if="errors.expenseGrowthPercent" class="field-error">{{ errors.expenseGrowthPercent }}</small>
        <small v-else-if="!fields.expenseGrowthPercent" class="hint">{{ percentHint(effective.expenseGrowthPercent) }}</small>
      </div>

      <div class="field full">
        <label class="field-control">
          <span>投资年化</span>
          <input
            v-model="fields.investmentAnnualReturnPercent"
            aria-label="投资年化"
            inputmode="decimal"
            placeholder="例如 4.5"
            autocomplete="off"
          />
        </label>
        <small v-if="errors.investmentAnnualReturnPercent" class="field-error">{{ errors.investmentAnnualReturnPercent }}</small>
        <small v-else-if="!fields.investmentAnnualReturnPercent" class="hint">{{ percentHint(effective.investmentAnnualReturnPercent) }}</small>
        <div class="presets">
          <button
            v-for="preset in investmentPresets"
            :key="preset.value"
            type="button"
            class="preset"
            @click.prevent="applyPreset(preset.value)"
          >
            {{ preset.label }}
          </button>
        </div>
        <small class="hint">历史年化参考，不代表未来收益；可手动修改</small>
      </div>

      <div class="derived">
        <span>月净积累</span>
        <b>{{ formatNet(previewNet) }}</b>
        <span>组合年化</span>
        <b>{{ formatRate(previewBlended) }}</b>
      </div>

      <p v-if="serverError" class="form-error">{{ serverError }}</p>

      <div class="actions">
        <button type="button" class="ghost" :disabled="saving" @click="resetAssumptions">恢复假设默认</button>
        <LoadingButton class="primary" :loading="saving" loading-label="保存中…" @click="save">
          保存并重新计算
        </LoadingButton>
      </div>
    </form>
  </BottomSheet>

  <ConfirmSheet
    :open="discardOpen"
    title="放弃修改？"
    message="离开后，本次修改会丢失。"
    confirm-label="放弃修改"
    cancel-label="继续编辑"
    danger
    @confirm="resolveDiscard(true)"
    @cancel="resolveDiscard(false)"
  />
</template>

<style scoped>
.form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px 10px;
}

.field {
  display: block;
  min-width: 0;
}

.field.full {
  grid-column: 1 / -1;
}

.field-control {
  display: block;
}

.field-control > span:not(.input-unit) {
  display: block;
  margin-bottom: 5px;
  color: var(--color-text-secondary);
  font-size: 12px;
}

.field input {
  width: 100%;
  height: 44px;
  box-sizing: border-box;
  padding: 0 12px;
  border: 0;
  border-radius: var(--radius-control);
  outline: none;
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  font: inherit;
  font-size: 15px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.field input:focus {
  box-shadow: 0 0 0 2px var(--color-focus-ring);
}

.input-unit {
  position: relative;
  display: block;
}

.input-unit input {
  padding-right: 48px;
}

.input-unit .unit {
  position: absolute;
  top: 50%;
  right: 12px;
  transform: translateY(-50%);
  color: var(--color-text-secondary);
  font-size: 12px;
  font-style: normal;
  font-weight: 500;
  pointer-events: none;
}

.hint,
.field-error {
  display: block;
  margin-top: 4px;
  font-size: 11px;
  font-weight: 400;
  line-height: 1.4;
}

.hint {
  color: var(--color-text-secondary);
}

.field-error {
  color: var(--color-error);
}

.quick-fill,
.preset {
  display: inline-flex;
  align-items: center;
  min-height: 32px;
  margin-top: 5px;
  padding: 4px 9px;
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
  color: var(--color-brand);
  font-size: 11px;
  font-weight: 500;
  line-height: 1.3;
}

.quick-fill:active,
.preset:active {
  background: var(--color-brand-soft);
}

.presets {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 5px;
}

.presets .preset {
  margin-top: 0;
}

.derived {
  display: flex;
  align-items: center;
  gap: 8px;
  grid-column: 1 / -1;
  margin-top: 2px;
  padding: 11px 12px;
  border-radius: var(--radius-control);
  background: var(--color-brand-soft);
  color: var(--color-text-secondary);
  font-size: 12px;
}

.derived b {
  margin-right: 8px;
  color: var(--color-brand);
  font-size: 14px;
  font-variant-numeric: tabular-nums;
}

.form-error {
  grid-column: 1 / -1;
  margin: 0;
  color: var(--color-error);
  font-size: 12px;
}

.actions {
  display: grid;
  grid-column: 1 / -1;
  grid-template-columns: 1fr 1.4fr;
  gap: 10px;
  margin-top: 2px;
}

.ghost,
.primary {
  min-height: 48px;
  border: 0;
  border-radius: var(--radius-pill);
  font-size: 15px;
  font-weight: 600;
}

.ghost {
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
}

.primary {
  background: var(--color-brand);
  color: var(--color-on-brand);
}
</style>
