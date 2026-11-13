import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import type { CapitalMilestone, CapitalStatus, CapitalYearLabel } from '../types/capital'
import GlassTrack, { resetProgressMemory } from './GlassTrack.vue'

interface TrackProps {
  progressPercent: number
  currentLabel: string
  etaLabel: string | null
  years: CapitalYearLabel[]
  milestones: CapitalMilestone[]
  status: CapitalStatus
  amountsMasked: boolean
  animateUpdates: boolean
  ariaSummary: string
}

const milestones: CapitalMilestone[] = [
  { thresholdAmount: 100_000, progressPercent: 10, date: '2026-07-01', title: '资本突破 10 万' },
  { thresholdAmount: 200_000, progressPercent: 20, date: '2026-08-01', title: '资本突破 20 万' },
  { thresholdAmount: 300_000, progressPercent: 30, date: '2026-10-01', title: '资本突破 30 万' }
]

const years: CapitalYearLabel[] = [
  { label: '2028', leftPercent: 55.6 },
  { label: '2030', leftPercent: 74.3 },
  { label: '2033', leftPercent: 100 }
]

const nextMilestone: CapitalMilestone = {
  thresholdAmount: 400_000,
  progressPercent: 40,
  date: '2026-12-01',
  title: '资本突破 40 万'
}

let frames: FrameRequestCallback[] = []

function mountTrack(overrides: Partial<TrackProps> = {}) {
  return mount(GlassTrack, {
    props: {
      progressPercent: 37.6,
      currentLabel: '2026 年 11 月',
      etaLabel: '预计 2033 年 6 月达成',
      years,
      milestones,
      status: 'onTrack',
      amountsMasked: false,
      animateUpdates: false,
      ariaSummary: '当前资本 37.6 万，目标 100 万，进度 37.6%',
      ...overrides
    }
  })
}

function finishFrames(): void {
  while (frames.length > 0) {
    const callback = frames.shift()!
    callback(5000)
  }
}

beforeEach(() => {
  resetProgressMemory()
  frames = []
  vi.spyOn(performance, 'now').mockReturnValue(0)
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    frames.push(callback)
    return frames.length
  })
  vi.stubGlobal('cancelAnimationFrame', () => {})
})

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('GlassTrack', () => {
  it('renders the progress, label, aria summary and year labels', async () => {
    const wrapper = mountTrack()
    finishFrames()
    await nextTick()

    const root = wrapper.find('.glass').element as HTMLElement

    expect(root.style.getPropertyValue('--progress')).toBe('37.6%')
    expect(wrapper.find('.glass-label').text()).toBe('2026 年 11 月')
    expect(wrapper.find('.glass').attributes('aria-label')).toContain('进度 37.6%')
    expect(wrapper.findAll('.glass-year').map((item) => item.text())).toEqual(['2028', '2030', '2033'])
  })

  it('aligns the label at the track edges', async () => {
    const left = mountTrack({ progressPercent: 10 })
    finishFrames()
    await nextTick()
    expect(left.find('.glass-label').classes()).toContain('left')

    const right = mountTrack({ progressPercent: 90 })
    finishFrames()
    await nextTick()
    expect(right.find('.glass-label').classes()).toContain('right')
  })

  it('shows a milestone popover and closes it', async () => {
    const wrapper = mountTrack()
    finishFrames()
    await nextTick()

    await wrapper.find('.glass-star').trigger('click')

    expect(wrapper.find('.glass-pop').text()).toContain('2026 年 7 月')
    expect(wrapper.find('.glass-pop').text()).toContain('资本突破 10 万')

    document.body.click()
    await nextTick()
    expect(wrapper.find('.glass-pop').exists()).toBe(false)
  })

  it('masks milestone amounts when amounts are hidden', async () => {
    const wrapper = mountTrack({ amountsMasked: true })
    finishFrames()
    await nextTick()

    const star = wrapper.find('.glass-star')
    expect(star.attributes('aria-label')).toContain('•• 万')
    expect(star.attributes('aria-label')).not.toContain('10 万')

    await star.trigger('click')
    expect(wrapper.find('.glass-pop').text()).toContain('•• 万')
    expect(wrapper.find('.glass-pop').text()).not.toContain('10 万')
  })

  it('renders the no-target state as an empty slot', async () => {
    const wrapper = mountTrack({ status: 'noTarget', progressPercent: 0, years: [], milestones: [] })
    finishFrames()
    await nextTick()

    expect(wrapper.find('.glass-hint').text()).toContain('设定目标')
    expect(wrapper.find('.glass-cursor').exists()).toBe(false)
    expect(wrapper.find('.glass-star').exists()).toBe(false)
    expect(wrapper.find('.glass-year').exists()).toBe(false)
    expect(wrapper.find('.glass-goal').exists()).toBe(false)
  })

  it('renders the reached state', async () => {
    const wrapper = mountTrack({ status: 'reached', progressPercent: 100 })
    finishFrames()
    await nextTick()

    expect(wrapper.find('.glass-label').text()).toBe('已达成')
    expect(wrapper.find('.glass-goal').classes()).toContain('is-reached')
  })

  it('plays the light beam and reveals a crossed milestone on update', async () => {
    const wrapper = mountTrack()
    finishFrames()
    await nextTick()

    await wrapper.setProps({
      progressPercent: 42,
      animateUpdates: true,
      milestones: [...milestones, nextMilestone]
    })
    await nextTick()

    expect(wrapper.find('.glass-beam').exists()).toBe(true)

    finishFrames()
    await nextTick()

    expect(wrapper.find('.glass-beam').exists()).toBe(false)
    expect(wrapper.findAll('.glass-star')).toHaveLength(4)
  })

  it('does not play the beam when updates are not animated', async () => {
    const wrapper = mountTrack()
    finishFrames()
    await nextTick()

    await wrapper.setProps({
      progressPercent: 42,
      animateUpdates: false,
      milestones: [...milestones, nextMilestone]
    })
    await nextTick()

    expect(wrapper.find('.glass-beam').exists()).toBe(false)

    finishFrames()
    await nextTick()

    expect(wrapper.findAll('.glass-star')).toHaveLength(4)
  })

  it('resumes from the remembered progress after remount', async () => {
    const first = mountTrack()
    finishFrames()
    await nextTick()
    first.unmount()

    const second = mountTrack({
      progressPercent: 42,
      animateUpdates: true,
      milestones: [...milestones, nextMilestone]
    })
    await nextTick()

    expect(second.find('.glass-beam').exists()).toBe(true)

    finishFrames()
    await nextTick()

    expect(second.find('.glass-beam').exists()).toBe(false)
    expect(second.findAll('.glass-star')).toHaveLength(4)
  })
})
