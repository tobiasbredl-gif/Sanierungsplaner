using System.IO;
using System.Text.Json.Serialization;

namespace Sanierungsplaner.Desktop.Models;

public sealed record IncomingRepayment(Guid Id, string Description, string Payer, decimal Amount,
    DateOnly Date, DateTimeOffset RecordedAt, string Note, Guid? ReversesId = null)
{
    public static IReadOnlyList<string> Payers { get; } = Array.AsReadOnly(new[] { "Lea", "Wolfgang", "Jennifer" });
    [JsonIgnore] public decimal EffectiveAmount => ReversesId is null ? Amount : -Amount;
    [JsonIgnore] public string AmountLabel => $"{(ReversesId is null ? "+" : "−")}{CostItem.Money(Amount)}";
    [JsonIgnore] public string Title => ReversesId is null ? Description : $"Storno: {Description}";
    [JsonIgnore] public string DetailLabel => $"{Date:dd.MM.yyyy} · Zahlende Person: {Payer}";
    [JsonIgnore] public string RecordedLabel => $"Erfasst am {RecordedAt.ToLocalTime():dd.MM.yyyy HH:mm}";

    public static void ValidateLedger(IReadOnlyList<IncomingRepayment> ledger)
    {
        if (ledger.Count > 10000) throw new InvalidDataException("Maximal 10.000 Rückzahlungseinträge pro Projekt.");
        var known = new Dictionary<Guid, IncomingRepayment>();
        var reversed = new HashSet<Guid>();
        foreach (var entry in ledger)
        {
            if (entry is null || entry.Id == Guid.Empty || string.IsNullOrWhiteSpace(entry.Description) || entry.Description.Length > 200
                || !Payers.Contains(entry.Payer) || entry.Amount <= 0 || entry.Amount > 1000000000m
                || decimal.Round(entry.Amount, 2) != entry.Amount || entry.Date == default || entry.RecordedAt == default
                || entry.Note is null || entry.Note.Length > 500 || !known.TryAdd(entry.Id, entry))
                throw new InvalidDataException("Ungültige Verkaufsgutschrift.");
            if (entry.ReversesId is Guid target && (!known.TryGetValue(target, out var original) || target == entry.Id
                || original.ReversesId is not null || original.Payer != entry.Payer || original.Amount != entry.Amount
                || original.Description != entry.Description || !reversed.Add(target)))
                throw new InvalidDataException("Ungültiger oder doppelter Rückzahlung-Storno.");
        }
    }
}
