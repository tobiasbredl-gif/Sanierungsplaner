using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.ViewModels;

namespace Sanierungsplaner.Desktop.Views;

public partial class CostItemWindow : Window
{
    public CostItem? Result { get; private set; }
    public CostItemWindow(CostItem? existing)
    {
        InitializeComponent();
        var draft = new CostItemDraft(existing);
        DataContext = draft;
        Closing += (_, e) =>
        {
            if (Result is null && draft.IsDirty)
                e.Cancel = MessageBox.Show(this, "Änderungen an dieser Position verwerfen?", "Position schließen",
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes;
        };
    }
    private void Apply(object sender, RoutedEventArgs args)
    {
        Result = ((CostItemDraft)DataContext).Build();
        if (Result is not null) DialogResult = true;
    }
}
