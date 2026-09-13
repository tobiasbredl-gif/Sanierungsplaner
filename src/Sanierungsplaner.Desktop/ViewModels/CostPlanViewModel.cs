using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Sanierungsplaner.Desktop.Commands;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;

namespace Sanierungsplaner.Desktop.ViewModels;

public sealed class CostPlanViewModel : INotifyPropertyChanged
{
    private readonly ICostItemEditor _editor;
    private readonly IReimbursementEditor _repayments;
    private readonly Action _changed;
    private string _budget = "0,00";
    private decimal _originalBudget;
    private CostItem[] _originalItems = [];
    private Reimbursement[] _originalReimbursements = [];
    public CostPlanViewModel(ICostItemEditor editor, Action changed, IReimbursementEditor? repayments = null)
    {
        _editor = editor;
        _repayments = repayments ?? new ReimbursementEditor();
        _changed = changed;
        AddCommand = new RelayCommand(() => Edit(null));
        EditCommand = new RelayCommand(p => Edit((CostItem)p!), p => p is CostItem);
        RemoveCommand = new RelayCommand(p =>
        {
            var item = (CostItem)p!;
            if (_editor.ConfirmRemoval(item) && CheckLedger(Items.Where(i => i.Id != item.Id), Reimbursements))
            { Items.Remove(item); Refresh(); }
        }, p => p is CostItem);
        ReimburseCommand = new RelayCommand(p => RecordRepayment(((PersonTotal)p!).Person),
            p => p is PersonTotal person && person.Person != "Tobias" && People.First(r => r.Person == person.Person).Amount > 0);
        ReverseCommand = new RelayCommand(p => Reverse((Reimbursement)p!),
            p => p is Reimbursement entry && entry.ReversesId is null && !Reimbursements.Any(r => r.ReversesId == entry.Id));
    }
    public ObservableCollection<CostItem> Items { get; } = [];
    public ObservableCollection<Reimbursement> Reimbursements { get; } = [];
    public string Error { get; private set; } = "";
    public string Notice { get; private set; } = "";
    public string BudgetText { get => _budget; set { _budget = value; Refresh(); } }
    public bool IsDirty => BudgetText != Format(_originalBudget) || !Items.SequenceEqual(_originalItems) || !Reimbursements.SequenceEqual(_originalReimbursements);
    public decimal Total => Items.Sum(i => i.Total);
    public decimal Paid => Items.Sum(i => i.Payments.Total);
    public decimal Returned => Reimbursements.Sum(r => r.EffectiveAmount);
    public string TotalLabel => CostItem.Money(Total);
    public string PaidLabel => CostItem.Money(Paid);
    public string OutstandingLabel => CostItem.Money(Total - Paid);
    public string RemainingLabel => TryBudget(out var budget) ? CostItem.Money(budget - Paid) : "Budget prüfen";
    public string ForecastLabel => TryBudget(out var budget) ? CostItem.Money(budget - Total) : "Budget prüfen";
    public string BudgetError => TryBudget(out _) ? "" : "Budget bitte als Eurobetrag von 0 bis 1.000.000.000 mit maximal zwei Nachkommastellen eingeben.";
    public bool IsEmpty => Items.Count == 0;
    public string LedgerHint => Reimbursements.Count == 0 ? "Noch keine Rückzahlungen erfasst." : "Rückzahlungen und Stornos bleiben im Protokoll. Zum dauerhaften Sichern das Projekt speichern.";
    public IReadOnlyList<Reimbursement> History => Reimbursements.Reverse().ToArray();
    public IReadOnlyList<PurchaseGroup> Groups => Items.GroupBy(CostItemMatching.Key).Select(g => new PurchaseGroup(g.ToArray())).ToArray();
    public IReadOnlyList<PersonTotal> People => new[] { "Lea", "Wolfgang", "Jennifer", "Tobias" }.Select(person =>
    {
        var gross = Items.Sum(i => Reimbursement.ForPerson(i.Payments, person));
        var received = person == "Tobias" ? -Returned : Reimbursements.Where(r => r.Recipient == person).Sum(r => r.EffectiveAmount);
        return new PersonTotal(person, gross, received);
    }).ToArray();
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ReimburseCommand { get; }
    public RelayCommand ReverseCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool TryBudget(out decimal value) => decimal.TryParse(BudgetText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
        CultureInfo.GetCultureInfo("de-DE"), out value) && value >= 0 && value <= 1000000000m && decimal.Round(value, 2) == value;

    public void Reset(RenovationProject? project)
    {
        _originalBudget = project?.Budget ?? 0;
        _budget = Format(_originalBudget);
        _originalItems = project?.Items.ToArray() ?? [];
        _originalReimbursements = project?.Reimbursements.ToArray() ?? [];
        Items.Clear();
        foreach (var item in _originalItems) Items.Add(item);
        Reimbursements.Clear();
        foreach (var entry in _originalReimbursements) Reimbursements.Add(entry);
        Error = "";
        Notice = "";
        Notify();
    }
    private void Edit(CostItem? existing)
    {
        var result = _editor.Edit(existing);
        if (result is null) return;
        result.Validate();
        if (existing is null)
        {
            var exact = Items.FirstOrDefault(i => CostItemMatching.Key(i) == CostItemMatching.Key(result));
            if (exact is null)
            {
                var candidates = CostItemMatching.Similar(result, Items);
                if (candidates.Count > 0)
                {
                    var choice = _editor.ChooseSimilar(result, candidates);
                    if (choice.Cancelled) return;
                    if (choice.MatchId is Guid matchId) exact = candidates.First(i => i.Id == matchId);
                }
            }
            if (exact is not null) result = result with { Material = exact.Material, Unit = exact.Unit, Floor = exact.Floor, Room = exact.Room };
            if (!CheckLedger(Items.Append(result), Reimbursements)) return;
            Items.Add(result);
            Refresh();
            Notice = exact is null ? "Neue Position aufgenommen. Bitte das Projekt speichern." : $"Einkauf zu „{exact.Material}“ hinzugerechnet. Der einzelne Eintrag bleibt im Verlauf erhalten. Bitte das Projekt speichern.";
            Notify();
        }
        else if (CheckLedger(Items.Select(i => i.Id == existing.Id ? result : i), Reimbursements))
        {
            Items[Items.IndexOf(existing)] = result;
            Refresh();
        }
    }
    private void RecordRepayment(string recipient)
    {
        var outstanding = People.First(p => p.Person == recipient).Amount;
        var entry = _repayments.Record(recipient, outstanding);
        if (entry is null) return;
        if (entry.Recipient != recipient || entry.ReversesId is not null || !CheckLedger(Items, Reimbursements.Append(entry))) return;
        Reimbursements.Add(entry);
        Refresh();
        Notice = $"{CostItem.Money(entry.Amount)} von {recipient} auf Tobias übertragen. Bitte das Projekt speichern.";
        Notify();
    }
    private void Reverse(Reimbursement entry)
    {
        if (!_repayments.ConfirmReversal(entry)) return;
        var reversal = new Reimbursement(Guid.NewGuid(), entry.Recipient, entry.Amount, DateOnly.FromDateTime(DateTime.Today),
            DateTimeOffset.UtcNow, $"Storno der Rückzahlung vom {entry.Date:dd.MM.yyyy}.", entry.Id);
        if (!CheckLedger(Items, Reimbursements.Append(reversal))) return;
        Reimbursements.Add(reversal);
        Refresh();
    }
    private bool CheckLedger(IEnumerable<CostItem> items, IEnumerable<Reimbursement> ledger)
    {
        try { Reimbursement.ValidateLedger(ledger.ToArray(), items); return true; }
        catch (InvalidDataException error) { Error = error.Message; Notice = ""; Notify(); return false; }
    }
    private void Refresh()
    {
        Error = "";
        Notice = "";
        Notify();
        _changed();
    }
    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        ReimburseCommand.Refresh();
        ReverseCommand.Refresh();
    }
    public static string Format(decimal amount) => amount.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"));
    public sealed record PersonTotal(string Person, decimal Gross, decimal Received)
    {
        public decimal Amount => Gross - Received;
        public string AmountLabel => CostItem.Money(Amount);
        public string DetailLabel => Person == "Tobias"
            ? $"Eigene Einkäufe: {CostItem.Money(Gross)} · übernommen: {CostItem.Money(-Received)}"
            : $"Einkäufe: {CostItem.Money(Gross)} · zurückerhalten: {CostItem.Money(Received)}";
        public string AmountCaption => Person == "Tobias" ? "Deine Ausgaben" : "Noch offen";
    }
    public sealed class PurchaseGroup(CostItem[] entries)
    {
        public IReadOnlyList<CostItem> Entries { get; } = entries.OrderByDescending(i => i.Date).ThenByDescending(i => i.RecordedAt).ToArray();
        public string Material => entries[0].Material;
        public decimal Quantity => entries.Sum(i => i.Quantity);
        public decimal Total => entries.Sum(i => i.Total);
        public decimal Paid => entries.Sum(i => i.Payments.Total);
        public string TotalLabel => CostItem.Money(Total);
        public string PaidLabel => CostItem.Money(Paid);
        public string QuantityLabel => $"{Quantity.ToString("0.###", CultureInfo.GetCultureInfo("de-DE"))} {entries[0].Unit}";
        public string DetailLabel => string.Join(" · ", new[] { entries[0].Floor, entries[0].Room, $"{entries.Length} Einträge", $"Zuletzt: {Entries[0].DateLabel}" }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
