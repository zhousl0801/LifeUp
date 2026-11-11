using LifeUp.Application.Quotes;
using Microsoft.Extensions.Logging;

namespace LifeUp.Infrastructure.Quotes;

// 上金所现货价为主源，工行积存金平台报价为备用源：现货源稳定且与积存金定价基准一致；
// 银行报价作为备用，可在现货接口不可用时仍拿到平台真实价格。
public sealed class GoldQuoteProvider : IGoldQuoteProvider
{
    private readonly SgeGoldQuoteProvider _spotProvider;
    private readonly IcbcGoldQuoteProvider _bankProvider;
    private readonly ILogger<GoldQuoteProvider> _logger;

    public GoldQuoteProvider(
        SgeGoldQuoteProvider spotProvider,
        IcbcGoldQuoteProvider bankProvider,
        ILogger<GoldQuoteProvider> logger)
    {
        _spotProvider = spotProvider;
        _bankProvider = bankProvider;
        _logger = logger;
    }

    public async Task<ExternalGoldQuote> GetLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _spotProvider.GetLatestAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "黄金现货行情获取失败，尝试积存金平台报价。");
        }

        try
        {
            return await _bankProvider.GetLatestAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new QuoteUnavailableException(
                $"黄金现货行情与积存金平台报价均不可用：{exception.Message}",
                exception);
        }
    }
}
