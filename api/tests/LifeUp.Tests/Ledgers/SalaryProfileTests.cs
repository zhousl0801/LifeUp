using LifeUp.Domain.Ledgers;

namespace LifeUp.Tests.Ledgers;

public sealed class SalaryProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 13);

    [Fact]
    public void TryValidate_AcceptsValidProfile()
    {
        var profile = new SalaryProfile(20_000m, 12_000m, 4_800m, 4m, 60_000m, 1, 15);

        Assert.True(SalaryProfile.TryValidate(profile, out var errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void TryValidate_AcceptsZeroBonus()
    {
        var profile = new SalaryProfile(20_000m, 12_000m, 0m, 0m, 0m, 1, 1);

        Assert.True(SalaryProfile.TryValidate(profile, out var errors));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0, 12_000, 4_800, 0, 0, 1, 15)]
    [InlineData(20_000, 0, 4_800, 0, 0, 1, 15)]
    [InlineData(20_000, 12_000, -1, 0, 0, 1, 15)]
    [InlineData(20_000, 12_000, 4_800, 25, 60_000, 1, 15)]
    [InlineData(20_000, 12_000, 4_800, 4, 0, 1, 15)]
    [InlineData(20_000, 12_000, 4_800, 0, 0, 0, 15)]
    [InlineData(20_000, 12_000, 4_800, 0, 0, 13, 15)]
    [InlineData(20_000, 12_000, 4_800, 0, 0, 1, 0)]
    [InlineData(20_000, 12_000, 4_800, 0, 0, 1, 32)]
    public void TryValidate_RejectsInvalidProfile(
        decimal baseSalary,
        decimal netSalary,
        decimal provident,
        decimal bonusMonths,
        decimal bonusNet,
        int payoutMonth,
        int payDay)
    {
        var profile = new SalaryProfile(baseSalary, netSalary, provident, bonusMonths, bonusNet, payoutMonth, payDay);

        Assert.False(SalaryProfile.TryValidate(profile, out var errors));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void TryValidate_RejectsBonusMonthsWithoutNetAmount()
    {
        var profile = new SalaryProfile(20_000m, 12_000m, 4_800m, 4m, 0m, 1, 15);

        Assert.False(SalaryProfile.TryValidate(profile, out var errors));
        Assert.Contains(errors, error => error.Contains("年终奖到手"));
    }

    [Fact]
    public void Ledger_SetSalaryProfile_RoundTripsAndReturnsFalseWhenUnchanged()
    {
        var ledger = Ledger.Create(new DateOnly(2026, 11, 1), Today, Now);
        var profile = new SalaryProfile(20_000m, 12_000m, 4_800m, 4m, 60_000m, 1, 15);
        var version = ledger.Version;

        Assert.True(ledger.SetSalaryProfile(profile, Now));
        Assert.Equal(version + 1, ledger.Version);
        Assert.Equal(profile, ledger.GetSalaryProfile());
        Assert.False(ledger.SetSalaryProfile(profile, Now.AddMinutes(1)));
        Assert.Equal(version + 1, ledger.Version);
    }

    [Fact]
    public void Ledger_SetSalaryProfile_RejectsInvalidProfile()
    {
        var ledger = Ledger.Create(new DateOnly(2026, 11, 1), Today, Now);
        var profile = new SalaryProfile(20_000m, 12_000m, 4_800m, 4m, 0m, 1, 15);

        Assert.Throws<ArgumentException>(() => ledger.SetSalaryProfile(profile, Now));
        Assert.Null(ledger.SalaryProfileJson);
    }

    [Fact]
    public void Ledger_ClearSalaryProfile_RemovesJson()
    {
        var ledger = Ledger.Create(new DateOnly(2026, 11, 1), Today, Now);
        ledger.SetSalaryProfile(new SalaryProfile(20_000m, 12_000m, 4_800m, 0m, 0m, 1, 15), Now);

        Assert.True(ledger.SetSalaryProfile(null, Now.AddMinutes(1)));
        Assert.Null(ledger.SalaryProfileJson);
        Assert.Null(ledger.GetSalaryProfile());
    }

    [Fact]
    public void Ledger_HasValidSalaryProfile_ReturnsFalseForMalformedJson()
    {
        var ledger = Ledger.Create(new DateOnly(2026, 11, 1), Today, Now);
        ledger.SetSalaryProfile(new SalaryProfile(20_000m, 12_000m, 4_800m, 0m, 0m, 1, 15), Now);

        typeof(Ledger).GetProperty(nameof(Ledger.SalaryProfileJson))!
            .SetValue(ledger, "{\"baseMonthlySalary\":-1}");

        Assert.False(ledger.HasValidSalaryProfile());
    }
}
