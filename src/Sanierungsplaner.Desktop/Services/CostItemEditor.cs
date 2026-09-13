using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Views;

namespace Sanierungsplaner.Desktop.Services;

public sealed class CostItemEditor : ICostItemEditor
{
    public MatchDecision ChooseSimilar(CostItem incoming, IReadOnlyList<CostItem> candidates)
    {
        var dialog = new MatchWindow(incoming, candidates) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? new MatchDecision(dialog.MatchId) : new MatchDecision(Cancelled: true);
    }
    public bool ConfirmRemoval(CostItem existing) => MessageBox.Show(Application.Current.MainWindow,
        $"Den Eintrag „{existing.Material}“ vom {existing.DateLabel} einschließlich seiner Zahlungen entfernen? Andere Einkäufe bleiben erhalten. Die Änderung wird erst mit dem Projekt gespeichert.",
        "Position entfernen", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    public CostItem? Edit(CostItem? existing, bool planned = false)
    {
        var dialog = new CostItemWindow(existing, planned) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}
