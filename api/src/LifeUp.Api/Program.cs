using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeUp.Application.Cash;
using LifeUp.Application.History;
using LifeUp.Application.Funds;
using LifeUp.Application.Gold;
using LifeUp.Api.Plans;
using LifeUp.Api.Quotes;
using LifeUp.Application.Ledgers;
using LifeUp.Application.Plans;
using LifeUp.Application.Provident;
using LifeUp.Application.Quotes;
using LifeUp.Application.Stocks;
using LifeUp.Infrastructure.Backup;
using LifeUp.Infrastructure.Persistence;
using LifeUp.Infrastructure.Quotes;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var connectionString = builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Host=localhost;Port=5432;Database=asset_keeper";
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();
builder.Services.AddSingleton<ReplayDataCache>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<TrendService>();
builder.Services.AddScoped<MonthChangeService>();
builder.Services.AddScoped<LedgerEntryService>();
builder.Services.AddScoped<EntryStatsService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<CashService>();
builder.Services.AddScoped<ProvidentFundService>();
builder.Services.AddScoped<GoldService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<FundService>();
builder.Services.AddScoped<FundGroupService>();
builder.Services.AddScoped<FundReturnService>();
builder.Services.AddScoped<BackupService>();
builder.Services.Configure<QuoteRefreshOptions>(builder.Configuration.GetSection("QuoteRefreshScheduler"));
builder.Services.AddSingleton<QuoteRefreshMonitor>();
builder.Services.AddScoped<QuoteRefreshService>();
builder.Services.AddHostedService<QuoteRefreshSchedulerService>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<PlanService>();
builder.Services.AddScoped<PlanExecutionService>();
builder.Services.Configure<PlanSchedulerOptions>(builder.Configuration.GetSection("PlanScheduler"));
builder.Services.AddHostedService<PlanSchedulerService>();
builder.Services.Configure<QuoteProviderOptions>(builder.Configuration.GetSection("QuoteProviders"));
builder.Services.AddHttpClient<IFundQuoteProvider, EastmoneyFundQuoteProvider>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<QuoteProviderOptions>>().Value;
    client.BaseAddress = new Uri(options.FundBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LifeUp/1.0");
});
builder.Services.AddHttpClient<IGoldQuoteProvider, IcbcGoldQuoteProvider>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<QuoteProviderOptions>>().Value;
    client.BaseAddress = new Uri(options.GoldBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LifeUp/1.0");
});
builder.Services.AddHttpClient<IStockQuoteProvider, EastmoneyStockQuoteProvider>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<QuoteProviderOptions>>().Value;
    client.BaseAddress = new Uri(options.StockBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LifeUp/1.0");
});
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<BusinessDateProvider>();

var app = builder.Build();

app.UseResponseCompression();

if (app.Environment.IsProduction())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // 生产使用 PostgreSQL；内存数据库（集成测试）没有迁移。
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}
