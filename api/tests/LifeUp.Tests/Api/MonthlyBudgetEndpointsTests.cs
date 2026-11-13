using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LifeUp.Tests.Api;

public sealed class MonthlyBudgetEndpointsTests
{
    [Fact]
    public async Task BudgetCanBeSetAndClearedForOneMonthWithoutAffectingAnother()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        var setup = await client.PostAsJsonAsync("/api/ledger/initialize", new { startDate = "2026-10-01", cashBalance = 1000m });
        Assert.Equal(HttpStatusCode.Created, setup.StatusCode);

        const string october = "/api/ledger/monthly-budget?month=2026-10";
        const string november = "/api/ledger/monthly-budget?month=2026-11";
        Assert.Equal(JsonValueKind.Null, (await client.GetFromJsonAsync<JsonElement>(october)).GetProperty("amount").ValueKind);

        var saved = await client.PutAsJsonAsync(october, new { amount = 950.25m });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(950.25m, (await client.GetFromJsonAsync<JsonElement>(october)).GetProperty("amount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, (await client.GetFromJsonAsync<JsonElement>(november)).GetProperty("amount").ValueKind);

        var invalid = await client.PutAsJsonAsync(october, new { amount = 1.234m });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(950.25m, (await client.GetFromJsonAsync<JsonElement>(october)).GetProperty("amount").GetDecimal());

        var missing = await client.PutAsJsonAsync(october, new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var cleared = await client.PutAsJsonAsync(october, new { amount = (decimal?)null });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await client.GetFromJsonAsync<JsonElement>(october)).GetProperty("amount").ValueKind);
    }
}
