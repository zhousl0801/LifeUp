<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from '../api/client'
import LoadingState from '../components/LoadingState.vue'
import LoadingButton from '../components/LoadingButton.vue'
import { useLedgerStore } from '../stores/ledger'
import { MaximumAmount, parseAmount, todayInBusinessTimeZone } from '../utils/format'

const router = useRouter()
const ledgerStore = useLedgerStore()
const { isSaving } = storeToRefs(ledgerStore)

const today = todayInBusinessTimeZone()
const isChecking = ref(true)
const loadError = ref('')
const form = reactive({
  startDate: today,
  cashAsOfDate: today,
  cashBalance: ''
})

watch(() => form.startDate, (next, previous) => {
  if (form.cashAsOfDate === previous || form.cashAsOfDate < next) {
    form.cashAsOfDate = next
  }
})
const errors = reactive({
  startDate: '',
  cashBalance: '',
  form: ''
})

onMounted(checkLedger)

async function checkLedger(): Promise<void> {
  if (ledgerStore.isInitialized) {
    await router.replace({ name: 'home' })
    return
  }

  if (ledgerStore.lastLoadStatus === 'missing') {
    isChecking.value = false
    return
  }

  isChecking.value = true
  loadError.value = ''
  const status = await ledgerStore.load()
  isChecking.value = false

  if (status === 'ready') {
    await router.replace({ name: 'home' })
    return
  }

  if (status === 'error') {
    loadError.value = ledgerStore.errorMessage ?? '无法读取账本。'
  }
}

async function submit(): Promise<void> {
  clearErrors()

  if (!form.startDate) {
    errors.startDate = '请选择起始日期。'
    return
  }

  if (form.startDate > today) {
    errors.startDate = '起始日期不能晚于今天。'
    return
  }

  if (!form.cashAsOfDate || form.cashAsOfDate < form.startDate || form.cashAsOfDate > today) {
    errors.form = '现金余额日期须在账本起始日期与今天之间。'
    return
  }

  const amount = parseAmount(form.cashBalance)

  if (amount === null) {
    errors.cashBalance = `请输入不小于 0 的金额，最多两位小数，且不超过 ${MaximumAmount.toLocaleString('zh-CN')}。`
    return
  }

  try {
    await ledgerStore.initialize({
      startDate: form.startDate,
      cashAsOfDate: form.cashAsOfDate,
      cashBalance: amount
    })
    await router.replace({ name: 'home' })
  } catch (error) {
    errors.form = getApiErrorMessage(error, '保存失败，请检查网络后重试。')
  }
}

function clearErrors(): void {
  errors.startDate = ''
  errors.cashBalance = ''
  errors.form = ''
}
</script>

<template>
  <main class="page">
    <header class="app-header">
      <div>
        <p class="app-name">LifeUp</p>
        <p class="app-sub">建立起始账本</p>
      </div>
    </header>

    <LoadingState v-if="isChecking" variant="card" label="正在检查账本" />

    <section v-else-if="loadError" class="state">
      <p>{{ loadError }}</p>
      <LoadingButton class="secondary-button" :loading="isChecking" loading-label="检查中…" @click="checkLedger">重新检查</LoadingButton>
    </section>

    <template v-else>
      <section class="intro">
        <h1>今天手上有多少可用现金？</h1>
        <p>填一个起点金额即可开始记账，不计为收入。基金、股票、公积金和贵金属可以稍后再补录。</p>
      </section>

      <form class="setup-form" novalidate @submit.prevent="submit">
        <label class="field">
          <span>可用现金（元）</span>
          <input
            v-model="form.cashBalance"
            type="text"
            inputmode="decimal"
            autocomplete="off"
            placeholder="0.00"
            required
          />
          <small class="field-hint">微信、支付宝、储蓄卡等日常可用现金合计。</small>
          <small v-if="errors.cashBalance" class="field-error">{{ errors.cashBalance }}</small>
        </label>

        <details class="past-dates">
          <summary>从过去某天开始记（默认今天）</summary>
          <div class="past-dates-fields">
            <label class="field">
              <span>从哪天开始记账</span>
              <input v-model="form.startDate" type="date" :max="today" required />
              <small v-if="errors.startDate" class="field-error">{{ errors.startDate }}</small>
            </label>
            <label class="field">
              <span>这笔余额是哪天的</span>
              <input v-model="form.cashAsOfDate" type="date" :min="form.startDate" :max="today" required />
              <small class="field-hint">只在补录历史账本时才需要和起始日不同。</small>
            </label>
          </div>
        </details>

        <p v-if="errors.form" class="form-error" role="alert">{{ errors.form }}</p>

        <LoadingButton class="primary-button" type="submit" :loading="isSaving" loading-label="保存中…">
          开始记账
        </LoadingButton>
      </form>
    </template>
  </main>
</template>

<style scoped>
.intro h1 {
  margin: 0 0 10px;
  font-size: 1.625rem;
  font-weight: 600;
  letter-spacing: -0.6px;
}

.intro p {
  margin: 0 0 28px;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
  line-height: 1.8;
}

.setup-form {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.field {
  display: grid;
  grid-template-columns: minmax(0, auto) minmax(0, 1fr);
  align-items: center;
  column-gap: 12px;
  row-gap: 4px;
  min-height: 48px;
  padding: 10px 14px;
  background: var(--color-bg-surface);
  border-bottom: 1px solid var(--color-border-subtle);
  color: var(--color-text-primary);
  font-size: 0.875rem;
}

.setup-form > .field:first-of-type {
  border-top-left-radius: 16px;
  border-top-right-radius: 16px;
}

.setup-form > .field:last-of-type {
  border-bottom-left-radius: 16px;
  border-bottom-right-radius: 16px;
  border-bottom: 0;
  margin-bottom: 14px;
}

.past-dates {
  margin-bottom: 14px;
}

.past-dates > summary {
  padding: 6px 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  cursor: pointer;
}

.past-dates-fields {
  margin-top: 8px;
  overflow: hidden;
  border-radius: 16px;
}

.past-dates-fields > .field:last-child {
  border-bottom: 0;
}

.field > span {
  font-weight: 500;
}

.field input {
  appearance: none;
  -webkit-appearance: none;
  box-sizing: border-box;
  width: 100%;
  min-width: 0;
  min-height: 30px;
  padding: 2px 0;
  border: 0;
  border-radius: 0;
  background: transparent;
  color: var(--color-text-primary);
  font-size: 0.9375rem;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
  text-align: right;
}

.field input[type='date'] {
  justify-self: end;
  width: auto;
}

.field input[type='date']::-webkit-date-and-time-value {
  margin: 0;
  text-align: right;
}

.field input:focus-visible {
  outline: none;
  box-shadow: none;
}

.field:focus-within {
  background: color-mix(in srgb, var(--color-brand) 4%, var(--color-bg-surface));
}

.field > small {
  grid-column: 1 / -1;
}

.field-hint {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.field-error {
  color: var(--color-error);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.form-error {
  margin: 0;
  padding: 10px 12px;
  border-radius: var(--radius-tag);
  background: var(--color-error-bg);
  color: var(--color-error);
  font-size: 0.75rem;
  line-height: 1.7;
}
</style>
