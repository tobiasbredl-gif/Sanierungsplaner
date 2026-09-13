using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Views;

namespace Sanierungsplaner.Desktop.Services;

public sealed class CostItemEditor : ICostItemEditor
{
    public bool ConfirmRemoval(CostItem existing) => MessageBox.Show(Application.Current.MainWindow,
        $"Die Position „{existing.Material}“ einschließlich ihrer Zahlungen entfernen? Die Änderung wird erst mit dem Projekt gespeichert.",
        "Position entfernen", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    public CostItem? Edit(CostItem? existing)
    {
        var dialog = new CostItemWindow(existing) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}
