using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Views;

namespace Sanierungsplaner.Desktop.Services;

public sealed class IncomingRepaymentEditor : IIncomingRepaymentEditor
{
    public IncomingRepayment? Record()
    {
        var dialog = new IncomingRepaymentWindow { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
    public bool ConfirmReversal(IncomingRepayment entry) => MessageBox.Show(Application.Current.MainWindow,
        $"Die Rückzahlung „{entry.Description}“ über {CostItem.Money(entry.Amount)} stornieren? Der Originaleintrag bleibt im Protokoll; die Gegenrechnung wird aufgehoben.",
        "Rückzahlung korrigieren", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
