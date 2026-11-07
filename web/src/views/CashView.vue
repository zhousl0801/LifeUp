<script setup lang="ts">
import { navigateBack } from '../utils/navigation'
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { fetchCashOverview, reconcileCash } from '../api/cash'
import { ApiError, getApiErrorMessage } from '../api/client'
import AppNav from '../components/AppNav.vue'
import AnimatedMoney from '../components/AnimatedMoney.vue'
import BackButton from '../components/BackButton.vue'
import BottomSheet from '../components/BottomSheet.vue'
import LoadingButton from '../components/LoadingButton.vue'
import LoadingState from '../components/LoadingState.vue'
import { amountTone, formatMoney, formatSignedMoney, parseAmount, subtractAmounts } from '../utils/format'
import { formatPeriodLabel } from '../utils/period'
import type { CashActivity, CashActivityType, CashOverview } from '../types/cash'
import { createRequestId } from '../utils/request'
import type { EntryKind } from '../types/entry'

type CashFilter = 'all' | CashActivityType

interface ActivityGroup {
  date: string
  activities: CashActivity[]
}

const route = useRoute()
const router = useRouter()
const overview = ref<CashOverview | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')
const notice = ref('')
const filter = ref<CashFilter>('all')
const isReconcileOpen = ref(false)
const actualBalance = ref('')
const reason = ref('')
const formError = ref('')
const isSaving = ref(false)
const requestId = ref(createRequestId())
const expectedVersion = ref(0)

const filters: Array<{ value: CashFilter; label: string }> = [
  { value: 'all', label: '全部' },
  { value: 'daily', label: '日常收支' },
  { value: 'assetTransfer', label: '资产流转' },
  { value: 'adjustment', label: '余额调整' }
]

const monthLabel = computed(() =>
  overview.value ? formatPeriodLabel(overview.value.from, 'month') : '')
const filteredActivities = computed(() => {
  const activities = overview.value?.activities ?? []

  if (filter.value === 'all') {
    return activities
  }

  return activities.filter((activity) => activity.type === filter.value)
})
const groupedActivities = computed<ActivityGroup[]>(() => {
  const groups = new Map<string, CashActivity[]>()

  for (const activity of filteredActivities.value) {
    const activities = groups.get(activity.businessDate) ?? []
    activities.push(activity)
    groups.set(activity.businessDate, activities)
  }

  return [...groups.entries()].map(([date, activities]) => ({ date, activities }))
})
const previewDifference = computed(() => {
  if (!overview.value) {
    return null
  }

  const amount = parseAmount(actualBalance.value)

  return amount === null ? null : subtractAmounts(amount, overview.value.balance)
})
const isBalanced = computed(() => previewDifference.value === 0)
const canSave = computed(() =>
  previewDifference.value !== null && !isSaving.value)

onMounted(async () => {
  await loadCash()

  if (route.query.action === 'reconcile' && overview.value) {
    openReconcile()
    void router.replace({ name: 'cash' })
  }
})

async function loadCash(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''

  try {
    overview.value = await fetchCashOverview()
  } catch (error) {
    overview.value = null

    if (error instanceof ApiError && error.status === 404) {
      await router.replace({ name: 'setup' })
      return
    }

    errorMessage.value = getApiErrorMessage(error, '现金数据加载失败，请重试。')
  } finally {
    isLoading.value = false
  }
}

function openEntry(kind: EntryKind): void {
  void router.push({
    name: 'entry',
    query: { kind, returnTo: 'cash' }
  })
}

function openActivity(activity: CashActivity): void {
  if (activity.type === 'daily') {
    void router.push({
      name: 'entry',
      query: { id: activity.id, returnTo: 'cash' }
    })
    return
  }

  if (activity.type !== 'assetTransfer' || !activity.assetType) {
    return
  }

  const routeName = activity.assetType === 'providentFund'
    ? 'provident'
    : activity.assetType === 'fund' ? 'funds' : activity.assetType === 'stock' ? 'stocks' : 'gold'

  void router.push({ name: routeName })
}

function activityMark(activity: CashActivity): string {
  if (activity.type === 'adjustment') {
    return '调'
  }

  if (activity.type === 'assetTransfer') {
    return '转'
  }

  return activity.amount > 0 ? '入' : '出'
}

function activityNote(activity: CashActivity): string {
  const parts: string[] = []

  if (activity.note) {
    parts.push(activity.note)
  }

  if (activity.source === 'automatic') {
    parts.push('自动记账')
  }

  if (parts.length === 0) {
    return activity.type === 'daily' ? '日常收支' : '资产流转'
  }

  return parts.join(' · ')
}

function activityAmountClass(activity: CashActivity): string {
  if (activity.type === 'adjustment' || activity.type === 'assetTransfer') {
    return 'transfer'
  }

  return activity.amount > 0 ? 'income' : ''
}

function formatDay(date: string): string {
  const parts = date.split('-')

  if (parts.length !== 3) {
    return date
  }

  return `${Number(parts[1])}月${Number(parts[2])}日`
}

function openReconcile(): void {
  if (!overview.value) {
    return
  }

  requestId.value = createRequestId()
  expectedVersion.value = overview.value.version
  actualBalance.value = overview.value.balance.toFixed(2)
  reason.value = ''
  formError.value = ''
  isReconcileOpen.value = true
}

function closeReconcile(): void {
  if (!isSaving.value) {
    isReconcileOpen.value = false
  }
}

async function submitReconcile(): Promise<void> {
  const amount = parseAmount(actualBalance.value)

  if (amount === null) {
    formError.value = '请输入不小于 0、最多两位小数的实际余额。'
    return
  }

  if (previewDifference.value !== 0 && !reason.value.trim()) {
    formError.value = '存在差额时，请填写调整原因。'
    return
  }

  if (isSaving.value) {
    return
  }

  isSaving.value = true
  formError.value = ''

  try {
    const result = await reconcileCash({
      requestId: requestId.value,
      expectedVersion: expectedVersion.value,
      actualBalance: amount,
      reason: reason.value.trim()
    })

    isReconcileOpen.value = false
    notice.value = result.isBalanced
      ? '核对一致，无需调整。'
      : `余额已调整为 ¥${formatMoney(result.cashBalance)}，生活收支保持不变。`
    await loadCash()
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      await router.replace({ name: 'setup' })
      return
    }

    if (error instanceof ApiError && error.status === 409) {
      await loadCash()
      expectedVersion.value = overview.value?.version ?? expectedVersion.value
      requestId.value = createRequestId()
    }
    formError.value = getApiErrorMessage(error, '核对失败，请重试。')
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <main class="page cash-page with-nav">
    <header class="cash-header">
      <BackButton @back="navigateBack(router)" />
      <h1>现金</h1>
      <button type="button" class="reconcile-link" @click="openReconcile">核对余额</button>
    </header>

    <LoadingState v-if="isLoading && !overview" variant="hero" label="正在读取现金" />
    <LoadingState v-if="isLoading && !overview" variant="list" :rows="3" label="正在读取现金明细" />

    <section v-else-if="errorMessage" class="state">
      <p>{{ errorMessage }}</p>
      <LoadingButton class="secondary-button" :loading="isLoading" loading-label="加载中…" @click="loadCash">重新加载</LoadingButton>
    </section>

    <template v-else-if="overview">
      <section class="cash-hero">
        <p class="hero-label">
          可用现金
          <span class="hero-tag">统一余额</span>
        </p>
        <h2 class="hero-balance"><span>¥</span><AnimatedMoney :value="overview.balance" prefix="" /></h2>
        <div class="hero-metrics">
          <div>
            <span>本月现金流入</span>
            <b class="income">{{ formatMoney(overview.inflow) }}</b>
          </div>
          <div>
            <span>本月现金流出</span>
            <b>{{ formatMoney(overview.outflow) }}</b>
          </div>
          <div>
            <span>余额净变动</span>
            <b :class="amountTone(overview.netChange)">{{ formatSignedMoney(overview.netChange) }}</b>
          </div>
        </div>
      </section>

      <div class="cash-actions">
        <button type="button" class="action-primary" @click="openEntry('expense')">记支出</button>
        <button type="button" class="action-secondary" @click="openEntry('income')">记收入</button>
      </div>

      <p v-if="notice" class="notice" role="status">{{ notice }}</p>

      <div class="section-head">
        <h2>现金变动</h2>
        <span class="meta">{{ monthLabel }}</span>
      </div>

      <div class="filter-tabs" role="tablist" aria-label="现金变动类型">
        <button
          v-for="item in filters"
          :key="item.value"
          type="button"
          role="tab"
          :aria-selected="filter === item.value"
          :class="{ active: filter === item.value }"
          @click="filter = item.value"
        >
          {{ item.label }}
        </button>
      </div>

      <section v-for="group in groupedActivities" :key="group.date" class="day-group">
        <header class="day-header">
          <span>{{ formatDay(group.date) }}</span>
          <span>{{ group.activities.length }} 笔</span>
        </header>
        <div class="day-card">
          <component
            :is="activity.type === 'daily' || activity.type === 'assetTransfer' ? 'button' : 'div'"
            v-for="activity in group.activities"
            :key="activity.id"
            :type="activity.type === 'daily' || activity.type === 'assetTransfer' ? 'button' : undefined"
            class="activity-row"
            :class="{ clickable: activity.type === 'daily' || activity.type === 'assetTransfer' }"
            @click="openActivity(activity)"
          >
            <span class="activity-mark">{{ activityMark(activity) }}</span>
            <span class="activity-copy">
              <strong>{{ activity.title }}</strong>
              <small>{{ activityNote(activity) }}</small>
            </span>
            <b class="activity-value" :class="activityAmountClass(activity)">
              {{ formatSignedMoney(activity.amount) }}
            </b>
          </component>
        </div>
      </section>

      <div v-if="!filteredActivities.length" class="empty-state">
        <p>{{ filter === 'all' ? '本月还没有现金变动' : '暂无这类变动' }}</p>
        <button
          v-if="filter === 'all'"
          type="button"
          class="primary-button"
          @click="openEntry('expense')"
        >
          记一笔支出
        </button>
      </div>
    </template>

    <BottomSheet
      :open="isReconcileOpen && overview !== null"
      title="核对现金余额"
      subtitle="按实际可用现金修正差额"
      :close-disabled="isSaving"
      @close="closeReconcile"
    >
      <template v-if="overview">
          <div class="reconcile-fields">
            <div class="balance-row">
              <span>当前账面余额</span>
              <b>¥{{ formatMoney(overview.balance) }}</b>
            </div>

            <label class="field">
              <span>核对后的实际余额（元）</span>
              <input
                v-model="actualBalance"
                type="text"
                inputmode="decimal"
                autocomplete="off"
                placeholder="0.00"
                @input="formError = ''"
              />
            </label>

            <label class="field">
              <span>调整原因</span>
              <input
                v-model="reason"
                type="text"
                maxlength="100"
                placeholder="例如：漏记一笔消费"
                @input="formError = ''"
              />
            </label>

            <div class="difference-row">
              <span>核对差额</span>
              <b :class="{ transfer: previewDifference !== null && !isBalanced }">
                {{ formatSignedMoney(previewDifference) }}
              </b>
            </div>

            <p class="sheet-note">
              {{ isBalanced ? '核对一致，不会生成调整记录。' : '只记录与当前余额的差额，不改变生活收支。' }}
            </p>

            <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

            <LoadingButton
              class="primary-button"
              :loading="isSaving"
              :disabled="!canSave"
              loading-label="保存中…"
              @click="submitReconcile"
            >
              保存核对
            </LoadingButton>
          </div>
      </template>
    </BottomSheet>
    <AppNav />
  </main>
</template>

<style scoped>
.reconcile-fields {
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.cash-page {
  padding-bottom: calc(97px + env(safe-area-inset-bottom));
}

.cash-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
}

.cash-header h1 {
  margin: 0;
  font-size: var(--text-title3);
  font-weight: 700;
  letter-spacing: -0.4px;
}

.reconcile-link {
  min-height: 40px;
  margin-left: auto;
  padding: 0 16px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
  color: var(--color-brand);
  font-size: 0.875rem;
  font-weight: 600;
}

.reconcile-link:hover {
  background: var(--color-brand-soft);
}

.cash-hero {
  padding: var(--card-padding);
  border-radius: var(--radius-card);
  background: linear-gradient(180deg, var(--color-hero-bg) 0%, var(--color-hero-bg-end) 100%);
  box-shadow: var(--shadow-hero);
  color: var(--color-hero-text);
}

.hero-label {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.hero-tag {
  padding: 2px 8px;
  border-radius: var(--radius-pill);
  background: var(--color-hero-surface);
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.hero-balance {
  margin: 10px 0 0;
  color: var(--color-hero-text);
  font-size: clamp(2rem, 10.5vw, 2.375rem);
  font-weight: 700;
  letter-spacing: -0.04em;
  line-height: 1.2;
  font-variant-numeric: tabular-nums;
}

.hero-balance span {
  margin-right: 5px;
  color: var(--color-hero-text-secondary);
  font-size: 1.1875rem;
  font-weight: 500;
  letter-spacing: 0;
}

.hero-metrics {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  margin-top: 20px;
}

.hero-metrics > div + div {
  padding-left: 12px;
  border-left: 1px solid var(--color-hero-border);
}

.hero-metrics span {
  display: block;
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.hero-metrics b {
  display: block;
  margin-top: 7px;
  color: var(--color-hero-text);
  font-size: 0.9375rem;
  font-weight: 600;
  letter-spacing: -0.3px;
  font-variant-numeric: tabular-nums;
}

.hero-metrics b.income {
  color: var(--color-accent-mint);
}

.transfer {
  color: var(--color-finance-transfer);
}

.cash-actions {
  display: flex;
  gap: 10px;
  margin-top: 16px;
}

.cash-actions button {
  flex: 1;
  min-height: 48px;
  border: 0;
  border-radius: var(--radius-pill);
  font-size: 0.9375rem;
  font-weight: 600;
}

.action-primary {
  background: var(--color-brand);
  color: var(--color-on-brand);
}

.action-primary:hover {
  background: var(--color-brand-hover);
}

.action-secondary {
  background: var(--color-bg-subtle);
  color: var(--color-brand);
}

.action-secondary:hover {
  background: var(--color-brand-soft);
}

.notice {
  margin: 14px 0 0;
  padding: 10px 12px;
  border-radius: var(--radius-tag);
  background: var(--color-success-bg);
  color: var(--color-success);
  font-size: 0.75rem;
  line-height: 1.7;
}

.section-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  margin: 26px 0 12px;
}

.section-head h2 {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
  letter-spacing: -0.3px;
}

.meta {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.filter-tabs {
  display: flex;
  gap: 6px;
  margin-bottom: 4px;
  overflow-x: auto;
  scrollbar-width: none;
}

.filter-tabs::-webkit-scrollbar {
  display: none;
}

.filter-tabs button {
  flex: 0 0 auto;
  min-height: 38px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  font-size: 0.8125rem;
  font-weight: 600;
  white-space: nowrap;
}

.filter-tabs button:hover:not(.active) {
  background: var(--color-bg-hover);
  color: var(--color-brand);
}

.filter-tabs button.active {
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-weight: 600;
}

.day-group {
  margin-top: 16px;
}

.day-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
  padding: 0 2px 7px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.day-card {
  overflow: hidden;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.activity-row {
  display: flex;
  align-items: center;
  gap: 12px;
  box-sizing: border-box;
  width: 100%;
  min-height: 64px;
  padding: 14px 16px;
  border: 0;
  border-bottom: 1px solid var(--color-border-subtle);
  background: transparent;
  color: var(--color-text-primary);
  text-align: left;
}

.activity-row:last-child {
  border-bottom: 0;
}

.activity-row.clickable:hover {
  background: var(--color-bg-hover);
}

.activity-mark {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  flex: 0 0 auto;
  width: 33px;
  height: 33px;
  border-radius: 50%;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 0.75rem;
  font-weight: 500;
}

.activity-copy {
  display: flex;
  flex-direction: column;
  gap: 3px;
  flex: 1;
  min-width: 0;
}

.activity-copy strong {
  overflow: hidden;
  font-size: 0.9375rem;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.activity-copy small {
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.activity-value {
  flex: 0 0 auto;
  font-size: 0.9375rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.empty-state {
  margin-top: 16px;
  padding: 34px 20px;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
  text-align: center;
}

.empty-state p {
  margin: 0 0 14px;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.empty-state .primary-button {
  width: 100%;
}

.summary-note {
  margin: 14px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
  text-align: center;
}

.balance-row,
.difference-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  padding: 11px 12px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.balance-row b,
.difference-row b {
  color: var(--color-text-primary);
  font-size: 0.9375rem;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 8px;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.field input {
  box-sizing: border-box;
  width: 100%;
  min-height: 48px;
  padding: 0 14px;
  border: 1px solid transparent;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  font-size: 1rem;
}

.field input:hover {
  border-color: var(--color-border-default);
}

.field input:focus-visible {
  border-color: var(--color-border-focus);
  background: var(--color-bg-surface);
}

.sheet-note {
  margin: 0;
  padding: 10px 12px;
  border-radius: var(--radius-tag);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
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

.primary-button {
  width: 100%;
}

</style>
