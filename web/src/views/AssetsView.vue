<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { fetchAssetTrend, fetchMonthChange, updateAssetCoverage } from '../api/ledger'
import { getApiErrorMessage } from '../api/client'
import AppNav from '../components/AppNav.vue'
import AnimatedMoney from '../components/AnimatedMoney.vue'
import AssetDistribution from '../components/AssetDistribution.vue'
import BackButton from '../components/BackButton.vue'
import BottomSheet from '../components/BottomSheet.vue'
import LoadingState from '../components/LoadingState.vue'
import { useLedgerStore } from '../stores/ledger'
import { todayInBusinessTimeZone } from '../utils/format'
import type { AssetKey, LedgerOverview, MonthChange } from '../types/ledger'

const router = useRouter()
const ledgerStore = useLedgerStore()
const overview = ref<LedgerOverview | null>(null)
const monthChange = ref<MonthChange | null>(null)
const updateDate = ref(todayInBusinessTimeZone())
const isLoading = ref(false)
const errorMessage = ref('')
const notice = ref('')
const maintenanceOpen = ref(false)

const missingAssets = computed(() => {
  const ledger = overview.value

  if (!ledger) {
    return []
  }

  return [
    { label: '基金', route: '/funds', configured: ledger.fund.isConfigured, notHeld: ledger.fund.notHeld },
    { label: '股票', route: '/stocks', configured: ledger.stock.isConfigured, notHeld: ledger.stock.notHeld },
    { label: '公积金', route: '/provident', configured: ledger.providentFund.isConfigured, notHeld: ledger.providentFund.notHeld },
    { label: '积存金', route: '/gold', configured: ledger.gold.isConfigured, notHeld: ledger.gold.notHeld }
  ].filter((asset) => !asset.configured && !asset.notHeld)
})

const primaryMissing = computed(() => missingAssets.value[0] ?? null)

const maintenanceRows = computed(() => {
  const ledger = overview.value

  if (!ledger) {
    return []
  }

  return [
    {
      key: 'cash' as const,
      label: '现金',
      note: ledger.cash.note,
      route: '/cash?action=reconcile',
      action: '核对余额',
      notHeld: false
    },
    {
      key: 'fund' as const,
      label: '基金',
      note: ledger.fund.note ?? (ledger.fund.isConfigured ? '基金持仓与交易' : '尚未录入'),
      route: '/funds',
      action: ledger.fund.notHeld ? '恢复' : ledger.fund.isConfigured ? '查看' : '去录入',
      notHeld: ledger.fund.notHeld
    },
    {
      key: 'stock' as const,
      label: '股票',
      note: ledger.stock.note ?? (ledger.stock.isConfigured ? '持仓与交易' : '尚未录入'),
      route: '/stocks',
      action: ledger.stock.notHeld ? '恢复' : ledger.stock.isConfigured ? '查看' : '去录入',
      notHeld: ledger.stock.notHeld
    },
    {
      key: 'providentFund' as const,
      label: '公积金',
      note: ledger.providentFund.note ?? (ledger.providentFund.isConfigured ? '统一公积金余额' : '尚未录入'),
      route: '/provident',
      action: ledger.providentFund.notHeld ? '恢复' : ledger.providentFund.isConfigured ? '更新' : '去录入',
      notHeld: ledger.providentFund.notHeld
    },
    {
      key: 'gold' as const,
      label: '积存金',
      note: ledger.gold.note ?? (ledger.gold.isConfigured ? '持仓与交易' : '尚未录入'),
      route: '/gold',
      action: ledger.gold.notHeld ? '恢复' : ledger.gold.isConfigured ? '核对' : '去录入',
      notHeld: ledger.gold.notHeld
    }
  ]
})

onMounted(load)

async function load(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''

  try {
    const status = await ledgerStore.load()

    if (status === 'missing') {
      await router.replace({ name: 'setup' })
      return
    }

    if (status === 'error') {
      errorMessage.value = ledgerStore.errorMessage ?? '资产数据加载失败，请重试。'
      return
    }

    overview.value = ledgerStore.overview

    try {
      const trend = await fetchAssetTrend('month')
      updateDate.value = trend.to
    } catch {
      updateDate.value = todayInBusinessTimeZone()
    }

    try {
      monthChange.value = await fetchMonthChange()
    } catch {
      monthChange.value = null
    }
  } finally {
    isLoading.value = false
  }
}

async function toggleNotHeld(key: AssetKey, notHeld: boolean): Promise<void> {
  const ledger = overview.value

  if (!ledger || key === 'cash') {
    return
  }

  errorMessage.value = ''
  notice.value = ''

  try {
    overview.value = await updateAssetCoverage({
      fundNotHeld: key === 'fund' ? notHeld : ledger.fundNotHeld,
      providentFundNotHeld: key === 'providentFund' ? notHeld : ledger.providentFundNotHeld,
      goldNotHeld: key === 'gold' ? notHeld : ledger.goldNotHeld,
      stockNotHeld: key === 'stock' ? notHeld : ledger.stockNotHeld
    })
    notice.value = notHeld ? '已标记为不持有。' : '已恢复为待录入。'
  } catch (error) {
    errorMessage.value = getApiErrorMessage(error, '保存失败，请重试。')
  }
}

function goHome(): void {
  void router.push({ name: 'home' })
}
</script>

<template>
  <main class="page with-nav">
    <header class="page-head">
      <div class="page-head-row">
        <BackButton @back="goHome" />
        <div class="page-head-title">
          <h1>资产</h1>
        </div>
        <button type="button" class="maintenance-link" @click="maintenanceOpen = true">维护</button>
      </div>
    </header>

    <LoadingState v-if="isLoading" variant="hero" label="正在读取资产" />
    <LoadingState v-if="isLoading" variant="list" :rows="3" label="正在读取资产分布" />

    <section v-else-if="errorMessage && !overview" class="state">
      <p>{{ errorMessage }}</p>
      <button class="secondary-button" type="button" @click="load">重新加载</button>
    </section>

    <template v-else-if="overview">
      <section class="hero">
        <p class="hero-label">
          {{ overview.coverageComplete ? '总资产' : '已记录资产' }}
          <span class="tag">{{ overview.currency }}</span>
        </p>
        <h1 class="hero-value"><span class="currency">¥</span><AnimatedMoney :value="overview.totalAssets" prefix="" /></h1>
      </section>

      <p v-if="notice" class="notice">{{ notice }}</p>
      <p v-if="errorMessage" class="form-error" role="alert">{{ errorMessage }}</p>

      <section v-if="primaryMissing" class="asset-callout">
        <div>
          <strong>还有 {{ missingAssets.length }} 类资产未录入</strong>
          <p>可以录入余额，或标记为不持有。</p>
        </div>
        <RouterLink :to="primaryMissing.route" class="callout-action">去录入 ›</RouterLink>
      </section>

      <AssetDistribution
        :overview="overview"
        :changes="monthChange?.assetChanges"
        :comparable="monthChange?.hasComparableData ?? false"
        editable
        @toggle-not-held="toggleNotHeld"
      />
    </template>

    <BottomSheet
      :open="maintenanceOpen"
      title="维护资产"
      subtitle="录入、核对余额，或恢复已标记不持有的资产"
      @close="maintenanceOpen = false"
    >
      <div class="maintenance-list">
        <div v-for="row in maintenanceRows" :key="row.key" class="maintenance-row">
          <div class="maintenance-copy">
            <strong>{{ row.label }}</strong>
            <small>{{ row.note }}</small>
          </div>
          <button
            v-if="row.notHeld"
            type="button"
            class="maintenance-action"
            @click="toggleNotHeld(row.key, false)"
          >
            恢复
          </button>
          <RouterLink
            v-else
            :to="row.route"
            class="maintenance-action"
            @click="maintenanceOpen = false"
          >
            {{ row.action }} ›
          </RouterLink>
        </div>
      </div>
    </BottomSheet>

    <AppNav />
  </main>
</template>

<style scoped>
.page {
  padding-top: 0;
}

.page-head {
  margin: 0 calc(var(--page-gutter) * -1) 16px;
  padding: max(14px, env(safe-area-inset-top)) var(--page-gutter) 10px;
  background: transparent;
}

.page-head-row {
  display: flex;
  align-items: center;
  gap: 11px;
}

.page-head-title {
  min-width: 0;
}

.maintenance-link {
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

.maintenance-link:hover {
  background: var(--color-brand-soft);
}

.maintenance-list {
  display: grid;
  gap: 2px;
}

.maintenance-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  min-height: 64px;
  padding: 10px 0;
  border-top: 1px solid var(--color-border-subtle);
}

.maintenance-row:first-child {
  border-top: 0;
}

.maintenance-copy {
  min-width: 0;
}

.maintenance-copy strong {
  display: block;
  font-size: var(--text-subhead);
  font-weight: 600;
}

.maintenance-copy small {
  display: block;
  margin-top: 3px;
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.maintenance-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  min-height: var(--touch-target);
  padding: 0 12px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: var(--text-footnote);
  font-weight: 600;
  text-decoration: none;
}

.page-head h1 {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 700;
  letter-spacing: -0.4px;
}

.hero {
  margin: 0 0 16px;
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

.tag {
  padding: 2px 8px;
  border-radius: var(--radius-pill);
  background: var(--color-hero-surface);
  color: var(--color-hero-text-secondary);
  font-size: var(--text-caption);
}

.hero-value {
  display: flex;
  align-items: baseline;
  gap: 2px;
  margin: 10px 0 0;
  color: var(--color-hero-text);
  font-size: clamp(2rem, 11vw, 2.5rem);
  font-weight: 700;
  letter-spacing: -0.04em;
  line-height: 1.15;
  font-variant-numeric: tabular-nums;
}

.hero-value .currency {
  margin-right: 5px;
  color: var(--color-hero-text-secondary);
  font-size: 1.25rem;
  font-weight: 500;
  letter-spacing: 0;
}

.notice {
  margin: 0 0 14px;
  padding: 9px 11px;
  border-radius: var(--radius-tag);
  background: var(--color-success-bg);
  color: var(--color-success);
  font-size: var(--text-footnote);
}

.form-error {
  margin: 0 0 14px;
  padding: 9px 11px;
  border-radius: var(--radius-tag);
  background: var(--color-error-bg);
  color: var(--color-error);
  font-size: var(--text-footnote);
}

.asset-callout {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 14px 16px;
  border-radius: var(--radius-card);
  background: var(--color-brand-soft);
}

.asset-callout strong {
  display: block;
  font-size: var(--text-subhead);
  font-weight: 600;
}

.asset-callout p {
  margin: 3px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.callout-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  min-height: 40px;
  padding: 0 16px;
  border-radius: var(--radius-pill);
  background: var(--color-bg-surface);
  color: var(--color-brand);
  font-size: var(--text-footnote);
  font-weight: 600;
  text-decoration: none;
}
</style>
