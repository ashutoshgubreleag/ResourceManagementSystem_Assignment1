namespace ResourceManagementSystem.Models.ValueObjects;

public sealed record Address
{
    public Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
    }

    public string Street { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string PostalCode { get; init; }
    public string Country { get; init; }

    public override string ToString() => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
