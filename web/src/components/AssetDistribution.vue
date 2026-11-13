<script setup lang="ts">
import { computed, useId } from 'vue'
import { RouterLink } from 'vue-router'
import AppIcon from './AppIcon.vue'
import LoadingButton from './LoadingButton.vue'
import AnimatedMoney from './AnimatedMoney.vue'
import { amountTone, formatMaskedMoney, formatMoney, formatSignedMoney, sumAmounts } from '../utils/format'
import type { AssetChange, AssetKey, LedgerOverview } from '../types/ledger'

interface DistributionRow {
  key: AssetKey
  label: string
  icon: string
  to: string
  amount: number | null
  configured: boolean
  notHeld: boolean
  note: string
  noteAmount?: number | null
  change: AssetChange | null
  percent: number | null
}

const props = defineProps<{
  overview: LedgerOverview
  changes?: AssetChange[]
  comparable?: boolean
  changeScope?: 'day' | 'month'
  cards?: boolean
  amountsMasked?: boolean
  editable?: boolean
  hideFundCount?: boolean
  busyKey?: AssetKey | null
}>()

const emit = defineEmits<{ toggleNotHeld: [key: AssetKey, notHeld: boolean] }>()

const rows = computed<DistributionRow[]>(() => {
  const ledger = props.overview
  const changeByKey = new Map((props.changes ?? []).map((item) => [item.assetType, item]))
  const recordedTotal = sumAmounts([ledger.cash, ledger.providentFund, ledger.fund, ledger.gold, ledger.stock]
    .filter((asset) => asset.isConfigured && !asset.notHeld && asset.amount !== null)
    .map(asset => asset.amount ?? 0))
  const percentOf = (amount: number | null): number | null =>
    amount === null || recordedTotal <= 0 ? null : amount / recordedTotal * 100

  return [
    {
      key: 'cash',
      label: '现金',
      icon: 'cash',
      to: '/cash',
      amount: ledger.cash.amount,
      configured: true,
      notHeld: false,
      note: ledger.month.hasRecords ? '本月结余' : '统一账户余额',
      noteAmount: ledger.month.hasRecords ? ledger.month.surplus : null,
      change: changeByKey.get('cash') ?? null,
      percent: percentOf(ledger.cash.amount)
    },
    {
      key: 'fund',
      label: '基金',
      icon: 'chart',
      to: '/funds',
      amount: ledger.fund.amount,
      configured: ledger.fund.isConfigured,
      notHeld: ledger.fund.notHeld,
      note: fundNote(ledger.fund.note ?? '尚未录入'),
      change: changeByKey.get('fund') ?? null,
      percent: percentOf(ledger.fund.amount)
    },
    {
      key: 'stock',
      label: '股票',
      icon: 'stock',
      to: '/stocks',
      amount: ledger.stock.amount,
      configured: ledger.stock.isConfigured,
      notHeld: ledger.stock.notHeld,
      note: ledger.stock.note ?? '尚未录入',
      change: changeByKey.get('stock') ?? null,
      percent: percentOf(ledger.stock.amount)
    },
    {
      key: 'providentFund',
      label: '公积金',
      icon: 'home',
      to: '/provident',
      amount: ledger.providentFund.amount,
      configured: ledger.providentFund.isConfigured,
      notHeld: ledger.providentFund.notHeld,
      note: ledger.providentFund.note ?? '尚未录入',
      change: changeByKey.get('providentFund') ?? null,
      percent: percentOf(ledger.providentFund.amount)
    },
    {
      key: 'gold',
      label: '贵金属',
      icon: 'gold',
      to: '/gold',
      amount: ledger.gold.amount,
      configured: ledger.gold.isConfigured,
      notHeld: ledger.gold.notHeld,
      note: ledger.gold.note ?? '尚未录入',
      change: changeByKey.get('gold') ?? null,
      percent: percentOf(ledger.gold.amount)
    }
  ]
})

const barSegments = computed(() => rows.value
  .filter((row) => row.configured && !row.notHeld && row.amount !== null && row.amount > 0)
  .map((row) => ({ key: row.key, width: row.percent ?? 0 })))

const changePrefix = computed(() => props.changeScope === 'day' ? '较昨日' : '较上月')

const gradientId = useId()

const barLabel = computed(() => {
  const parts = rows.value
    .filter((row) => row.configured && !row.notHeld && row.percent !== null)
    .map((row) => `${row.label} ${row.percent!.toFixed(1)}%`)

  return parts.length ? `资产占比：${parts.join('、')}` : '暂无可计算资产占比'
})

function fundNote(note: string): string {
  if (!props.hideFundCount) {
    return note
  }

  const withoutCount = note.replace(/^持有\s*\d+\s*只基金\s*(?:·\s*)?/, '').trim()

  return withoutCount || '按最新净值估值'
}

function amountLabel(row: DistributionRow, masked = false): string {
  if (row.notHeld) {
    return '不持有'
  }

  if (!row.configured) {
    return '暂未录入'
  }

  if (row.amount === null) return '依据不足'

  return masked ? formatMaskedMoney(row.amount) : `¥${formatMoney(row.amount)}`
}

function changeLabel(row: DistributionRow): string {
  if (row.notHeld) {
    return '不持有'
  }

  if (!row.configured) {
    return ''
  }

  if (row.change?.isNew) {
    return `${props.changeScope === 'day' ? '当日' : '本月'}新增`
  }

  if (props.comparable === false || row.change?.change == null) {
    return `${changePrefix.value} —`
  }

  return `${changePrefix.value} ${formatSignedMoney(row.change.change)}`
}

function changeTone(row: DistributionRow): string {
  if (row.notHeld || !row.configured || row.change?.isNew || props.comparable === false || row.change?.change == null) {
    return ''
  }

  return amountTone(row.change.change)
}
</script>

<template>
  <section class="distribution" :class="{ 'distribution-cards': cards }">
    <h2 v-if="!cards" class="distribution-head">资产分布</h2>

    <div v-if="cards" class="asset-cards">
      <article
        v-for="row in rows"
        :key="row.key"
        class="distribution-card"
        :class="`card-${row.key}`"
        :style="{ '--share': `${Math.min(100, Math.max(0, row.percent ?? 0))}%` }"
        :aria-label="`${row.label}资产`"
      >
        <svg
          v-if="row.configured && !row.notHeld && row.percent !== null && row.percent > 0"
          class="card-glow"
          viewBox="0 0 100 100"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <defs>
            <linearGradient :id="`${gradientId}-${row.key}`" gradientUnits="userSpaceOnUse" x1="-38" x2="3" y1="0" y2="0">
              <stop offset="0" style="stop-color: var(--card-color); stop-opacity: 0" />
              <stop offset=".67" style="stop-color: var(--card-color); stop-opacity: var(--card-glow-soft)" />
              <stop offset=".92" style="stop-color: var(--card-color); stop-opacity: var(--card-glow-strong)" />
              <stop offset="1" style="stop-color: var(--card-highlight)" />
            </linearGradient>
          </defs>
          <g class="card-glow-edge">
            <path
              class="card-glow-wave"
              :fill="`url(#${gradientId}-${row.key})`"
              d="M-110 0H0 C-2 12 -1 25 0 33 S1 44 2.7 49 Q4 53 1 56 Q0 58 1.5 61 Q3 65 1 69 C-2 74 -2 86 0 100 H-110Z"
            />
          </g>
        </svg>

        <div class="card-content">
          <div class="card-heading">
            <h3><AppIcon :name="row.icon" />{{ row.label }}</h3>
            <div v-if="row.configured && !row.notHeld && row.percent !== null" class="card-share">
              <span>资产占比</span>
              <strong>{{ row.percent.toFixed(1) }}%</strong>
            </div>
          </div>
          <strong class="card-balance">{{ amountLabel(row, amountsMasked) }}</strong>
          <div class="card-footer">
            <div class="card-note">
              <span v-if="row.configured && !row.notHeld">
                {{ row.note }}<template v-if="row.noteAmount != null"> {{ formatSignedMoney(row.noteAmount) }}</template>
              </span>
              <span v-if="row.configured && !row.notHeld">{{ changeLabel(row) }}</span>
            </div>
            <RouterLink v-if="!row.notHeld" :to="row.to" class="card-link">
              {{ row.configured ? '查看详情' : '去录入' }} <AppIcon name="chevron" />
            </RouterLink>
          </div>
        </div>
      </article>
    </div>

    <div v-if="!cards" class="ratio-bar" role="img" :aria-label="barLabel">
      <span
        v-for="segment in barSegments"
        :key="segment.key"
        class="ratio-segment"
        :class="`type-${segment.key}`"
        :style="{ width: `${segment.width}%` }"
      />
      <span v-if="!barSegments.length" class="ratio-empty" />
    </div>

    <ul v-if="!cards" class="asset-list">
      <li
        v-for="(row, index) in rows"
        :key="row.key"
        class="asset-row stagger-item"
        :class="{ muted: row.notHeld }"
        :style="{ '--stagger': Math.min(index, 8) }"
      >
        <RouterLink v-if="row.configured && !row.notHeld" :to="row.to" class="asset-link">
          <span class="asset-main">
            <span class="asset-title">
              <i class="dot" :class="`type-${row.key}`" />
              <b>{{ row.label }}</b>
            </span>
            <small>
              {{ row.note }}<template v-if="row.noteAmount != null">&nbsp;<span :class="amountTone(row.noteAmount)">{{ formatSignedMoney(row.noteAmount) }}</span></template>
            </small>
          </span>
          <span class="asset-side">
            <b class="asset-amount">
              <AnimatedMoney v-if="row.amount !== null" :value="row.amount" />
              <template v-else>{{ amountLabel(row) }}</template>
            </b>
            <small><span :class="changeTone(row)">{{ changeLabel(row) }}</span></small>
          </span>
          <span class="asset-percent">
            <span>{{ row.percent === null ? '—' : `${row.percent.toFixed(1)}%` }}</span>
            <AppIcon class="row-chevron" name="chevron" />
          </span>
        </RouterLink>

        <div v-else class="asset-link static">
          <span class="asset-main">
            <span class="asset-title">
              <i class="dot" :class="`type-${row.key}`" />
              <b>{{ row.label }}</b>
            </span>
            <small>{{ row.notHeld ? '已标记不持有' : '还没有记录' }}</small>
          </span>
          <span class="asset-side">
            <b class="asset-amount muted-text">{{ amountLabel(row) }}</b>
          </span>
          <span v-if="editable" class="row-actions">
            <RouterLink v-if="!row.notHeld" :to="row.to" class="row-action">添加</RouterLink>
            <LoadingButton
              class="row-action subtle"
              :loading="busyKey === row.key"
              loading-label="处理中…"
              @click="emit('toggleNotHeld', row.key, !row.notHeld)"
            >{{ row.notHeld ? '恢复' : '不持有' }}</LoadingButton>
          </span>
          <RouterLink v-else-if="!row.notHeld" :to="row.to" class="row-action">添加</RouterLink>
          <span v-else class="asset-percent">—</span>
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.distribution {
  margin-bottom: 24px;
  padding: var(--card-padding);
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.distribution-head {
  margin: 0 0 14px;
  font-size: 1.125rem;
  font-weight: 700;
  letter-spacing: -0.3px;
}

.distribution-cards {
  padding: 0;
  background: transparent;
  box-shadow: none;
}

.asset-cards {
  display: grid;
  gap: var(--space-3);
}

.distribution-card {
  --card-color: #2be7a8;
  --card-highlight: #b5ffdf;
  --card-accent: var(--card-highlight);
  --card-link: var(--card-highlight);
  --card-share: var(--card-highlight);
  --card-halo: 0 26px 46px -26px color-mix(in srgb, var(--card-color) 55%, transparent);
  --card-glow-soft: var(--color-asset-glow-soft);
  --card-glow-strong: var(--color-asset-glow-strong);
  --card-text-shadow: 0 1px 7px #10111a, 0 1px 3px #10111a;
  position: relative;
  overflow: hidden;
  isolation: isolate;
  border: 1px solid var(--color-asset-card-border);
  border-radius: var(--radius-card);
  background: linear-gradient(180deg, var(--color-asset-card-bg), var(--color-asset-card-bg-end));
  color: var(--color-asset-card-text);
  box-shadow: var(--card-halo), var(--color-asset-card-shadow);
}

.card-fund { --card-color: #a477ff; --card-highlight: #e7cdff; }
.card-stock { --card-color: #fd507e; --card-highlight: #ffb191; }
.card-providentFund { --card-color: #21c7ff; --card-highlight: #b5f3ff; }
.card-gold { --card-color: #ffae38; --card-highlight: #fff2a1; }

/* 浅色主题：白卡分层，分类色退到图标、占比、链接与流光。
 * 选择器整体放进 :global()，否则 scoped 编译会丢掉后代部分。 */
:global(html[data-color-scheme='light'] .distribution-card) {
  --card-link: var(--card-accent);
  --card-share: var(--card-accent);
  --card-halo: 0 0 transparent;
  --card-text-shadow: none;
}

:global(html[data-color-scheme='light'] .card-cash) { --card-accent: var(--color-asset-accent-cash); }
:global(html[data-color-scheme='light'] .card-fund) { --card-accent: var(--color-asset-accent-fund); }
:global(html[data-color-scheme='light'] .card-stock) { --card-accent: var(--color-asset-accent-stock); }
:global(html[data-color-scheme='light'] .card-providentFund) { --card-accent: var(--color-asset-accent-provident); }
:global(html[data-color-scheme='light'] .card-gold) { --card-accent: var(--color-asset-accent-gold); }

.card-glow {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}

.card-glow-edge {
  transform-box: view-box;
  transform: translateX(var(--share));
  transition: transform .48s cubic-bezier(.22, 1, .36, 1);
}

.card-glow-wave {
  transform-box: view-box;
  transform-origin: center;
  transform: scaleY(1.2);
}

@media (prefers-reduced-motion: no-preference) {
  .card-glow-wave {
    animation: card-glow-float 6s ease-in-out infinite alternate;
  }

  .distribution-card:nth-child(even) .card-glow-wave {
    animation-delay: -3s;
  }
}

@keyframes card-glow-float {
  from { transform: translateY(-7%) scaleY(1.2); }
  to { transform: translateY(7%) scaleY(1.2); }
}

.card-content {
  position: relative;
  display: grid;
  gap: 6px;
  padding: 16px 16px 10px;
  background: var(--color-asset-card-veil);
  text-shadow: var(--card-text-shadow);
}

.card-heading,
.card-footer {
  display: flex;
  justify-content: space-between;
  gap: 10px;
}

.card-heading {
  align-items: flex-start;
}

.card-heading h3 {
  display: flex;
  align-items: center;
  gap: 7px;
  margin: 0;
  font-size: .9375rem;
}

.card-heading h3 .app-icon {
  width: 18px;
  height: 18px;
  color: var(--card-accent);
}

.card-share {
  display: grid;
  justify-items: end;
  gap: 2px;
  flex-shrink: 0;
  font-size: .75rem;
  color: var(--color-asset-card-note);
}

.card-share strong {
  color: var(--card-share);
  font-size: 1.25rem;
  line-height: 1;
  font-variant-numeric: tabular-nums;
}

.card-balance {
  font-size: clamp(1.375rem, 6vw, 1.875rem);
  font-variant-numeric: tabular-nums;
  line-height: 1.25;
  overflow-wrap: anywhere;
}

.card-footer {
  align-items: center;
  min-height: var(--touch-target);
}

.card-note {
  display: grid;
  gap: 3px;
  color: var(--color-asset-card-note);
  font-size: .75rem;
  line-height: 1.35;
}

.card-link {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  min-height: var(--touch-target);
  color: var(--card-link);
  font-size: .75rem;
  text-decoration: none;
  white-space: nowrap;
  transition: opacity var(--motion-fast) ease;
}

@media (hover: hover) and (pointer: fine) {
  .card-link:hover {
    text-decoration: underline;
    text-underline-offset: 3px;
  }
}

.card-link:active {
  opacity: .7;
}

.card-link .app-icon {
  width: 14px;
  height: 14px;
}

.card-link:focus-visible {
  outline: 2px solid var(--card-accent);
  outline-offset: 2px;
}

@media (prefers-reduced-motion: reduce) {
  .card-glow-edge { transition: none; }
}

@media (forced-colors: active) {
  .card-glow { display: none; }
}

.ratio-bar {
  display: flex;
  gap: 4px;
  overflow: hidden;
  height: 8px;
  margin-bottom: 6px;
  border-radius: var(--radius-pill);
}

.ratio-segment {
  height: 100%;
  min-width: 2px;
  border-radius: var(--radius-pill);
}

.ratio-empty {
  width: 100%;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
}

.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.type-cash {
  background: var(--color-chart-cash);
}

.type-providentFund {
  background: var(--color-chart-property);
}

.type-fund {
  background: var(--color-chart-investment);
}

.type-gold {
  background: var(--color-chart-receivable);
}

.type-stock {
  background: var(--color-chart-stock);
}

.asset-list {
  margin: 0;
  padding: 0;
  background: transparent;
  list-style: none;
}

.asset-row + .asset-row {
  border-top: 1px solid var(--color-border-subtle);
}

.asset-link {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto 58px;
  align-items: center;
  gap: 10px;
  padding: 14px 0;
  color: inherit;
  text-decoration: none;
}

.asset-link.static {
  grid-template-columns: minmax(0, 1fr) auto auto;
  column-gap: 12px;
}

.asset-link.static .asset-amount {
  font-size: 0.75rem;
  font-weight: 500;
}

.asset-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.asset-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.875rem;
}

.asset-title b {
  font-weight: 600;
}

.asset-main small {
  display: -webkit-box;
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.35;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.asset-side small {
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.asset-side {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
}

.asset-amount {
  font-size: 0.875rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.asset-percent {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  justify-content: center;
  gap: 2px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  text-align: right;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.asset-percent .row-chevron {
  width: 16px;
  height: 16px;
  color: var(--color-text-secondary);
}

.muted-text {
  color: var(--color-text-secondary);
  font-weight: 400;
}

.asset-row.muted .asset-title {
  color: var(--color-text-secondary);
}

.row-actions {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.row-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  box-sizing: border-box;
  min-height: var(--touch-target);
  padding: 0 12px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 0.75rem;
  font-weight: 600;
  line-height: 1;
  text-decoration: none;
  white-space: nowrap;
  transition: background .15s ease, color .15s ease;
}

.row-action:hover {
  background: var(--color-brand-soft-hover);
}

.row-action.subtle {
  padding: 0 10px;
  background: transparent;
  color: var(--color-text-secondary);
  font-weight: 500;
}

.row-action.subtle:hover {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.row-action:focus-visible {
  outline: 2px solid var(--color-brand);
  outline-offset: 2px;
}

@media (max-width: 360px) {
  .asset-link,
  .asset-link.static {
    grid-template-columns: minmax(0, 1fr) auto;
  }

  .asset-percent {
    display: none;
  }

  .asset-link.static .row-actions {
    grid-column: 1 / -1;
    justify-self: end;
    margin-top: 2px;
  }
}
</style>
