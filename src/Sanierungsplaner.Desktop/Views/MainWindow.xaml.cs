using System.Windows;
using Sanierungsplaner.Desktop.ViewModels;

namespace Sanierungsplaner.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
