using System.Windows;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.ViewModels;

namespace Sanierungsplaner.Desktop.Views;

public partial class ReimbursementWindow : Window
{
    public Reimbursement? Result { get; private set; }
    public ReimbursementWindow(string recipient, decimal outstanding)
    {
        InitializeComponent();
        DataContext = new ReimbursementDraft(recipient, outstanding);
    }
    private void Apply(object sender, RoutedEventArgs args)
    {
        Result = ((ReimbursementDraft)DataContext).Build();
        if (Result is not null) DialogResult = true;
    }
}
