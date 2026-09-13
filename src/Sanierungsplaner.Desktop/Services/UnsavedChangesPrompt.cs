using System.Windows;
namespace Sanierungsplaner.Desktop.Services;

public sealed class UnsavedChangesPrompt : IUnsavedChangesPrompt
{
    public UnsavedChangesChoice Ask()
    {
        const string message = "Du hast ungespeicherte Änderungen. Möchtest du sie speichern?\n\nJa: Speichern · Nein: Verwerfen · Abbrechen: Weiterbearbeiten";
        var owner = Application.Current?.MainWindow;
        var result = owner is null
            ? MessageBox.Show(message, "Änderungen speichern?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question)
            : MessageBox.Show(owner, message, "Änderungen speichern?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => UnsavedChangesChoice.Save,
            MessageBoxResult.No => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel
        };
    }
}
