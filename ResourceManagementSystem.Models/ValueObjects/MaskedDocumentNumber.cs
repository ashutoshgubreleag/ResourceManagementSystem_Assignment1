using System.Text.RegularExpressions;

namespace ResourceManagementSystem.Models.ValueObjects;

/// <summary>
/// An identity document number reduced to its last four characters (<c>****1234</c>).
/// The only way to build one from a raw number is <see cref="FromRaw"/>, which discards
/// everything else, so a full document number can never reach storage or logs.
/// </summary>
public readonly partial record struct MaskedDocumentNumber
{
    private const int VisibleCharacters = 4;

    private MaskedDocumentNumber(string masked) => _value = masked;

    private readonly string? _value;

    /// <summary>The normalised value (empty for <c>default</c>).</summary>
    public string Value => _value ?? string.Empty;

    public static MaskedDocumentNumber FromRaw(string rawDocumentNumber)
    {
        var cleaned = new string((rawDocumentNumber ?? string.Empty)
            .Where(char.IsAsciiLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (cleaned.Length <= VisibleCharacters)
        {
            throw new ArgumentException("Document number is too short to verify.", nameof(rawDocumentNumber));
        }

        return new MaskedDocumentNumber(new string('*', VisibleCharacters) + cleaned[^VisibleCharacters..]);
    }

    /// <summary>Rehydrates an already-masked value (from storage). Rejects anything unmasked.</summary>
    public static MaskedDocumentNumber FromMasked(string masked) =>
        MaskedPattern().IsMatch(masked ?? string.Empty)
            ? new MaskedDocumentNumber(masked!)
            : throw new ArgumentException("Value is not a masked document number.", nameof(masked));

    public override string ToString() => Value;

    [GeneratedRegex("^\\*{4}[A-Z0-9]{4}$")]
    private static partial Regex MaskedPattern();
}
