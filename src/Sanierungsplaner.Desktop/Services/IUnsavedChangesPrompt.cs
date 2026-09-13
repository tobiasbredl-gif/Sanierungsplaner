namespace Sanierungsplaner.Desktop.Services;

public enum UnsavedChangesChoice { Save, Discard, Cancel }
public interface IUnsavedChangesPrompt
{
    UnsavedChangesChoice Ask();
}
