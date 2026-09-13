using System.IO;
using System.Text.Json.Serialization;

namespace Sanierungsplaner.Desktop.Models;

public sealed record Reimbursement(Guid Id, string Recipient, decimal Amount, DateOnly Date,
    DateTimeOffset RecordedAt, string Note, Guid? ReversesId = null)
{
    public static IReadOnlyList<string> Recipients { get; } = Array.AsReadOnly(new[] { "Lea", "Wolfgang", "Jennifer" });
    [JsonIgnore] public decimal EffectiveAmount => ReversesId is null ? Amount : -Amount;
    [JsonIgnore] public string Description => $"{Date:dd.MM.yyyy} · {(ReversesId is null ? "Tobias → " : "Storno: Tobias → ")}{Recipient} · {CostItem.Money(Amount)}";
    [JsonIgnore] public string RecordedLabel => $"Erfasst am {RecordedAt.ToLocalTime():dd.MM.yyyy HH:mm}";

    public static void ValidateLedger(IReadOnlyList<Reimbursement> ledger, IEnumerable<CostItem> items)
    {
        if (ledger.Count > 10000) throw new InvalidDataException("Maximal 10.000 Protokolleinträge pro Projekt.");
        var known = new Dictionary<Guid, Reimbursement>();
        var reversed = new HashSet<Guid>();
        foreach (var entry in ledger)
        {
            if (entry is null || entry.Id == Guid.Empty || !Recipients.Contains(entry.Recipient)
                || entry.Amount <= 0 || entry.Amount > 10000000000000000m || decimal.Round(entry.Amount, 2) != entry.Amount
                || entry.Date == default || entry.RecordedAt == default || entry.Note is null || entry.Note.Length > 500
                || !known.TryAdd(entry.Id, entry))
                throw new InvalidDataException("Ungültiger Rückzahlungseintrag.");
            if (entry.ReversesId is Guid target && (!known.TryGetValue(target, out var original) || target == entry.Id
                || original.ReversesId is not null || original.Recipient != entry.Recipient || original.Amount != entry.Amount
                || !reversed.Add(target)))
                throw new InvalidDataException("Ungültiger oder doppelter Stornoeintrag.");
        }
        var all = items.ToArray();
        foreach (var person in Recipients)
        {
            var paid = all.Sum(i => ForPerson(i.Payments, person));
            var returned = ledger.Where(r => r.Recipient == person).Sum(r => r.EffectiveAmount);
            if (returned < 0 || returned > paid)
                throw new InvalidDataException($"An {person} wurden bereits {CostItem.Money(returned)} zurückgezahlt. Die ursprünglichen Zahlungen dürfen nicht darunter liegen. Korrigiere gegebenenfalls zuerst die Rückzahlung mit einem Storno.");
        }
    }
    public static decimal ForPerson(Payments payments, string person) => person switch
    {
        "Lea" => payments.Lea, "Wolfgang" => payments.Wolfgang,
        "Jennifer" => payments.Jennifer, "Tobias" => payments.Tobias,
        _ => throw new ArgumentException("Unbekannte Person.", nameof(person))
    };
}
