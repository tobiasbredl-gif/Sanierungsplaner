using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Views;

namespace Sanierungsplaner.Desktop.Services;

public sealed class SalesCreditEditor : ISalesCreditEditor
{
    public SalesCredit? Record()
    {
        var dialog = new SalesCreditWindow { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
    public bool ConfirmReversal(SalesCredit entry) => MessageBox.Show(Application.Current.MainWindow,
        $"Die Gutschrift „{entry.Description}“ über {CostItem.Money(entry.Amount)} stornieren? Der Originaleintrag bleibt im Protokoll; die Gegenrechnung wird aufgehoben.",
        "Gutschrift korrigieren", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
