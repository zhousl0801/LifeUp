import type {
  FundPosition,
  FundShareClass,
  FundSummary,
  PendingFundArrival,
  PendingFundBuy
} from '../types/fund'
import { calculatePercent, subtractAmounts, sumAmounts } from './format'

export function normalizePositionKey(value: string | null | undefined): string {
  return (value ?? '').normalize('NFKC').replace(/\s/g, '').toUpperCase()
}

export function normalizePlatformKey(value: string | null | undefined): string {
  const key = normalizePositionKey(value)
  return key === '直销' ? '券商直销' : key
}

export function findDuplicateFundPosition(
  positions: FundPosition[] | undefined,
  codeInput: string,
  shareClassInput: string,
  platformInput: string
): FundPosition | null {
  const code = normalizePositionKey(codeInput)
  const shareClass = normalizePositionKey(shareClassInput)
  const platform = normalizePlatformKey(platformInput)

  if (!code || !positions?.length) {
    return null
  }

  return positions.find((position) => {
    if (normalizePositionKey(position.code) !== code
      || normalizePositionKey(position.shareClass) !== shareClass) {
      return false
    }

    const existingPlatform = normalizePlatformKey(position.platform)

    return existingPlatform === platform || existingPlatform === '' || platform === ''
  }) ?? null
}

export function buildFallbackFunds(
  positions: FundPosition[],
  pendingBuys: PendingFundBuy[],
  pendingArrivals: PendingFundArrival[]
): FundSummary[] {
  const byName = new Map<string, FundPosition[]>()

  for (const position of positions) {
    const members = byName.get(position.name) ?? []
    members.push(position)
    byName.set(position.name, members)
  }

  return Array.from(byName, ([name, members]) => {
    const classes: FundShareClass[] = []
    const byClass = new Map<string, FundPosition[]>()

    for (const member of members) {
      const key = `${member.code?.trim() || member.name.normalize('NFKC').replace(/\s/g, '')}|${member.shareClass?.trim().toUpperCase() ?? ''}`
      const classMembers = byClass.get(key) ?? []
      classMembers.push(member)
      byClass.set(key, classMembers)
    }

    for (const [key, classMembers] of byClass) {
      const netValues = [...new Set(classMembers.map((member) => member.netValue))]
      const netValueDates = [...new Set(classMembers.map((member) => member.netValueDate))]
      const classValue = sumAmounts(classMembers.map(member => member.value))
      const classDailyDate = classMembers
        .map((member) => member.dailyProfitDate)
        .filter((date): date is string => Boolean(date))
        .sort()
        .at(-1) ?? null
      const classDailyMembers = classDailyDate === null
        ? []
        : classMembers.filter((member) => member.hasDailyProfit && member.dailyProfitDate === classDailyDate)
      const hasDailyProfit = classMembers.length > 0 && classDailyMembers.length === classMembers.length
      const dailyProfit = sumAmounts(classDailyMembers.map((member) => member.dailyProfit))

      classes.push({
        key,
        code: classMembers[0]?.code ?? null,
        shareClass: classMembers[0]?.shareClass || null,
        shares: sumAmounts(classMembers.map(member => member.shares)),
        cost: sumAmounts(classMembers.map(member => member.cost)),
        value: classValue,
        holdingProfit: sumAmounts(classMembers.map(member => member.holdingProfit)),
        realizedProfit: sumAmounts(classMembers.map(member => member.realizedProfit)),
        netValue: netValues.length === 1 ? netValues[0]! : null,
        netValueDate: netValueDates.length === 1 ? netValueDates[0]! : null,
        dailyProfit,
        dailyProfitRate: hasDailyProfit
          ? calculatePercent(dailyProfit, subtractAmounts(classValue, dailyProfit))
          : null,
        hasDailyProfit,
        dailyProfitDate: classDailyDate,
        platforms: classMembers.map((member) => ({
          positionId: member.id,
          platform: member.platform,
          name: member.name,
          code: member.code,
          shareClass: member.shareClass,
          shares: member.shares,
          cost: member.cost,
          netValue: member.netValue,
          netValueDate: member.netValueDate,
          value: member.value,
          holdingProfit: member.holdingProfit,
          realizedProfit: member.realizedProfit,
          hasEffectiveQuote: member.hasEffectiveQuote,
          confirmationDelayDays: member.confirmationDelayDays,
          dailyProfit: member.dailyProfit ?? 0,
          dailyProfitRate: member.dailyProfitRate ?? null,
          hasDailyProfit: member.hasDailyProfit,
          dailyProfitDate: member.dailyProfitDate
        }))
      })
    }

    const memberIds = new Set(members.map((member) => member.id))
    const pendingBuyAmount = sumAmounts(pendingBuys
      .filter((item) => memberIds.has(item.positionId))
      .map(item => item.amount))
    const pendingArrivalAmount = sumAmounts(pendingArrivals
      .filter((item) => memberIds.has(item.positionId))
      .map(item => item.amount))
    const value = sumAmounts(members.map(member => member.value))
    const dailyProfitDate = members
      .map((member) => member.dailyProfitDate)
      .filter((date): date is string => Boolean(date))
      .sort()
      .at(-1) ?? null
    const dailyMembers = dailyProfitDate === null
      ? []
      : members.filter((member) => member.hasDailyProfit && member.dailyProfitDate === dailyProfitDate)
    const hasDailyProfit = members.length > 0 && dailyMembers.length === members.length
    const dailyProfit = sumAmounts(dailyMembers.map((member) => member.dailyProfit))
    const groupId = members.find((member) => member.groupId)?.groupId ?? null

    return {
      key: name,
      name,
      groupId,
      groupName: members.find((member) => member.groupName)?.groupName ?? null,
      value,
      cost: sumAmounts(members.map(member => member.cost)),
      holdingProfit: sumAmounts(members.map(member => member.holdingProfit)),
      realizedProfit: sumAmounts(members.map(member => member.realizedProfit)),
      pendingBuyAmount,
      pendingArrivalAmount,
      assetValue: sumAmounts([value, pendingBuyAmount, pendingArrivalAmount]),
      platformCount: new Set(members.map((member) => member.platform).filter(Boolean)).size,
      latestNetValueDate: members.map((member) => member.netValueDate).sort().at(-1) ?? null,
      historyConfirmed: members.every((member) => member.historyConfirmed),
      hasEffectiveQuotes: members.every((member) => member.hasEffectiveQuote),
      classes,
      dailyProfit,
      dailyProfitRate: hasDailyProfit
        ? calculatePercent(dailyProfit, subtractAmounts(value, dailyProfit))
        : null,
      hasDailyProfit,
      dailyProfitDate
    } satisfies FundSummary
  }).sort((left, right) => left.name.localeCompare(right.name, 'zh-Hans-CN'))
}
