<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { SalaryProfile } from '../types/salary'
import { formatMoney, parseAmount } from '../utils/format'
import BottomSheet from './BottomSheet.vue'
import ConfirmSheet from './ConfirmSheet.vue'
import LoadingButton from './LoadingButton.vue'

const props = defineProps<{
  open: boolean
  profile: SalaryProfile | null
  prefill: {
    netMonthlySalary: number | null
    providentMonthly: number | null
    payDay: number | null
  }
  saving: boolean
  serverError: string | null
}>()

const emit = defineEmits<{
  save: [profile: SalaryProfile]
  disable: []
  close: []
}>()

const fields = reactive({
  base: '',
  net: '',
  provident: '',
  bonusMonths: '0',
  bonusNet: '0',
  payout: '1',
  payDay: '15'
})
const errors = reactive<Record<string, string>>({})
const confirmDisable = ref(false)

const annualPackage = computed(() => {
  const base = parseAmount(fields.base) ?? 0
  const months = parseAmount(fields.bonusMonths) ?? 0

  return base * 12 + base * months
})
const netAnnualCash = computed(() => {
  const net = parseAmount(fields.net) ?? 0
  const bonus = parseAmount(fields.bonusNet) ?? 0

  return net * 12 + bonus
})
const providentAnnual = computed(() => (parseAmount(fields.provident) ?? 0) * 12)

watch(
  () => props.open,
  (open) => {
    if (open) {
      resetForm()
    }
  },
  { immediate: true }
)

function resetForm(): void {
  const profile = props.profile
  fields.base = profile ? String(profile.baseMonthlySalary) : ''
  fields.net = profile
    ? String(profile.netMonthlySalary)
    : props.prefill.netMonthlySalary === null ? '' : String(props.prefill.netMonthlySalary)
  fields.provident = profile
    ? String(profile.providentMonthly)
    : props.prefill.providentMonthly === null ? '' : String(props.prefill.providentMonthly)
  fields.bonusMonths = profile ? String(profile.bonusMonths) : '0'
  fields.bonusNet = profile ? String(profile.bonusNetAmount) : '0'
  fields.payout = profile ? String(profile.bonusPayoutMonth) : '1'
  fields.payDay = profile
    ? String(profile.payDay)
    : props.prefill.payDay === null ? '15' : String(props.prefill.payDay)
  clearErrors()
  confirmDisable.value = false
}

function clearErrors(): void {
  for (const key of Object.keys(errors)) {
    delete errors[key]
  }
}

function validate(): boolean {
  clearErrors()

  const base = parseAmount(fields.base)
  const net = parseAmount(fields.net)
  const provident = parseAmount(fields.provident)
  const bonusMonths = parseAmount(fields.bonusMonths)
  const bonusNet = parseAmount(fields.bonusNet)
  const payout = Number(fields.payout)
  const payDay = Number(fields.payDay)

  if (base === null || base <= 0) {
    errors.base = '税前 Base 必须大于 0。'
  }

  if (net === null || net <= 0) {
    errors.net = '税后工资必须大于 0。'
  }

  if (provident === null || provident < 0) {
    errors.provident = '公积金不能为负。'
  }

  if (bonusMonths === null || bonusMonths < 0 || bonusMonths > 24) {
    errors.bonusMonths = '年终奖月数需在 0 到 24 之间。'
  }

  if (bonusNet === null || bonusNet < 0) {
    errors.bonusNet = '年终奖到手不能为负。'
  } else if ((bonusMonths ?? 0) > 0 && bonusNet <= 0) {
    errors.bonusNet = '填写月数后，年终奖到手必须大于 0。'
  }

  if (!Number.isInteger(payout) || payout < 1 || payout > 12) {
    errors.payout = '发放月需在 1 到 12 之间。'
  }

  if (!Number.isInteger(payDay) || payDay < 1 || payDay > 31) {
    errors.payDay = '发薪日需在 1 到 31 之间。'
  }

  return Object.keys(errors).length === 0
}

function submit(): void {
  if (props.saving || !validate()) {
    return
  }

  emit('save', {
    baseMonthlySalary: parseAmount(fields.base)!,
    netMonthlySalary: parseAmount(fields.net)!,
    providentMonthly: parseAmount(fields.provident)!,
    bonusMonths: parseAmount(fields.bonusMonths)!,
    bonusNetAmount: parseAmount(fields.bonusNet)!,
    bonusPayoutMonth: Number(fields.payout),
    payDay: Number(fields.payDay)
  })
}

function confirmDisableProfile(): void {
  confirmDisable.value = false
  emit('disable')
}
</script>

<template>
  <BottomSheet
    :open="open"
    title="工资档案"
    subtitle="保存后会同步工资、公积金和年终奖计划，只影响未发生的期次"
    :close-disabled="saving"
    @close="$emit('close')"
  >
    <form class="salary-form" @submit.prevent="submit">
      <div class="salary-field">
        <label>
          <span>税前 Base（元/月）</span>
          <input v-model="fields.base" aria-label="税前 Base" inputmode="decimal" placeholder="0.00" autocomplete="off" />
        </label>
        <small v-if="errors.base" class="field-error">{{ errors.base }}</small>
        <small v-else class="hint">用于展示税前年包和估算税前年终奖，不参与到手金额计算。</small>
      </div>

      <div class="salary-field">
        <label>
          <span>每月税后工资（元）</span>
          <input v-model="fields.net" aria-label="每月税后工资" inputmode="decimal" placeholder="0.00" autocomplete="off" />
        </label>
        <small v-if="errors.net" class="field-error">{{ errors.net }}</small>
        <small v-else class="hint">实际到账金额，工资计划和资本预测都使用它。</small>
      </div>

      <div class="salary-field">
        <label>
          <span>公积金月缴存（个人＋单位）</span>
          <input v-model="fields.provident" aria-label="公积金月缴存" inputmode="decimal" placeholder="0.00" autocomplete="off" />
        </label>
        <small v-if="errors.provident" class="field-error">{{ errors.provident }}</small>
        <small v-else class="hint">和现有公积金缴存计划一致，只填合计。</small>
      </div>

      <div class="salary-pair">
        <div class="salary-field">
          <label>
            <span>年终奖月数</span>
            <input v-model="fields.bonusMonths" aria-label="年终奖月数" inputmode="decimal" placeholder="0" autocomplete="off" />
          </label>
          <small v-if="errors.bonusMonths" class="field-error">{{ errors.bonusMonths }}</small>
        </div>
        <div class="salary-field">
          <label>
            <span>年终奖到手（元）</span>
            <input v-model="fields.bonusNet" aria-label="年终奖到手" inputmode="decimal" placeholder="0.00" autocomplete="off" />
          </label>
          <small v-if="errors.bonusNet" class="field-error">{{ errors.bonusNet }}</small>
        </div>
      </div>

      <div class="salary-pair">
        <div class="salary-field">
          <label>
            <span>发放月</span>
            <select v-model="fields.payout" aria-label="年终奖发放月">
              <option v-for="month in 12" :key="month" :value="String(month)">{{ month }} 月</option>
            </select>
          </label>
          <small v-if="errors.payout" class="field-error">{{ errors.payout }}</small>
        </div>
        <div class="salary-field">
          <label>
            <span>发薪日</span>
            <input v-model="fields.payDay" aria-label="发薪日" inputmode="numeric" placeholder="15" autocomplete="off" />
          </label>
          <small v-if="errors.payDay" class="field-error">{{ errors.payDay }}</small>
        </div>
      </div>

      <div class="salary-preview">
        <div><span>税前年包</span><b>¥{{ formatMoney(annualPackage) }}</b></div>
        <div><span>税后现金</span><b>¥{{ formatMoney(netAnnualCash) }}</b></div>
        <div><span>公积金年入账</span><b>¥{{ formatMoney(providentAnnual) }}</b></div>
      </div>

      <p v-if="serverError" class="form-error" role="alert">{{ serverError }}</p>

      <LoadingButton class="primary-button salary-save" type="submit" :loading="saving" loading-label="保存中…">
        保存工资档案
      </LoadingButton>

      <button
        v-if="profile"
        type="button"
        class="salary-disable"
        :disabled="saving"
        @click="confirmDisable = true"
      >停用工资档案</button>
    </form>

    <ConfirmSheet
      :open="confirmDisable"
      title="停用工资档案？"
      message="停用后工资、公积金和年终奖计划会保留，金额恢复可手动编辑；已经记下的期次和流水不变。"
      confirm-label="停用"
      danger
      :busy="saving"
      @confirm="confirmDisableProfile"
      @cancel="confirmDisable = false"
    />
  </BottomSheet>
</template>

<style scoped>
.salary-form {
  display: grid;
  gap: 16px;
  min-width: 0;
}

.salary-field {
  display: grid;
  gap: 6px;
  min-width: 0;
}

.salary-field label {
  display: grid;
  gap: 6px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.salary-field input,
.salary-field select {
  box-sizing: border-box;
  width: 100%;
  min-width: 0;
  min-height: 44px;
  padding: 10px 12px;
  border: 1px solid var(--color-border-default);
  border-radius: 12px;
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  font: inherit;
}

.salary-field input:focus,
.salary-field select:focus {
  outline: none;
  border-color: var(--color-border-focus);
}

.salary-pair {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  min-width: 0;
}

.salary-pair > * {
  min-width: 0;
}

.hint {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.salary-preview {
  display: grid;
  gap: 8px;
  padding: 14px;
  border-radius: 14px;
  background: var(--color-bg-subtle);
}

.salary-preview > div {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.salary-preview b {
  color: var(--color-text-primary);
  font-weight: 600;
}

.salary-save {
  width: 100%;
  min-height: 52px;
}

.salary-disable {
  justify-self: center;
  padding: 8px;
  border: 0;
  background: none;
  color: var(--color-finance-loss);
  font: inherit;
  font-size: var(--text-footnote);
}

.salary-disable:disabled {
  opacity: 0.5;
}
</style>
