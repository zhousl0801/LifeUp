<script lang="ts">
let lastKnownProgress: number | null = null

export function resetProgressMemory(): void {
  lastKnownProgress = null
}
</script>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { CapitalMilestone, CapitalStatus, CapitalYearLabel } from '../types/capital'
import { clampPercent, formatMilestoneTitle, formatMonthLabel, progressAlignment } from '../utils/capital'

const props = defineProps<{
  progressPercent: number
  currentLabel: string
  etaLabel: string | null
  years: CapitalYearLabel[]
  milestones: CapitalMilestone[]
  status: CapitalStatus
  amountsMasked: boolean
  animateUpdates: boolean
  ariaSummary: string
}>()

const displayedProgress = ref(0)
const displayLabel = ref(props.currentLabel)
const visibleMilestones = ref<CapitalMilestone[]>([])
const newMilestoneKeys = ref<string[]>([])
const showBeam = ref(false)
const cursorBright = ref(false)
const selectedMilestone = ref<CapitalMilestone | null>(null)
const popoverLeft = ref(0)
const animating = ref(false)
let frameId = 0
let brightTimer: number | undefined
const newStarTimers: number[] = []

const alignment = computed(() => progressAlignment(displayedProgress.value))
const isNoTarget = computed(() => props.status === 'noTarget')
const isReached = computed(() => props.status === 'reached')
const labelText = computed(() => (isReached.value ? '已达成' : displayLabel.value))
const trackStyle = computed(() => ({ '--progress': `${displayedProgress.value}%` }))

function milestoneKey(milestone: CapitalMilestone): string {
  return `${milestone.thresholdAmount}:${milestone.progressPercent}`
}

function maskedTitle(milestone: CapitalMilestone): string {
  return formatMilestoneTitle(milestone.thresholdAmount, props.amountsMasked)
}

function milestoneDate(milestone: CapitalMilestone): string {
  return formatMonthLabel(milestone.date.slice(0, 7))
}

function starAria(milestone: CapitalMilestone): string {
  return `${milestoneDate(milestone)}，${maskedTitle(milestone)}`
}

function isNew(milestone: CapitalMilestone): boolean {
  return newMilestoneKeys.value.includes(milestoneKey(milestone))
}

function revealMilestones(progress: number, pop: boolean): void {
  const known = new Set(visibleMilestones.value.map(milestoneKey))

  for (const milestone of props.milestones) {
    const key = milestoneKey(milestone)

    if (milestone.progressPercent > progress || known.has(key)) {
      continue
    }

    visibleMilestones.value.push(milestone)
    known.add(key)

    if (!pop) {
      continue
    }

    newMilestoneKeys.value = [...newMilestoneKeys.value, key]

    const timer = window.setTimeout(() => {
      newMilestoneKeys.value = newMilestoneKeys.value.filter((item) => item !== key)
    }, 320)
    newStarTimers.push(timer)
  }
}

function animateTo(target: number, duration: number, withBeam: boolean, pop: boolean): void {
  cancelAnimationFrame(frameId)
  animating.value = true
  showBeam.value = withBeam

  if (duration <= 0 || prefersReducedMotion()) {
    displayedProgress.value = target
    revealMilestones(target, pop)
    finish(withBeam)
    return
  }

  const from = displayedProgress.value
  const started = performance.now()

  const step = (now: number): void => {
    const elapsed = Math.min(duration, Math.max(0, now - started))
    const ratio = elapsed / duration
    const eased = 1 - Math.pow(1 - ratio, 3)
    const value = from + (target - from) * eased

    displayedProgress.value = value
    revealMilestones(value, pop)

    if (ratio < 1) {
      frameId = requestAnimationFrame(step)
      return
    }

    displayedProgress.value = target
    revealMilestones(target, pop)
    finish(withBeam)
  }

  frameId = requestAnimationFrame(step)
}

function finish(withBeam: boolean): void {
  animating.value = false
  displayedProgress.value = clampPercent(displayedProgress.value)
  displayLabel.value = props.currentLabel
  lastKnownProgress = displayedProgress.value
  showBeam.value = false

  if (withBeam) {
    cursorBright.value = true
    window.clearTimeout(brightTimer)
    brightTimer = window.setTimeout(() => {
      cursorBright.value = false
    }, 380)
  }
}

function selectMilestone(milestone: CapitalMilestone): void {
  selectedMilestone.value = milestone
  popoverLeft.value = Math.min(88, Math.max(12, milestone.progressPercent))
}

function closePopover(): void {
  selectedMilestone.value = null
}

function prefersReducedMotion(): boolean {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

onMounted(() => {
  document.addEventListener('click', closePopover)
  const target = clampPercent(props.progressPercent)
  const remembered = lastKnownProgress
  const from = remembered === null ? 0 : clampPercent(remembered)
  const withBeam = props.animateUpdates && remembered !== null && target > from

  displayedProgress.value = from
  displayLabel.value = props.currentLabel
  visibleMilestones.value = props.milestones.filter((milestone) => milestone.progressPercent <= from)

  if (Math.abs(target - from) < 0.01) {
    displayedProgress.value = target
    finish(withBeam)
    return
  }

  const pop = remembered === null || withBeam
  animateTo(target, withBeam ? 800 : remembered === null ? 900 : 600, withBeam, pop)
})

watch(
  () => props.progressPercent,
  (value) => {
    const target = clampPercent(value)
    const from = displayedProgress.value

    if (Math.abs(target - from) < 0.01) {
      return
    }

    const withBeam = props.animateUpdates && target > from
    animateTo(target, withBeam ? 800 : 600, withBeam, withBeam)
  }
)

watch(
  () => props.currentLabel,
  (label) => {
    if (!animating.value) {
      displayLabel.value = label
    }
  }
)

watch(
  () => props.milestones,
  (milestones) => {
    const keys = new Set(milestones.map(milestoneKey))
    visibleMilestones.value = visibleMilestones.value.filter((milestone) => keys.has(milestoneKey(milestone)))
    revealMilestones(displayedProgress.value, false)

    if (selectedMilestone.value && !keys.has(milestoneKey(selectedMilestone.value))) {
      selectedMilestone.value = null
    }
  },
  { deep: true }
)

onBeforeUnmount(() => {
  document.removeEventListener('click', closePopover)
  cancelAnimationFrame(frameId)
  window.clearTimeout(brightTimer)
  newStarTimers.forEach((timer) => window.clearTimeout(timer))
})
</script>

<template>
  <div class="glass" :class="{ 'is-reached': isReached }" role="group" :aria-label="ariaSummary" :style="trackStyle">
    <span class="glass-slot" aria-hidden="true">
      <span v-if="!isNoTarget" class="glass-fill"></span>
    </span>

    <span v-if="isNoTarget" class="glass-hint">设定目标后显示达成进度</span>

    <template v-if="!isNoTarget">
      <span v-if="showBeam" class="glass-beam" aria-hidden="true"></span>

      <button
        v-for="milestone in visibleMilestones"
        :key="milestoneKey(milestone)"
        type="button"
        class="glass-star"
        :class="{ 'is-new': isNew(milestone) }"
        :style="{ left: `${milestone.progressPercent}%` }"
        :aria-label="starAria(milestone)"
        @click.stop="selectMilestone(milestone)"
      >
        ✦
      </button>

      <span class="glass-cursor" :class="{ 'is-bright': cursorBright }" aria-hidden="true"></span>
      <span class="glass-label" :class="alignment" aria-hidden="true">{{ labelText }}</span>
      <span class="glass-goal" :class="{ 'is-reached': isReached }" aria-hidden="true"></span>

      <span
        v-for="year in years"
        :key="year.label"
        class="glass-year"
        :class="{ 'is-right': year.leftPercent > 85 }"
        :style="{ left: `${year.leftPercent}%` }"
        aria-hidden="true"
      >
        {{ year.label }}
      </span>
    </template>

    <div v-if="selectedMilestone" class="glass-pop" :style="{ left: `${popoverLeft}%` }" @click.stop>
      <b>{{ milestoneDate(selectedMilestone) }}</b>
      <span>{{ maskedTitle(selectedMilestone) }}</span>
    </div>
  </div>
</template>

<style scoped>
.glass {
  --progress: 0%;
  position: relative;
  height: 58px;
  margin-top: 26px;
}

.glass-slot {
  position: absolute;
  top: 17px;
  right: 0;
  left: 0;
  overflow: hidden;
  height: 16px;
  border-radius: 999px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.20), rgba(180, 170, 255, 0.10));
  box-shadow:
    inset 0 1px 1px rgba(255, 255, 255, 0.28),
    inset 0 -2px 3px rgba(20, 14, 50, 0.32),
    0 2px 12px rgba(20, 14, 50, 0.22),
    0 0 0 1px rgba(255, 255, 255, 0.04);
}

.glass-slot::before {
  position: absolute;
  top: 2px;
  right: 2px;
  left: 2px;
  height: 1px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.50);
  content: "";
}

.glass-fill {
  position: absolute;
  top: 0;
  bottom: 0;
  left: 0;
  width: var(--progress);
  border-radius: 999px;
  background: linear-gradient(90deg, #6FE3B4 0%, #8FE8D6 45%, #C8F0FF 100%);
  box-shadow: 0 0 18px rgba(126, 231, 210, 0.55), 0 0 4px rgba(255, 255, 255, 0.35);
}

.glass-fill::after {
  position: absolute;
  top: 2px;
  right: 2px;
  left: 2px;
  height: 1px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.85);
  content: "";
}

.glass-cursor {
  position: absolute;
  top: 6px;
  left: var(--progress);
  z-index: 3;
  width: 4px;
  height: 28px;
  transform: translateX(-50%);
  border-radius: 999px;
  background: #FFFFFF;
  box-shadow: 0 0 12px rgba(255, 255, 255, 0.85), 0 0 4px #FFFFFF;
  transition: box-shadow 200ms cubic-bezier(0.2, 0.8, 0.2, 1);
}

.glass-cursor.is-bright {
  box-shadow: 0 0 18px rgba(255, 255, 255, 0.95), 0 0 6px #FFFFFF;
}

.glass-label {
  position: absolute;
  top: -18px;
  left: var(--progress);
  z-index: 4;
  padding: 5px 9px;
  transform: translateX(-50%);
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: 10px;
  background: rgba(42, 34, 112, 0.92);
  box-shadow: 0 6px 18px rgba(20, 14, 50, 0.28);
  color: #FFFFFF;
  font-size: 12px;
  font-weight: 600;
  white-space: nowrap;
}

.glass-label.left {
  transform: translateX(0);
}

.glass-label.right {
  transform: translateX(-100%);
}

.glass-star {
  position: absolute;
  top: 25px;
  z-index: 4;
  display: grid;
  width: 32px;
  height: 32px;
  padding: 0;
  transform: translate(-50%, -50%);
  border: 0;
  background: none;
  color: #FFFFFF;
  font-size: 14px;
  line-height: 1;
  text-shadow: 0 0 6px rgba(255, 255, 255, 0.85);
  cursor: pointer;
  place-items: center;
}

.glass-star.is-new {
  animation: star-pop 320ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
}

@keyframes star-pop {
  from { transform: translate(-50%, -50%) scale(0); opacity: 0; }
  to { transform: translate(-50%, -50%) scale(1); opacity: 1; }
}

.glass-goal {
  position: absolute;
  top: 25px;
  left: 100%;
  width: 12px;
  height: 12px;
  transform: translate(-50%, -50%) rotate(45deg);
  border: 1.5px solid #FFFFFF;
  border-radius: 2px;
  box-shadow: 0 0 8px rgba(255, 255, 255, 0.35);
}

.glass-goal.is-reached {
  border-color: #7CE7B1;
  background: #7CE7B1;
  box-shadow: 0 0 12px rgba(124, 231, 177, 0.7);
}

.glass-year {
  position: absolute;
  top: 38px;
  transform: translateX(-50%);
  color: #F1EDFF;
  font-size: 11px;
  white-space: nowrap;
}

.glass-year.is-right {
  transform: translateX(-100%);
}

.glass-beam {
  position: absolute;
  top: 17px;
  left: var(--progress);
  z-index: 3;
  width: 18px;
  height: 16px;
  transform: translateX(-50%);
  border-radius: 999px;
  background: radial-gradient(ellipse at center, #FFFFFF 0%, #BDEBFF 35%, rgba(189, 235, 255, 0) 72%);
  filter: blur(0.4px);
  pointer-events: none;
}

.glass-hint {
  position: absolute;
  top: 17px;
  right: 0;
  left: 0;
  display: grid;
  height: 16px;
  color: #F1EDFF;
  font-size: 11px;
  place-items: center;
}

.glass-pop {
  position: absolute;
  top: 54px;
  z-index: 6;
  padding: 8px 10px;
  transform: translateX(-50%);
  border-radius: 12px;
  background: #FFFFFF;
  box-shadow: 0 8px 24px rgba(20, 14, 50, 0.18);
  color: var(--color-text-primary);
  font-size: 12px;
  white-space: nowrap;
}

.glass-pop b {
  display: block;
  font-size: 12px;
}

.glass-pop span {
  color: var(--color-text-secondary);
  font-size: 11px;
}

@media (prefers-reduced-motion: reduce) {
  .glass-star.is-new {
    animation: none;
  }

  .glass-cursor {
    transition: none;
  }
}
</style>
