using LifeUp.Tests.Quotes;
using System.Text;
using LifeUp.Application.Backup;
using LifeUp.Application.Cash;
using LifeUp.Application.Funds;
using LifeUp.Application.Gold;
using LifeUp.Application.Stocks;
using LifeUp.Application.Ledgers;
using LifeUp.Application.Plans;
using LifeUp.Application.Provident;
using LifeUp.Domain.Assets;
using LifeUp.Domain.Ledgers;
using LifeUp.Domain.Plans;
using LifeUp.Infrastructure.Backup;
using LifeUp.Infrastructure.Persistence;
using LifeUp.Tests.Ledgers;
using Microsoft.EntityFrameworkCore;

namespace LifeUp.Tests.Backup;

public sealed class BackupServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExportValidateAndRestore_PreservesAllSectionsAndDoesNotDuplicate()
    {
        await using var source = await CreateSourceAsync();
        await using var targetDb = CreateDb();
        var target = new BackupService(targetDb, source.Dates);
        await new LedgerService(new LedgerRepository(source.Db), source.Dates)
            .UpdateMonthlyBudgetAsync("2026-10", 1200.50m, CancellationToken.None);
        var export = await source.Backup.ExportAsync(CancellationToken.None);

        Assert.NotNull(export);
        Assert.NotEmpty(export.Entries);
        Assert.Equal(1, Assert.Single(export.FundPositions).ConfirmationDelayDays);
        Assert.Single(export.StockPositions);
        Assert.NotEmpty(export.AssetTransactions);
        Assert.NotEmpty(export.Plans);
        Assert.NotEmpty(export.PlanOccurrences);

        var validation = await target.ValidateAsync(export, CancellationToken.None);

        Assert.True(validation.IsValid);
        Assert.False(validation.WillReplace);
        Assert.NotNull(validation.ValidationToken);
        Assert.True(validation.Summary.EntryCount > 0);
        Assert.Equal(export.CashAccount.Balance, validation.Summary.CashBalance);

        var restored = await target.RestoreAsync(
            new RestoreBackupRequest(export, validation.ValidationToken!, false),
            CancellationToken.None);

        Assert.True(restored.Restored);
        Assert.False(restored.ReplacedExisting);
        Assert.Equal(validation.Summary.EntryCount, restored.Summary.EntryCount);

        var afterRestore = await target.ExportAsync(CancellationToken.None);
        Assert.NotNull(afterRestore);
        Assert.Equal(export.Ledger.Id, afterRestore.Ledger.Id);
        Assert.Equal(export.Entries.Count, afterRestore.Entries.Count);
        Assert.Equal(export.AssetTransactions.Count, afterRestore.AssetTransactions.Count);
        Assert.Equal(export.Plans.Count, afterRestore.Plans.Count);
        Assert.Equal(export.PlanOccurrences.Count, afterRestore.PlanOccurrences.Count);
        Assert.Equal(export.MutationReceipts.Count, afterRestore.MutationReceipts.Count);
        Assert.Equal(export.Categories.Count, afterRestore.Categories.Count);
        Assert.Equal(export.FundGroups.Count, afterRestore.FundGroups.Count);
        Assert.Equal(export.FundPositions.Count, afterRestore.FundPositions.Count);
        Assert.Equal(1, Assert.Single(afterRestore.FundPositions).ConfirmationDelayDays);
        Assert.Equal(export.StockPositions.Count, afterRestore.StockPositions.Count);
        Assert.Equal(export.StockPositions[0].Code, afterRestore.StockPositions[0].Code);
        Assert.Equal(export.CashAdjustments.Count, afterRestore.CashAdjustments.Count);
        Assert.Equal(export.PriceObservations.Count, afterRestore.PriceObservations.Count);
        Assert.Equal(export.CashAccount.Balance, afterRestore.CashAccount.Balance);
        var budget = await new LedgerService(new LedgerRepository(targetDb), source.Dates)
            .GetMonthlyBudgetAsync("2026-10", CancellationToken.None);
        Assert.Equal(1200.50m, budget?.Amount);
        Assert.Equal(export.Ledger.NextFactSequence, afterRestore.Ledger.NextFactSequence);

        var secondValidation = await target.ValidateAsync(afterRestore, CancellationToken.None);
        Assert.True(secondValidation.IsValid);
        Assert.True(secondValidation.WillReplace);
        await target.RestoreAsync(
            new RestoreBackupRequest(afterRestore, secondValidation.ValidationToken!, true),
            CancellationToken.None);

        var afterSecondRestore = await target.ExportAsync(CancellationToken.None);
        Assert.NotNull(afterSecondRestore);
        Assert.Equal(export.Entries.Count, afterSecondRestore.Entries.Count);
        Assert.Equal(export.AssetTransactions.Count, afterSecondRestore.AssetTransactions.Count);
        Assert.Equal(export.PlanOccurrences.Count, afterSecondRestore.PlanOccurrences.Count);
    }

    [Fact]
    public async Task BackupRoundTrip_PreservesCapitalGoal()
    {
        await using var source = await CreateSourceAsync();
        var goal = new CapitalGoal(1_000_000m, 12_000m, 7_200m, 2_000m, 3m, 2m, 5m, 1.5m);
        var ledger = await source.Db.Ledgers.SingleAsync();
        ledger.SetCapitalGoal(goal, Now);
        await source.Db.SaveChangesAsync();

        var export = (await source.Backup.ExportAsync(CancellationToken.None))!;

        Assert.Equal(4, export.Version);
        Assert.NotNull(export.Ledger.CapitalGoalJson);

        await using var targetDb = CreateDb();
        var target = new BackupService(targetDb, source.Dates);
        var validation = await target.ValidateAsync(export, CancellationToken.None);
        Assert.True(validation.IsValid, string.Join(" ", validation.Errors));

        await target.RestoreAsync(
            new RestoreBackupRequest(export, validation.ValidationToken!, false),
            CancellationToken.None);

        var restored = await targetDb.Ledgers.SingleAsync();
        Assert.Equal(goal, restored.GetCapitalGoal());
    }

    [Fact]
    public async Task Restore_WithInvalidCapitalGoalJson_FailsValidation()
    {
        await using var source = await CreateSourceAsync();
        var export = (await source.Backup.ExportAsync(CancellationToken.None))!;
        export.Ledger.CapitalGoalJson = "{\"targetAmount\":-1}";

        var validation = await source.Backup.ValidateAsync(export, CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("资本目标"));
    }

    [Fact]
    public async Task Restore_WithVersion3WithoutCapitalGoal_Succeeds()
    {
        await using var source = await CreateSourceAsync();
        var export = (await source.Backup.ExportAsync(CancellationToken.None))!;
        export.Version = 3;
        export.Ledger.CapitalGoalJson = null;

        await using var targetDb = CreateDb();
        var target = new BackupService(targetDb, source.Dates);
        var validation = await target.ValidateAsync(export, CancellationToken.None);
        Assert.True(validation.IsValid, string.Join(" ", validation.Errors));

        await target.RestoreAsync(
            new RestoreBackupRequest(export, validation.ValidationToken!, false),
            CancellationToken.None);

        var restored = await targetDb.Ledgers.SingleAsync();
        Assert.Null(restored.GetCapitalGoal());
    }

    [Fact]
    public async Task RestoringFlatBackup_AddsGroupsAndPreservesExistingCategoryIdsAndBalance()
    {
        await using var source = await CreateSourceAsync();
        await using var targetDb = CreateDb();
        var target = new BackupService(targetDb, source.Dates);
        var export = (await source.Backup.ExportAsync(CancellationToken.None))!;
        export.Version = 2;
        export.Categories.RemoveAll(category => category.IsGroup);
        foreach (var category in export.Categories)
        {
            category.ParentId = null;
        }

        var diningId = export.Categories.Single(category => category.Kind == "Expense" && category.Name == "餐饮").Id;
        var validation = await target.ValidateAsync(export, CancellationToken.None);
        Assert.True(validation.IsValid, string.Join(" ", validation.Errors));
        await target.RestoreAsync(new RestoreBackupRequest(export, validation.ValidationToken!, false), CancellationToken.None);

        var restored = (await target.ExportAsync(CancellationToken.None))!;
        var dining = Assert.Single(restored.Categories, category => category.Id == diningId);
        Assert.Contains(restored.Categories, group => group.Id == dining.ParentId && group.IsGroup && group.Name == "必要");
        Assert.Equal(export.CashAccount.Balance, restored.CashAccount.Balance);
        Assert.Equal(export.Entries.Select(entry => entry.Id), restored.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task RestoreRejectsValidationTokenAfterCategoryMoved()
    {
        await using var source = await CreateSourceAsync();
        var export = (await source.Backup.ExportAsync(CancellationToken.None))!;
        var validation = await source.Backup.ValidateAsync(export, CancellationToken.None);
        Assert.True(validation.IsValid);

        var service = new CategoryService(new LedgerRepository(source.Db), source.Dates);
        var dining = Assert.Single(export.Categories, category => category.Name == "餐饮");
        var flexible = Assert.Single(export.Categories, category => category.IsGroup && category.Name == "弹性");
        var moved = await service.UpdateAsync(dining.Id, new UpdateCategoryRequest(dining.Name, true, flexible.Id), CancellationToken.None);
        Assert.Equal(CategoryOperationStatus.Success, moved.Status);

        var error = await Assert.ThrowsAsync<BackupValidationException>(() => source.Backup.RestoreAsync(
            new RestoreBackupRequest(export, validation.ValidationToken!, true), CancellationToken.None));
        Assert.Contains(error.Errors, message => message.Contains("重新校验"));
    }

    [Fact]
    public async Task Validate_ReportsDuplicateRequestIdAndBrokenLink()
    {
        await using var source = await CreateSourceAsync();
        var export = await source.Backup.ExportAsync(CancellationToken.None);
        Assert.NotNull(export);

        export.Entries.Add(export.Entries[0]);
        export.Entries.Add(new BackupLedgerEntry
        {
            Id = Guid.NewGuid(),
            LedgerId = export.Ledger.Id,
            Kind = "Expense",
            Category = "餐饮",
            Amount = 10m,
            BusinessDate = new DateOnly(2026, 10, 1),
            Source = "Manual",
            Status = "Active",
            RequestId = Guid.NewGuid(),
            RelatedCashAdjustmentId = Guid.NewGuid(),
            CreatedAt = Now,
            UpdatedAt = Now
        });

        var validation = await source.Backup.ValidateAsync(export, CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Null(validation.ValidationToken);
        Assert.Contains(validation.Errors, error => error.Contains("重复", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("现金核对不存在", StringComparison.Ordinal));
        Assert.NotEmpty(validation.DuplicateRisks);
    }

    [Fact]
    public async Task Restore_RejectsChangedValidationToken()
    {
        await using var source = await CreateSourceAsync();
        await using var targetDb = CreateDb();
        var target = new BackupService(targetDb, source.Dates);
        var export = await source.Backup.ExportAsync(CancellationToken.None);
        var validation = await target.ValidateAsync(export, CancellationToken.None);

        await Assert.ThrowsAsync<BackupValidationException>(() => target.RestoreAsync(
            new RestoreBackupRequest(export!, "not-the-token", false),
            CancellationToken.None));
    }

    [Fact]
    public async Task DetailExport_ContainsEntriesAssetsPlansAndStatuses()
    {
        await using var source = await CreateSourceAsync();

        var result = await source.Backup.ExportDetailsAsync(null, null, CancellationToken.None);

        Assert.NotNull(result);
        var content = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("日期,业务类型,方向,金额", content);
        Assert.Contains("工资", content);
        Assert.Contains("基金买入待确认", content);
        Assert.Contains("自动计划", content);
        Assert.Contains("纳指基金", content);
        Assert.Contains("request=", content);
    }


    [Fact]
    public void BackupDtos_CoverEveryScalarEntityProperty()
    {
        var pairs = new (Type Entity, Type Dto)[]
        {
            (typeof(LifeUp.Domain.Ledgers.Ledger), typeof(BackupLedger)),
            (typeof(LifeUp.Domain.Cash.CashAccount), typeof(BackupCashAccount)),
            (typeof(LifeUp.Domain.Assets.ProvidentFundAccount), typeof(BackupProvidentFundAccount)),
            (typeof(LifeUp.Domain.Assets.GoldHolding), typeof(BackupGoldHolding)),
            (typeof(LifeUp.Domain.Ledgers.EntryCategory), typeof(BackupEntryCategory)),
            (typeof(LifeUp.Domain.Ledgers.LedgerEntry), typeof(BackupLedgerEntry)),
            (typeof(LifeUp.Domain.Cash.CashAdjustment), typeof(BackupCashAdjustment)),
            (typeof(LifeUp.Domain.Assets.FundGroup), typeof(BackupFundGroup)),
            (typeof(LifeUp.Domain.Assets.FundPosition), typeof(BackupFundPosition)),
            (typeof(LifeUp.Domain.Assets.StockPosition), typeof(BackupStockPosition)),
            (typeof(LifeUp.Domain.Assets.AssetTransaction), typeof(BackupAssetTransaction)),
            (typeof(LifeUp.Domain.Assets.PriceObservation), typeof(BackupPriceObservation)),
            (typeof(AutoPlan), typeof(BackupPlan)),
            (typeof(PlanOccurrence), typeof(BackupPlanOccurrence)),
            (typeof(LifeUp.Domain.Ledgers.MutationReceipt), typeof(BackupMutationReceipt))
        };

        foreach (var (entity, dto) in pairs)
        {
            var missing = entity.GetProperties()
                .Where(property => property.CanWrite && IsScalar(property.PropertyType))
                .Where(property => dto.GetProperty(property.Name) is null)
                .Select(property => property.Name)
                .ToList();

            Assert.True(missing.Count == 0, $"{entity.Name} 缺少备份字段：{string.Join("、", missing)}");
        }
    }

    private static bool IsScalar(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;

        return target.IsPrimitive
            || target.IsEnum
            || target == typeof(string)
            || target == typeof(decimal)
            || target == typeof(Guid)
            || target == typeof(DateOnly)
            || target == typeof(TimeOnly)
            || target == typeof(DateTimeOffset);
    }

    private static AppDbContext CreateDb()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static async Task<SourceContext> CreateSourceAsync()
    {
        var db = CreateDb();
        var repository = new LedgerRepository(db);
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        await new LedgerService(repository, dates).InitializeAsync(
            new InitializeLedgerRequest(new DateOnly(2026, 9, 1), 10000m),
            CancellationToken.None);
        var entries = new LedgerEntryService(repository, dates);
        await entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Income, "工资", 1000m, new DateOnly(2026, 10, 1), "工资到账", Guid.NewGuid()),
            CancellationToken.None);
        await entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Expense, "餐饮", 200m, new DateOnly(2026, 10, 2), "聚餐", Guid.NewGuid()),
            CancellationToken.None);
        await new ProvidentFundService(repository, dates).DepositAsync(
            new ProvidentDepositRequest(500m, new DateOnly(2026, 10, 3), "缴存", Guid.NewGuid()),
            CancellationToken.None);
        var funds = new FundService(repository, dates);
        var position = await TestFundQuotes.CreatePositionAsync(repository, dates,
            new CreateFundPositionRequest(
                "纳指基金",
                "000001",
                "A",
                "平台一",
                100m,
                1000m,
                10m,
                new DateOnly(2026, 9, 20),
                null,
                Guid.NewGuid(),
                HoldingDate: new DateOnly(2026, 9, 20), ConfirmationDelayDays: 1),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(repository, dates, position.Result!.Position.Id, 11m, new DateOnly(2026, 10, 10));
        await funds.BuyAsync(
            new BuyFundRequest(position.Result.Position.Id, 300m, new DateOnly(2026, 10, 12), "定投", Guid.NewGuid()),
            CancellationToken.None);
        var cash = new CashService(repository, dates);
        var cashOverview = await cash.GetOverviewAsync(CancellationToken.None);
        await cash.ReconcileAsync(
            new ReconcileCashRequest(10850m, "漏记", Guid.NewGuid(), cashOverview!.Version),
            CancellationToken.None);
        var groups = new FundGroupService(repository, dates);
        var group = await groups.CreateAsync(new CreateFundGroupRequest("纳斯达克指数"), CancellationToken.None);
        await groups.AssignAsync(position.Result.Position.Id, new AssignFundGroupRequest(group.Group!.Id), CancellationToken.None);
        await new GoldService(repository, dates).SaveHoldingAsync(
            new SaveGoldHoldingRequest(10m, 8000m, 900m, new DateOnly(2026, 9, 20), "已有积存金", Guid.NewGuid(), HoldingDate: new DateOnly(2026, 9, 20)),
            CancellationToken.None);
        await new StockService(repository, dates).SaveHoldingAsync(
            new SaveStockHoldingRequest(
                LifeUp.Domain.Assets.StockMarket.Shanghai,
                "600519",
                "贵州茅台",
                100m,
                150000m,
                1600m,
                new DateOnly(2026, 9, 20),
                new DateOnly(2026, 9, 20),
                "已有股票",
                Guid.NewGuid()),
            CancellationToken.None);

        var planRepository = new PlanRepository(db);
        var plan = AutoPlan.Create(
            repository.GetAsync(CancellationToken.None).Result!.Id,
            AutoPlanType.SalaryIncome,
            "工资计划",
            1000m,
            AutoPlanCycle.Monthly,
            15,
            null,
            new TimeOnly(9, 0),
            new DateOnly(2026, 10, 1),
            null,
            Now);
        await planRepository.AddPlanAsync(plan, CancellationToken.None);
        var occurrence = PlanOccurrence.Create(plan, "2026-10", new DateOnly(2026, 10, 15), new TimeOnly(9, 0), Now);
        await planRepository.AddOccurrenceAsync(occurrence, CancellationToken.None);
        await planRepository.SaveChangesAsync(CancellationToken.None);

        return new SourceContext(db, dates, new BackupService(db, dates));
    }

    private sealed record SourceContext(
        AppDbContext Db,
        BusinessDateProvider Dates,
        BackupService Backup) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
