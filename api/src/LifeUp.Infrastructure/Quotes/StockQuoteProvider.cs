using LifeUp.Application.Quotes;
using LifeUp.Domain.Assets;
using Microsoft.Extensions.Logging;

namespace LifeUp.Infrastructure.Quotes;

// 股票行情与搜索优先使用腾讯财经，不可用（或搜索无命中）时自动回退新浪财经。
public sealed class StockQuoteProvider : IStockQuoteProvider
{
    private readonly TencentStockQuoteProvider _tencentProvider;
    private readonly SinaStockQuoteProvider _sinaProvider;
    private readonly ILogger<StockQuoteProvider> _logger;

    public StockQuoteProvider(
        TencentStockQuoteProvider tencentProvider,
        SinaStockQuoteProvider sinaProvider,
        ILogger<StockQuoteProvider> logger)
    {
        _tencentProvider = tencentProvider;
        _sinaProvider = sinaProvider;
        _logger = logger;
    }

    public async Task<ExternalStockQuote> GetLatestAsync(
        StockMarket market,
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _tencentProvider.GetLatestAsync(market, code, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "腾讯股票行情获取失败（{Market}:{Code}），尝试新浪财经。", market, code);
        }

        try
        {
            return await _sinaProvider.GetLatestAsync(market, code, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "新浪股票行情获取失败（{Market}:{Code}）。", market, code);
            throw new QuoteUnavailableException("股票行情服务暂时无法连接，请稍后重试。", exception);
        }
    }

    public async Task<IReadOnlyList<ExternalStockSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        Exception? tencentError = null;

        try
        {
            var results = await _tencentProvider.SearchAsync(query, cancellationToken);

            if (results.Count > 0)
            {
                return results;
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            tencentError = exception;
            _logger.LogWarning(exception, "腾讯股票搜索失败（{Query}），尝试新浪财经。", query);
        }

        try
        {
            return await _sinaProvider.SearchAsync(query, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "新浪股票搜索失败（{Query}）。", query);

            if (tencentError is not null)
            {
                throw new QuoteUnavailableException("股票搜索服务暂时无法连接，请稍后重试。", exception);
            }

            return [];
        }
    }
}
