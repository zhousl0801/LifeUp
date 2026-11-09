namespace LifeUp.Application.Quotes;

public sealed class QuoteUnavailableException : Exception
{
    public QuoteUnavailableException(string message)
        : base(message)
    {
    }

    public QuoteUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
