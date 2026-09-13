using System.Windows;
using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.Views;

public partial class MatchWindow : Window
{
    public Guid? MatchId { get; private set; }
    public MatchWindow(CostItem incoming, IReadOnlyList<CostItem> candidates)
    {
        InitializeComponent();
        IncomingLabel.Text = $"Du hast „{incoming.Material}“ eingetragen. Einheit, Raum und Etage stimmen mit diesen Positionen überein. Wähle nur dann eine aus, wenn dasselbe Material gemeint ist.";
        Candidates.ItemsSource = candidates;
        Candidates.SelectedIndex = 0;
    }
    private void UseSelected(object sender, RoutedEventArgs args)
    {
        if (Candidates.SelectedItem is not CostItem selected) return;
        MatchId = selected.Id;
        DialogResult = true;
    }
    private void KeepNew(object sender, RoutedEventArgs args) => DialogResult = true;
}
