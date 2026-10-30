using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ApexPost.Models.ValueObjects;

/// <summary>Stock keeping unit such as <c>PKG-001</c>.</summary>
public readonly partial record struct Sku
{
    public Sku(string value)
    {
        var normalised = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Pattern().IsMatch(normalised))
        {
            throw new ArgumentException($"'{value}' is not a valid SKU (expected AAA-000).", nameof(value));
        }

        _value = normalised;
    }

    private readonly string? _value;

    /// <summary>The normalised value (empty for <c>default</c>).</summary>
    public string Value => _value ?? string.Empty;

    public static bool TryParse(string? value, [NotNullWhen(true)] out Sku? sku)
    {
        var normalised = value?.Trim().ToUpperInvariant() ?? string.Empty;
        sku = Pattern().IsMatch(normalised) ? new Sku(normalised) : null;
        return sku is not null;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z]{3}-[0-9]{3}$")]
    private static partial Regex Pattern();
}
