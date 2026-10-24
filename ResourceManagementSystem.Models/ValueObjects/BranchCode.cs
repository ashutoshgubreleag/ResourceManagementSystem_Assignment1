using System;

namespace ResourceManagementSystem.Models.ValueObjects;

public readonly record struct BranchCode
{
    public string Value { get; }

    public BranchCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Branch code is required.", nameof(value));

        Value = value.Trim().ToUpperInvariant();
    }

    public override string ToString() => Value;

    public static implicit operator string(BranchCode branchCode) => branchCode.Value;

    public static implicit operator BranchCode(string value) => new(value);
}
