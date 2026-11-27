<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, shallowRef, watch } from 'vue'
import type {
  CapitalEffectiveInputs,
  CapitalGoalSettings,
  CapitalOverview,
  CapitalSuggestions,
  FireExpenseCategory,
  FireGoalMode,
  FireOverview
} from '../types/capital'
import { formatCapitalAmount } from '../utils/capital'
import { useUnsavedSheet } from '../composables/useUnsavedSheet'
import {
  amountHint,
  fieldKeys,
  formatNet,
  formatRate,
  investmentPresets,
  parseInput,
  parseSafeWithdrawalRate,
  percentHint,
  toAssumptionsForm,
  toAssumptionsSettings,
  validateAssumptionsFields,
  type FieldKey
} from '../utils/capitalAssumptionsForm'
import { formatMoney, parseAmount, todayInBusinessTimeZone } from '../utils/format'
import BottomSheet from './BottomSheet.vue'
import ConfirmSheet from './ConfirmSheet.vue'
import FireExpenseCalibration from './FireExpenseCalibration.vue'
import LoadingButton from './LoadingButton.vue'

const props = defineProps<{
  open: boolean
  settings: CapitalGoalSettings
  effective: CapitalEffectiveInputs
  suggestions: CapitalSuggestions
  preview: CapitalOverview | null
  saving: boolean
  serverError: string | null
  hasSalaryProfile: boolean
  fire?: FireOverview | null
  currentYear?: number
  amountsMasked?: boolean
}>()

const emit = defineEmits<{
  close: []
  preview: [settings: CapitalGoalSettings]
  save: [settings: CapitalGoalSettings]
}>()

const fields = reactive(toAssumptionsForm(props.settings))
const errors = reactive<Partial<Record<FieldKey, string>>>({})
const sessionKey = ref(0)
const followProfile = ref(false)
const confirmClearGoal = ref(false)
const goalMode = shallowRef<FireGoalMode>('custom')
const leanCategories = shallowRef<string[] | null>(null)
const excludedCategories = shallowRef<string[]>([])
const swrPresets = [3, 3.5, 4, 5]
const coastOffsets = [20, 25, 30]
const currentYear = computed(() => props.currentYear ?? Number(todayInBusinessTimeZone().slice(0, 4)))
const firePreview = computed(() => props.preview?.fire ?? props.fire)
const categories = computed(() => firePreview.value?.categories ?? [])
const activeFire = computed(() => firePreview.value?.[firePreview.value.activeMode])
const swrHint = computed(() => {
  const rate = parseSafeWithdrawalRate(fields.safeWithdrawalRatePercent)
  if (rate === null) return '默认 4.0%，相当于 25 倍年支出'
  return `相当于 ${Number((100 / rate).toFixed(2))} 倍年支出`
})
const hasHiddenErrors = computed(() => goalMode.value === 'fire'
  ? Boolean(errors.targetAmount)
  : Boolean(errors.safeWithdrawalRatePercent || errors.coastTargetYear
    || errors.fireMonthlyExpenseOverride || errors.leanMonthlyExpenseOverride))
let previewTimer: ReturnType<typeof setTimeout> | null = null
let skipNextPreviewSchedule = false

function clearPreviewTimer(): void {
  if (previewTimer) {
    clearTimeout(previewTimer)
    previewTimer = null
  }
}

function assignFieldsSilently(next: Partial<Record<FieldKey, string>>): void {
  skipNextPreviewSchedule = true
  Object.assign(fields, next)
  void Promise.resolve().then(() => {
    skipNextPreviewSchedule = false
  })
}

const { discardOpen, requestClose, resolveDiscard } = useUnsavedSheet(
  () => String(sessionKey.value),
  () => props.open,
  () => props.saving,
  () => ({
    ...fields,
    followProfile: followProfile.value,
    goalMode: goalMode.value,
    fireMode: props.settings.fireMode ?? props.fire?.activeMode ?? 'standard',
    leanCategories: JSON.stringify(leanCategories.value?.slice().sort() ?? null),
    excludedCategories: JSON.stringify(excludedCategories.value.slice().sort())
  }),
  () => emit('close')
)

const computedNet = computed(() => {
  const income = followProfile.value
    ? props.effective.monthlyIncome.value
    : parseInput(fields.monthlyIncome, parseAmount)
  const expense = parseInput(fields.monthlyExpense, parseAmount)
  const provident = followProfile.value
    ? props.effective.monthlyProvidentDeposit.value
    : parseInput(fields.monthlyProvidentDeposit, parseAmount)

  if (income === null || expense === null || provident === null) {
    return null
  }

  return income - expense + provident
})

const bonusAmount = computed(() => followProfile.value
  ? props.effective.annualBonus?.value ?? 0
  : parseInput(fields.annualBonus, parseAmount) ?? 0)
const bonusMonth = computed(() => followProfile.value
  ? props.effective.bonusPayoutMonth?.value ?? 1
  : Number(fields.bonusPayoutMonth) || 1)

const previewNet = computed(() => props.preview?.projection.monthlyNetAccumulation ?? computedNet.value)
const previewBlended = computed(() => props.preview?.projection.blendedAnnualReturnPercent ?? null)

watch(
  () => props.open,
  (open) => {
    clearPreviewTimer()
    if (open) {
      followProfile.value = props.hasSalaryProfile
        && (props.settings.useSalaryProfile
          || (props.settings.monthlyIncome === null
            && props.settings.monthlyProvidentDeposit === null
            && (props.settings.annualBonus ?? null) === null))
      goalMode.value = props.settings.goalMode ?? props.fire?.goalMode ?? 'custom'
      leanCategories.value = props.settings.leanExpenseCategories?.slice() ?? null
      excludedCategories.value = props.settings.excludedExpenseCategories?.slice() ?? []
      assignFieldsSilently(toAssumptionsForm(props.settings))
      clearErrors()
      sessionKey.value++
    }
  },
  { immediate: true }
)

watch(
  [fields, goalMode, leanCategories, excludedCategories],
  () => {
    if (!props.open || skipNextPreviewSchedule) {
      return
    }

    validateFields()
    schedulePreview()
  },
  { deep: true }
)

onBeforeUnmount(() => {
  clearPreviewTimer()
})

function validateFields(): void {
  clearErrors()
  Object.assign(errors, validateAssumptionsFields(fields, followProfile.value))
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
  clearPreviewTimer()

  previewTimer = setTimeout(() => {
    previewTimer = null
    if (props.open && !hasErrors()) {
      emit('preview', toSettings())
    }
  }, 300)
}

function toSettings(): CapitalGoalSettings {
  return {
    ...toAssumptionsSettings(fields, followProfile.value),
    goalMode: goalMode.value,
    fireMode: props.settings.fireMode ?? props.fire?.activeMode ?? 'standard',
    leanExpenseCategories: leanCategories.value,
    excludedExpenseCategories: excludedCategories.value.length ? excludedCategories.value : null
  }
}

function cycleCategory(category: FireExpenseCategory): void {
  const name = category.name
  const lean = leanCategories.value ?? categories.value.filter((item) => item.isDefaultLean).map((item) => item.name)
  if (excludedCategories.value.includes(name)) {
    excludedCategories.value = excludedCategories.value.filter((item) => item !== name)
    if (!lean.includes(name)) leanCategories.value = [...lean, name]
  } else if (lean.includes(name)) {
    leanCategories.value = lean.filter((item) => item !== name)
  } else {
    excludedCategories.value = [...excludedCategories.value, name]
  }
}

function resetCategories(): void {
  leanCategories.value = null
  excludedCategories.value = []
}

function money(value: number): string {
  return props.amountsMasked ? '••••' : `¥${formatMoney(value)}`
}

function switchToManual(): void {
  fields.monthlyIncome = String(props.effective.monthlyIncome.value)
  fields.monthlyProvidentDeposit = String(props.effective.monthlyProvidentDeposit.value)
  fields.annualBonus = (props.effective.annualBonus?.value ?? 0) > 0
    ? String(props.effective.annualBonus?.value ?? 0)
    : ''
  fields.bonusPayoutMonth = String(props.effective.bonusPayoutMonth?.value ?? 1)
  followProfile.value = false
  validateFields()
  schedulePreview()
}

function switchToFollow(): void {
  followProfile.value = true
  validateFields()
  schedulePreview()
}

function save(): void {
  if (props.saving) {
    return
  }

  clearPreviewTimer()
  validateFields()

  if (hasErrors()) {
    return
  }

  emit('save', toSettings())
}

function resetAssumptions(): void {
  clearPreviewTimer()
  assignFieldsSilently({
    monthlyIncome: '',
    monthlyExpense: '',
    monthlyProvidentDeposit: '',
    annualBonus: '',
    bonusPayoutMonth: '1',
    incomeGrowthPercent: '',
    expenseGrowthPercent: '',
    investmentAnnualReturnPercent: '',
    providentAnnualReturnPercent: '',
    safeWithdrawalRatePercent: '',
    coastTargetYear: ''
  })
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

function clearTargetGoal(): void {
  if (props.saving) return
  confirmClearGoal.value = false
  clearPreviewTimer()
  fields.targetAmount = ''
  emit('save', {
    ...props.settings,
    goalMode: 'custom',
    targetAmount: null
  })
}
</script>

<template>
  <BottomSheet
    :open="open"
    title="调整预测"
    subtitle="留空项按工资档案、账单或默认假设计算"
    :close-disabled="saving"
    @close="requestClose"
  >
    <form class="form" @submit.prevent="save">
      <div class="goal-mode" role="group" aria-label="目标模式">
        <button type="button" :aria-pressed="goalMode === 'fire'" @click="goalMode = 'fire'">FIRE 模式</button>
        <button type="button" :aria-pressed="goalMode === 'custom'" @click="goalMode = 'custom'">自定义目标</button>
      </div>

      <template v-if="goalMode === 'fire'">
        <div class="field">
          <label class="field-control">
            <span>安全提取率 (SWR)</span>
            <span class="input-unit">
              <input
                v-model="fields.safeWithdrawalRatePercent"
                aria-label="安全提取率"
                :aria-invalid="Boolean(errors.safeWithdrawalRatePercent)"
                inputmode="decimal" placeholder="4.0" autocomplete="off"
              />
              <i class="unit">%</i>
            </span>
          </label>
          <div class="presets fire-presets">
            <button
              v-for="rate in swrPresets" :key="rate" type="button" class="preset"
              :aria-pressed="(parseSafeWithdrawalRate(fields.safeWithdrawalRatePercent) ?? 4) === rate"
              @click="fields.safeWithdrawalRatePercent = String(rate)"
            >{{ rate }}%</button>
          </div>
          <small v-if="errors.safeWithdrawalRatePercent" class="field-error">{{ errors.safeWithdrawalRatePercent }}</small>
          <small v-else class="hint">{{ swrHint }}</small>
        </div>
        <div class="field">
          <label class="field-control">
            <span>Coast 目标年份</span>
            <span class="input-unit">
              <input
                v-model="fields.coastTargetYear"
                aria-label="Coast 目标年份"
                :aria-invalid="Boolean(errors.coastTargetYear)"
                inputmode="numeric" :placeholder="String(currentYear + 25)" autocomplete="off"
              />
              <i class="unit">年</i>
            </span>
          </label>
          <div class="presets fire-presets">
            <button
              v-for="offset in coastOffsets" :key="offset" type="button" class="preset"
              :aria-pressed="(Number(fields.coastTargetYear) || currentYear + 25) === currentYear + offset"
              @click="fields.coastTargetYear = String(currentYear + offset)"
            >{{ offset }} 年后</button>
          </div>
          <small v-if="errors.coastTargetYear" class="field-error">{{ errors.coastTargetYear }}</small>
        </div>

        <FireExpenseCalibration
          :key="sessionKey"
          v-model:standard-override="fields.fireMonthlyExpenseOverride"
          v-model:lean-override="fields.leanMonthlyExpenseOverride"
          :fire="firePreview"
          :categories="categories"
          :excluded-categories="excludedCategories"
          :lean-categories="leanCategories"
          :amounts-masked="Boolean(amountsMasked)"
          :standard-error="errors.fireMonthlyExpenseOverride"
          :lean-error="errors.leanMonthlyExpenseOverride"
          @cycle="cycleCategory"
          @reset="resetCategories"
        />
      </template>

      <div v-else class="field full">
        <label class="field-control">
          <span>目标金额（万元）</span>
          <span class="input-unit">
            <input
              v-model="fields.targetAmount" :class="{ 'private-amount': amountsMasked }"
              aria-label="目标金额"
              inputmode="decimal"
              placeholder="例如 100"
              autocomplete="off"
            />
            <i class="unit">万元</i>
          </span>
        </label>
        <small v-if="errors.targetAmount" class="field-error">{{ errors.targetAmount }}</small>
        <small v-else class="hint">例如 100 表示 100 万。</small>
        <button
          v-if="settings.targetAmount !== null"
          type="button"
          class="clear-goal-link"
          :disabled="saving"
          @click="confirmClearGoal = true"
        >取消当前目标</button>
      </div>

      <div v-if="hasSalaryProfile" class="field full follow-banner">
        <div>
          <b>{{ followProfile ? '跟随工资档案' : '手动覆盖' }}</b>
          <small>{{ followProfile ? '月收入、公积金和年终奖来自工资档案。' : '当前使用手填值，工资档案变化不会覆盖。' }}</small>
        </div>
        <button
          type="button"
          class="quick-fill"
          @click.prevent="followProfile ? switchToManual() : switchToFollow()"
        >{{ followProfile ? '改为手动' : '改用工资档案' }}</button>
      </div>
      <div v-else-if="settings.useSalaryProfile" class="field full follow-banner warning">
        <div>
          <b>工资档案已停用</b>
          <small>请改为手动，或到「我的 → 个人信息」重新建立工资档案。</small>
        </div>
      </div>

      <div v-if="followProfile" class="field">
        <div class="field-control readonly-control">
          <span>月收入</span>
          <b>{{ money(effective.monthlyIncome.value) }}</b>
          <small class="source-badge">工资档案</small>
        </div>
      </div>
      <div v-else class="field">
        <label class="field-control">
          <span>月收入</span>
          <input v-model="fields.monthlyIncome" :class="{ 'private-amount': amountsMasked }" aria-label="月收入" inputmode="decimal" placeholder="现金到账" autocomplete="off" />
        </label>
        <small v-if="errors.monthlyIncome" class="field-error">{{ errors.monthlyIncome }}</small>
        <small v-else-if="!fields.monthlyIncome" class="hint">{{ amountHint(effective.monthlyIncome, amountsMasked) }}</small>
        <button
          v-if="suggestions.income !== null"
          type="button"
          class="quick-fill"
          @click.prevent="applySuggestion('monthlyIncome', suggestions.income)"
        >
          用计划收入 {{ amountsMasked ? '••••' : formatMoney(suggestions.income) }}
        </button>
      </div>

      <div class="field">
        <label class="field-control">
          <span>月支出</span>
          <input v-model="fields.monthlyExpense" :class="{ 'private-amount': amountsMasked }" aria-label="月支出" inputmode="decimal" placeholder="日常支出" autocomplete="off" />
        </label>
        <small v-if="errors.monthlyExpense" class="field-error">{{ errors.monthlyExpense }}</small>
        <small v-else-if="!fields.monthlyExpense" class="hint">{{ amountHint(effective.monthlyExpense, amountsMasked) }}</small>
        <button
          v-if="suggestions.expense !== null"
          type="button"
          class="quick-fill"
          @click.prevent="applySuggestion('monthlyExpense', suggestions.expense)"
        >
          用账单预设 {{ amountsMasked ? '••••' : formatMoney(suggestions.expense) }}
        </button>
      </div>

      <div v-if="followProfile" class="field">
        <div class="field-control readonly-control">
          <span>公积金月缴存</span>
          <b>{{ money(effective.monthlyProvidentDeposit.value) }}</b>
          <small class="source-badge">工资档案</small>
        </div>
      </div>
      <div v-else class="field">
        <label class="field-control">
          <span>公积金月缴存</span>
          <input
            v-model="fields.monthlyProvidentDeposit" :class="{ 'private-amount': amountsMasked }"
            aria-label="公积金月缴存"
            inputmode="decimal"
            placeholder="0"
            autocomplete="off"
          />
        </label>
        <small v-if="errors.monthlyProvidentDeposit" class="field-error">{{ errors.monthlyProvidentDeposit }}</small>
        <small v-else-if="!fields.monthlyProvidentDeposit" class="hint">{{ amountHint(effective.monthlyProvidentDeposit, amountsMasked) }}</small>
        <button
          v-if="suggestions.providentDeposit !== null"
          type="button"
          class="quick-fill"
          @click.prevent="applySuggestion('monthlyProvidentDeposit', suggestions.providentDeposit)"
        >
          用缴存设置 {{ amountsMasked ? '••••' : formatMoney(suggestions.providentDeposit) }}
        </button>
      </div>

      <div v-if="followProfile" class="field">
        <div class="field-control readonly-control">
          <span>年终奖（到手）</span>
          <b>{{ (effective.annualBonus?.value ?? 0) > 0 ? money(effective.annualBonus?.value ?? 0) : '未设置' }}</b>
          <small class="source-badge">{{ effective.bonusPayoutMonth?.value ?? 1 }} 月到账</small>
        </div>
      </div>
      <template v-else>
        <div class="field">
          <label class="field-control">
            <span>年终奖（到手）</span>
            <input v-model="fields.annualBonus" :class="{ 'private-amount': amountsMasked }" aria-label="年终奖到手" inputmode="decimal" placeholder="0" autocomplete="off" />
          </label>
          <small v-if="errors.annualBonus" class="field-error">{{ errors.annualBonus }}</small>
          <small v-else class="hint">预测和年度表按发放月计入，不计入月净积累。</small>
        </div>
        <div class="field">
          <label class="field-control">
            <span>年终奖发放月</span>
            <select v-model="fields.bonusPayoutMonth" aria-label="年终奖发放月">
              <option v-for="month in 12" :key="month" :value="String(month)">{{ month }} 月</option>
            </select>
          </label>
          <small v-if="errors.bonusPayoutMonth" class="field-error">{{ errors.bonusPayoutMonth }}</small>
        </div>
      </template>

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

      <div class="derived" :class="{ 'is-fire': goalMode === 'fire' }">
        <div v-if="goalMode === 'fire'">
          <span>储蓄率</span>
          <b>{{ formatRate(firePreview?.savingsRatePercent ?? null) }}</b>
        </div>
        <div>
          <span>月净积累</span>
          <b>{{ amountsMasked && previewNet !== null ? '••••' : formatNet(previewNet) }}</b>
        </div>
        <div v-if="goalMode === 'fire'">
          <span>FIRE 目标本金</span>
          <b>{{ activeFire && activeFire.status !== 'noTarget' ? formatCapitalAmount(activeFire.targetCapital, Boolean(amountsMasked), true) : '—' }}</b>
        </div>
        <div v-else>
          <span>组合年化</span>
          <b>{{ formatRate(previewBlended) }}</b>
        </div>
      </div>

      <p v-if="bonusAmount > 0" class="bonus-hint field full">
        年终奖 {{ money(bonusAmount) }} 于每年 {{ bonusMonth }} 月计入达成时间，不计入月净积累。
      </p>

      <p v-if="hasHiddenErrors" class="form-error">
        请切换到{{ goalMode === 'fire' ? '自定义目标' : ' FIRE 模式' }}修正输入。
      </p>

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
    :open="confirmClearGoal"
    title="取消当前目标？"
    message="取消后不再显示目标达成倒计时，页面仍会按当前速度展示长期积累预测。"
    confirm-label="取消目标"
    cancel-label="先保留"
    danger
    :busy="saving"
    @confirm="clearTargetGoal"
    @cancel="confirmClearGoal = false"
  />
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
  display: flex;
  flex-direction: column;
  gap: 0;
  font-variant-numeric: tabular-nums;
}

.goal-mode {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  margin-bottom: 10px;
  padding: 4px;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
}

.goal-mode button {
  min-height: var(--touch-target);
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.goal-mode button[aria-pressed='true'] {
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
  color: var(--color-brand);
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
  min-height: 46px;
  padding: 8px 14px;
  background: var(--color-bg-surface);
  border-bottom: 1px solid var(--color-border-subtle);
  transition: background-color var(--motion-feedback) var(--ease-move);
}

.field:focus-within {
  background: color-mix(in srgb, var(--color-brand) 4%, var(--color-bg-surface));
}

.form > .field:not(.field + *) {
  border-top-left-radius: 16px;
  border-top-right-radius: 16px;
}

.form > .field:not(:has(+ .field)) {
  border-bottom-left-radius: 16px;
  border-bottom-right-radius: 16px;
  border-bottom: 0;
  margin-bottom: 10px;
}

.field.full {
  grid-column: 1 / -1;
}

.field-control {
  display: grid;
  grid-template-columns: minmax(0, auto) minmax(0, 1fr);
  align-items: center;
  gap: 12px;
}

.field-control > span:not(.input-unit) {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 0.875rem;
  font-weight: 500;
}

.field input,
.field select {
  appearance: none;
  -webkit-appearance: none;
  width: 100%;
  min-width: 0;
  min-height: 30px;
  box-sizing: border-box;
  padding: 2px 0;
  border: 0;
  border-radius: 0;
  outline: none;
  background: transparent;
  color: var(--color-text-primary);
  font: inherit;
  font-size: 0.9375rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  text-align: right;
  text-align-last: right;
}

.field select {
  padding-right: 16px;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='%238A8E9E' stroke-width='2.2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='m6 9 6 6 6-6'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right center;
  background-size: 12px 12px;
}

.field input:focus,
.field select:focus {
  box-shadow: none;
}

.field input.private-amount:not(:focus) {
  -webkit-text-security: disc;
}

.input-unit {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 6px;
}

.input-unit input {
  padding-right: 0;
}

.input-unit .unit {
  position: static;
  transform: none;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-style: normal;
  font-weight: 500;
  pointer-events: none;
  flex-shrink: 0;
}

.hint,
.field-error {
  display: block;
  margin-top: 2px;
  font-size: 11px;
  font-weight: 400;
  line-height: 1.4;
}

.hint {
  color: var(--color-text-secondary);
}

.clear-goal-link {
  align-self: flex-start;
  padding: 2px 0;
  border: 0;
  background: transparent;
  color: var(--color-error);
  font-size: var(--text-caption);
  font-weight: 500;
}

.field-error {
  color: var(--color-error);
}

.quick-fill,
.preset {
  display: inline-flex;
  align-items: center;
  align-self: flex-start;
  min-height: 28px;
  margin-top: 4px;
  padding: 2px 9px;
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
  margin-top: 4px;
}

.presets .preset {
  margin-top: 0;
}

.derived {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
  padding: 10px 14px;
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-brand) 7%, var(--color-bg-surface));
  color: var(--color-text-secondary);
  font-size: 12px;
}

.derived > div {
  display: flex;
  align-items: center;
  gap: 6px;
}

.derived.is-fire {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 4px;
}

.derived.is-fire > div {
  flex-direction: column;
  text-align: center;
}

.derived.is-fire b {
  margin-right: 0;
  overflow-wrap: anywhere;
}

.fire-presets .preset {
  min-width: var(--touch-target);
  min-height: var(--touch-target);
}

.fire-presets .preset[aria-pressed='true'] {
  background: var(--color-brand-soft);
}

.derived b {
  margin-right: 8px;
  color: var(--color-brand);
  font-size: 14px;
  font-variant-numeric: tabular-nums;
}

.form-error {
  margin: 0 0 10px;
  color: var(--color-error);
  font-size: 12px;
}

.actions {
  display: grid;
  grid-template-columns: 1fr 1.4fr;
  gap: 10px;
  margin-top: 4px;
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
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
}

.primary {
  background: var(--color-brand);
  color: var(--color-on-brand);
}

.follow-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 10px;
  padding: 10px 14px;
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-brand) 8%, var(--color-bg-surface));
}

.follow-banner > div {
  display: grid;
  gap: 3px;
}

.follow-banner b {
  font-size: var(--text-footnote);
  font-weight: 600;
}

.follow-banner small {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.follow-banner.warning {
  background: var(--color-bg-surface);
}

.readonly-control {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.readonly-control > span {
  color: var(--color-text-primary);
  font-size: 0.875rem;
  font-weight: 500;
}

.readonly-control b {
  margin-left: auto;
  font-size: 0.9375rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.source-badge {
  justify-self: start;
  padding: 1px 6px;
  border-radius: 999px;
  background: var(--color-brand-soft);
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.bonus-hint {
  margin: 0 0 10px;
  padding: 2px 6px;
  background: transparent;
  border: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.55;
}
</style>
