using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LifeUp.Tests.Api;

public sealed class CapitalEndpointsTests
{
    [Fact]
    public async Task Get_WithoutLedger_ReturnsNotFound()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ledger/capital");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_SavesGoalAndGetReturnsIt()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 376_000m);

        var put = await client.PutAsJsonAsync("/api/ledger/capital", new
        {
            targetAmount = 1_000_000m,
            monthlyIncome = 12_000m,
            monthlyExpense = 7_200m,
            monthlyProvidentDeposit = 2_000m,
            incomeGrowthPercent = 3m,
            expenseGrowthPercent = 2m,
            investmentAnnualReturnPercent = 5m,
            providentAnnualReturnPercent = 1.5m
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1_000_000m, saved.GetProperty("settings").GetProperty("targetAmount").GetDecimal());
        Assert.Equal("onTrack", saved.GetProperty("projection").GetProperty("status").GetString());
        Assert.Equal(376_000m, saved.GetProperty("current").GetProperty("total").GetDecimal());
        Assert.Equal("user", saved.GetProperty("effective").GetProperty("monthlyIncome").GetProperty("source").GetString());

        var get = await client.GetFromJsonAsync<JsonElement>("/api/ledger/capital");
        Assert.Equal(1_000_000m, get.GetProperty("settings").GetProperty("targetAmount").GetDecimal());
        Assert.Equal(37.6m, get.GetProperty("projection").GetProperty("progressPercent").GetDecimal());
        Assert.Equal(3, get.GetProperty("projection").GetProperty("milestones").GetArrayLength());
    }

    [Fact]
    public async Task Put_InvalidGoal_ReturnsBadRequest()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);

        var zeroTarget = await client.PutAsJsonAsync("/api/ledger/capital", new { targetAmount = 0m });
        var threeDecimals = await client.PutAsJsonAsync("/api/ledger/capital", new { targetAmount = 1000.123m });
        var badPercent = await client.PutAsJsonAsync("/api/ledger/capital", new { investmentAnnualReturnPercent = 101m });

        Assert.Equal(HttpStatusCode.BadRequest, zeroTarget.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, threeDecimals.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badPercent.StatusCode);
    }

    [Fact]
    public async Task Preview_DoesNotPersistDraft()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);
        var saved = await client.PutAsJsonAsync("/api/ledger/capital", new
        {
            targetAmount = 1_000_000m,
            monthlyIncome = 12_000m
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var preview = await client.PostAsJsonAsync("/api/ledger/capital/preview", new
        {
            targetAmount = 2_000_000m,
            monthlyIncome = 15_000m
        });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2_000_000m, previewBody.GetProperty("settings").GetProperty("targetAmount").GetDecimal());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/ledger/capital");
        Assert.Equal(1_000_000m, after.GetProperty("settings").GetProperty("targetAmount").GetDecimal());
    }

    [Fact]
    public async Task Put_NullTarget_ReturnsNoTarget()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);
        await client.PutAsJsonAsync("/api/ledger/capital", new { targetAmount = 1_000_000m });

        var cleared = await client.PutAsJsonAsync("/api/ledger/capital", new { targetAmount = (decimal?)null });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);

        var get = await client.GetFromJsonAsync<JsonElement>("/api/ledger/capital");
        Assert.Equal(JsonValueKind.Null, get.GetProperty("settings").GetProperty("targetAmount").ValueKind);
        Assert.Equal("noTarget", get.GetProperty("projection").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, get.GetProperty("projection").GetProperty("etaMonth").ValueKind);
    }

    private static Task<HttpResponseMessage> InitializeAsync(HttpClient client, decimal cashBalance)
    {
        return client.PostAsJsonAsync("/api/ledger/initialize", new
        {
            startDate = "2026-11-01",
            cashBalance
        });
    }
}
