<script setup lang="ts">
import { computed } from 'vue'
import CategoryIcon from './CategoryIcon.vue'
import { formatBusinessDateWithWeekday, formatMoney, sumAmounts } from '../utils/format'
import type { EntryStatsPoint } from '../types/entry'

const props = defineProps<{
  month: string
  selected: string
  today: string
  points: EntryStatsPoint[]
  direction?: -1 | 0 | 1
}>()
defineEmits<{ select: [date: string] }>()

const monthKey = computed(() => props.month.slice(0, 7))
const gridMotionClass = computed(() => {
  if (props.direction === -1) return 'is-prev'
  if (props.direction === 1) return 'is-next'
  return ''
})

const cells = computed(() => {
  const start = new Date(props.month.slice(0, 7) + '-01T00:00:00Z')
  const end = new Date(start)
  end.setUTCMonth(end.getUTCMonth() + 1, 0)
  const points = new Map(props.points.map(point => [point.date, point]))
  return Array.from({ length: start.getUTCDay() + end.getUTCDate() }, (_, index) => {
    const day = index - start.getUTCDay() + 1
    if (day < 1) return null
    const date = props.month.slice(0, 7) + '-' + String(day).padStart(2, '0')
    const point = points.get(date)
    const net = point ? sumAmounts([point.income, -point.expense]) : 0
    return { date, day, category: point?.category ?? '', iconKey: point?.iconKey, net, hasEntries: Boolean(point && (point.income || point.expense)) }
  })
})

function shortAmount(amount: number, compact = false): string {
  const absolute = Math.abs(amount)
  const sign = amount > 0 ? '+' : amount < 0 ? '−' : ''
  if (absolute >= 10000) return sign + Number((absolute / 10000).toFixed(1)) + '万'
  if (absolute >= 1000) return sign + Number((absolute / 1000).toFixed(1)) + '千'
  return sign + (compact ? Number(absolute.toPrecision(3)) : absolute)
}
</script>

<template>
  <div class="calendar-shell" aria-label="收支日历">
    <div class="calendar-weekdays" aria-hidden="true">
      <span v-for="day in '日一二三四五六'" :key="day" class="weekday">{{ day }}</span>
    </div>
    <div :key="monthKey" class="calendar-grid" :class="gridMotionClass">
      <template v-for="(cell, index) in cells" :key="cell?.date ?? index">
        <button v-if="cell" type="button" :disabled="cell.date > today"
          :aria-pressed="cell.date === selected" :aria-current="cell.date === today ? 'date' : undefined"
          :aria-label="formatBusinessDateWithWeekday(cell.date) + (cell.category ? '，' + cell.category : '') + (cell.hasEntries ? '，净额 ' + formatMoney(cell.net) : '')"
          :class="{ recorded: cell.hasEntries, positive: cell.hasEntries && cell.net > 0, selected: cell.date === selected }"
          @click="$emit('select', cell.date)">
          <span>{{ cell.day }}</span>
          <CategoryIcon v-if="cell.hasEntries" :name="cell.category" :icon-key="cell.iconKey" :size="16" />
          <template v-if="cell.hasEntries">
            <small class="day-amount" aria-hidden="true">{{ shortAmount(cell.net) }}</small>
            <small class="day-amount-compact" aria-hidden="true">{{ shortAmount(cell.net, true) }}</small>
          </template>
        </button>
        <span v-else aria-hidden="true"></span>
      </template>
    </div>
  </div>
</template>

<style scoped>
.calendar-shell {
  display: grid;
  gap: 3px;
}

.calendar-weekdays,
.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: 3px;
}

.calendar-grid {
  min-height: 309px;
  align-content: start;
}

.calendar-grid.is-prev {
  animation: calendar-enter-prev var(--motion-content-in) var(--ease-enter);
}

.calendar-grid.is-next {
  animation: calendar-enter-next var(--motion-content-in) var(--ease-enter);
}

@keyframes calendar-enter-prev {
  from { opacity: 0; transform: translate3d(-8px, 0, 0); }
  to { opacity: 1; transform: translate3d(0, 0, 0); }
}

@keyframes calendar-enter-next {
  from { opacity: 0; transform: translate3d(8px, 0, 0); }
  to { opacity: 1; transform: translate3d(0, 0, 0); }
}

.weekday { text-align: center; color: var(--color-text-secondary); font-size: var(--text-footnote); }
button {
  min-width: 0;
  min-height: 49px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1px;
  padding: 2px 0;
  border: 0;
  border-radius: 12px;
  background: transparent;
  color: var(--color-text-primary);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  line-height: 1.05;
  transition:
    background-color var(--motion-feedback) var(--ease-move),
    color var(--motion-feedback) var(--ease-move),
    transform var(--motion-press-out) var(--ease-settle);
}
button:active:not(:disabled) {
  transform: scale(0.95);
  transition-duration: var(--motion-press-in);
}
button:disabled { color: var(--color-text-disabled); }
button.recorded { background: color-mix(in srgb, var(--ledger-accent) 14%, var(--color-bg-surface)); }
button.positive { background: color-mix(in srgb, var(--color-finance-income) 24%, var(--color-bg-surface)); }
button.selected { background: var(--ledger-accent); color: var(--color-on-brand); }
small { font-size: .6875rem; font-weight: 600; }
.day-amount-compact { display: none; }

@media (max-width: 360px) {
  .day-amount { display: none; }
  .day-amount-compact { display: block; }
}

@media (prefers-reduced-motion: reduce) {
  .calendar-grid.is-prev,
  .calendar-grid.is-next {
    animation: none;
  }

  button {
    transition: background-color var(--motion-reduced-fade) var(--ease-linear);
  }

  button:active:not(:disabled) {
    transform: none;
  }
}
</style>
