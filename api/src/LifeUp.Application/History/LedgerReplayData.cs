using LifeUp.Domain.Assets;
using LifeUp.Domain.Cash;
using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.History;

public sealed record LedgerReplayData(
    Ledger Ledger,
    IReadOnlyList<LedgerEntry> Entries,
    IReadOnlyList<CashAdjustment> Adjustments,
    IReadOnlyList<AssetTransaction> Transactions,
    IReadOnlyList<PriceObservation> Prices,
    IReadOnlyList<FundPosition> Positions,
    IReadOnlyList<StockPosition> Stocks,
    ProvidentFundAccount? Provident,
    GoldHolding? Gold);
