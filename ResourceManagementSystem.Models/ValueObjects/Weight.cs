using System;

namespace ResourceManagementSystem.Models.ValueObjects;

public readonly record struct Weight(decimal Kilograms)
{
    public Weight(decimal kilograms, bool validate) : this(kilograms)
    {
        if (validate && kilograms < 0m)
            throw new ArgumentOutOfRangeException(nameof(kilograms), "Weight cannot be negative.");
    }

    public decimal InKilograms => Kilograms;

    public decimal InGrams => Kilograms * 1000m;

    public static Weight Zero => new(0m);

    public Weight Add(Weight other) => new(Kilograms + other.Kilograms);

    public Weight Subtract(Weight other) => new(Kilograms - other.Kilograms);

    public override string ToString() => $"{Kilograms:0.##} kg";
}
