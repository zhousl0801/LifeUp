using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeUp.Tests.Ledgers;

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
            goalMode = "custom",
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
    public async Task Put_SavesBonusAndFollowFlag()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);

        var put = await client.PutAsJsonAsync("/api/ledger/capital", new
        {
            targetAmount = 1_000_000m,
            annualBonus = 60_000m,
            bonusPayoutMonth = 1,
            useSalaryProfile = false
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(60_000m, saved.GetProperty("settings").GetProperty("annualBonus").GetDecimal());
        Assert.Equal(1, saved.GetProperty("settings").GetProperty("bonusPayoutMonth").GetInt32());
        Assert.Equal(60_000m, saved.GetProperty("effective").GetProperty("annualBonus").GetProperty("value").GetDecimal());
        Assert.False(saved.GetProperty("hasSalaryProfile").GetBoolean());
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

    [Fact]
    public async Task FireSettings_PersistAndPreviewSwitchesMode()
    {
        using var factory = new LifeUpApiFactory
        {
            Clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 12, 2, 0, 0, TimeSpan.Zero))
        };
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);

        var initial = await client.GetFromJsonAsync<JsonElement>("/api/ledger/capital");
        Assert.Equal("fire", initial.GetProperty("settings").GetProperty("goalMode").GetString());
        Assert.Equal("noTarget", initial.GetProperty("projection").GetProperty("status").GetString());
        Assert.Equal(0m, initial.GetProperty("projection").GetProperty("targetAmount").GetDecimal());

        var expense = await client.PostAsJsonAsync("/api/ledger/entries", new
        {
            kind = "expense",
            category = "餐饮",
            amount = 50m,
            businessDate = "2026-11-12",
            requestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);

        var put = await client.PutAsJsonAsync("/api/ledger/capital", new
        {
            goalMode = " ",
            fireMode = "lean",
            safeWithdrawalRatePercent = 5m,
            monthlyIncome = 12_000m,
            monthlyExpense = 6_000m,
            annualBonus = 12_000m,
            excludedExpenseCategories = new[] { " 娱乐 ", "娱乐", " " },
            leanExpenseCategories = Array.Empty<string>(),
            fireMonthlyExpenseOverride = 5_000m,
            leanMonthlyExpenseOverride = 3_000m,
            coastTargetYear = 2055
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var saved = await client.GetFromJsonAsync<JsonElement>("/api/ledger/capital");
        var settings = saved.GetProperty("settings");
        Assert.Equal("fire", settings.GetProperty("goalMode").GetString());
        Assert.Equal("lean", settings.GetProperty("fireMode").GetString());
        Assert.Equal("娱乐", Assert.Single(settings.GetProperty("excludedExpenseCategories").EnumerateArray()).GetString());
        Assert.Empty(settings.GetProperty("leanExpenseCategories").EnumerateArray());
        var fire = saved.GetProperty("fire");
        Assert.Equal(53.8m, fire.GetProperty("savingsRatePercent").GetDecimal());
        Assert.Equal("lean", fire.GetProperty("activeMode").GetString());
        Assert.Equal(1_200_000m, fire.GetProperty("standard").GetProperty("targetCapital").GetDecimal());
        Assert.Equal(2055, fire.GetProperty("coast").GetProperty("targetYear").GetInt32());
        Assert.Equal(50m, saved.GetProperty("actualMonth").GetProperty("actualExpense").GetDecimal());
        var projected = saved.GetProperty("projection");
        Assert.Equal(720_000m, projected.GetProperty("targetAmount").GetDecimal());
        Assert.Equal(36_000m, projected.GetProperty("points")[0].GetProperty("expectedAnnualExpense").GetDecimal());
        Assert.True(projected.GetProperty("years")[0].GetProperty("passiveMonthlyCashflow").GetDecimal() > 0m);

        var preview = await client.PostAsJsonAsync("/api/ledger/capital/preview", new
        {
            goalMode = "custom",
            targetAmount = 900_000m,
            fireMonthlyExpenseOverride = 4_000m
        });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var draft = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("custom", draft.GetProperty("fire").GetProperty("goalMode").GetString());
        Assert.Equal(900_000m, draft.GetProperty("projection").GetProperty("targetAmount").GetDecimal());
        Assert.Single(draft.GetProperty("projection").GetProperty("milestones").EnumerateArray());

        var persisted = await client.GetFromJsonAsync<JsonElement>("/api/ledger/capital");
        Assert.Equal("fire", persisted.GetProperty("fire").GetProperty("goalMode").GetString());
        Assert.Equal("lean", persisted.GetProperty("fire").GetProperty("activeMode").GetString());
    }

    [Fact]
    public async Task FireSettings_NormalizeNullModesAndRejectInvalidValues()
    {
        using var factory = new LifeUpApiFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client, 100_000m);

        var preview = await client.PostAsJsonAsync("/api/ledger/capital/preview", new
        {
            goalMode = (string?)null,
            fireMode = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var fire = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fire");
        Assert.Equal("fire", fire.GetProperty("goalMode").GetString());
        Assert.Equal("standard", fire.GetProperty("activeMode").GetString());
        Assert.Equal(4m, fire.GetProperty("safeWithdrawalRatePercent").GetDecimal());

        var badMode = await client.PutAsJsonAsync("/api/ledger/capital", new { fireMode = "invalid" });
        var badRate = await client.PostAsJsonAsync("/api/ledger/capital/preview", new { safeWithdrawalRatePercent = 4.123m });
        Assert.Equal(HttpStatusCode.BadRequest, badMode.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badRate.StatusCode);
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
