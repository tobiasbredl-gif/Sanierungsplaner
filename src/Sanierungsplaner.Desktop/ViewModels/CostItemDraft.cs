using System.ComponentModel;
using System.Globalization;
using System.IO;
using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.ViewModels;

public sealed class CostItemDraft : INotifyPropertyChanged
{
    private readonly Guid _id;
    private readonly Dictionary<string, string> _values;
    private readonly Dictionary<string, string> _initial;
    private string _error = "";
    public CostItemDraft(CostItem? item)
    {
        _id = item?.Id ?? Guid.NewGuid();
        _values = new()
        {
            ["Material"] = item?.Material ?? "", ["Floor"] = item?.Floor ?? "", ["Room"] = item?.Room ?? "",
            ["Quantity"] = (item?.Quantity ?? 1).ToString("0.###", CultureInfo.GetCultureInfo("de-DE")),
            ["Unit"] = item?.Unit ?? "Stück", ["UnitPrice"] = CostPlanViewModel.Format(item?.UnitPrice ?? 0),
            ["Status"] = item?.Status ?? "Geplant", ["Lea"] = CostPlanViewModel.Format(item?.Payments.Lea ?? 0),
            ["Wolfgang"] = CostPlanViewModel.Format(item?.Payments.Wolfgang ?? 0),
            ["Jennifer"] = CostPlanViewModel.Format(item?.Payments.Jennifer ?? 0),
            ["Tobias"] = CostPlanViewModel.Format(item?.Payments.Tobias ?? 0)
        };
        _initial = new(_values);
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
    public bool IsDirty => _values.Any(pair => _initial[pair.Key] != pair.Value);
    public IReadOnlyList<string> Statuses => CostItem.Statuses;
    public string Error => _error;
    public string TotalLabel
    {
        get
        {
            if (!TryNumber("Quantity", out var quantity) || !TryNumber("UnitPrice", out var price)
                || quantity > 1000000m || price > 1000000m) return "Menge und Preis prüfen";
            return CostItem.Money(decimal.Round(quantity * price, 2, MidpointRounding.AwayFromZero));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public CostItem? Build()
    {
        try
        {
            var item = new CostItem(_id, this["Material"].Trim(), this["Floor"].Trim(), this["Room"].Trim(),
                Number("Quantity"), this["Unit"].Trim(), Number("UnitPrice"), this["Status"],
                new Payments(Number("Lea"), Number("Wolfgang"), Number("Jennifer"), Number("Tobias")));
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
