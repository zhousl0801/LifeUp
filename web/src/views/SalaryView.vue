<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from '../api/client'
import { fetchPlanOverview } from '../api/plans'
import { disableSalaryProfile, fetchSalaryProfile, saveSalaryProfile } from '../api/salary'
import AppNav from '../components/AppNav.vue'
import BackButton from '../components/BackButton.vue'
import LoadingState from '../components/LoadingState.vue'
import SalaryProfileCard from '../components/SalaryProfileCard.vue'
import SalaryProfileSheet from '../components/SalaryProfileSheet.vue'
import { toast } from '../composables/useMessage'
import type { SalaryProfile } from '../types/salary'
import { navigateBack } from '../utils/navigation'
import { createRequestId } from '../utils/request'

const router = useRouter()
const profile = ref<SalaryProfile | null>(null)
const loading = ref(true)
const error = ref('')
const sheetOpen = ref(false)
const saving = ref(false)
const serverError = ref('')
const prefill = ref<{
  netMonthlySalary: number | null
  providentMonthly: number | null
  payDay: number | null
}>({ netMonthlySalary: null, providentMonthly: null, payDay: null })

onMounted(load)

async function load(): Promise<void> {
  loading.value = true
  error.value = ''

  try {
    profile.value = (await fetchSalaryProfile()).profile
  } catch (loadError) {
    error.value = getApiErrorMessage(loadError, '工资档案加载失败，请重试。')
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  navigateBack(router, { name: 'mine' })
}

async function openSheet(): Promise<void> {
  serverError.value = ''

  if (!profile.value) {
    try {
      const overview = await fetchPlanOverview()
      const salaryPlan = overview.plans.find(plan => plan.type === 'salaryIncome')
      const providentPlan = overview.plans.find(plan => plan.type === 'providentDeposit')
      prefill.value = {
        netMonthlySalary: salaryPlan?.amount ?? null,
        providentMonthly: providentPlan?.amount ?? null,
        payDay: salaryPlan?.dayOfMonth ?? 15
      }
    } catch {
      prefill.value = { netMonthlySalary: null, providentMonthly: null, payDay: 15 }
    }
  }

  sheetOpen.value = true
}

function closeSheet(): void {
  if (!saving.value) {
    sheetOpen.value = false
  }
}

async function saveSalary(next: SalaryProfile): Promise<void> {
  if (saving.value) {
    return
  }

  saving.value = true
  serverError.value = ''

  try {
    const result = await saveSalaryProfile({ ...next, requestId: createRequestId() })
    profile.value = result.profile
    sheetOpen.value = false
    toast.success('工资档案已保存，计划和预测已同步。')
    await load()
  } catch (saveError) {
    serverError.value = getApiErrorMessage(saveError, '保存工资档案失败，请重试。')
  } finally {
    saving.value = false
  }
}

async function disableSalary(): Promise<void> {
  if (saving.value) {
    return
  }

  saving.value = true
  serverError.value = ''

  try {
    const result = await disableSalaryProfile(createRequestId())
    profile.value = result.profile
    sheetOpen.value = false
    toast.success('工资档案已停用，计划金额恢复可编辑。')
    await load()
  } catch (disableError) {
    serverError.value = getApiErrorMessage(disableError, '停用工资档案失败，请重试。')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <main class="page with-nav salary-page">
    <header class="salary-page-header">
      <BackButton @back="goBack" />
      <h1>工资档案</h1>
      <span class="header-spacer" />
    </header>

    <p class="page-intro">
      工资、公积金和年终奖的汇总，金额默认隐藏。保存后会同步工资、公积金和年终奖三个自动计划，只影响未发生的期次。
    </p>

    <LoadingState v-if="loading" variant="card" label="正在读取工资档案" />

    <template v-else>
      <SalaryProfileCard
        :profile="profile"
        @create="openSheet"
        @edit="openSheet"
      />
      <p v-if="error" class="salary-error">{{ error }}</p>

      <div class="list-box">
        <RouterLink to="/plans" class="list-row">
          <span>自动计划</span>
          <b>查看工资 / 公积金 / 年终奖 ›</b>
        </RouterLink>
      </div>
    </template>

    <SalaryProfileSheet
      :open="sheetOpen"
      :profile="profile"
      :prefill="prefill"
      :saving="saving"
      :server-error="serverError"
      @save="saveSalary"
      @disable="disableSalary"
      @close="closeSheet"
    />

    <AppNav />
  </main>
</template>

<style scoped>
.salary-page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.header-spacer {
  width: 36px;
  flex-shrink: 0;
}

.salary-page-header h1 {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 600;
  letter-spacing: -0.4px;
}

.page-intro {
  margin: 14px 0 18px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.8;
}

.salary-error {
  margin: -8px 0 12px 4px;
  color: var(--color-finance-loss);
  font-size: var(--text-caption);
}

.list-box {
  overflow: hidden;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.list-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  box-sizing: border-box;
  width: 100%;
  min-height: 60px;
  padding: 0 18px;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  font-size: var(--text-subhead);
  font-weight: 500;
  text-align: left;
  text-decoration: none;
}

.list-row:hover {
  background: var(--color-bg-hover);
}

.list-row b {
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}
</style>
