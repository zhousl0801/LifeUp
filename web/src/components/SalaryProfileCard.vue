<script setup lang="ts">
import { computed, ref } from 'vue'
import type { SalaryProfile } from '../types/salary'
import { formatMoney } from '../utils/format'

const props = defineProps<{
  profile: SalaryProfile | null
}>()

defineEmits<{
  create: []
  edit: []
}>()

const masked = ref(true)

const netAnnualCash = computed(() => props.profile
  ? props.profile.netMonthlySalary * 12 + props.profile.bonusNetAmount
  : 0)
const providentAnnual = computed(() => props.profile
  ? props.profile.providentMonthly * 12
  : 0)

function money(value: number): string {
  return masked.value ? '••••' : `¥${formatMoney(value)}`
}
</script>

<template>
  <section class="salary-card" aria-label="工资档案">
    <header class="salary-head">
      <div class="salary-heading">
        <span class="salary-kicker">工资档案</span>
        <strong>{{ profile ? '工资、公积金和年终奖' : '还没有工资档案' }}</strong>
      </div>
      <div v-if="profile" class="salary-head-actions">
        <button type="button" class="salary-action" @click="masked = !masked">
          {{ masked ? '显示金额' : '隐藏金额' }}
        </button>
        <button type="button" class="salary-action is-brand" @click="$emit('edit')">编辑</button>
      </div>
    </header>

    <template v-if="profile">
      <div class="salary-grid">
        <div>
          <span>税后工资</span>
          <b>{{ money(profile.netMonthlySalary) }}/月</b>
        </div>
        <div>
          <span>公积金（个人＋单位）</span>
          <b>{{ money(profile.providentMonthly) }}/月</b>
        </div>
      </div>

      <div v-if="profile.bonusMonths > 0" class="salary-bonus">
        <span>年终奖</span>
        <b>{{ money(profile.bonusNetAmount) }}</b>
      </div>

      <div class="salary-annual">
        <div>
          <span>年度到手现金</span>
          <b>{{ money(netAnnualCash) }}</b>
        </div>
        <div>
          <span>公积金年积累</span>
          <b>{{ money(providentAnnual) }}</b>
        </div>
      </div>
    </template>

    <template v-else>
      <p class="salary-note">
        在个人信息里维护税前 Base、税后工资、公积金和年终奖，自动计划和资本预测都会用它。
      </p>
      <button type="button" class="secondary-button salary-create" @click="$emit('create')">
        建立工资档案
      </button>
    </template>
  </section>
</template>

<style scoped>
.salary-card {
  margin: 0 0 18px;
  padding: 18px;
  border: 1px solid var(--color-border-default);
  border-radius: 22px;
  background: var(--color-bg-surface);
}

.salary-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.salary-heading {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.salary-kicker {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  letter-spacing: 0.08em;
}

.salary-heading strong {
  font-size: var(--text-subhead);
}

.salary-head-actions {
  display: flex;
  flex: 0 0 auto;
  align-items: center;
  gap: 2px;
}

.salary-action {
  min-height: 44px;
  padding: 0 6px;
  border: 0;
  background: transparent;
  color: var(--color-text-secondary);
  font: inherit;
  font-size: var(--text-caption);
  font-weight: 600;
}

.salary-action.is-brand {
  color: var(--color-brand);
}

.salary-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-top: 16px;
}

.salary-grid > div,
.salary-annual > div {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.salary-grid span,
.salary-annual span,
.salary-bonus > span {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.salary-grid b,
.salary-annual b,
.salary-bonus b {
  font-size: var(--text-footnote);
  font-weight: 600;
}

.salary-bonus {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  align-items: baseline;
  gap: 12px;
  margin-top: 14px;
  padding-top: 12px;
  border-top: 1px solid var(--color-border-subtle);
}

.salary-annual {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-top: 14px;
  padding-top: 12px;
  border-top: 1px solid var(--color-border-subtle);
}

.salary-note {
  margin: 12px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.6;
}

.salary-create {
  margin-top: 14px;
}
</style>
