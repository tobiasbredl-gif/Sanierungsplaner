using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "Sanierungsplaner-Tests", Guid.NewGuid().ToString("N"));
        try
        {
            TestPersistence(Path.Combine(testRoot, "storage"));
            TestEditing(Path.Combine(testRoot, "editing"));
            TestCosts(Path.Combine(testRoot, "costs"));
            TestMatching();
            TestRefresh(Path.Combine(testRoot, "refresh"));
            TestRepayments(Path.Combine(testRoot, "repayments"));
            TestLegacyCosts(Path.Combine(testRoot, "legacy-costs"));
            TestIncoming(Path.Combine(testRoot, "incoming"));
            TestFillAmount();
            TestCredits(Path.Combine(testRoot, "credits"));
            TestWindow(Path.Combine(testRoot, "window"), args.FirstOrDefault());
            Console.WriteLine("PASS: Rückzahlungen, Stornos, Protokollschutz, Zusammenführung, Tippfehler, Datumsverlauf, Eingabefokus, Migration und bestehende Funktionen.");
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

    private static void TestRefresh(string folder)
    {
        var store = new JsonProjectStore(folder);
        var model = new MainWindowViewModel(store, new TestPrompt());
        model.NewProjectCommand.Execute(null);
        model.Name = "Vorher";
        model.SaveProjectCommand.Execute(null);
        model.SelectedProjectTab = 2;
        var original = store.Load().Single();
        store.Save(original with { Revision = Guid.NewGuid(), Name = "Vom Handy" }, original.Revision);
        model.RefreshCurrentProject();
        Check(model.Name == "Vom Handy" && model.IsEditing && model.SelectedProjectTab == 2, "Aktualisieren übernimmt externe Änderungen und erhält Projekt/Reiter");
        model.Name = "Ungespeichert";
        model.RefreshCurrentProject();
        Check(model.Name == "Ungespeichert" && model.IsDirty, "Aktualisieren schützt ungespeicherte Eingaben");
    }

    private static void TestPersistence(string folder)
    {
        var store = new JsonProjectStore(folder);
        Check(store.Load().Count == 0, "Leerer Speicher");
        var now = DateTimeOffset.UtcNow;
        var first = new RenovationProject(Guid.NewGuid(), Guid.NewGuid(), "Haus am See", "Seestraße 12, 80331 München", "Dach prüfen.\nFenster erneuern.", now, now);
        store.Save(first, null);
        Check(Same(new JsonProjectStore(folder).Load().Single(), first), "JSON-Roundtrip einschließlich Umlauten und Zeilenumbrüchen");
        var updated = first with { Revision = Guid.NewGuid(), Name = "Haus am See · Umbau", UpdatedAt = now.AddMinutes(1) };
        store.Save(updated, first.Revision);
        Expect<IOException>(() => store.Save(first, first.Revision));
        Check(Same(store.Load().Single(), updated), "Konflikt darf vorhandene Daten nicht ersetzen");
        using (var guard = new FileStream(Path.Combine(folder, ".write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Expect<IOException>(() => store.Save(updated with { Revision = Guid.NewGuid() }, updated.Revision));
        Check(Same(store.Load().Single(), updated), "Dateisperre erhält bestehende Daten");
        Expect<InvalidDataException>(() => store.Save(first with { Name = " " }, null));
        var file = Path.Combine(folder, $"{first.Id:D}.json");
        var valid = File.ReadAllText(file);
        File.WriteAllText(file, "{kaputt");
        Expect<InvalidDataException>(() => store.Load());
        Expect<InvalidDataException>(() => store.Save(updated, updated.Revision));
        Check(File.ReadAllText(file) == "{kaputt", "Beschädigte Datei bleibt erhalten");
        File.WriteAllText(file, valid.Replace("\"SchemaVersion\": 6", "\"SchemaVersion\": 99"));
        Expect<InvalidDataException>(() => store.Load());
        File.WriteAllText(file, valid);
        var legacy = JsonNode.Parse(valid)!;
        legacy["SchemaVersion"] = 1;
        legacy["Project"]!.AsObject().Remove("Items");
        legacy["Project"]!.AsObject().Remove("Budget");
        legacy["Project"]!.AsObject().Remove("Reimbursements");
        legacy["Project"]!.AsObject().Remove("Credits");
        File.WriteAllText(file, legacy.ToJsonString());
        var migrated = store.Load().Single();
        Check(migrated.Budget == 0 && migrated.Items.Length == 0 && migrated.Name == updated.Name, "Bestehendes v0.2-Projekt wird verlustfrei geladen");
        store.Save(migrated with { Revision = Guid.NewGuid() }, migrated.Revision);
        Check(JsonNode.Parse(File.ReadAllText(file))!["SchemaVersion"]!.GetValue<int>() == 6, "Migration schreibt neues Format erst beim Speichern");
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
        Check(model.ShowAbout && model.PageDescription.Contains("1.0.1"), "App-Information");
        model.ShowHomeCommand.Execute(null);
        model.OpenProjectCommand.Execute(model.Projects.Single());
        Pump(window);
        Check(((TabControl)window.FindName("ProjectTabs")).SelectedIndex == 1, "Projekt öffnet direkt Kosten und Zahlungen");
        TestCostWindow(window, screenshot);
        TestRepaymentWindow(window, screenshot);
        TestMatchWindow(window, screenshot);
        TestCreditWindow(window, screenshot);
        TestIncomingWindow(window, screenshot);
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

    private static bool Same(RenovationProject left, RenovationProject right)
        => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

    private static CostItem Sample() => new(Guid.NewGuid(), "Materiallieferung", "EG", "Wohnzimmer", 10, "m²", 12.50m, "Gekauft", new Payments(25, 40, 10, 0));

    private static void TestCosts(string folder)
    {
        var sample = Sample();
        sample.Validate();
        Check(sample.Total == 125 && sample.Payments.Total == 75 && sample.Outstanding == 50, "Teilzahlungen und offene Kosten");
        Check((sample with { Quantity = 0.333m, UnitPrice = 0.50m }).Total == 0.17m, "Kaufmännische Rundung auf Cent");
        Expect<InvalidDataException>(() => (sample with { Payments = new Payments(126) }).Validate());
        Expect<InvalidDataException>(() => (sample with { Status = "Geplant" }).Validate());
        Expect<InvalidDataException>(() => (sample with { Payments = new Payments(-1) }).Validate());
        Expect<InvalidDataException>(() => (sample with { Payments = new Payments(1.001m) }).Validate());
        var draft = new CostItemDraft(sample);
        draft["UnitPrice"] = "12,50";
        Check(draft.Build() == sample, "Deutsches Dezimalkomma und stabile Position-ID");
        draft["UnitPrice"] = "12.50";
        Check(draft.Build() is null, "Punkt wird nicht als Tausenderzeichen fehlinterpretiert");
        var editor = new TestCostEditor { Next = sample };
        var model = new MainWindowViewModel(new JsonProjectStore(folder), new TestPrompt(), editor);
        model.NewProjectCommand.Execute(null);
        model.Name = "Kostenprojekt";
        model.CostPlan.BudgetText = "200,00";
        model.CostPlan.AddCommand.Execute(null);
        Check(model.CostPlan.People.Select(p => p.Person).SequenceEqual(new[] { "Lea", "Wolfgang", "Jennifer", "Tobias" }), "Alle vier Personen, auch ohne Zahlung");
        Check(model.CostPlan.People.Select(p => p.Amount).SequenceEqual(new decimal[] { 25, 40, 10, 0 }), "Summen je Person");
        Check(model.CostPlan.RemainingLabel == CostItem.Money(125) && model.CostPlan.ForecastLabel == CostItem.Money(75), "Budget und Prognose getrennt");
        model.SaveProjectCommand.Execute(null);
        Check(!model.HasError && !model.IsDirty, "Kostenprojekt gespeichert");
        editor.Next = sample with { Payments = new Payments(25, 40, 10, 50), Status = "Verbaut" };
        model.CostPlan.EditCommand.Execute(model.CostPlan.Items.Single());
        Check(model.IsDirty && model.CostPlan.Items.Count == 1 && model.CostPlan.Paid == 125, "Bearbeiten ersetzt ohne Doppelzählung");
        model.SaveProjectCommand.Execute(null);
        var loaded = new JsonProjectStore(folder).Load().Single();
        Check(loaded.Items.Single().Payments.Tobias == 50 && loaded.Budget == 200, "Zahlungen bleiben nach erneutem Laden erhalten");
        editor.Next = null;
        model.CostPlan.AddCommand.Execute(null);
        Check(!model.IsDirty && model.CostPlan.Items.Count == 1, "Dialogabbruch verändert nichts");
        model.CostPlan.BudgetText = "50,00";
        Check(model.CostPlan.RemainingLabel == CostItem.Money(-75), "Budgetüberschreitung sichtbar");
        model.CostPlan.BudgetText = "ungültig";
        model.SaveProjectCommand.Execute(null);
        Check(model.HasError && new JsonProjectStore(folder).Load().Single().Budget == 200, "Ungültiges Budget verändert gespeicherte Daten nicht");
        model.CostPlan.RemoveCommand.Execute(model.CostPlan.Items.Single());
        Check(model.CostPlan.Items.Count == 1, "Abgebrochenes Entfernen behält Position");
        editor.AllowRemoval = true;
        model.CostPlan.RemoveCommand.Execute(model.CostPlan.Items.Single());
        Check(model.CostPlan.Paid == 0 && model.CostPlan.People.All(p => p.Amount == 0) && model.IsDirty, "Entfernen aktualisiert alle Personensummen");
    }

    private static void TestCostWindow(Window owner, string? screenshot)
    {
        var dialog = new CostItemWindow(null) { Owner = owner };
        Exception? failure = null;
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                TestNumericFocus(dialog);
                ((TextBox)dialog.FindName("MaterialInput")).Text = "Materiallieferung";
                ((TextBox)dialog.FindName("QuantityInput")).Text = "10";
                ((TextBox)dialog.FindName("PriceInput")).Text = "12,50";
                ((ComboBox)dialog.FindName("StatusInput")).SelectedItem = "Gekauft";
                ((TextBox)dialog.FindName("LeaInput")).Text = "25,00";
                ((TextBox)dialog.FindName("WolfgangInput")).Text = "40,00";
                ((TextBox)dialog.FindName("JenniferInput")).Text = "10,00";
                Click(dialog, "TobiasTotalButton");
                Check(((TextBox)dialog.FindName("TobiasInput")).Text == "50,00", "Gesamtbetrag-Button ergänzt den offenen Rest im echten Formular");
                Pump(dialog);
                if (screenshot is not null) Capture(dialog, Path.ChangeExtension(screenshot, ".position.png"));
                Click(dialog, "ApplyButton");
            }
            catch (Exception error) { failure = error; dialog.DataContext = new CostItemDraft(null); dialog.Hide(); }
        });
        dialog.ShowDialog();
        if (failure is not null) throw failure;
        Check(dialog.Result?.Total == 125 && dialog.Result.Payments.Total == 125, "Kostenformular vollständig über WPF bedient");
        var model = (MainWindowViewModel)owner.DataContext;
        model.CostPlan.Items.Add(dialog.Result!);
        model.CostPlan.BudgetText = "200,00";
        model.SaveProjectCommand.Execute(null);
        ((TabControl)owner.FindName("ProjectTabs")).SelectedIndex = 1;
        Pump(owner);
        if (screenshot is not null) Capture(owner, Path.ChangeExtension(screenshot, ".costs.png"));
        ((TabControl)owner.FindName("ProjectTabs")).SelectedIndex = 0;
        Pump(owner);
    }
    private sealed class TestCostEditor : ICostItemEditor
    {
        public CostItem? Next { get; set; }
        public CostItem? Edit(CostItem? existing, bool planned = false) => Next;
        public bool AllowRemoval { get; set; }
        public bool ConfirmRemoval(CostItem existing) => AllowRemoval;
        public MatchDecision Decision { get; set; } = new();
        public int MatchQuestions { get; private set; }
        public MatchDecision ChooseSimilar(CostItem incoming, IReadOnlyList<CostItem> candidates) { MatchQuestions++; return Decision; }
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
