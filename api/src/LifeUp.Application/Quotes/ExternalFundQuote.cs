namespace LifeUp.Application.Quotes;

public sealed record ExternalFundQuote(
    string Code,
    decimal NetValue,
    DateOnly EffectiveDate,
    string Source,
    DateTimeOffset ObtainedAt);
