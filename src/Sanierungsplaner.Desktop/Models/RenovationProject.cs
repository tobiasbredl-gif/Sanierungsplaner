using System.Text.Json.Serialization;

namespace Sanierungsplaner.Desktop.Models;

public sealed record RenovationProject(
    Guid Id, Guid Revision, string Name, string Address, string Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    [JsonIgnore]
    public string AddressLabel => string.IsNullOrWhiteSpace(Address) ? "Noch keine Objektadresse" : Address;
    [JsonIgnore]
    public string UpdatedLabel => $"Zuletzt gespeichert: {UpdatedAt.ToLocalTime():dd.MM.yyyy · HH:mm}";
    public void Validate()
    {
        if (Id == Guid.Empty || Revision == Guid.Empty || string.IsNullOrWhiteSpace(Name)
            || Name.Length > 120 || Address is null || Address.Length > 300
            || Notes is null || Notes.Length > 10000 || CreatedAt == default || UpdatedAt < CreatedAt)
            throw new System.IO.InvalidDataException("Die Projektdaten sind unvollständig oder ungültig.");
    }
}
