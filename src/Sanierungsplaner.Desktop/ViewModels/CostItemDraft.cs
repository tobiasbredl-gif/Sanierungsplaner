using System.ComponentModel;
using System.Globalization;
using System.IO;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Commands;

namespace Sanierungsplaner.Desktop.ViewModels;

public sealed class CostItemDraft : INotifyPropertyChanged
{
    private readonly Guid _id;
    private readonly Dictionary<string, string> _values;
    private readonly Dictionary<string, string> _initial;
    private string _error = "";
    private readonly DateTime? _initialDate;
    private readonly DateTimeOffset? _recordedAt;
    private readonly bool _initialVat;
    private bool _addVat;
    public bool AddVat
    {
        get => _addVat;
        set { _addVat = value; _error = ""; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); }
    }
    public DateTime? Date { get; set; }
    public CostItemDraft(CostItem? item)
    {
        _id = item?.Id ?? Guid.NewGuid();
        _recordedAt = item is null ? DateTimeOffset.UtcNow : item.RecordedAt;
        Date = item is null ? DateTime.Today : item.Date?.ToDateTime(TimeOnly.MinValue);
        _initialDate = Date; _initialVat = _addVat = item?.AddVat ?? false;
        _values = new()
        {
            ["Material"] = item?.Material ?? "", ["Floor"] = item?.Floor ?? "", ["Room"] = item?.Room ?? "",
            ["Quantity"] = (item?.Quantity ?? 1).ToString("0.###", CultureInfo.GetCultureInfo("de-DE")),
            ["Unit"] = item?.Unit ?? "Stück", ["UnitPrice"] = CostPlanViewModel.Format(item?.UnitPrice ?? 0),
            ["Status"] = item?.Status ?? "Gekauft", ["Lea"] = CostPlanViewModel.Format(item?.Payments.Lea ?? 0),
            ["Wolfgang"] = CostPlanViewModel.Format(item?.Payments.Wolfgang ?? 0),
            ["Jennifer"] = CostPlanViewModel.Format(item?.Payments.Jennifer ?? 0),
            ["Tobias"] = CostPlanViewModel.Format(item?.Payments.Tobias ?? 0)
        };
        _initial = new(_values);
        FillAmountCommand = new RelayCommand(p => FillAmount((string)p!), p => p is string person && new[] { "Lea", "Wolfgang", "Jennifer", "Tobias" }.Contains(person));
    }
    public RelayCommand FillAmountCommand { get; }
    private void FillAmount(string person)
    {
        try
        {
            var quantity = Number("Quantity");
            var price = Number("UnitPrice");
            if (quantity <= 0 || quantity > 1000000 || decimal.Round(quantity, 3) != quantity
                || price < 0 || price > 1000000 || decimal.Round(price, 2) != price)
                throw new InvalidDataException("Bitte zuerst eine gültige Menge und einen Einzelpreis eingeben.");
            var total = CostItem.CalculateTotal(quantity, price, AddVat);
            decimal others = 0;
            foreach (var other in new[] { "Lea", "Wolfgang", "Jennifer", "Tobias" }.Where(p => p != person))
            {
                var amount = Number(other);
                if (amount < 0 || amount > 1000000000m || decimal.Round(amount, 2) != amount)
                    throw new InvalidDataException("Bitte zuerst die Zahlungen der anderen Personen prüfen.");
                others += amount;
            }
            if (others > total) throw new InvalidDataException("Die Zahlungen der anderen Personen übersteigen bereits die Positionskosten.");
            if (total - others > 1000000000m) throw new InvalidDataException("Der Betrag für eine Person darf höchstens 1.000.000.000 € betragen.");
            _values[person] = CostPlanViewModel.Format(total - others);
            if (total > 0 && _values["Status"] == "Geplant") _values["Status"] = "Gekauft";
            _error = "";
        }
        catch (InvalidDataException error) { _error = error.Message; }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
    public string this[string key]
    {
        get => _values[key];
        set
        {
            _values[key] = value;
            _error = "";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }
    public bool IsDirty => AddVat != _initialVat || Date != _initialDate || _values.Any(pair => _initial[pair.Key] != pair.Value);
    public IReadOnlyList<string> Statuses => CostItem.Statuses;
    public string Error => _error;
    public string TotalLabel
    {
        get
        {
            if (!TryNumber("Quantity", out var quantity) || !TryNumber("UnitPrice", out var price)
                || quantity > 1000000m || price > 1000000m) return "Menge und Preis prüfen";
            return CostItem.Money(CostItem.CalculateTotal(quantity, price, AddVat));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public CostItem? Build()
    {
        try
        {
            if (Date is null && _recordedAt is not null) throw new InvalidDataException("Bitte ein Datum angeben.");
            if (Date?.Date > DateTime.Today) throw new InvalidDataException("Das Datum darf nicht in der Zukunft liegen.");
            var item = new CostItem(_id, this["Material"].Trim(), this["Floor"].Trim(), this["Room"].Trim(),
                Number("Quantity"), this["Unit"].Trim(), Number("UnitPrice"), this["Status"],
                new Payments(Number("Lea"), Number("Wolfgang"), Number("Jennifer"), Number("Tobias")))
            { AddVat = AddVat, Date = Date is null ? null : DateOnly.FromDateTime(Date.Value), RecordedAt = _recordedAt };
            item.Validate();
            return item;
        }
        catch (InvalidDataException error)
        {
            _error = error.Message;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
            return null;
        }
    }
    private decimal Number(string key) => TryNumber(key, out var value) ? value
        : throw new InvalidDataException("Bitte Zahlen ohne Tausendertrennzeichen eingeben; als Dezimaltrennzeichen ein Komma verwenden, z. B. 12,50.");
    private bool TryNumber(string key, out decimal value) => decimal.TryParse(this[key],
        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
        CultureInfo.GetCultureInfo("de-DE"), out value);
}
