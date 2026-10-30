using System.Globalization;
using System.Text.RegularExpressions;

namespace ApexPost.Models.Validation;

/// <summary>
/// Base class for declarative business-input rules on service commands. Unlike MVC
/// DataAnnotations these live next to the domain, so the same rules apply whether a
/// command arrives from a Razor form, the integration API or a test.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public abstract class RuleAttribute : Attribute
{
    /// <summary>Returns an error message, or <c>null</c> when the value is valid.</summary>
    public abstract string? Validate(string propertyName, object? value);
}

/// <summary>String must contain non-whitespace characters.</summary>
public sealed class NotBlankAttribute : RuleAttribute
{
    public override string? Validate(string propertyName, object? value) =>
        value is string text && !string.IsNullOrWhiteSpace(text) ? null : $"{propertyName} is required.";
}

/// <summary>String length upper bound (ignores null; combine with NotBlank to require).</summary>
public sealed class MaxLengthRuleAttribute(int maxLength) : RuleAttribute
{
    public int MaxLength { get; } = maxLength;

    public override string? Validate(string propertyName, object? value) =>
        value is string text && text.Length > MaxLength
            ? $"{propertyName} must be at most {MaxLength} characters."
            : null;
}

/// <summary>Numeric value within an inclusive range. Works for int and decimal properties.</summary>
public sealed class InRangeAttribute(double minimum, double maximum) : RuleAttribute
{
    public double Minimum { get; } = minimum;
    public double Maximum { get; } = maximum;

    public override string? Validate(string propertyName, object? value)
    {
        // Pattern matching keeps one rule usable for every numeric property type.
        decimal? number = value switch
        {
            int i => i,
            decimal d => d,
            double d => (decimal)d,
            null => null,
            _ => null
        };

        if (number is null)
        {
            return null;
        }

        return number < (decimal)Minimum || number > (decimal)Maximum
            ? string.Create(CultureInfo.InvariantCulture, $"{propertyName} must be between {Minimum} and {Maximum}.")
            : null;
    }
}

/// <summary>String must match a regular expression (case-insensitive).</summary>
public sealed class MatchesAttribute(string pattern, string description) : RuleAttribute
{
    private readonly Regex _regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public string Description { get; } = description;

    public override string? Validate(string propertyName, object? value) =>
        value is string text && !string.IsNullOrEmpty(text) && !_regex.IsMatch(text.Trim())
            ? $"{propertyName} must be {Description}."
            : null;
}

/// <summary>Enum value must be one of the defined members (stops (Role)42 from a tampered form).</summary>
public sealed class DefinedEnumAttribute : RuleAttribute
{
    public override string? Validate(string propertyName, object? value) =>
        value is Enum e && !Enum.IsDefined(e.GetType(), e) ? $"{propertyName} has an invalid value." : null;
}
