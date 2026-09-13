using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sanierungsplaner.Desktop.Commands;

namespace Sanierungsplaner.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private bool _showAbout;

    public MainWindowViewModel()
    {
        ShowHomeCommand = new RelayCommand(() => ShowAbout = false);
        ShowAboutCommand = new RelayCommand(() => ShowAbout = true);
    }

    public bool ShowAbout
    {
        get => _showAbout;
        private set
        {
            if (_showAbout == value) return;
            _showAbout = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageDescription));
        }
    }

    public string PageTitle => ShowAbout ? "Ein klarer Anfang." : "Raum für deine Pläne.";
    public string PageDescription => ShowAbout
        ? "Sanierungsplaner · Version 0.1.0\nEine native Windows-App mit C# und WPF."
        : "Willkommen bei Sanierungsplaner. Hier entsteht dein Arbeitsplatz für die Planung von Sanierungen.";

    public ICommand ShowHomeCommand { get; }
    public ICommand ShowAboutCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
