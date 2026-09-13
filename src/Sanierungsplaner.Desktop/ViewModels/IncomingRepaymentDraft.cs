using System.ComponentModel;
using System.Globalization;
using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.ViewModels;

public sealed class IncomingRepaymentDraft : INotifyPropertyChanged
{
    public string Description { get; set; } = "Kostenanteil an Tobias";
    public string Payer { get; set; } = "Lea";
    public IReadOnlyList<string> Payers => IncomingRepayment.Payers;
    public string AmountText { get; set; } = "0,00";
    public DateTime? Date { get; set; } = DateTime.Today;
    public string Note { get; set; } = "";
    public string Error { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public IncomingRepayment? Build()
    {
        if (string.IsNullOrWhiteSpace(Description) || Description.Length > 200) Error = "Bitte eine Beschreibung mit maximal 200 Zeichen eingeben.";
        else if (!Payers.Contains(Payer)) Error = "Bitte auswählen, wer an Tobias zurückgezahlt hat.";
        else if (!decimal.TryParse(AmountText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.GetCultureInfo("de-DE"), out var amount) || amount <= 0 || amount > 1000000000m || decimal.Round(amount, 2) != amount)
            Error = "Bitte einen positiven Eurobetrag bis 1.000.000.000 mit maximal zwei Nachkommastellen eingeben (Dezimalkomma, keine Tausendertrennzeichen).";
        else if (Date is null || Date.Value.Date > DateTime.Today) Error = "Bitte das Einnahmedatum angeben; es darf nicht in der Zukunft liegen.";
        else if (Note.Length > 500) Error = "Die Notiz darf höchstens 500 Zeichen enthalten.";
        else return new IncomingRepayment(Guid.NewGuid(), Description.Trim(), Payer, amount, DateOnly.FromDateTime(Date.Value), DateTimeOffset.UtcNow, Note.Trim());
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
        return null;
    }
}
