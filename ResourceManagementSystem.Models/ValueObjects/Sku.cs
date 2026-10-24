using System;

namespace ResourceManagementSystem.Models.ValueObjects;

public readonly record struct Sku
{
    public string Value { get; }

    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU is required.", nameof(value));

        Value = value.Trim().ToUpperInvariant();
    }

    public override string ToString() => Value;

    public static implicit operator string(Sku sku) => sku.Value;

    public static implicit operator Sku(string value) => new(value);
}
