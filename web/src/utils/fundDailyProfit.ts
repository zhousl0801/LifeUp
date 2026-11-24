import type { FundSummary } from '../types/fund'
import { sumAmounts } from './format'

type ReportedFundProfit = Pick<FundSummary, 'hasDailyProfit' | 'dailyProfitDate' | 'dailyProfit'>

export function latestFundDailyProfit(funds: readonly ReportedFundProfit[], today: string) {
  const disclosed = funds.filter(fund => fund.hasDailyProfit && fund.dailyProfitDate)
  const latestDate = disclosed.map(fund => fund.dailyProfitDate!).sort().at(-1) ?? null
  const latest = disclosed.filter(fund => fund.dailyProfitDate === latestDate)

  return {
    value: sumAmounts(latest.map(fund => fund.dailyProfit)),
    hasValue: latest.length > 0,
    complete: funds.length > 0 && latest.length === funds.length,
    covered: latest.length,
    total: funds.length,
    latestDate,
    isToday: latestDate === today
  }
}
