using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Sanierungsplaner.Desktop.Commands;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;

namespace Sanierungsplaner.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IProjectStore _store;
    private readonly IUnsavedChangesPrompt _prompt;
    private RenovationProject? _original;
    private bool _showAbout, _isEditing, _loadFailed;
    private string _name = "", _address = "", _notes = "", _error = "", _status = "";

    public MainWindowViewModel(IProjectStore store, IUnsavedChangesPrompt prompt, ICostItemEditor? costEditor = null, IReimbursementEditor? repayments = null, ISalesCreditEditor? sales = null, IIncomingRepaymentEditor? incoming = null)
    {
        _store = store;
        _prompt = prompt;
        CostPlan = new CostPlanViewModel(costEditor ?? new CostItemEditor(), () => DraftChanged(nameof(CostPlan)), repayments, sales, incoming);
        ShowHomeCommand = new RelayCommand(() => { _showAbout = false; NotifyView(); });
        ShowAboutCommand = new RelayCommand(() => { _showAbout = true; NotifyView(); });
        NewProjectCommand = new RelayCommand(() => { if (CanLeaveEditor()) Edit(null); }, () => !_loadFailed);
        OpenProjectCommand = new RelayCommand(p => { if (CanLeaveEditor()) Edit((RenovationProject)p!); }, p => !_loadFailed && p is RenovationProject);
        SaveProjectCommand = new RelayCommand(() => Save(), () => IsEditing && !_loadFailed && IsDirty);
        BackCommand = new RelayCommand(() => { if (CanLeaveEditor()) { _isEditing = false; Error = ""; NotifyView(); } });
        ReloadCommand = new RelayCommand(() => { if (CanLeaveEditor()) { _isEditing = false; Load(); } });
        Load();
    }

    public ObservableCollection<RenovationProject> Projects { get; } = [];
    public CostPlanViewModel CostPlan { get; }
    private int _selectedProjectTab;
    public int SelectedProjectTab { get => _selectedProjectTab; set { _selectedProjectTab = value; OnPropertyChanged(); } }
    public string StoragePath => _store.FolderPath;
    public bool ShowAbout => _showAbout;
    public bool IsEditing => _isEditing;
    public bool ShowProjects => !ShowAbout && !IsEditing;
    public bool ShowEditor => !ShowAbout && IsEditing;
    public bool IsEmpty => Projects.Count == 0 && !_loadFailed;
    public string ProjectCount => Projects.Count == 1 ? "1 gespeichertes Projekt" : $"{Projects.Count} gespeicherte Projekte";
    public string PageTitle => ShowAbout ? "Deine Pläne. Lokal gespeichert." : IsEditing ? (_original is null ? "Ein neues Projekt." : "Dein Projekt im Detail.") : "Raum für deine Pläne.";
    public string PageDescription => ShowAbout ? "Sanierungsplaner · Version 0.10.0"
        : IsEditing ? "Erfasse die Grundlagen für deine Sanierung. Du kannst alle Angaben später ändern."
        : "Alle Sanierungsvorhaben an einem Ort. Lege ein Projekt an oder arbeite an einem bestehenden weiter.";
    public string Name { get => _name; set { _name = value; DraftChanged(); } }
    public string Address { get => _address; set { _address = value; DraftChanged(); } }
    public string Notes { get => _notes; set { _notes = value; DraftChanged(); } }
    public bool IsDirty => IsEditing && (CostPlan.IsDirty || (_original is null
        ? Name.Length > 0 || Address.Length > 0 || Notes.Length > 0
        : Name != _original.Name || Address != _original.Address || Notes != _original.Notes));
    public string DraftStatus => IsDirty ? "Ungespeicherte Änderungen" : _original is null ? "Noch nicht gespeichert" : _original.UpdatedLabel;
    public string Error { get => _error; private set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public RelayCommand ShowHomeCommand { get; }
    public RelayCommand ShowAboutCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand OpenProjectCommand { get; }
    public RelayCommand SaveProjectCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand ReloadCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CanLeaveEditor()
    {
        if (!IsDirty) return true;
        return _prompt.Ask() switch
        {
            UnsavedChangesChoice.Save => Save(),
            UnsavedChangesChoice.Discard => true,
            _ => false
        };
    }

    private void Load()
    {
        Error = "";
        try
        {
            var projects = _store.Load();
            Projects.Clear();
            foreach (var project in projects) Projects.Add(project);
            _loadFailed = false;
            Status = "Projekte werden auf diesem Computer gespeichert.";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _loadFailed = true;
            Error = $"Projekte konnten nicht geladen werden. Deine Dateien bleiben erhalten. {error.Message}";
            Status = "Prüfe den Speicherordner unter „Über die App“ und lade anschließend erneut.";
        }
        NotifyView();
    }

    private void Edit(RenovationProject? project)
    {
        SelectedProjectTab = project is null ? 0 : 1;
        _original = project;
        _name = project?.Name ?? "";
        _address = project?.Address ?? "";
        _notes = project?.Notes ?? "";
        CostPlan.Reset(project);
        _isEditing = true;
        _showAbout = false;
        Error = "";
        Status = "";
        NotifyView();
    }

    private bool Save()
    {
        if (_loadFailed || !IsEditing) return false;
        if (string.IsNullOrWhiteSpace(Name))
        {
            _showAbout = false;
            Error = "Bitte gib einen Projektnamen ein.";
            NotifyView();
            return false;
        }
        var now = DateTimeOffset.UtcNow;
        if (!CostPlan.TryBudget(out var budget))
        {
            Error = CostPlan.BudgetError;
            return false;
        }
        var project = new RenovationProject(_original?.Id ?? Guid.NewGuid(), Guid.NewGuid(),
            Name.Trim(), Address.Trim(), Notes.Trim(), _original?.CreatedAt ?? now, now)
        { Budget = budget, Items = CostPlan.Items.ToArray(), Reimbursements = CostPlan.Reimbursements.ToArray(), Credits = CostPlan.Credits.ToArray(), IncomingRepayments = CostPlan.IncomingRepayments.ToArray() };
        try
        {
            _store.Save(project, _original?.Revision);
            var previous = Projects.FirstOrDefault(p => p.Id == project.Id);
            if (previous is not null) Projects.Remove(previous);
            Projects.Insert(0, project);
            var selectedTab = SelectedProjectTab;
            Edit(project);
            SelectedProjectTab = selectedTab;
            Status = "Projekt erfolgreich gespeichert.";
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Error = $"Speichern fehlgeschlagen. Deine Eingaben bleiben im Formular erhalten. {error.Message}";
            return false;
        }
    }

    private void DraftChanged([CallerMemberName] string? property = null)
    {
        Error = "";
        Status = "";
        OnPropertyChanged(property);
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DraftStatus));
        SaveProjectCommand.Refresh();
    }
    private void NotifyView()
    {
        OnPropertyChanged(string.Empty);
        NewProjectCommand.Refresh();
        OpenProjectCommand.Refresh();
        SaveProjectCommand.Refresh();
    }
    private void OnPropertyChanged([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
