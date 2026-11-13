import { describe, expect, it } from 'vitest'
import type { CapitalYearRow } from '../types/capital'
import {
  clampPercent,
  formatMilestoneTitle,
  formatMonthLabel,
  progressAlignment,
  selectYearLabels
} from './capital'

function yearRow(year: number, capital: number, isGoalYear = false): CapitalYearRow {
  return {
    year,
    yearEndCapital: capital,
    netSavings: 0,
    providentDeposit: 0,
    return: 0,
    total: 0,
    isGoalYear
  }
}

describe('clampPercent', () => {
  it('clamps to the 0–100 range', () => {
    expect(clampPercent(-5)).toBe(0)
    expect(clampPercent(37.6)).toBe(37.6)
    expect(clampPercent(120)).toBe(100)
    expect(clampPercent(Number.NaN)).toBe(0)
  })
})

describe('progressAlignment', () => {
  it('aligns labels near the track edges', () => {
    expect(progressAlignment(10)).toBe('left')
    expect(progressAlignment(50)).toBe('center')
    expect(progressAlignment(90)).toBe('right')
  })
})

describe('formatMilestoneTitle', () => {
  it('formats wan and yuan milestones', () => {
    expect(formatMilestoneTitle(100_000, false)).toBe('资本突破 10 万')
    expect(formatMilestoneTitle(10_000, false)).toBe('资本突破 1 万')
    expect(formatMilestoneTitle(5_000, false)).toBe('资本突破 5,000 元')
  })

  it('masks the amount while keeping the title', () => {
    const masked = formatMilestoneTitle(100_000, true)
    expect(masked).toContain('资本突破')
    expect(masked).not.toMatch(/\d/)
  })
})

describe('formatMonthLabel', () => {
  it('formats a year-month label', () => {
    expect(formatMonthLabel('2026-11')).toBe('2026 年 11 月')
  })

  it('returns the input for invalid values', () => {
    expect(formatMonthLabel('bad')).toBe('bad')
  })
})

describe('selectYearLabels', () => {
  const rows = [
    yearRow(2026, 384_050),
    yearRow(2027, 470_000),
    yearRow(2028, 556_000),
    yearRow(2029, 647_000),
    yearRow(2030, 743_000),
    yearRow(2031, 845_000),
    yearRow(2032, 953_000),
    yearRow(2033, 1_000_000, true)
  ]

  it('selects two evenly spaced even years and the goal year', () => {
    const labels = selectYearLabels(rows, 2026, 1_000_000, 2033, 37.6)

    expect(labels.map((item) => item.label)).toEqual(['2028', '2030', '2033'])
    expect(labels[0]!.leftPercent).toBeCloseTo(55.6, 1)
    expect(labels[1]!.leftPercent).toBeCloseTo(74.3, 1)
    expect(labels[2]!.leftPercent).toBe(100)
  })

  it('skips labels too close to the current cursor', () => {
    const labels = selectYearLabels(rows, 2026, 1_000_000, 2033, 56)

    expect(labels.map((item) => item.label)).not.toContain('2028')
    expect(labels.map((item) => item.label)).toContain('2033')
  })

  it('returns no labels without a target', () => {
    expect(selectYearLabels(rows, 2026, null, null, 37.6)).toEqual([])
  })

  it('includes a goal reached in the current year', () => {
    const labels = selectYearLabels([yearRow(2026, 100_000, true)], 2026, 100_000, 2026, 80)

    expect(labels).toEqual([{ label: '2026', leftPercent: 100 }])
  })

  it('shows only the goal year when it is within two years', () => {
    const nearRows = [
      yearRow(2026, 50_000),
      yearRow(2027, 75_000),
      yearRow(2028, 100_000, true)
    ]
    const labels = selectYearLabels(nearRows, 2026, 100_000, 2028, 50)

    expect(labels.map((item) => item.label)).toEqual(['2028'])
  })
})
