using System.Globalization;
using System.Text.Json;

namespace LifeUp.Infrastructure.Quotes;

internal static class EastmoneyJsonReader
{
    public static IEnumerable<string> NormalizeBaseUrls(params string?[] urls)
    {
        return urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!.EndsWith('/') ? url : $"{url}/")
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static decimal? ReadScaledDecimal(JsonElement element, string propertyName, int precision)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number / (decimal)Math.Pow(10, precision);
        }

        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
        {
            return number / (decimal)Math.Pow(10, precision);
        }

        return null;
    }

    public static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    public static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var result) ? result : null;
    }

    public static long? ReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt64(out var result) ? result : null;
    }
}
