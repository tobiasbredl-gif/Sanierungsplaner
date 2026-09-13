using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.ViewModels;

namespace Sanierungsplaner.Desktop.Views;

public partial class SalesCreditWindow : Window
{
    public SalesCredit? Result { get; private set; }
    public SalesCreditWindow()
    {
        InitializeComponent();
        DataContext = new SalesCreditDraft();
    }
    private void Apply(object sender, RoutedEventArgs args)
    {
        Result = ((SalesCreditDraft)DataContext).Build();
        if (Result is not null) DialogResult = true;
    }
}
