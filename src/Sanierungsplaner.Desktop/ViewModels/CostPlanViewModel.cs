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
    private readonly ISalesCreditEditor _sales;
    private readonly Action _changed;
    private string _budget = "0,00";
    private decimal _originalBudget;
    private CostItem[] _originalItems = [];
    private Reimbursement[] _originalReimbursements = [];
    private SalesCredit[] _originalCredits = [];
    public CostPlanViewModel(ICostItemEditor editor, Action changed, IReimbursementEditor? repayments = null, ISalesCreditEditor? sales = null)
    {
        _editor = editor;
        _repayments = repayments ?? new ReimbursementEditor();
        _sales = sales ?? new SalesCreditEditor();
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
        AddCreditCommand = new RelayCommand(RecordCredit);
        ReverseCreditCommand = new RelayCommand(p => ReverseCredit((SalesCredit)p!),
            p => p is SalesCredit entry && entry.ReversesId is null && !Credits.Any(r => r.ReversesId == entry.Id));
    }
    public ObservableCollection<CostItem> Items { get; } = [];
    public ObservableCollection<Reimbursement> Reimbursements { get; } = [];
    public ObservableCollection<SalesCredit> Credits { get; } = [];
    public string Error { get; private set; } = "";
    public string Notice { get; private set; } = "";
    public string BudgetText { get => _budget; set { _budget = value; Refresh(); } }
    public bool IsDirty => BudgetText != Format(_originalBudget) || !Items.SequenceEqual(_originalItems) || !Reimbursements.SequenceEqual(_originalReimbursements) || !Credits.SequenceEqual(_originalCredits);
    public decimal Total => Items.Sum(i => i.Total);
    public decimal Paid => Items.Sum(i => i.Payments.Total);
    public decimal Returned => Reimbursements.Sum(r => r.EffectiveAmount);
    public decimal Income => Credits.Sum(c => c.EffectiveAmount);
    public decimal NetPaid => Paid - Income;
    public string IncomeLabel => $"+{CostItem.Money(Income)}";
    public string NetPaidLabel => CostItem.Money(NetPaid);
    public string CreditsHint => Credits.Count == 0 ? "Noch keine Gutschriften erfasst." : "Verkäufe zählen als positive Einnahmen. Originale und Stornos bleiben im Protokoll.";
    public IReadOnlyList<SalesCredit> CreditHistory => Credits.Reverse().ToArray();
    public string TotalLabel => CostItem.Money(Total);
    public string PaidLabel => CostItem.Money(Paid);
    public string OutstandingLabel => CostItem.Money(Total - Paid);
    public string RemainingLabel => TryBudget(out var budget) ? CostItem.Money(budget - NetPaid) : "Budget prüfen";
    public string ForecastLabel => TryBudget(out var budget) ? CostItem.Money(budget - Total + Income) : "Budget prüfen";
    public string BudgetError => TryBudget(out _) ? "" : "Budget bitte als Eurobetrag von 0 bis 1.000.000.000 mit maximal zwei Nachkommastellen eingeben.";
    public bool IsEmpty => Items.Count == 0;
    public string LedgerHint => Reimbursements.Count == 0 ? "Noch keine Rückzahlungen erfasst." : "Rückzahlungen und Stornos bleiben im Protokoll. Zum dauerhaften Sichern das Projekt speichern.";
    public IReadOnlyList<Reimbursement> History => Reimbursements.Reverse().ToArray();
    public IReadOnlyList<PurchaseGroup> Groups => Items.GroupBy(CostItemMatching.Key).Select(g => new PurchaseGroup(g.ToArray())).ToArray();
    public IReadOnlyList<PersonTotal> People => new[] { "Lea", "Wolfgang", "Jennifer", "Tobias" }.Select(person =>
    {
        var gross = Items.Sum(i => Reimbursement.ForPerson(i.Payments, person));
        var received = person == "Tobias" ? -Returned : Reimbursements.Where(r => r.Recipient == person).Sum(r => r.EffectiveAmount);
        var income = Credits.Where(c => c.Recipient == person).Sum(c => c.EffectiveAmount);
        return new PersonTotal(person, gross, received, income);
    }).ToArray();
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ReimburseCommand { get; }
    public RelayCommand ReverseCommand { get; }
    public RelayCommand AddCreditCommand { get; }
    public RelayCommand ReverseCreditCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool TryBudget(out decimal value) => decimal.TryParse(BudgetText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
        CultureInfo.GetCultureInfo("de-DE"), out value) && value >= 0 && value <= 1000000000m && decimal.Round(value, 2) == value;

    public void Reset(RenovationProject? project)
    {
        _originalBudget = project?.Budget ?? 0;
        _budget = Format(_originalBudget);
        _originalItems = project?.Items.ToArray() ?? [];
        _originalReimbursements = project?.Reimbursements.ToArray() ?? [];
        _originalCredits = project?.Credits.ToArray() ?? [];
        Items.Clear();
        foreach (var item in _originalItems) Items.Add(item);
        Reimbursements.Clear();
        foreach (var entry in _originalReimbursements) Reimbursements.Add(entry);
        Credits.Clear();
        foreach (var credit in _originalCredits) Credits.Add(credit);
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
        if (entry.Amount > outstanding)
        {
            Error = "Die Rückzahlung übersteigt den nach Gutschriften noch offenen Betrag.";
            Notify();
            return;
        }
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
    private void RecordCredit()
    {
        var credit = _sales.Record();
        if (credit is null) return;
        if (credit.ReversesId is not null || !CheckCredits(Credits.Append(credit))) return;
        Credits.Add(credit);
        Refresh();
        Notice = $"Gutschrift über +{CostItem.Money(credit.Amount)} erfasst. Bitte das Projekt speichern.";
        Notify();
    }
    private void ReverseCredit(SalesCredit entry)
    {
        if (!_sales.ConfirmReversal(entry)) return;
        var reversal = new SalesCredit(Guid.NewGuid(), entry.Description, entry.Recipient, entry.Amount,
            DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow, $"Storno der Gutschrift vom {entry.Date:dd.MM.yyyy}.", entry.Id);
        if (!CheckCredits(Credits.Append(reversal))) return;
        Credits.Add(reversal);
        Refresh();
    }
    private bool CheckCredits(IEnumerable<SalesCredit> credits)
    {
        try { SalesCredit.ValidateLedger(credits.ToArray()); return true; }
        catch (InvalidDataException error) { Error = error.Message; Notice = ""; Notify(); return false; }
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
        ReverseCreditCommand.Refresh();
    }
    public static string Format(decimal amount) => amount.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"));
    public sealed record PersonTotal(string Person, decimal Gross, decimal Received, decimal Income = 0)
    {
        public decimal Amount => Gross - Received - Income;
        public string AmountLabel => CostItem.Money(Amount);
        public string DetailLabel => Person == "Tobias"
            ? $"Eigene Einkäufe: {CostItem.Money(Gross)} · übernommen: {CostItem.Money(-Received)} · Einnahmen: +{CostItem.Money(Income)}"
            : $"Einkäufe: {CostItem.Money(Gross)} · zurückerhalten: {CostItem.Money(Received)} · Einnahmen: +{CostItem.Money(Income)}";
        public string AmountCaption => Amount < 0 ? "Überschuss" : Person == "Tobias" ? "Deine Nettoausgaben" : "Noch offen";
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
