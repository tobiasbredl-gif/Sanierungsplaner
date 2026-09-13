using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.ViewModels;
using Sanierungsplaner.Desktop.Views;

internal static partial class Program
{
    private static void TestMatching()
    {
        var first = Sample() with { Material = "Estrich", Unit = "kg", Quantity = 10, UnitPrice = 1, Payments = new Payments(10), Date = new DateOnly(2026, 1, 2) };
        var editor = new TestCostEditor { Next = first };
        var changes = 0;
        var plan = new CostPlanViewModel(editor, () => changes++);
        plan.AddCommand.Execute(null);
        var second = first with { Id = Guid.NewGuid(), Material = "  esTRich  ", Quantity = 5, UnitPrice = 2, Date = first.Date.Value.AddDays(5) };
        editor.Next = second;
        plan.AddCommand.Execute(null);
        Check(plan.Groups.Count == 1 && plan.Groups[0].Quantity == 15 && plan.Groups[0].Total == 20, "Einkäufe werden automatisch addiert");
        Check(plan.Items.Count == 2 && plan.Items[0].UnitPrice == 1 && plan.Items[1].UnitPrice == 2 && plan.Items[1].Date == second.Date, "Einzelpreise und Datum bleiben erhalten");
        Check(editor.MatchQuestions == 0, "Eindeutige Treffer ohne Rückfrage");
        editor.Next = first with { Id = Guid.NewGuid(), Material = "Estrcih" };
        editor.Decision = new(first.Id);
        plan.AddCommand.Execute(null);
        Check(editor.MatchQuestions == 1 && plan.Groups.Count == 1 && plan.Groups[0].Entries.Count == 3, "Buchstabendreher nur nach Bestätigung zusammenführen");
        editor.Next = first with { Id = Guid.NewGuid(), Material = "Estrih" };
        editor.Decision = new(Cancelled: true);
        plan.AddCommand.Execute(null);
        Check(plan.Items.Count == 3 && changes == 3, "Abbrechen übernimmt keinen Einkauf");
        editor.Decision = new();
        plan.AddCommand.Execute(null);
        Check(plan.Groups.Count == 2, "Ähnlichen Namen ausdrücklich getrennt lassen");
        foreach (var item in new[] { first with { Unit = "Sack" }, first with { Room = "Bad" }, first with { Floor = "OG" } })
        {
            Check(CostItemMatching.Similar(item with { Material = "Estrcih" }, new[] { first }).Count == 0, "Einheit, Raum und Etage müssen übereinstimmen");
            Check(CostItemMatching.Key(item) != CostItemMatching.Key(first), "Verschiedene Zuordnung bleibt getrennt");
        }
        Check(CostItemMatching.Similar(first with { Material = "Rohr 25" }, new[] { first with { Material = "Rohr 20" } }).Count == 0, "Andere Abmessungen nicht als Tippfehler vorschlagen");
        Check(CostItemMatching.Similar(first with { Material = "Mörte" }, new[] { first with { Material = "Mörtel" }, first with { Id = Guid.NewGuid(), Material = "Mörtei" } }).Count == 2, "Mehrere mögliche Treffer sichtbar anbieten");
        var rounding = first with { Quantity = 0.333m, UnitPrice = 0.5m, Payments = new Payments() };
        var group = new CostPlanViewModel.PurchaseGroup(new[] { rounding, rounding with { Id = Guid.NewGuid() } });
        Check(group.Total == 0.34m, "Summe gerundeter Einkaufsbeträge statt neu gerundeter Gesamtmenge");
        Check(CostItemMatching.Key(first with { Material = "  Estrich   fein " }) == CostItemMatching.Key(first with { Material = "estrich fein" }), "Überflüssige Leerzeichen ignorieren");
        var draft = new CostItemDraft(null);
        Check(draft.Date == DateTime.Today, "Datum neuer Einträge ist heute");
        draft["Material"] = "Estrich";
        draft.Date = DateTime.Today.AddDays(1);
        Check(draft.Build() is null, "Keine versehentlichen Einkäufe in der Zukunft");
    }

    private static Reimbursement Return(string recipient, decimal amount) => new(Guid.NewGuid(), recipient, amount,
        DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow, "Test: bereits zurückgezahlt");
    private static decimal Person(CostPlanViewModel plan, string person) => plan.People.First(p => p.Person == person).Amount;

    private static void TestRepayments(string folder)
    {
        var store = new JsonProjectStore(folder);
        var first = Sample() with { Payments = new Payments(100, 20, 5, 0), Date = DateOnly.FromDateTime(DateTime.Today) };
        var editor = new TestCostEditor { Next = first, AllowRemoval = true };
        var repayment = new TestReimbursementEditor();
        var model = new MainWindowViewModel(store, new TestPrompt(), editor, repayment);
        model.NewProjectCommand.Execute(null);
        model.Name = "Rückzahlungstest";
        var plan = model.CostPlan;
        plan.BudgetText = "200,00";
        plan.AddCommand.Execute(null);
        model.SaveProjectCommand.Execute(null);
        repayment.Next = Return("Lea", 100);
        plan.ReimburseCommand.Execute(plan.People[0]);
        Check(Person(plan, "Lea") == 0 && Person(plan, "Tobias") == 100, "Vollständige Rückzahlung überträgt Ausgaben auf Tobias");
        Check(plan.Total == 125 && plan.Paid == 125 && plan.RemainingLabel == CostItem.Money(75), "Rückzahlung verändert Gesamtkosten und Restbudget nicht");
        Check(plan.Items.Single() == first && model.IsDirty, "Ursprünglicher Einkauf unverändert, Protokoll als ungespeichert erkannt");
        Check(!plan.ReimburseCommand.CanExecute(plan.People[0]), "Bei null keine weitere Rückzahlung");
        repayment.Next = Return("Wolfgang", 7);
        plan.ReimburseCommand.Execute(plan.People[1]);
        Check(Person(plan, "Wolfgang") == 13 && Person(plan, "Tobias") == 107, "Teilrückzahlung");
        repayment.Next = Return("Wolfgang", 14);
        plan.ReimburseCommand.Execute(plan.People[1]);
        Check(plan.Reimbursements.Count == 2 && plan.Error.Length > 0, "Überzahlung abgefangen");
        repayment.Next = Return("Jennifer", 5);
        plan.ReimburseCommand.Execute(plan.People[2]);
        Check(Person(plan, "Jennifer") == 0 && Person(plan, "Tobias") == 112, "Rückzahlung an Jennifer");
        editor.Next = first with { Id = Guid.NewGuid(), Quantity = 2, UnitPrice = 10, Payments = new Payments(20) };
        plan.AddCommand.Execute(null);
        Check(plan.Groups.Count == 1 && Person(plan, "Lea") == 20 && plan.Total == 145, "Neuer Einkauf nach Ausgleich erzeugt wieder offenen Betrag");
        editor.Next = first with { Payments = new Payments(0, 20, 5) };
        plan.EditCommand.Execute(first);
        Check(plan.Items.First().Payments.Lea == 100 && plan.Error.Length > 0, "Bearbeiten darf bereits zurückgezahlte Einkäufe nicht unterschreiten");
        plan.RemoveCommand.Execute(first);
        Check(plan.Items.Count == 2, "Entfernen nach Rückzahlung geschützt");
        model.SaveProjectCommand.Execute(null);
        Check(!model.HasError && !model.IsDirty, "Rückzahlungen zusammen mit Projekt speichern");
        var saved = store.Load().Single();
        Check(saved.Reimbursements.SequenceEqual(plan.Reimbursements) && saved.Items.Length == 2, "Persistiertes Protokoll und Einzelkäufe");
        Expect<InvalidDataException>(() => store.Save(saved with { Revision = Guid.NewGuid(), Reimbursements = [] }, saved.Revision));
        var changedLog = saved.Reimbursements.ToArray();
        changedLog[0] = changedLog[0] with { Note = "Überschreiben" };
        Expect<InvalidDataException>(() => store.Save(saved with { Revision = Guid.NewGuid(), Reimbursements = changedLog }, saved.Revision));
        repayment.Reverse = true;
        plan.ReverseCommand.Execute(plan.Reimbursements[0]);
        Check(plan.Reimbursements.Count == 4 && Person(plan, "Lea") == 120 && Person(plan, "Tobias") == 12, "Storno stellt Beträge wieder her und behält Originaleintrag");
        Check(!plan.ReverseCommand.CanExecute(plan.Reimbursements[0]), "Kein doppeltes Stornieren");
        Check(!plan.ReverseCommand.CanExecute(plan.Reimbursements[3]), "Storno kann nicht erneut storniert werden");
        Check(plan.People.Sum(p => p.Amount) == plan.Paid, "Ausgaben aller Personen bleiben gleich Gesamtausgaben");
        model.SaveProjectCommand.Execute(null);
        var reopened = new MainWindowViewModel(store, new TestPrompt(), editor, repayment);
        reopened.OpenProjectCommand.Execute(reopened.Projects.Single());
        Check(reopened.CostPlan.Reimbursements.Count == 4 && Person(reopened.CostPlan, "Tobias") == 12 && reopened.CostPlan.Groups.Count == 1, "Neustart erhält Stornos, Ausgleich und Gruppen");
        var draft = new ReimbursementDraft("Lea", 100);
        Check(draft.AmountText == "100,00" && draft.Date == DateTime.Today, "Voller offener Betrag und heutiges Datum vorbelegt");
        foreach (var invalid in new[] { "0", "-1", "100,01", "1,001", "1.00" })
        { draft.AmountText = invalid; Check(draft.Build() is null, "Ungültigen Rückzahlungsbetrag abweisen"); }
        draft.AmountText = "30,50";
        draft.Date = DateTime.Today.AddDays(-2);
        Check(draft.Build() is { Amount: 30.50m } entry && entry.Date == DateOnly.FromDateTime(draft.Date.Value), "Rückzahlung rückdatierbar");
    }

    private static void TestLegacyCosts(string folder)
    {
        var store = new JsonProjectStore(folder);
        var project = new RenovationProject(Guid.NewGuid(), Guid.NewGuid(), "Altbestand", "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        { Budget = 200, Items = new[] { Sample() } };
        store.Save(project, null);
        var path = Path.Combine(folder, $"{project.Id:D}.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["SchemaVersion"] = 2;
        document["Project"]!.AsObject().Remove("Reimbursements");
        document["Project"]!["Items"]![0]!.AsObject().Remove("Date");
        document["Project"]!["Items"]![0]!.AsObject().Remove("RecordedAt");
        var legacy = document.ToJsonString();
        File.WriteAllText(path, legacy);
        var restored = store.Load().Single();
        Check(restored.Items.Single().Date is null && restored.Items.Single().DateLabel.Contains("Altbestand") && restored.Reimbursements.Length == 0, "Altdaten erhalten kein erfundenes Einkaufsdatum");
        Check(restored.Budget == 200 && restored.Items.Single().Payments.Total == 75 && File.ReadAllText(path) == legacy, "Migration liest ohne Datenänderung");
    }

    private static void TestNumericFocus(Window dialog)
    {
        foreach (var name in new[] { "QuantityInput", "PriceInput", "LeaInput", "WolfgangInput", "JenniferInput", "TobiasInput" })
        {
            ((TextBox)dialog.FindName("MaterialInput")).Focus();
            var box = (TextBox)dialog.FindName(name);
            var before = box.Text;
            box.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            Pump(dialog);
            Check(box.SelectionLength == before.Length && box.Text == before, "Erster Klick markiert alles und löscht nichts: " + name);
            box.SelectedText = "2";
            Check(box.Text == "2", "Direkte Eingabe ersetzt Inhalt: " + name);
            box.Text = before;
        }
        var quantity = (TextBox)dialog.FindName("QuantityInput");
        ((TextBox)dialog.FindName("MaterialInput")).Focus();
        quantity.Focus();
        Check(quantity.SelectionLength == quantity.Text.Length, "Tastaturfokus markiert ebenfalls die Menge");
    }

    private static void TestRepaymentWindow(Window owner, string? screenshot)
    {
        var dialog = new ReimbursementWindow("Lea", 25) { Owner = owner };
        Exception? failure = null;
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var amount = (TextBox)dialog.FindName("AmountInput");
                amount.Focus();
                Check(amount.SelectionLength == amount.Text.Length, "Rückzahlungsbetrag sofort überschreibbar");
                if (screenshot is not null) Capture(dialog, Path.ChangeExtension(screenshot, ".repayment.png"));
                Click(dialog, "ApplyButton");
            }
            catch (Exception error) { failure = error; dialog.Hide(); }
        });
        dialog.ShowDialog();
        if (failure is not null) throw failure;
        Check(dialog.Result is { Amount: 25, Recipient: "Lea" }, "Echte Rückzahlungsmaske");
        var model = (MainWindowViewModel)owner.DataContext;
        model.CostPlan.Reimbursements.Add(dialog.Result!);
        model.CostPlan.BudgetText = "200,00";
        model.SaveProjectCommand.Execute(null);
        ((TabControl)owner.FindName("ProjectTabs")).SelectedIndex = 1;
        if (screenshot is not null) Capture(owner, Path.ChangeExtension(screenshot, ".settled.png"));
        var journal = Visuals<Expander>(owner).First(e => Equals(e.Header, "Rückzahlungsprotokoll"));
        journal.IsExpanded = true;
        Pump(owner);
        journal.BringIntoView();
        Pump(owner);
        Check(Visuals<TextBlock>(journal).Any(t => t.Text.Contains("Tobias → Lea")), "Gespeicherte Rückzahlung im sichtbaren Protokoll");
        if (screenshot is not null) Capture(owner, Path.ChangeExtension(screenshot, ".journal.png"));
        journal.IsExpanded = false;
        var original = model.CostPlan.Items.Single();
        model.CostPlan.Items.Add(original with { Id = Guid.NewGuid(), Quantity = 2, UnitPrice = 10, Payments = new Payments(20), Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), RecordedAt = DateTimeOffset.UtcNow });
        model.CostPlan.BudgetText = "200,00";
        model.SaveProjectCommand.Execute(null);
        Pump(owner);
        var entries = Visuals<Expander>(owner).First(e => Equals(e.Header, "Einzelne Einträge mit Datum"));
        entries.IsExpanded = true;
        Pump(owner);
        entries.BringIntoView();
        Pump(owner);
        Check(Visuals<TextBlock>(entries).Count(t => t.Text.Contains("×")) == 2, "Beide datierten Einkäufe werden im Verlauf gerendert");
        if (screenshot is not null) Capture(owner, Path.ChangeExtension(screenshot, ".purchases.png"));
        ((TabControl)owner.FindName("ProjectTabs")).SelectedIndex = 0;
    }

    private static void TestMatchWindow(Window owner, string? screenshot)
    {
        var existing = Sample() with { Material = "Estrich" };
        var dialog = new MatchWindow(existing with { Material = "Estrcih" }, new[] { existing }) { Owner = owner };
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(() =>
        {
            if (screenshot is not null) Capture(dialog, Path.ChangeExtension(screenshot, ".matching.png"));
            Click(dialog, "MatchButton");
        });
        Check(dialog.ShowDialog() == true && dialog.MatchId == existing.Id, "Tippfehler-Dialog bestätigt konkrete Position");
    }

    private sealed class TestReimbursementEditor : IReimbursementEditor
    {
        public Reimbursement? Next { get; set; }
        public bool Reverse { get; set; }
        public Reimbursement? Record(string recipient, decimal outstanding) => Next;
        public bool ConfirmReversal(Reimbursement entry) => Reverse;
    }

    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Visuals<T>(child)) yield return descendant;
        }
    }
}
