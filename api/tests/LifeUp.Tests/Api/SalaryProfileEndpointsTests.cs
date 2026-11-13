using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LifeUp.Tests.Api;

public sealed class SalaryProfileEndpointsTests
{
    [Fact]
    public async Task Get_WithoutLedger_ReturnsNotFound()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ledger/salary-profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutProfile_ReturnsNullProfile()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/ledger/salary-profile");

        Assert.Equal(JsonValueKind.Null, body.GetProperty("profile").ValueKind);
    }

    [Fact]
    public async Task Put_CreatesProfileAndPlans()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);

        var put = await client.PutAsJsonAsync("/api/ledger/salary-profile", new
        {
            requestId = Guid.NewGuid(),
            baseMonthlySalary = 20_000m,
            netMonthlySalary = 12_000m,
            providentMonthly = 4_800m,
            bonusMonths = 4m,
            bonusNetAmount = 60_000m,
            bonusPayoutMonth = 1,
            payDay = 15
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20_000m, saved.GetProperty("profile").GetProperty("baseMonthlySalary").GetDecimal());
        Assert.Equal(60_000m, saved.GetProperty("profile").GetProperty("bonusNetAmount").GetDecimal());

        var get = await client.GetFromJsonAsync<JsonElement>("/api/ledger/salary-profile");
        Assert.Equal(1, get.GetProperty("profile").GetProperty("bonusPayoutMonth").GetInt32());

        var plans = await client.GetFromJsonAsync<JsonElement>("/api/ledger/plans");
        var types = plans.GetProperty("plans").EnumerateArray()
            .Select(plan => plan.GetProperty("type").GetString())
            .ToList();
        Assert.Equal(3, types.Count);
        Assert.Contains("salaryIncome", types);
        Assert.Contains("providentDeposit", types);
        Assert.Contains("bonusIncome", types);
    }

    [Fact]
    public async Task Put_InvalidProfile_ReturnsBadRequest()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);

        var put = await client.PutAsJsonAsync("/api/ledger/salary-profile", new
        {
            requestId = Guid.NewGuid(),
            baseMonthlySalary = 20_000m,
            netMonthlySalary = 0m,
            providentMonthly = 4_800m,
            bonusMonths = 4m,
            bonusNetAmount = 60_000m,
            bonusPayoutMonth = 1,
            payDay = 15
        });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Put_SameRequestId_DoesNotDuplicatePlans()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);
        var request = new
        {
            requestId = Guid.NewGuid(),
            baseMonthlySalary = 20_000m,
            netMonthlySalary = 12_000m,
            providentMonthly = 4_800m,
            bonusMonths = 4m,
            bonusNetAmount = 60_000m,
            bonusPayoutMonth = 1,
            payDay = 15
        };

        var first = await client.PutAsJsonAsync("/api/ledger/salary-profile", request);
        var second = await client.PutAsJsonAsync("/api/ledger/salary-profile", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var plans = await client.GetFromJsonAsync<JsonElement>("/api/ledger/plans");
        Assert.Equal(3, plans.GetProperty("plans").GetArrayLength());
    }

    [Fact]
    public async Task Disable_RemovesProfileAndKeepsPlans()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);
        await client.PutAsJsonAsync("/api/ledger/salary-profile", new
        {
            requestId = Guid.NewGuid(),
            baseMonthlySalary = 20_000m,
            netMonthlySalary = 12_000m,
            providentMonthly = 4_800m,
            bonusMonths = 4m,
            bonusNetAmount = 60_000m,
            bonusPayoutMonth = 1,
            payDay = 15
        });

        var disable = await client.PostAsJsonAsync(
            "/api/ledger/salary-profile/disable",
            new { requestId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        var get = await client.GetFromJsonAsync<JsonElement>("/api/ledger/salary-profile");
        Assert.Equal(JsonValueKind.Null, get.GetProperty("profile").ValueKind);
        var plans = await client.GetFromJsonAsync<JsonElement>("/api/ledger/plans");
        Assert.Equal(3, plans.GetProperty("plans").GetArrayLength());
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
