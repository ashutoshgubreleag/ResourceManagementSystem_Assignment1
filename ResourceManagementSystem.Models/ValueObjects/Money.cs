using System;

namespace ResourceManagementSystem.Models.ValueObjects;

public readonly record struct Money(decimal Amount, string Currency = "AUD")
{
    public static Money Zero(string currency = "AUD") => new(0m, currency);

    public Money RoundToCurrency()
    {
        var rounded = Math.Round(Amount, 2, MidpointRounding.AwayFromZero);
        return new Money(rounded, Currency);
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(RoundToTwoDecimalPlaces(Amount + other.Amount), Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(RoundToTwoDecimalPlaces(Amount - other.Amount), Currency);
    }

    public Money Multiply(decimal multiplier)
    {
        if (multiplier < 0m)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier cannot be negative.");

        return new Money(RoundToTwoDecimalPlaces(Amount * multiplier), Currency);
    }

    public decimal ToDecimal() => Amount;

    public override string ToString() => $"{Amount:0.00} {Currency}";

    private static decimal RoundToTwoDecimalPlaces(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot operate on money with different currencies.");
    }
}
