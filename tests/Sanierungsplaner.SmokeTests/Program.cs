using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sanierungsplaner.Desktop;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.ViewModels;
using Sanierungsplaner.Desktop.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "Sanierungsplaner-Tests", Guid.NewGuid().ToString("N"));
        try
        {
            TestPersistence(Path.Combine(testRoot, "storage"));
            TestEditing(Path.Combine(testRoot, "editing"));
            TestWindow(Path.Combine(testRoot, "window"), args.FirstOrDefault());
            Console.WriteLine("PASS: Speicherung, Wiederöffnen, Validierung, Konflikte, Fehlerfälle, Entwurfsschutz und WPF-Oberfläche.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    private static void TestPersistence(string folder)
    {
        var store = new JsonProjectStore(folder);
        Check(store.Load().Count == 0, "Leerer Speicher");
        var now = DateTimeOffset.UtcNow;
        var first = new RenovationProject(Guid.NewGuid(), Guid.NewGuid(), "Haus am See", "Seestraße 12, 80331 München", "Dach prüfen.\nFenster erneuern.", now, now);
        store.Save(first, null);
        Check(new JsonProjectStore(folder).Load().Single() == first, "JSON-Roundtrip einschließlich Umlauten und Zeilenumbrüchen");
        var updated = first with { Revision = Guid.NewGuid(), Name = "Haus am See · Umbau", UpdatedAt = now.AddMinutes(1) };
        store.Save(updated, first.Revision);
        Expect<IOException>(() => store.Save(first, first.Revision));
        Check(store.Load().Single() == updated, "Konflikt darf vorhandene Daten nicht ersetzen");
        using (var guard = new FileStream(Path.Combine(folder, ".write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Expect<IOException>(() => store.Save(updated with { Revision = Guid.NewGuid() }, updated.Revision));
        Check(store.Load().Single() == updated, "Dateisperre erhält bestehende Daten");
        Expect<InvalidDataException>(() => store.Save(first with { Name = " " }, null));
        var file = Path.Combine(folder, $"{first.Id:D}.json");
        var valid = File.ReadAllText(file);
        File.WriteAllText(file, "{kaputt");
        Expect<InvalidDataException>(() => store.Load());
        Expect<InvalidDataException>(() => store.Save(updated, updated.Revision));
        Check(File.ReadAllText(file) == "{kaputt", "Beschädigte Datei bleibt erhalten");
        File.WriteAllText(file, valid.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 99"));
        Expect<InvalidDataException>(() => store.Load());
        File.WriteAllText(file, valid);
        Check(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "Keine temporären Dateien nach erfolgreichem Speichern");
    }

    private static void TestEditing(string folder)
    {
        var store = new JsonProjectStore(folder);
        var prompt = new TestPrompt();
        var model = new MainWindowViewModel(store, prompt);
        model.NewProjectCommand.Execute(null);
        model.Name = " ";
        model.SaveProjectCommand.Execute(null);
        Check(model.HasError && store.Load().Count == 0, "Pflichtfeldvalidierung");
        model.Name = "  Altbau  ";
        model.Address = "  Hauptstraße 12  ";
        model.Notes = "Bestand aufnehmen";
        model.SaveProjectCommand.Execute(null);
        Check(!model.IsDirty && model.Name == "Altbau", "Speichern und Normalisierung");
        var id = model.Projects.Single().Id;
        model.Name = "Geänderter Altbau";
        prompt.Choice = UnsavedChangesChoice.Cancel;
        model.BackCommand.Execute(null);
        Check(model.IsEditing && model.IsDirty, "Abbrechen behält Entwurf");
        model.ShowAboutCommand.Execute(null);
        model.ShowHomeCommand.Execute(null);
        Check(model.ShowEditor && model.Name == "Geänderter Altbau", "Navigation behält Entwurf");
        prompt.Choice = UnsavedChangesChoice.Save;
        model.BackCommand.Execute(null);
        Check(!model.IsEditing && store.Load().Single().Name == "Geänderter Altbau", "Speichern beim Verlassen");
        model.OpenProjectCommand.Execute(model.Projects.Single());
        model.Name = "Verwerfen";
        prompt.Choice = UnsavedChangesChoice.Discard;
        model.BackCommand.Execute(null);
        model.OpenProjectCommand.Execute(model.Projects.Single());
        Check(model.Name == "Geänderter Altbau", "Verwerfen verändert gespeichertes Projekt nicht");
        var reloaded = new MainWindowViewModel(new JsonProjectStore(folder), prompt);
        Check(reloaded.Projects.Single().Id == id, "Neuer App-Zustand lädt bestehende Projekte");
        var failure = new MainWindowViewModel(new FailingStore(), prompt);
        failure.NewProjectCommand.Execute(null);
        failure.Name = "Entwurf bleibt";
        failure.SaveProjectCommand.Execute(null);
        Check(failure.HasError && failure.IsDirty && failure.Name == "Entwurf bleibt" && failure.Projects.Count == 0, "Schreibfehler erhält Entwurf");
        prompt.Choice = UnsavedChangesChoice.Save;
        Check(!failure.CanLeaveEditor(), "Fehlgeschlagenes Speichern verhindert Schließen");
        File.WriteAllText(Path.Combine(folder, "broken.json"), "{}");
        var broken = new MainWindowViewModel(store, prompt);
        Check(broken.HasError && !broken.NewProjectCommand.CanExecute(null), "Lesefehler wird angezeigt und sperrt Bearbeitung");
        File.Delete(Path.Combine(folder, "broken.json"));
        broken.ReloadCommand.Execute(null);
        Check(!broken.HasError && broken.Projects.Count == 1, "Erneut laden behebt Fehlerzustand");
    }

    private static void TestWindow(string folder, string? screenshot)
    {
        var app = new App();
        app.InitializeComponent();
        var model = new MainWindowViewModel(new JsonProjectStore(folder), new TestPrompt());
        var window = new MainWindow(model);
        window.Show();
        Pump(window);
        Click(window, "NewProjectButton");
        Check(model.ShowEditor, "Button öffnet Editor");
        ((TextBox)window.FindName("ProjectName")).Text = "Altbau am Stadtpark";
        ((TextBox)window.FindName("ProjectAddress")).Text = "Parkstraße 12, 80331 München";
        ((TextBox)window.FindName("ProjectNotes")).Text = "Dach und Fenster prüfen.\nIm nächsten Schritt Maßnahmen und Kosten erfassen.";
        Pump(window);
        Check(model.IsDirty && model.Name == "Altbau am Stadtpark", "Formularbindung");
        Click(window, "SaveProjectButton");
        Check(!model.IsDirty && model.Projects.Count == 1, "Speicherbutton persistiert Projekt");
        if (screenshot is not null) Capture(window, Path.ChangeExtension(screenshot, ".detail.png"));
        model.BackCommand.Execute(null);
        Pump(window);
        if (screenshot is not null) Capture(window, screenshot);
        model.ShowAboutCommand.Execute(null);
        Pump(window);
        Check(model.ShowAbout && model.PageDescription.Contains("0.2.0"), "App-Information");
        model.ShowHomeCommand.Execute(null);
        model.OpenProjectCommand.Execute(model.Projects.Single());
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        Pump(window);
        if (screenshot is not null) Capture(window, Path.ChangeExtension(screenshot, ".small.png"));
        Check(window.IsVisible && window.ActualWidth >= window.MinWidth, "Startfenster und Mindestgröße");
        model.Name = "Ungespeichert";
        window.Close();
        Check(window.IsVisible, "Schließen bei Abbrechen verhindert");
        model.Name = "Altbau am Stadtpark";
        window.Close();
        app.Shutdown();
    }

    private static void Click(Window window, string name)
    {
        var button = (Button)window.FindName(name);
        Check(button.IsEnabled, $"{name} aktiviert");
        var peer = new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        Pump(window);
    }
    private static void Pump(Window window)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }
    private static void Capture(Window window, string path)
    {
        Pump(window);
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Erwartete Ausnahme: {typeof(T).Name}");
    }
    private sealed class TestPrompt : IUnsavedChangesPrompt
    {
        public UnsavedChangesChoice Choice { get; set; } = UnsavedChangesChoice.Cancel;
        public UnsavedChangesChoice Ask() => Choice;
    }
    private sealed class FailingStore : IProjectStore
    {
        public string FolderPath => "Test";
        public IReadOnlyList<RenovationProject> Load() => [];
        public void Save(RenovationProject project, Guid? expectedRevision) => throw new IOException("Test: kein Schreibzugriff.");
    }
}
