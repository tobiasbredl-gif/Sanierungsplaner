using System.ComponentModel;
using System.Globalization;
using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.ViewModels;

public sealed class ReimbursementDraft(string recipient, decimal outstanding) : INotifyPropertyChanged
{
    public string Recipient { get; } = recipient;
    public decimal Outstanding { get; } = outstanding;
    public string Heading => $"Rückzahlung an {Recipient}";
    public string BalanceLabel => $"Noch offen: {CostItem.Money(Outstanding)}";
    public string AmountText { get; set; } = CostPlanViewModel.Format(outstanding);
    public DateTime? Date { get; set; } = DateTime.Today;
    public string Note { get; set; } = "";
    public string Error { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public Reimbursement? Build()
    {
        if (!decimal.TryParse(AmountText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.GetCultureInfo("de-DE"), out var amount) || amount <= 0 || amount > Outstanding || decimal.Round(amount, 2) != amount)
            Error = $"Bitte einen Betrag größer als 0 bis {CostItem.Money(Outstanding)} mit maximal zwei Nachkommastellen eingeben (Dezimalkomma, keine Tausendertrennzeichen).";
        else if (Date is null || Date.Value.Date > DateTime.Today)
            Error = "Bitte das Datum der Rückzahlung angeben; es darf nicht in der Zukunft liegen.";
        else if (Note.Length > 500) Error = "Die Notiz darf höchstens 500 Zeichen enthalten.";
        else return new Reimbursement(Guid.NewGuid(), Recipient, amount, DateOnly.FromDateTime(Date.Value), DateTimeOffset.UtcNow, Note.Trim());
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
        return null;
    }
}
