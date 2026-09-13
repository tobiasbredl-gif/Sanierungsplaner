using System.Text.Json.Serialization;

namespace Sanierungsplaner.Desktop.Models;

public sealed record RenovationProject(
    Guid Id, Guid Revision, string Name, string Address, string Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public decimal Budget { get; init; }
    public CostItem[] Items { get; init; } = [];
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
        if (Budget < 0 || Budget > 1000000000m || decimal.Round(Budget, 2) != Budget || Items is null || Items.Length > 10000)
            throw new System.IO.InvalidDataException("Ungültiges Budget oder zu viele Kostenpositionen (maximal 10.000).");
        foreach (var item in Items)
        {
            if (item is null) throw new System.IO.InvalidDataException("Ungültige Kostenposition.");
            item.Validate();
        }
        if (Items.Select(item => item.Id).Distinct().Count() != Items.Length)
            throw new System.IO.InvalidDataException("Doppelte Kostenpositionen sind nicht zulässig.");
    }
}
