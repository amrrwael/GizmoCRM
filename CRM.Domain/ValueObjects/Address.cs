using CRM.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Domain.ValueObjects;

[NotMapped]
public class Address : ValueObject
{
    public string Street { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;

    // Parameterless constructor for EF Core
    private Address() { }

    public Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
}