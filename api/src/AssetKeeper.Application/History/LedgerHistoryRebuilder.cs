using AssetKeeper.Application.Ledgers;
using AssetKeeper.Domain.Assets;
using AssetKeeper.Domain.Ledgers;

namespace AssetKeeper.Application.History;

public sealed class LedgerHistoryRebuilder
{
    private readonly ILedgerRepository _repository;
    private readonly BusinessDateProvider _dates;

    public LedgerHistoryRebuilder(
        ILedgerRepository repository,
        BusinessDateProvider dates)
    {
        _repository = repository;
        _dates = dates;
    }

    public async Task StageAsync(
        Ledger ledger,
        AssetTransaction transaction,
        DateOnly date,
        DateOnly? priceDate,
        Guid? adjustmentId,
        CancellationToken cancellationToken,
        LedgerReplayData? replayData = null)
    {
        var data = replayData ?? await _repository.GetReplayDataAsync(ledger.Id, cancellationToken);
        var isQuote = transaction.Kind is AssetTransactionKind.FundQuote or AssetTransactionKind.GoldQuote
            or AssetTransactionKind.StockQuote;

        if (!isQuote && data.Transactions.Any(item => !item.IsReversed && item.NeedsDateConfirmation
            && item.AssetType == transaction.AssetType && item.PositionId == transaction.PositionId))
        {
            throw new HistoryValidationException("该资产的起始持仓日期待确认，请先在历史核对中确认原始记录日期。");
        }

        transaction.SetHistoryMetadata(date, priceDate, ledger.AllocateFactSequence());
        transaction.LinkCashAdjustment(adjustmentId);
        await _repository.AddAssetTransactionAsync(transaction, cancellationToken);
        var price = await AddPriceAsync(ledger, transaction, cancellationToken);

        // 复用本次已经加载的账本快照，把新事实和行情补进去，避免 RebuildAsync 再读一遍全账本。
        var transactions = data.Transactions.Any(item => item.Id == transaction.Id)
            ? data.Transactions
            : data.Transactions.Append(transaction).ToList();
        var prices = price is null || data.Prices.Any(item => item.Id == price.Id)
            ? data.Prices
            : data.Prices.Append(price).ToList();
        var stagedData = data with
        {
            Transactions = transactions,
            Prices = prices
        };

        await RebuildAsync(ledger.Id, transaction.Id, cancellationToken, replayData: stagedData);
    }

    public async Task<PriceObservation?> AddPriceAsync(Ledger ledger, AssetTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction.Price is decimal price && transaction.PriceDate is DateOnly priceDate && !transaction.IsReversed)
        {
            var observation = PriceObservation.Create(ledger.Id, transaction.AssetType,
                transaction.PositionId, transaction.Id, price, priceDate, _dates.UtcNow, ledger.AllocateFactSequence(),
                transaction.QuoteSource);
            await _repository.AddPriceObservationAsync(observation, cancellationToken);
            return observation;
        }

        return null;
    }

    public async Task<ReplayResult> RebuildAsync(
        Guid ledgerId,
        Guid changedId,
        CancellationToken cancellationToken,
        bool confirmedImpact = false,
        LedgerReplayData? replayData = null)
    {
        var data = replayData ?? await _repository.GetReplayDataAsync(ledgerId, cancellationToken);
        var facts = BusinessFact.From(data);
        var changed = facts.SingleOrDefault(fact => fact.Id == changedId);

        if (!data.Ledger.CashAccount.HistoryConfirmed && (changed?.CashEffect != 0m || changed?.Source == "adjustment"))
        {
            throw new HistoryValidationException("现金起始依据待确认，请先在历史核对中确认起始现金。");
        }

        var result = new LedgerReplayCalculator().Calculate(data, facts, _dates.Today, currentDate: _dates.Today);

        foreach (var replay in result.Adjustments)
        {
            var adjustment = data.Adjustments.Single(item => item.Id == replay.Id);

            if (!confirmedImpact && adjustment.Id != changedId && adjustment.Difference != replay.Difference
                && changed?.RelatedCashAdjustmentId != adjustment.Id)
            {
                throw new HistoryValidationException($"这笔变动会改变现金核对差额（{adjustment.BusinessDate:yyyy-MM-dd}），请关联漏记业务，或到历史核对预览并确认影响。");
            }
        }

        if (result.CashBalance is decimal cash && (changed?.CashEffect != 0m || changed?.Source == "adjustment"
            || changedId == data.Ledger.CashAccount.Id || cash != data.Ledger.CashAccount.Balance))
        {
            data.Ledger.CashAccount.RestoreProjection(cash, result.CashBalanceDate, _dates.UtcNow);
        }

        if (data.Provident is not null)
        {
            data.Provident.RestoreProjection(result.ProvidentBalance ?? 0m, result.ProvidentBalanceDate, _dates.UtcNow);
        }

        foreach (var fund in result.Funds)
        {
            var position = data.Positions.Single(item => item.Id == fund.PositionId);
            position.RestoreProjection(fund.Quantity, fund.Cost, fund.RealizedProfit, fund.Price ?? position.NetValue,
                fund.PriceDate ?? position.NetValueDate, _dates.UtcNow);
        }

        foreach (var stock in result.Stocks)
        {
            var position = data.Stocks.Single(item => item.Id == stock.PositionId);
            position.RestoreProjection(stock.Quantity, stock.Cost, stock.RealizedProfit, stock.Price ?? position.Price,
                stock.PriceDate ?? position.PriceDate, _dates.UtcNow);
        }

        if (data.Gold is not null && result.Gold is not null)
        {
            data.Gold.RestoreProjection(result.Gold.Quantity, result.Gold.Cost, result.Gold.RealizedProfit,
                result.Gold.Price ?? data.Gold.Quote, result.Gold.PriceDate ?? data.Gold.QuoteDate, _dates.UtcNow);
        }

        foreach (var replay in result.Adjustments)
        {
            data.Adjustments.Single(item => item.Id == replay.Id).Recalculate(replay.PreviousBalance);
        }

        return result;
    }
}
