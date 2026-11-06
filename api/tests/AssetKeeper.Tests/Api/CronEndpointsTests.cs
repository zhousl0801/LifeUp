using System.Net;
using System.Net.Http.Json;
using AssetKeeper.Api.Controllers;
using AssetKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssetKeeper.Tests.Api;

public sealed class CronEndpointsTests
{
    [Fact]
    public async Task RunPlans_WithoutConfiguredToken_IsDisabled()
    {
        using var factory = new CronApiFactory(token: null);
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsync("/api/cron/plans/run", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task RunPlans_WithoutTokenHeader_IsRejected()
    {
        using var factory = new CronApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsync("/api/cron/plans/run", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunPlans_WithWrongToken_IsRejected()
    {
        using var factory = new CronApiFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(CronController.TokenHeader, "not-the-token");

        var response = await client.PostAsync("/api/cron/plans/run", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunPlans_WithToken_RunsDuePlansWithoutSession()
    {
        using var factory = new CronApiFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(CronController.TokenHeader, CronApiFactory.Token);

        var response = await client.PostAsync("/api/cron/plans/run", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PlanRunResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.Created);
    }

    [Fact]
    public async Task Health_StaysPublic_ForWakeUpPings()
    {
        using var factory = new CronApiFactory();
        using var client = factory.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }

    private sealed record PlanRunResponse(int Created, int Completed, int Waiting, int Failed);
}

public sealed class CronApiFactory : WebApplicationFactory<Program>
{
    public const string Token = "cron-token-for-tests";

    private readonly string? _token;
    private readonly string _databaseName = $"asset-keeper-cron-{Guid.NewGuid()}";

    public CronApiFactory(string? token = Token)
    {
        _token = token;
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["PlanScheduler:Enabled"] = "false",
                ["QuoteRefreshScheduler:Enabled"] = "false"
            };

            if (_token is not null)
            {
                settings[CronController.TokenKey] = _token;
            }

            configuration.AddInMemoryCollection(settings);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
