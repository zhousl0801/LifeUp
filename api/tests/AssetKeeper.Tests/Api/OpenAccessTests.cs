using System.Net;

namespace AssetKeeper.Tests.Api;

public sealed class OpenAccessTests
{
    [Fact]
    public async Task DataEndpoints_AreOpenWithoutSession()
    {
        using var factory = new AssetKeeperApiFactory { Environment = "Production" };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ledger");

        // 生产环境不再要求登录；未建账时返回 404，而不是认证失败。
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AccessEndpoints_AreRemoved()
    {
        using var factory = new AssetKeeperApiFactory { Environment = "Production" };
        using var client = factory.CreateClient();

        // 路由已移除，请求会落到 SPA 回退页而不是返回会话 JSON。
        var response = await client.GetAsync("/api/access/session");
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
