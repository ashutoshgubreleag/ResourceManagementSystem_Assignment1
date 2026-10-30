using System.Globalization;

namespace ApexPost.Models.ValueObjects;

/// <summary>Weight in kilograms. Cannot be negative; compared and added like a number.</summary>
public readonly record struct Weight : IComparable<Weight>
{
    public Weight(decimal kilograms)
    {
        if (kilograms < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(kilograms), "Weight cannot be negative.");
        }

        Kilograms = kilograms;
    }

    public decimal Kilograms { get; }

    public decimal Grams => Kilograms * 1000m;

    public static Weight Zero { get; } = new(0m);

    public static Weight FromKilograms(decimal kilograms) => new(kilograms);

    public static Weight operator +(Weight left, Weight right) => new(left.Kilograms + right.Kilograms);
    public static bool operator >(Weight left, Weight right) => left.Kilograms > right.Kilograms;
    public static bool operator <(Weight left, Weight right) => left.Kilograms < right.Kilograms;
    public static bool operator >=(Weight left, Weight right) => left.Kilograms >= right.Kilograms;
    public static bool operator <=(Weight left, Weight right) => left.Kilograms <= right.Kilograms;

    public static Weight Max(Weight left, Weight right) => left >= right ? left : right;

    public int CompareTo(Weight other) => Kilograms.CompareTo(other.Kilograms);

    public override string ToString() => Kilograms.ToString("0.###", CultureInfo.InvariantCulture) + " kg";
}
