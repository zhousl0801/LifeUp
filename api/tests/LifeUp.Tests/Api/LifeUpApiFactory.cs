using LifeUp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LifeUp.Application.Quotes;

namespace LifeUp.Tests.Api;

public sealed class LifeUpApiFactory : WebApplicationFactory<Program>
{
    public TestQuoteProvider Quotes { get; } = new();
    public TimeProvider? Clock { get; init; }
    public string Environment { get; init; } = "Development";
    private readonly string _databaseName = $"lifeup-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlanScheduler:Enabled"] = "false",
                ["QuoteRefreshScheduler:Enabled"] = "false"
            });
        });
        builder.ConfigureServices(services =>
        {
            if (Clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(Clock);
            }
            services.RemoveAll<IFundQuoteProvider>();
            services.AddSingleton<IFundQuoteProvider>(Quotes);
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public sealed class TestQuoteProvider : IFundQuoteProvider
    {
        public decimal NetValue { get; set; } = 10m;
        public DateOnly Date { get; set; } = new(2026, 10, 20);

        public Task<ExternalFundQuote> GetLatestAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(new ExternalFundQuote(code, NetValue, Date, "test-provider", DateTimeOffset.UtcNow));
    }
}
