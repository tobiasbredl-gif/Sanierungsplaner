using System.Windows;
using Sanierungsplaner.Desktop.ViewModels;
using Sanierungsplaner.Desktop.Services;

namespace Sanierungsplaner.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow() : this(new MainWindowViewModel(JsonProjectStore.CreateDefault(), new UnsavedChangesPrompt())) { }

    public MainWindow(MainWindowViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Closing += (_, e) => e.Cancel = !model.CanLeaveEditor();
    }
}
