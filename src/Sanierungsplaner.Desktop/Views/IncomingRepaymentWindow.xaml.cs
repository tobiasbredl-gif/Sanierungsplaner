using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.ViewModels;

namespace Sanierungsplaner.Desktop.Views;

public partial class IncomingRepaymentWindow : Window
{
    public IncomingRepayment? Result { get; private set; }
    public IncomingRepaymentWindow()
    {
        InitializeComponent();
        DataContext = new IncomingRepaymentDraft();
    }
    private void Apply(object sender, RoutedEventArgs args)
    {
        Result = ((IncomingRepaymentDraft)DataContext).Build();
        if (Result is not null) DialogResult = true;
    }
}
