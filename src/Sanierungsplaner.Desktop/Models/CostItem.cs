using System.Globalization;
using System.IO;
using System.Text.Json.Serialization;

namespace Sanierungsplaner.Desktop.Models;

public sealed record Payments(decimal Lea = 0, decimal Wolfgang = 0, decimal Jennifer = 0, decimal Tobias = 0)
{
    [JsonIgnore] public decimal Total => Lea + Wolfgang + Jennifer + Tobias;
    public void Validate()
    {
        foreach (var amount in new[] { Lea, Wolfgang, Jennifer, Tobias })
            if (amount < 0 || amount > 1000000000m || decimal.Round(amount, 2) != amount)
                throw new InvalidDataException("Zahlungen müssen positiv oder null sein und dürfen höchstens zwei Nachkommastellen haben.");
    }
}

public sealed record CostItem(Guid Id, string Material, string Floor, string Room,
    decimal Quantity, string Unit, decimal UnitPrice, string Status, Payments Payments)
{
    public static IReadOnlyList<string> Statuses { get; } = Array.AsReadOnly(new[] { "Geplant", "Gekauft", "Verbaut" });
    [JsonIgnore] public decimal Total => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    [JsonIgnore] public decimal Outstanding => Total - Payments.Total;
    [JsonIgnore] public string TotalLabel => Money(Total);
    [JsonIgnore] public string PaidLabel => Money(Payments.Total);
    [JsonIgnore] public string DetailLabel => string.Join(" · ", new[] { Floor, Room, Status }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public static string Money(decimal amount) => amount.ToString("C2", CultureInfo.GetCultureInfo("de-DE"));
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Material) || Material.Length > 200
            || Floor is null || Floor.Length > 100 || Room is null || Room.Length > 100
            || Quantity <= 0 || Quantity > 1000000 || decimal.Round(Quantity, 3) != Quantity
            || string.IsNullOrWhiteSpace(Unit) || Unit.Length > 30 || UnitPrice < 0 || UnitPrice > 1000000
            || decimal.Round(UnitPrice, 2) != UnitPrice || !Statuses.Contains(Status) || Payments is null)
            throw new InvalidDataException("Bitte prüfe Material, Status, Menge (bis 1.000.000, drei Nachkommastellen), Einheit und Einzelpreis (bis 1.000.000 €, zwei Nachkommastellen).");
        Payments.Validate();
        if (Payments.Total > Total) throw new InvalidDataException("Die Zahlungen dürfen die Positionskosten nicht übersteigen.");
        if (Status == "Geplant" && Payments.Total != 0) throw new InvalidDataException("Wähle für eine bereits bezahlte Position den Status Gekauft oder Verbaut.");
    }
}
