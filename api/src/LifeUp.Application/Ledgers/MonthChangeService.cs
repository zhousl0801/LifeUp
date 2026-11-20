using System.Globalization;
using LifeUp.Application.History;
using LifeUp.Domain.Assets;

namespace LifeUp.Application.Ledgers;

public sealed class MonthChangeService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly BusinessDateProvider _dates;

    public MonthChangeService(ILedgerRepository ledgerRepository, BusinessDateProvider dates)
    {
        _ledgerRepository = ledgerRepository;
        _dates = dates;
    }

    public Task<MonthChangeDto?> GetAsync(CancellationToken cancellationToken)
    {
        return GetAsync(ChangeScope.Month, cancellationToken);
    }

    public async Task<MonthChangeDto?> GetAsync(ChangeScope scope, CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return null;
        }

        var today = _dates.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var isDay = scope == ChangeScope.Day;
        var from = ledger.StartDate > monthStart ? ledger.StartDate : monthStart;
        var notes = new List<string>();
        var data = await _ledgerRepository.GetReplayDataReadOnlyAsync(ledger.Id, cancellationToken);
        var facts = BusinessFact.From(data);
        var calculator = new LedgerReplayCalculator();

        // 在账本日期范围内对比昨日，或月初前一天。
        var hasBaseline = isDay ? ledger.StartDate < today : ledger.StartDate < monthStart;
        var baselineDate = isDay
            ? (hasBaseline ? today.AddDays(-1) : ledger.StartDate)
            : (hasBaseline ? monthStart.AddDays(-1) : ledger.StartDate);
        var replays = calculator.CalculateDates(data, facts, [baselineDate, today], currentDate: today);
        var before = replays[baselineDate];
        var now = replays[today];

        if (isDay)
        {
            return BuildDayChange(data, facts, calculator, before, now);
        }

        var beforeValues = ReadValues(before, ledger);
        var nowValues = ReadValues(now, ledger);

        var comparableTypes = beforeValues
            .Where(pair => pair.Value.Present && nowValues[pair.Key].Present)
            .ToList();
        var commonKnown = comparableTypes.Count > 0
            && comparableTypes.All(pair => pair.Value.Value is not null && nowValues[pair.Key].Value is not null);
        var comparableChange = hasBaseline && commonKnown
            ? Round(comparableTypes.Sum(pair => nowValues[pair.Key].Value!.Value - pair.Value.Value!.Value))
            : (decimal?)null;
        var comparableBase = hasBaseline && commonKnown
            ? Round(comparableTypes.Sum(pair => pair.Value.Value!.Value))
            : (decimal?)null;

        var addedTypes = nowValues
            .Where(pair => !beforeValues[pair.Key].Present && pair.Value.Present && pair.Value.Value is not null)
            .ToList();
        var addedRecords = addedTypes.Count > 0
            ? Round(addedTypes.Sum(pair => pair.Value.Value!.Value))
            : 0m;

        // 按实际影响现金流的收支计算生活结余，与当日变化保持一致，自动排除历史月度汇总（MonthlyImport）。
        var livingSurplus = Round(facts
            .Where(fact => !fact.IsReversed
                && fact.EntryKind is not null
                && fact.Date >= from
                && fact.Date <= today)
            .Sum(fact => fact.CashEffect));
        var providentDeposit = Round(facts
            .Where(fact => !fact.IsReversed
                && fact.AssetType == AssetType.ProvidentFund
                && fact.Kind == AssetTransactionKind.ProvidentDeposit
                && fact.Date >= from
                && fact.Date <= today)
            .Sum(fact => fact.Amount));
        var cashDifference = Round(data.Adjustments
            .Where(adjustment => !adjustment.IsReversed
                && adjustment.BusinessDate >= from
                && adjustment.BusinessDate <= today)
            .Sum(adjustment => adjustment.Difference));
        var correctionDifference = Round(CalculateCorrectionDifferences(data, facts, calculator, from, today, notes)
            .Amounts.Values.Sum());
        var reconciliationDifference = Round(cashDifference + correctionDifference);

        decimal? netDeposit = null;
        decimal? marketValueChange = null;

        if (comparableChange is not null)
        {
            var providentComparable = beforeValues["providentFund"].Present
                && nowValues["providentFund"].Present;
            netDeposit = Round(livingSurplus
                + (providentComparable ? providentDeposit : 0m)
                + reconciliationDifference);
            marketValueChange = Round(comparableChange.Value - netDeposit.Value);
        }

        if (!hasBaseline)
        {
            notes.Add("账本起始日晚于本月月初，本月暂无可比基线；新增记录单独列出。");
        }
        else if (comparableChange is null)
        {
            notes.Add("部分日期资产或行情依据不足，无法计算同口径变化。");
            notes.AddRange(now.Issues.Distinct());
        }

        if (addedRecords > 0)
        {
            notes.Add($"含新增记录 {addedRecords.ToString("N2", CultureInfo.InvariantCulture)} 元，不计入本月变化。");
        }

        return new MonthChangeDto(
            from,
            today,
            comparableChange,
            livingSurplus,
            providentDeposit,
            marketValueChange,
            reconciliationDifference,
            comparableChange is not null,
            notes.Distinct().ToList(),
            BuildAssetChanges(beforeValues, nowValues, hasBaseline),
            comparableChange,
            comparableBase,
            addedRecords,
            marketValueChange,
            netDeposit,
            comparableChange is not null);
    }

    private static MonthChangeDto BuildDayChange(
        LedgerReplayData data,
        IReadOnlyList<BusinessFact> facts,
        LedgerReplayCalculator calculator,
        ReplayResult before,
        ReplayResult now)
    {
        var today = now.AsOfDate;
        var dayFacts = facts.Where(fact => fact.Date == today && !fact.IsReversed).ToList();
        var cashIncome = dayFacts.Where(fact => fact.EntryKind is not null && fact.CashEffect > 0m)
            .Sum(fact => fact.CashEffect);
        var cashExpense = -dayFacts.Where(fact => fact.EntryKind is not null && fact.CashEffect < 0m)
            .Sum(fact => fact.CashEffect);
        var providentDeposit = dayFacts.Where(fact => fact.Kind == AssetTransactionKind.ProvidentDeposit)
            .Sum(fact => fact.Amount);
        var adjustmentIds = dayFacts.Where(fact => fact.Source == "adjustment").Select(fact => fact.Id).ToHashSet();
        var cashAdjustment = now.Adjustments.Where(adjustment => adjustmentIds.Contains(adjustment.Id))
            .Sum(adjustment => adjustment.Difference);
        var openings = dayFacts
            .Where(fact => fact.Kind is AssetTransactionKind.ProvidentOpening or AssetTransactionKind.FundOpening
                or AssetTransactionKind.StockOpening or AssetTransactionKind.GoldOpening)
            .GroupBy(fact => fact.AssetType!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(fact => fact.Kind == AssetTransactionKind.ProvidentOpening
                ? fact.Amount
                : Round(fact.Quantity.GetValueOrDefault() * fact.Price.GetValueOrDefault())));
        var addedRecords = openings.Values.Sum();
        var beforeValues = ReadValues(before, data.Ledger);
        var nowValues = ReadValues(now, data.Ledger);
        var cashTransfers = dayFacts.Where(fact => fact.AssetType is not null && fact.CashEffect != 0m)
            .GroupBy(fact => fact.AssetType!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(fact => fact.CashEffect));
        var missingTransferValue = (!nowValues["fund"].Present && cashTransfers.GetValueOrDefault(AssetType.Fund) != 0m)
            || (!nowValues["stock"].Present && cashTransfers.GetValueOrDefault(AssetType.Stock) != 0m)
            || (!nowValues["gold"].Present && cashTransfers.GetValueOrDefault(AssetType.Gold) != 0m);
        var notes = new List<string>();
        var hasBaseline = before.AsOfDate < today;
        var hasComparableData = hasBaseline
            && before.CashBalance is not null
            && now.CashBalance is not null
            && beforeValues.Concat(nowValues).All(pair => !pair.Value.Present || pair.Value.Value is not null)
            && beforeValues.All(pair => !pair.Value.Present || nowValues[pair.Key].Present)
            && !missingTransferValue;

        decimal? comparableChange = null;
        decimal? comparableBase = null;
        decimal? investmentProfit = null;
        decimal? netDeposit = null;
        var reconciliationDifference = cashAdjustment;
        DayChangeBreakdownDto? breakdown = null;

        if (hasComparableData)
        {
            var corrections = CalculateCorrectionDifferences(data, facts, calculator, today, today, notes);
            var fund = InvestmentChangeFor(AssetType.Fund, "fund");
            var stock = InvestmentChangeFor(AssetType.Stock, "stock");
            var gold = InvestmentChangeFor(AssetType.Gold, "gold");
            investmentProfit = (fund?.Profit ?? 0m) + (stock?.Profit ?? 0m) + (gold?.Profit ?? 0m);
            var holdingAdjustment = corrections.Amounts.Values.Sum(value => Round(value));
            reconciliationDifference += holdingAdjustment;
            netDeposit = cashIncome - cashExpense + providentDeposit + reconciliationDifference;
            comparableBase = beforeValues.Values.Sum(asset => asset.Value.GetValueOrDefault());
            comparableChange = Round(nowValues.Values.Sum(asset => asset.Value.GetValueOrDefault())
                - comparableBase.Value - addedRecords);
            // 余额比较与已分类来源存在历史差额时单列，避免归入任一投资盈亏。
            var otherDifference = Round(comparableChange.Value - netDeposit.Value - investmentProfit.Value);
            breakdown = new DayChangeBreakdownDto(
                cashIncome, cashExpense, providentDeposit, fund, stock, gold,
                cashAdjustment, holdingAdjustment, otherDifference);

            DayInvestmentChangeDto? InvestmentChangeFor(AssetType assetType, string key)
            {
                if (nowValues[key].Value is not decimal current || corrections.Unresolved.Contains(assetType))
                {
                    return null;
                }

                // 加回这一类资产对应的现金流，抵消买卖本金和在途资金到账。
                var previous = beforeValues[key].Value.GetValueOrDefault();
                var cashTransfer = cashTransfers.GetValueOrDefault(assetType);
                var openingValue = openings.GetValueOrDefault(assetType);
                var adjustment = Round(corrections.Amounts.GetValueOrDefault(assetType));
                var profit = Round(current - previous + cashTransfer - openingValue - adjustment);
                return new DayInvestmentChangeDto(profit, previous, current, cashTransfer, openingValue, adjustment);
            }
        }
        else if (!hasBaseline)
        {
            notes.Add("账本从今天开始，当日暂无可比基线。");
        }
        else
        {
            notes.Add("昨日或今日的资产余额或行情依据不足，无法计算当日变化。");
            notes.AddRange(now.Issues.Distinct());
        }

        if (addedRecords > 0m)
        {
            notes.Add($"含新增记录 {addedRecords.ToString("N2", CultureInfo.InvariantCulture)} 元，不计入当日变化。");
        }

        return new MonthChangeDto(
            today, today, comparableChange, cashIncome - cashExpense, providentDeposit, investmentProfit,
            reconciliationDifference, hasComparableData, notes.Distinct().ToList(),
            BuildAssetChanges(beforeValues, nowValues, hasBaseline), comparableChange, comparableBase, addedRecords,
            investmentProfit, netDeposit, hasComparableData, breakdown);
    }

    private static IReadOnlyList<AssetChangeDto> BuildAssetChanges(
        Dictionary<string, AssetValue> before,
        Dictionary<string, AssetValue> now,
        bool hasBaseline)
    {
        return
        [
            BuildAssetChange("cash", before["cash"], now["cash"], hasBaseline),
            BuildAssetChange("providentFund", before["providentFund"], now["providentFund"], hasBaseline),
            BuildAssetChange("fund", before["fund"], now["fund"], hasBaseline),
            BuildAssetChange("gold", before["gold"], now["gold"], hasBaseline),
            BuildAssetChange("stock", before["stock"], now["stock"], hasBaseline)
        ];
    }

    private static Dictionary<string, AssetValue> ReadValues(
        ReplayResult result,
        LifeUp.Domain.Ledgers.Ledger ledger)
    {
        var values = new Dictionary<string, AssetValue>
        {
            ["cash"] = new(result.CashBalance is not null, result.CashBalance),
            ["providentFund"] = ledger.ProvidentFundNotHeld && result.ProvidentBalance is null
                ? new AssetValue(true, 0m)
                : new AssetValue(result.ProvidentBalance is not null, result.ProvidentBalance),
            ["fund"] = ReadFund(result, ledger),
            ["gold"] = ReadGold(result, ledger),
            ["stock"] = ReadStock(result, ledger)
        };

        return values;
    }

    private static AssetValue ReadFund(ReplayResult result, LifeUp.Domain.Ledgers.Ledger ledger)
    {
        var recorded = result.Funds
            .Where(position => position.CoverageFrom is not null && position.CoverageFrom <= result.AsOfDate)
            .ToList();

        if (recorded.Count == 0)
        {
            return ledger.FundNotHeld ? new AssetValue(true, 0m) : new AssetValue(false, null);
        }

        if (recorded.Any(position => position.Value is null))
        {
            return new AssetValue(true, null);
        }

        return new AssetValue(true, recorded.Sum(position =>
            (position.Value ?? 0m) + position.PendingValue + position.PendingArrivalAmount));
    }

    private static AssetValue ReadGold(ReplayResult result, LifeUp.Domain.Ledgers.Ledger ledger)
    {
        if (result.Gold?.CoverageFrom is not DateOnly goldFrom || goldFrom > result.AsOfDate)
        {
            return ledger.GoldNotHeld ? new AssetValue(true, 0m) : new AssetValue(false, null);
        }

        return new AssetValue(true, result.Gold.Value is null
            ? null
            : result.Gold.Value + result.Gold.PendingArrivalAmount);
    }

    private static AssetValue ReadStock(ReplayResult result, LifeUp.Domain.Ledgers.Ledger ledger)
    {
        var recorded = result.Stocks
            .Where(position => position.CoverageFrom is not null && position.CoverageFrom <= result.AsOfDate)
            .ToList();

        if (recorded.Count == 0)
        {
            return ledger.StockNotHeld ? new AssetValue(true, 0m) : new AssetValue(false, null);
        }

        if (recorded.Any(position => position.Value is null))
        {
            return new AssetValue(true, null);
        }

        return new AssetValue(true, recorded.Sum(position => position.Value ?? 0m));
    }

    private static AssetChangeDto BuildAssetChange(
        string assetType,
        AssetValue before,
        AssetValue now,
        bool hasBaseline)
    {
        if (!before.Present && now.Present)
        {
            return new AssetChangeDto(assetType, null, true);
        }

        if (!before.Present || !now.Present || before.Value is not decimal previousAmount || now.Value is not decimal currentAmount)
        {
            return new AssetChangeDto(assetType, null, false);
        }

        return new AssetChangeDto(assetType, Round(currentAmount - previousAmount), false)
        {
            BaselineAmount = hasBaseline ? previousAmount : null
        };
    }

    private static HoldingCorrections CalculateCorrectionDifferences(
        LedgerReplayData data,
        IReadOnlyList<BusinessFact> facts,
        LedgerReplayCalculator calculator,
        DateOnly from,
        DateOnly to,
        List<string> notes)
    {
        var corrections = facts
            .Where(fact => !fact.IsReversed
                && fact.Date >= from
                && fact.Date <= to
                && fact.AssetType is AssetType.Fund or AssetType.Gold or AssetType.Stock
                && fact.Kind is AssetTransactionKind.FundCorrection or AssetTransactionKind.GoldCorrection
                    or AssetTransactionKind.StockCorrection)
            .ToList();
        var differences = new Dictionary<AssetType, decimal>();
        var unresolved = new HashSet<AssetType>();

        foreach (var correction in corrections)
        {
            var assetType = correction.AssetType!.Value;
            if (correction.Price is null)
            {
                notes.Add("持仓核对差额缺少当日行情。");
                unresolved.Add(assetType);
                continue;
            }

            decimal previousQuantity;

            try
            {
                var earlierFacts = facts
                    .Where(fact => fact.Date < correction.Date
                        || (fact.Date == correction.Date && fact.Sequence < correction.Sequence))
                    .ToList();
                var earlier = calculator.Calculate(data, earlierFacts, correction.Date, currentDate: correction.Date);
                previousQuantity = correction.AssetType switch
                {
                    AssetType.Fund => earlier.Funds.FirstOrDefault(item => item.PositionId == correction.PositionId)?.Quantity ?? 0m,
                    AssetType.Stock => earlier.Stocks.FirstOrDefault(item => item.PositionId == correction.PositionId)?.Quantity ?? 0m,
                    _ => earlier.Gold?.Quantity ?? 0m
                };
            }
            catch (HistoryValidationException)
            {
                notes.Add("持仓核对前序业务依据不足。");
                unresolved.Add(assetType);
                continue;
            }

            differences[assetType] = differences.GetValueOrDefault(assetType)
                + ((correction.Quantity ?? 0m) - previousQuantity) * correction.Price.Value;
        }

        return new HoldingCorrections(differences, unresolved);
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2);
    }

    private sealed record AssetValue(bool Present, decimal? Value);

    private sealed record HoldingCorrections(
        IReadOnlyDictionary<AssetType, decimal> Amounts,
        IReadOnlySet<AssetType> Unresolved);
}
