namespace ResourceManagementSystem.Models.ValueObjects;

/// <summary>Recipient address printed on a shipping label. Immutable; equality by value.</summary>
public sealed record Address
{
    public Address(string recipientName, string line1, string city, string state, string postcode)
    {
        RecipientName = Required(recipientName, nameof(recipientName));
        Line1 = Required(line1, nameof(line1));
        City = Required(city, nameof(city));
        State = Required(state, nameof(state)).ToUpperInvariant();
        Postcode = Required(postcode, nameof(postcode));
    }

    public string RecipientName { get; }
    public string Line1 { get; }
    public string City { get; }
    public string State { get; }
    public string Postcode { get; }

    public override string ToString() => $"{RecipientName}, {Line1}, {City} {State} {Postcode}";

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
