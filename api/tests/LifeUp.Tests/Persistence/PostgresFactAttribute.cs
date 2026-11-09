namespace LifeUp.Tests.Persistence;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIFEUP_TEST_CONNECTION")))
        {
            Skip = "Set LIFEUP_TEST_CONNECTION to a local PostgreSQL admin connection; each test creates and removes its own database.";
        }
    }
}
