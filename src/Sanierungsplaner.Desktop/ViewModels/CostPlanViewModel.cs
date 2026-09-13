using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Sanierungsplaner.Desktop.Commands;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;

namespace Sanierungsplaner.Desktop.ViewModels;

public sealed class CostPlanViewModel : INotifyPropertyChanged
{
    private readonly ICostItemEditor _editor;
    private readonly Action _changed;
    private string _budget = "0,00";
    private decimal _originalBudget;
    private CostItem[] _originalItems = [];
    public CostPlanViewModel(ICostItemEditor editor, Action changed)
    {
        _editor = editor;
        _changed = changed;
        AddCommand = new RelayCommand(() => Edit(null));
        EditCommand = new RelayCommand(p => Edit((CostItem)p!), p => p is CostItem);
        RemoveCommand = new RelayCommand(p =>
        {
            if (_editor.ConfirmRemoval((CostItem)p!)) { Items.Remove((CostItem)p!); Refresh(); }
        }, p => p is CostItem);
    }
    public ObservableCollection<CostItem> Items { get; } = [];
    public string BudgetText { get => _budget; set { _budget = value; Refresh(); } }
    public bool IsDirty => BudgetText != Format(_originalBudget) || !Items.SequenceEqual(_originalItems);
    public decimal Total => Items.Sum(i => i.Total);
    public decimal Paid => Items.Sum(i => i.Payments.Total);
    public string TotalLabel => CostItem.Money(Total);
    public string PaidLabel => CostItem.Money(Paid);
    public string OutstandingLabel => CostItem.Money(Total - Paid);
    public string RemainingLabel => TryBudget(out var budget) ? CostItem.Money(budget - Paid) : "Budget prüfen";
    public string ForecastLabel => TryBudget(out var budget) ? CostItem.Money(budget - Total) : "Budget prüfen";
    public string BudgetError => TryBudget(out _) ? "" : "Budget bitte als Eurobetrag von 0 bis 1.000.000.000 mit maximal zwei Nachkommastellen eingeben.";
    public bool IsEmpty => Items.Count == 0;
    public IReadOnlyList<PersonTotal> People => new[]
    {
        new PersonTotal("Lea", Items.Sum(i => i.Payments.Lea)),
        new PersonTotal("Wolfgang", Items.Sum(i => i.Payments.Wolfgang)),
        new PersonTotal("Jennifer", Items.Sum(i => i.Payments.Jennifer)),
        new PersonTotal("Tobias", Items.Sum(i => i.Payments.Tobias))
    };
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool TryBudget(out decimal value) => decimal.TryParse(BudgetText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
        CultureInfo.GetCultureInfo("de-DE"), out value) && value >= 0 && value <= 1000000000m && decimal.Round(value, 2) == value;
    public void Reset(RenovationProject? project)
    {
        _originalBudget = project?.Budget ?? 0;
        _budget = Format(_originalBudget);
        _originalItems = project?.Items.ToArray() ?? [];
        Items.Clear();
        foreach (var item in _originalItems) Items.Add(item);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
    private void Edit(CostItem? existing)
    {
        var result = _editor.Edit(existing);
        if (result is null) return;
        result.Validate();
        if (existing is not null) Items[Items.IndexOf(existing)] = result;
        else Items.Add(result);
        Refresh();
    }
    private void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        _changed();
    }
    public static string Format(decimal amount) => amount.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"));
    public sealed record PersonTotal(string Person, decimal Amount)
    {
        public string AmountLabel => CostItem.Money(Amount);
    }
}
