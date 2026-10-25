using System.Globalization;

namespace ResourceManagementSystem.Models.ValueObjects;

/// <summary>
/// Monetary amount in the platform currency (AUD). Value object: equality is by amount and
/// every operation returns a new instance. ApexPost trades in one currency, so there is no
/// currency code to mismatch; rounding lives in <see cref="MoneyExtensions"/>.
/// </summary>
public readonly record struct Money(decimal Amount) : IComparable<Money>
{
    public static Money Zero { get; } = new(0m);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);
    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);
    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor);
    public static Money operator *(Money money, int quantity) => new(money.Amount * quantity);
    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;
    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;
    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;
    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public static Money Max(Money left, Money right) => left >= right ? left : right;

    public override string ToString() =>
        Amount < 0
            ? "-$" + (-Amount).ToString("0.00", CultureInfo.InvariantCulture)
            : "$" + Amount.ToString("0.00", CultureInfo.InvariantCulture);
}
