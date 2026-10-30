using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ApexPost.Models.ValueObjects;

/// <summary>
/// Branch code such as <c>BR-001</c>. A dedicated type stops a SKU or a staff email
/// being passed where a branch is expected, and normalises case once at the edge.
/// </summary>
public readonly partial record struct BranchCode
{
    public BranchCode(string value)
    {
        var normalised = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Pattern().IsMatch(normalised))
        {
            throw new ArgumentException($"'{value}' is not a valid branch code (expected BR-000).", nameof(value));
        }

        _value = normalised;
    }

    private readonly string? _value;

    /// <summary>The normalised value (empty for <c>default</c>).</summary>
    public string Value => _value ?? string.Empty;

    public static bool TryParse(string? value, [NotNullWhen(true)] out BranchCode? code)
    {
        var normalised = value?.Trim().ToUpperInvariant() ?? string.Empty;
        code = Pattern().IsMatch(normalised) ? new BranchCode(normalised) : null;
        return code is not null;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^BR-[0-9]{3}$")]
    private static partial Regex Pattern();
}
