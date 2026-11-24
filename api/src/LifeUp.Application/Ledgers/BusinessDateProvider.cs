using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Ledgers;

public sealed class BusinessDateProvider
{
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(Ledger.DefaultTimeZone);

    private readonly TimeProvider _timeProvider;

    public BusinessDateProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateOnly Today => ToBusinessDate(UtcNow);

    public DateOnly ToBusinessDate(DateTimeOffset instant)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, BusinessTimeZone).DateTime);
    }

    public DateTimeOffset ToBusinessInstant(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, BusinessTimeZone);

        return new DateTimeOffset(utc);
    }
}
