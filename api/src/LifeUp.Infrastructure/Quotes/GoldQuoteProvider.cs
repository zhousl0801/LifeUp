using LifeUp.Application.Quotes;
using Microsoft.Extensions.Logging;

namespace LifeUp.Infrastructure.Quotes;

// 东方财富现货行情不可用时，依次尝试上金所官方延时行情和工行积存金平台报价。
public sealed class GoldQuoteProvider : IGoldQuoteProvider
{
    private readonly SgeGoldQuoteProvider _spotProvider;
    private readonly SgeDelayedGoldQuoteProvider _delayedProvider;
    private readonly IcbcGoldQuoteProvider _bankProvider;
    private readonly ILogger<GoldQuoteProvider> _logger;

    public GoldQuoteProvider(
        SgeGoldQuoteProvider spotProvider,
        SgeDelayedGoldQuoteProvider delayedProvider,
        IcbcGoldQuoteProvider bankProvider,
        ILogger<GoldQuoteProvider> logger)
    {
        _spotProvider = spotProvider;
        _delayedProvider = delayedProvider;
        _bankProvider = bankProvider;
        _logger = logger;
    }

    public async Task<ExternalGoldQuote> GetLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _spotProvider.GetLatestAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "东方财富黄金现货行情获取失败，尝试上金所延时行情。");
        }

        try
        {
            return await _delayedProvider.GetLatestAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "上金所延时行情获取失败，尝试工行积存金平台报价。");
        }

        try
        {
            return await _bankProvider.GetLatestAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new QuoteUnavailableException(
                $"东方财富、上金所延时行情与工行积存金报价均不可用：{exception.Message}",
                exception);
        }
    }
}
