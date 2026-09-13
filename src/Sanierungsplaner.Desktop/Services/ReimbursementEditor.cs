using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Views;

namespace Sanierungsplaner.Desktop.Services;

public sealed class ReimbursementEditor : IReimbursementEditor
{
    public Reimbursement? Record(string recipient, decimal outstanding)
    {
        var dialog = new ReimbursementWindow(recipient, outstanding) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
    public bool ConfirmReversal(Reimbursement entry) => MessageBox.Show(Application.Current.MainWindow,
        $"Die erfasste Rückzahlung von {CostItem.Money(entry.Amount)} an {entry.Recipient} stornieren? Der ursprüngliche Eintrag bleibt im Protokoll. Der Betrag wird wieder als offen geführt.",
        "Rückzahlung korrigieren", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
