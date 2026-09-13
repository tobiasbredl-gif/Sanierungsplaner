using System.IO;
using System.Text.Json.Serialization;

namespace Sanierungsplaner.Desktop.Models;

public sealed record SalesCredit(Guid Id, string Description, string Recipient, decimal Amount,
    DateOnly Date, DateTimeOffset RecordedAt, string Note, Guid? ReversesId = null)
{
    public static IReadOnlyList<string> Recipients { get; } = Array.AsReadOnly(new[] { "Lea", "Wolfgang", "Jennifer", "Tobias" });
    [JsonIgnore] public decimal EffectiveAmount => ReversesId is null ? Amount : -Amount;
    [JsonIgnore] public string AmountLabel => $"{(ReversesId is null ? "+" : "−")}{CostItem.Money(Amount)}";
    [JsonIgnore] public string Title => ReversesId is null ? Description : $"Storno: {Description}";
    [JsonIgnore] public string DetailLabel => $"{Date:dd.MM.yyyy} · Geldempfänger: {Recipient}";
    [JsonIgnore] public string RecordedLabel => $"Erfasst am {RecordedAt.ToLocalTime():dd.MM.yyyy HH:mm}";

    public static void ValidateLedger(IReadOnlyList<SalesCredit> ledger)
    {
        if (ledger.Count > 10000) throw new InvalidDataException("Maximal 10.000 Gutschrifteinträge pro Projekt.");
        var known = new Dictionary<Guid, SalesCredit>();
        var reversed = new HashSet<Guid>();
        foreach (var entry in ledger)
        {
            if (entry is null || entry.Id == Guid.Empty || string.IsNullOrWhiteSpace(entry.Description) || entry.Description.Length > 200
                || !Recipients.Contains(entry.Recipient) || entry.Amount <= 0 || entry.Amount > 1000000000m
                || decimal.Round(entry.Amount, 2) != entry.Amount || entry.Date == default || entry.RecordedAt == default
                || entry.Note is null || entry.Note.Length > 500 || !known.TryAdd(entry.Id, entry))
                throw new InvalidDataException("Ungültige Verkaufsgutschrift.");
            if (entry.ReversesId is Guid target && (!known.TryGetValue(target, out var original) || target == entry.Id
                || original.ReversesId is not null || original.Recipient != entry.Recipient || original.Amount != entry.Amount
                || original.Description != entry.Description || !reversed.Add(target)))
                throw new InvalidDataException("Ungültiger oder doppelter Gutschrift-Storno.");
        }
    }
}
