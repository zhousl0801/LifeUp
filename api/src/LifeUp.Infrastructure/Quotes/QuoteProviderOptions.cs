namespace LifeUp.Infrastructure.Quotes;

public sealed class QuoteProviderOptions
{
    public string FundBaseUrl { get; set; } = "https://api.fund.eastmoney.com/";

    // 移动端净值接口在另一个集群，字段名与主站一致（FSRQ / DWJZ），作为基金净值的降级源。
    public string FundFallbackBaseUrl { get; set; } = "https://fundmobapi.eastmoney.com/";

    public string GoldBaseUrl { get; set; } = "https://mybank.icbc.com.cn/";

    // 上金所 Au99.99 现货报价走东方财富行情接口，与股票行情同一套站点，作为积存金估值的主来源。
    public string GoldSgeBaseUrl { get; set; } = "https://push2.eastmoney.com/";

    public string GoldSgeDelayedBaseUrl { get; set; } = "https://www.sge.com.cn/";

    public string GoldSgeFallbackBaseUrl { get; set; } = "https://push2delay.eastmoney.com/";

    public string StockBaseUrl { get; set; } = "https://push2.eastmoney.com/";

    // 主站偶发整机空响应（连上后不回包），备用站点字段完全一致，价格可能略有延迟。
    public string StockFallbackBaseUrl { get; set; } = "https://push2delay.eastmoney.com/";

    public int TimeoutSeconds { get; set; } = 8;
}
