import type { CapitalYearLabel, CapitalYearRow } from '../types/capital'

export function clampPercent(value: number): number {
  if (!Number.isFinite(value)) {
    return 0
  }

  return Math.min(100, Math.max(0, value))
}

export function progressAlignment(progress: number): 'left' | 'center' | 'right' {
  const value = clampPercent(progress)

  if (value < 15) {
    return 'left'
  }

  if (value > 85) {
    return 'right'
  }

  return 'center'
}

export function formatMilestoneTitle(amount: number, masked: boolean): string {
  if (masked) {
    return amount >= 10_000 ? '资本突破 •• 万' : '资本突破 •••• 元'
  }

  if (amount >= 10_000) {
    return `资本突破 ${formatWan(amount / 10_000)} 万`
  }

  return `资本突破 ${new Intl.NumberFormat('en-US').format(Math.round(amount))} 元`
}

export function formatMonthLabel(month: string): string {
  const parts = month.split('-')

  if (parts.length !== 2) {
    return month
  }

  const year = Number(parts[0])
  const monthNumber = Number(parts[1])

  if (!Number.isInteger(year) || !Number.isInteger(monthNumber) || monthNumber < 1 || monthNumber > 12) {
    return month
  }

  return `${year} 年 ${monthNumber} 月`
}

export function selectYearLabels(
  years: CapitalYearRow[],
  currentYear: number,
  targetAmount: number | null,
  targetYear: number | null,
  currentProgress: number
): CapitalYearLabel[] {
  if (targetAmount === null || targetAmount <= 0) {
    return []
  }

  const goalYear = targetYear ?? years.find((row) => row.isGoalYear)?.year ?? null
  const goalRow = years.find((row) => row.year === goalYear)
  const candidates = years.filter((row) => row.year > currentYear && row.year !== goalYear)
  const evenCandidates = candidates.filter((row) => row.year % 2 === 0)
  const pool = evenCandidates.length >= 2 ? evenCandidates : candidates
  const picked: CapitalYearRow[] = []

  if (goalYear !== null && goalYear - currentYear <= 2) {
    picked.push(...years.filter((row) => row.year === goalYear))
  } else if (pool.length <= 2) {
    picked.push(...pool)
  } else {
    picked.push(pool[Math.floor((pool.length - 1) / 3)]!)
    picked.push(pool[Math.floor(((pool.length - 1) * 2) / 3)]!)
  }

  const labels: CapitalYearLabel[] = picked.map((row) => ({
    label: String(row.year),
    leftPercent: percentOf(row.yearEndCapital, targetAmount)
  }))

  if (goalYear !== null && !picked.some((row) => row.year === goalYear)) {
    labels.push({
      label: String(goalYear),
      leftPercent: goalRow ? percentOf(goalRow.yearEndCapital, targetAmount) : 100
    })
  }

  return labels
    .filter((label) => Math.abs(label.leftPercent - currentProgress) >= 8)
    .sort((left, right) => left.leftPercent - right.leftPercent)
    .reduce<CapitalYearLabel[]>((result, label) => {
      const previous = result[result.length - 1]

      if (!previous || label.leftPercent - previous.leftPercent >= 8) {
        result.push(label)
      }

      return result
    }, [])
}

function percentOf(value: number, total: number): number {
  return Math.round(Math.min(100, Math.max(0, (value / total) * 100)) * 10) / 10
}

function formatWan(value: number): string {
  if (Number.isInteger(value)) {
    return String(value)
  }

  return value.toFixed(1).replace(/\.0$/, '')
}
