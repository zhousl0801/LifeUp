using System.Net;
using System.Text;
using LifeUp.Application.Quotes;
using LifeUp.Domain.Assets;
using LifeUp.Infrastructure.Quotes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LifeUp.Tests.Quotes;

public sealed class QuoteProviderTests
{
    [Fact]
    public async Task EastmoneyFundQuote_ParsesLatestNavAndDate()
    {
        const string payload = """
            {"Data":{"LSJZList":[{"FSRQ":"2026-10-21","DWJZ":"1.0640"}]},"ErrCode":0}
            """;
        var provider = new EastmoneyFundQuoteProvider(
            CreateClient(_ => JsonResponse(payload)),
            Options.Create(new QuoteProviderOptions { FundFallbackBaseUrl = "https://fund-fallback.test/" }));

        var quote = await provider.GetLatestAsync("000001", CancellationToken.None);

        Assert.Equal("000001", quote.Code);
        Assert.Equal(1.0640m, quote.NetValue);
        Assert.Equal(new DateOnly(2026, 10, 21), quote.EffectiveDate);
        Assert.Equal("eastmoney", quote.Source);
    }

    [Fact]
    public async Task EastmoneyFundQuote_WithEmptyList_ThrowsUnavailable()
    {
        const string payload = """{"Data":{"LSJZList":[]},"ErrCode":0}""";
        var provider = new EastmoneyFundQuoteProvider(
            CreateClient(_ => JsonResponse(payload)),
            Options.Create(new QuoteProviderOptions { FundFallbackBaseUrl = "https://fund-fallback.test/" }));

        await Assert.ThrowsAsync<QuoteUnavailableException>(() => provider.GetLatestAsync("000001", CancellationToken.None));
    }

    [Fact]
    public async Task IcbcGoldQuote_ParsesAccumulationGoldActivePrice()
    {
        const string payload = """
            {"sysdate":"2026-10-26 10:26:51","TranErrorCode":"","rf":[{"goldTypeNo":"JC001","RegPrice":"857.92","Active":"0","ProductName":"积存金","proCode":"080020000521","Reg":"2","HighPrice":"859.44","RegDate":"2026-10-26","ActivePrice":"858.01","SellPrice":"858.01","ActiveDate":"2026-10-26","LowPrice":"855.55"}],"TranErrorDisplayMsg":""}
            """;
        var provider = new IcbcGoldQuoteProvider(CreateClient(_ => JsonResponse(payload)));

        var quote = await provider.GetLatestAsync(CancellationToken.None);

        Assert.Equal(858.01m, quote.Quote);
        Assert.Equal(new DateOnly(2026, 10, 26), quote.EffectiveDate);
        Assert.Equal("icbc", quote.Source);
    }

    [Fact]
    public async Task IcbcGoldQuote_WithEmptyRecordList_ThrowsUnavailable()
    {
        const string payload = """{"sysdate":"2026-10-26 10:26:51","TranErrorCode":"","rf":[],"TranErrorDisplayMsg":""}""";
        var provider = new IcbcGoldQuoteProvider(CreateClient(_ => JsonResponse(payload)));

        await Assert.ThrowsAsync<QuoteUnavailableException>(() => provider.GetLatestAsync(CancellationToken.None));
    }

    [Fact]
    public async Task IcbcGoldQuote_WithTranError_ThrowsUnavailable()
    {
        const string payload = """{"sysdate":"2026-10-26 10:26:51","TranErrorCode":"ERR","rf":[],"TranErrorDisplayMsg":"系统繁忙"}""";
        var provider = new IcbcGoldQuoteProvider(CreateClient(_ => JsonResponse(payload)));

        var exception = await Assert.ThrowsAsync<QuoteUnavailableException>(() => provider.GetLatestAsync(CancellationToken.None));

        Assert.Equal("系统繁忙", exception.Message);
    }

    [Fact]
    public async Task SgeGoldQuote_ParsesSpotPriceAndDate()
    {
        const string payload = """
            {"rc":0,"data":{"f43":88342,"f57":"AU9999","f58":"黄金9999","f59":2,"f86":1794296700}}
            """;
        var provider = new SgeGoldQuoteProvider(
            CreateClient(_ => JsonResponse(payload)),
            Options.Create(new QuoteProviderOptions { GoldSgeFallbackBaseUrl = "https://sge-fallback.test/" }));

        var quote = await provider.GetLatestAsync(CancellationToken.None);

        Assert.Equal(883.42m, quote.Quote);
        Assert.Equal(new DateOnly(2026, 11, 10), quote.EffectiveDate);
        Assert.Equal("eastmoney_sge", quote.Source);
    }

    [Fact]
    public async Task SgeGoldQuote_WithEmptyData_ThrowsUnavailable()
    {
        const string payload = """{"rc":100,"data":null}""";
        var provider = new SgeGoldQuoteProvider(
            CreateClient(_ => JsonResponse(payload)),
            Options.Create(new QuoteProviderOptions { GoldSgeFallbackBaseUrl = "https://sge-fallback.test/" }));

        await Assert.ThrowsAsync<QuoteUnavailableException>(() => provider.GetLatestAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("""{"rc":100,"data":null}""")]
    [InlineData("""{"rc":0,"data":{"f43":88342,"f57":"AU9999","f59":2}}""")]
    [InlineData("""{"rc":0,"data":{"f43":88342,"f57":"AU9995","f59":2,"f86":1794296700}}""")]
    [InlineData("""{"rc":0,"data":{"f43":88342,"f57":"AU9999","f86":1794296700}}""")]
    public async Task SgeGoldQuote_FallsBackWhenPrimaryReturnsInvalidData(string primaryPayload)
    {
        const string fallbackPayload = """
            {"rc":0,"data":{"f43":88342,"f57":"AU9999","f59":2,"f86":1794296700}}
            """;
        var hosts = new List<string>();
        var client = new HttpClient(new StubHandler(request =>
        {
            var host = request.RequestUri!.Host;
            hosts.Add(host);
            return JsonResponse(host == "sge-primary.test" ? primaryPayload : fallbackPayload);
        }))
        {
            BaseAddress = new Uri("https://sge-primary.test/")
        };
        var provider = new SgeGoldQuoteProvider(
            client,
            Options.Create(new QuoteProviderOptions { GoldSgeFallbackBaseUrl = "https://sge-fallback.test/" }));

        var quote = await provider.GetLatestAsync(CancellationToken.None);

        Assert.Equal(["sge-primary.test", "sge-fallback.test"], hosts);
        Assert.Equal(883.42m, quote.Quote);
        Assert.Equal(new DateOnly(2026, 11, 10), quote.EffectiveDate);
    }

    [Fact]
    public async Task SgeGoldQuote_FallsBackToSecondaryHostWhenPrimaryFails()
    {
        const string payload = """
            {"rc":0,"data":{"f43":88342,"f57":"AU9999","f58":"黄金9999","f59":2,"f86":1794296700}}
            """;
        var hosts = new List<string>();
        var client = new HttpClient(new StubHandler(request =>
        {
            hosts.Add(request.RequestUri!.Host);

            if (request.RequestUri.Host == "sge-primary.test")
            {
                throw new HttpRequestException("An error occurred while sending the request.");
            }

            return JsonResponse(payload);
        }))
        {
            BaseAddress = new Uri("https://sge-primary.test/")
        };
        var provider = new SgeGoldQuoteProvider(
            client,
            Options.Create(new QuoteProviderOptions { GoldSgeFallbackBaseUrl = "https://sge-fallback.test/" }));

        var quote = await provider.GetLatestAsync(CancellationToken.None);

        Assert.Equal(883.42m, quote.Quote);
        Assert.Equal(["sge-primary.test", "sge-fallback.test"], hosts);
    }

    [Fact]
    public async Task GoldQuoteProvider_UsesOfficialDelayedPriceBeforeTheBank()
    {
        const string page = """
            <h1>上海黄金交易所2026年11月11日延时行情</h1>
            <table>
              <tr><th>合约</th><th>最新价</th><th>最高价</th></tr>
              <tr><td>Au99.95</td><td>870.15</td><td>871.40</td></tr>
              <tr><td>Au99.99</td><td><span class="colorGreen">862.05</span></td><td>873.20</td></tr>
            </table>
            """;
        var spotProvider = new SgeGoldQuoteProvider(
            CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)),
            Options.Create(new QuoteProviderOptions { GoldSgeFallbackBaseUrl = "https://sge-fallback.test/" }));
        var delayedProvider = new SgeDelayedGoldQuoteProvider(CreateClient(_ => new HttpResponseMessage
        {
            Content = new StringContent(page)
        }));
        var bankProvider = new IcbcGoldQuoteProvider(CreateClient(_ => throw new InvalidOperationException("不应请求工行。")));
        var provider = new GoldQuoteProvider(spotProvider, delayedProvider, bankProvider, NullLogger<GoldQuoteProvider>.Instance);

        var quote = await provider.GetLatestAsync(CancellationToken.None);

        Assert.Equal(862.05m, quote.Quote);
        Assert.Equal(new DateOnly(2026, 11, 11), quote.EffectiveDate);
        Assert.Equal("sge_delayed", quote.Source);
    }

    [Fact]
    public async Task GoldQuoteProvider_FallsBackToBankQuoteWhenSpotSourceFails()
    {
        const string bankPayload = """
            {"sysdate":"2026-11-10 16:45:48","TranErrorCode":"","rf":[{"goldTypeNo":"JC001","ProductName":"积存金","ActivePrice":"882.36","ActiveDate":"2026-11-10"}],"TranErrorDisplayMsg":""}
            """;
        var spotProvider = new SgeGoldQuoteProvider(
            CreateClient(_ => throw new HttpRequestException("ssl failure")),
            Options.Create(new QuoteProviderOptions { GoldSgeFallbackBaseUrl = "https://sge-fallback.test/" }));
        var bankProvider = new IcbcGoldQuoteProvider(CreateClient(_ => JsonResponse(bankPayload)));
        var provider = new GoldQuoteProvider(
            spotProvider,
            new SgeDelayedGoldQuoteProvider(CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway))),
            bankProvider,
            NullLogger<GoldQuoteProvider>.Instance);

        var quote = await provider.GetLatestAsync(CancellationToken.None);

        Assert.Equal(882.36m, quote.Quote);
        Assert.Equal(new DateOnly(2026, 11, 10), quote.EffectiveDate);
        Assert.Equal("icbc", quote.Source);
    }

    [Fact]
    public async Task GoldQuoteProvider_WhenBothSourcesFail_ThrowsUnavailable()
    {
        var spotProvider = new SgeGoldQuoteProvider(
            CreateClient(_ => throw new HttpRequestException("ssl failure")),
            Options.Create(new QuoteProviderOptions { GoldSgeFallbackBaseUrl = "https://sge-fallback.test/" }));
        var bankProvider = new IcbcGoldQuoteProvider(CreateClient(_ => throw new HttpRequestException("ssl failure")));
        var provider = new GoldQuoteProvider(
            spotProvider,
            new SgeDelayedGoldQuoteProvider(CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway))),
            bankProvider,
            NullLogger<GoldQuoteProvider>.Instance);

        await Assert.ThrowsAsync<QuoteUnavailableException>(() => provider.GetLatestAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TencentStockSearch_IncludesASharesAndEtfsAndExcludesIndices()
    {
        const string payload = """
            v_hint="sz~399006~\u521b\u4e1a\u677f\u6307~cybz~ZS^sz~159915~\u521b\u4e1a\u677fETF\u6613\u65b9\u8fbe~cybetfyfd~ETF^sh~510300~\u6caa\u6df1300ETF\u534e\u6cf0\u67cf\u745e~hs300etf~ETF^sh~601318~\u4e2d\u56fd\u5e73\u5b89~zgpa~GP-A^hk~03147~X\u5357\u65b9~xnf~ETF^sh~900901~\u4e91\u8d5bB\u80a1~ysbg~GP-B^sz~000001~\u534e\u590f\u6210\u957f~hxcz~JJ"
            """;
        var provider = new TencentStockQuoteProvider(
            CreateClient(_ => TextResponse(payload)),
            Options.Create(new QuoteProviderOptions()));

        var results = await provider.SearchAsync("创业板", CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(StockMarket.Shenzhen, results[0].Market);
        Assert.Equal("159915", results[0].Code);
        Assert.Equal("创业板ETF易方达", results[0].Name);
        Assert.Equal(StockMarket.Shanghai, results[1].Market);
        Assert.Equal("510300", results[1].Code);
        Assert.Equal(StockMarket.Shanghai, results[2].Market);
        Assert.Equal("601318", results[2].Code);
    }

    [Fact]
    public async Task TencentStockQuote_ParsesEtfPricePreviousCloseAndDate()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        const string payload =
            "v_sz159915=\"51~创业板ETF易方达~159915~2.199~2.292~2.247~54509434~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~20261125161418~-0.093~-4.06\";";
        var gbkBytes = Encoding.GetEncoding("GB18030").GetBytes(payload);
        var provider = new TencentStockQuoteProvider(
            CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gbkBytes)
            }),
            Options.Create(new QuoteProviderOptions()));

        var quote = await provider.GetLatestAsync(StockMarket.Shenzhen, "159915", CancellationToken.None);

        Assert.Equal(2.199m, quote.Price);
        Assert.Equal(2.292m, quote.PreviousClose);
        Assert.Equal("创业板ETF易方达", quote.Name);
        Assert.Equal(new DateOnly(2026, 11, 25), quote.PriceDate);
        Assert.Equal("tencent", quote.Source);
    }

    [Fact]
    public async Task SinaStockQuote_ParsesEtfPricePreviousCloseAndDateWithReferer()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        const string payload =
            "var hq_str_sz159915=\"创业板,2.247,2.292,2.199,2.291,2.199,2.198,2.199,5450943445,12208183133.072,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2026-11-25,15:35:00,00\";";
        Uri? observedReferer = null;
        var gbkBytes = Encoding.GetEncoding("GB18030").GetBytes(payload);
        var provider = new SinaStockQuoteProvider(
            CreateClient(request =>
            {
                observedReferer = request.Headers.Referrer;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(gbkBytes)
                };
            }),
            Options.Create(new QuoteProviderOptions()));

        var quote = await provider.GetLatestAsync(StockMarket.Shenzhen, "159915", CancellationToken.None);

        Assert.Equal(new Uri("https://finance.sina.com.cn/"), observedReferer);
        Assert.Equal(2.199m, quote.Price);
        Assert.Equal(2.292m, quote.PreviousClose);
        Assert.Equal("创业板", quote.Name);
        Assert.Equal(new DateOnly(2026, 11, 25), quote.PriceDate);
        Assert.Equal("sina", quote.Source);
    }

    [Fact]
    public async Task SinaStockSearch_IncludesASharesEtfsAndBeijingExchange()
    {
        const string payload =
            "var suggestvalue=\"创业板指,11,399006,sz399006,创业板指,,;创业板ETF易方达,22,159915,of159915,创业板ETF易方达,,;纬达光电,11,920001,bj920001,纬达光电,,;沪深300ETF,22,510300,of510300,沪深300ETF,,;华夏成长,23,000001,of000001,华夏成长,,\";";
        var provider = new SinaStockQuoteProvider(
            CreateClient(_ => TextResponse(payload)),
            Options.Create(new QuoteProviderOptions()));

        var results = await provider.SearchAsync("创业板", CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal((StockMarket.Shenzhen, "159915"), (results[0].Market, results[0].Code));
        Assert.Equal((StockMarket.Beijing, "920001"), (results[1].Market, results[1].Code));
        Assert.Equal((StockMarket.Shanghai, "510300"), (results[2].Market, results[2].Code));
    }

    [Fact]
    public async Task EastmoneyFundQuote_FallsBackToMobileSourceWhenPrimaryFails()
    {
        const string mobilePayload = """
            {"Datas":[{"FSRQ":"2026-10-29","DWJZ":"0.9680"},{"FSRQ":"2026-10-28","DWJZ":"1.0150"}],"ErrCode":0,"Success":true}
            """;
        var hosts = new List<string>();
        var client = new HttpClient(new StubHandler(request =>
        {
            hosts.Add(request.RequestUri!.Host);

            if (request.RequestUri.Host == "fund-primary.test")
            {
                throw new HttpRequestException("An error occurred while sending the request.");
            }

            return JsonResponse(mobilePayload);
        }))
        {
            BaseAddress = new Uri("https://unused.test/")
        };
        var provider = new EastmoneyFundQuoteProvider(
            client,
            Options.Create(new QuoteProviderOptions
            {
                FundBaseUrl = "https://fund-primary.test/",
                FundFallbackBaseUrl = "https://fund-fallback.test/"
            }));

        var quote = await provider.GetLatestAsync("000001", CancellationToken.None);

        Assert.Equal(0.9680m, quote.NetValue);
        Assert.Equal(new DateOnly(2026, 10, 29), quote.EffectiveDate);
        Assert.Equal(["fund-primary.test", "fund-fallback.test"], hosts);
    }

    [Fact]
    public async Task EastmoneyFundQuote_FallsBackToMobileSourceForSpecificDate()
    {
        const string mobilePayload = """
            {"Datas":[{"FSRQ":"2026-10-29","DWJZ":"0.9680"},{"FSRQ":"2026-10-28","DWJZ":"1.0150"}],"ErrCode":0,"Success":true}
            """;
        var client = new HttpClient(new StubHandler(request => request.RequestUri!.Host == "fund-primary.test"
            ? throw new HttpRequestException("boom")
            : JsonResponse(mobilePayload)))
        {
            BaseAddress = new Uri("https://unused.test/")
        };
        var provider = new EastmoneyFundQuoteProvider(
            client,
            Options.Create(new QuoteProviderOptions
            {
                FundBaseUrl = "https://fund-primary.test/",
                FundFallbackBaseUrl = "https://fund-fallback.test/"
            }));

        var quote = await provider.GetForDateAsync("000001", new DateOnly(2026, 10, 28), CancellationToken.None);

        Assert.Equal(1.0150m, quote.NetValue);
        Assert.Equal(new DateOnly(2026, 10, 28), quote.EffectiveDate);
    }

    [Fact]
    public async Task EastmoneyFundQuote_WhenAllSourcesFail_ThrowsUnavailable()
    {
        var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("boom")))
        {
            BaseAddress = new Uri("https://unused.test/")
        };
        var provider = new EastmoneyFundQuoteProvider(
            client,
            Options.Create(new QuoteProviderOptions
            {
                FundBaseUrl = "https://fund-primary.test/",
                FundFallbackBaseUrl = "https://fund-fallback.test/"
            }));

        var exception = await Assert.ThrowsAsync<QuoteUnavailableException>(
            () => provider.GetLatestAsync("000001", CancellationToken.None));

        Assert.Equal("基金净值服务暂时无法连接，请稍后重试。", exception.Message);
    }

    [Fact]
    public async Task StockQuoteProvider_FallsBackToSinaWhenTencentFails()
    {
        const string sinaPayload =
            "var hq_str_sz159915=\"创业板,2.247,2.292,2.199,2.291,2.199,2.198,2.199,5450943445,12208183133.072,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2026-11-25,15:35:00,00\";";
        var tencentProvider = new TencentStockQuoteProvider(
            CreateClient(_ => throw new HttpRequestException("tencent unavailable")),
            Options.Create(new QuoteProviderOptions()));
        var sinaProvider = new SinaStockQuoteProvider(
            CreateClient(_ => TextResponse(sinaPayload)),
            Options.Create(new QuoteProviderOptions()));
        var provider = new StockQuoteProvider(
            tencentProvider,
            sinaProvider,
            NullLogger<StockQuoteProvider>.Instance);

        var quote = await provider.GetLatestAsync(StockMarket.Shenzhen, "159915", CancellationToken.None);

        Assert.Equal(2.199m, quote.Price);
        Assert.Equal("sina", quote.Source);
    }

    [Fact]
    public async Task StockQuoteProvider_WhenBothSourcesFail_ThrowsFriendlyUnavailable()
    {
        var tencentProvider = new TencentStockQuoteProvider(
            CreateClient(_ => throw new HttpRequestException("tencent unavailable")),
            Options.Create(new QuoteProviderOptions()));
        var sinaProvider = new SinaStockQuoteProvider(
            CreateClient(_ => throw new HttpRequestException("sina unavailable")),
            Options.Create(new QuoteProviderOptions()));
        var provider = new StockQuoteProvider(
            tencentProvider,
            sinaProvider,
            NullLogger<StockQuoteProvider>.Instance);

        var exception = await Assert.ThrowsAsync<QuoteUnavailableException>(
            () => provider.GetLatestAsync(StockMarket.Shenzhen, "159915", CancellationToken.None));

        Assert.Equal("股票行情服务暂时无法连接，请稍后重试。", exception.Message);
    }

    [Fact]
    public async Task StockQuoteProvider_SearchFallsBackToSinaWhenTencentReturnsEmpty()
    {
        const string sinaPayload = "var suggestvalue=\"纬达光电,11,920001,bj920001,纬达光电,,\";";
        var tencentProvider = new TencentStockQuoteProvider(
            CreateClient(_ => TextResponse("v_hint=\"N\";")),
            Options.Create(new QuoteProviderOptions()));
        var sinaProvider = new SinaStockQuoteProvider(
            CreateClient(_ => TextResponse(sinaPayload)),
            Options.Create(new QuoteProviderOptions()));
        var provider = new StockQuoteProvider(
            tencentProvider,
            sinaProvider,
            NullLogger<StockQuoteProvider>.Instance);

        var results = await provider.SearchAsync("920001", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(StockMarket.Beijing, results[0].Market);
        Assert.Equal("920001", results[0].Code);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        return new HttpClient(new StubHandler(responseFactory))
        {
            BaseAddress = new Uri("https://example.test/")
        };
    }

    private static HttpResponseMessage TextResponse(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/plain")
        };
    }

    private static HttpResponseMessage JsonResponse(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
