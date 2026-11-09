namespace LifeUp.Application.Quotes;

public sealed record ExternalGoldQuote(
    decimal Quote,
    DateOnly EffectiveDate,
    string Source,
    DateTimeOffset ObtainedAt);
