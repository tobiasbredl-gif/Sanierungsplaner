using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.ViewModels;
using Sanierungsplaner.Desktop.Views;

internal static partial class Program
{
    private static void TestFillAmount()
    {
        foreach (var person in SalesCredit.Recipients)
        {
            var draft = new CostItemDraft(null);
            draft["Material"] = "Estrich";
            draft["Quantity"] = "10";
            draft["UnitPrice"] = "12,50";
            draft.FillAmountCommand.Execute(person);
            var item = draft.Build();
            Check(item is not null && Reimbursement.ForPerson(item.Payments, person) == 125 && item.Payments.Total == 125 && item.Status == "Gekauft", "Gesamtbetrag für " + person);
            draft.FillAmountCommand.Execute(person);
            Check(draft.Build()!.Payments.Total == 125, "Mehrfachklick verdoppelt nichts");
        }
        var vat = new CostItemDraft(Sample() with { Quantity = 1, UnitPrice = 100, Payments = new Payments(20) });
        Check(!vat.AddVat && !vat.IsDirty, "MwSt. standardmäßig aus");
        vat.AddVat = true;
        Check(vat.IsDirty, "MwSt. Änderung wird gespeichert");
        vat.FillAmountCommand.Execute("Tobias");
        var taxed = vat.Build()!;
        Check(taxed.Total == 119 && taxed.Payments.Tobias == 99 && taxed.Outstanding == 0, "MwSt. und Teilzahlung");
        var restored = System.Text.Json.JsonSerializer.Deserialize<CostItem>(System.Text.Json.JsonSerializer.Serialize(taxed))!;
        Check(new CostItemDraft(restored).AddVat && restored.Total == 119, "MwSt. nach erneutem Laden");
        vat.AddVat = false;
        Check(vat.Build() is null, "Steuer entfernen darf keine Überzahlung erzeugen");
        vat.FillAmountCommand.Execute("Tobias");
        Check(vat.Build()!.Total == 100 && vat.Build()!.Payments.Tobias == 80, "Steuer wieder ausschalten");
        Check(CostItem.CalculateTotal(0.333m, 0.5m, true) == 0.20m, "Netto und Steuer auf Cent runden");
        var planning = new CostItemDraft(null, true);
        planning["Material"] = "Fenster"; planning["UnitPrice"] = "100"; planning.AddVat = true;
        planning.Date = DateTime.Today.AddDays(30);
        var future = planning.Build();
        Check(future is not null && future.Status == "Geplant" && future.Total == 119, "Zukünftige Ausgabe mit MwSt.");
        var planEditor = new TestCostEditor { Next = future };
        var plan = new CostPlanViewModel(planEditor, () => { });
        plan.AddPlannedCommand.Execute(null);
        Check(plan.PlannedItems.Count == 1 && plan.Total == 119 && plan.Paid == 0, "Planung zählt nur zur Kalkulation");
        planning["Status"] = "Gekauft";
        Check(planning.Build() is null, "Einkauf benötigt tatsächliches Datum");
        planning.Date = DateTime.Today;
        planning.FillAmountCommand.Execute("Tobias");
        planEditor.Next = planning.Build();
        plan.EditCommand.Execute(plan.Items[0]);
        Check(plan.PlannedItems.Count == 0 && plan.Items.Count == 1 && plan.Total == 119 && plan.Paid == 119, "Kauf ersetzt Planung ohne Doppelzählung");
        var shared = new CostItemDraft(Sample());
        shared.FillAmountCommand.Execute("Tobias");
        Check(shared.Build()!.Payments == new Payments(25, 40, 10, 50), "Andere Teilzahlungen bleiben erhalten");
        shared["Tobias"] = "ungültig";
        shared.FillAmountCommand.Execute("Tobias");
        Check(shared.Build()!.Payments.Tobias == 50, "Eigene ungültige Eingabe durch Gesamtbetrag ersetzen");
        shared["Lea"] = "999,00";
        shared.FillAmountCommand.Execute("Tobias");
        Check(shared.Error.Length > 0 && shared["Tobias"] == "50,00" && shared["Lea"] == "999,00", "Überhöhte andere Zahlungen nicht heimlich verändern");
        shared["Quantity"] = "falsch";
        shared.FillAmountCommand.Execute("Lea");
        Check(shared.Error.Length > 0 && shared["Lea"] == "999,00", "Ungültige Menge schützt Werte");
        var rounding = new CostItemDraft(Sample() with { Quantity = 0.333m, UnitPrice = 0.5m, Payments = new Payments() });
        rounding.FillAmountCommand.Execute("Lea");
        Check(rounding.Build()!.Payments.Lea == 0.17m, "Gerundeten Positionsbetrag übernehmen");
    }

    private static SalesCredit Credit(string recipient, decimal amount) => new(Guid.NewGuid(), "Restmaterial verkauft", recipient, amount,
        DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow, "Testverkauf");

    private static void TestCredits(string folder)
    {
        var store = new JsonProjectStore(folder);
        var purchase = Sample() with { Quantity = 1, UnitPrice = 500, Payments = new Payments(100, 100, 100, 200) };
        var editor = new TestCostEditor { Next = purchase };
        var sales = new TestSalesCreditEditor();
        var repayments = new TestReimbursementEditor();
        var model = new MainWindowViewModel(store, new TestPrompt(), editor, repayments, sales);
        model.NewProjectCommand.Execute(null);
        model.Name = "Verkaufstest";
        var plan = model.CostPlan;
        plan.BudgetText = "1000,00";
        plan.AddCommand.Execute(null);
        model.SaveProjectCommand.Execute(null);
        sales.Next = Credit("Tobias", 80);
        plan.AddCreditCommand.Execute(null);
        Check(plan.Total == 500 && plan.Paid == 500 && plan.Income == 80 && plan.NetPaid == 420, "500 Ausgaben minus 80 Einnahmen sind 420 netto");
        Check(plan.RemainingLabel == CostItem.Money(580) && plan.ForecastLabel == CostItem.Money(580), "Budget profitiert einmalig von Einnahmen");
        Check(Person(plan, "Tobias") == 120 && plan.People.Sum(p => p.Amount) == plan.NetPaid, "Einnahme wird nur Geldempfänger zugerechnet");
        Check(plan.Items.Single() == purchase && model.IsDirty && plan.CreditHistory[0].AmountLabel.StartsWith("+"), "Originale bleiben erhalten, Gutschrift als Plus");
        sales.Next = Credit("Lea", 40);
        plan.AddCreditCommand.Execute(null);
        Check(Person(plan, "Lea") == 60, "Gutschrift senkt offenen Rückzahlungsbetrag");
        repayments.Next = Return("Lea", 61);
        plan.ReimburseCommand.Execute(plan.People[0]);
        Check(plan.Reimbursements.Count == 0 && plan.Error.Length > 0, "Rückzahlung darf nach Einnahme offenen Betrag nicht überschreiten");
        repayments.Next = Return("Lea", 60);
        plan.ReimburseCommand.Execute(plan.People[0]);
        Check(Person(plan, "Lea") == 0 && Person(plan, "Tobias") == 180 && plan.NetPaid == 380, "Rückzahlung nach Gutschrift ohne Doppelzählung");
        sales.Next = Credit("Jennifer", 10);
        plan.AddCreditCommand.Execute(null);
        sales.Next = Credit("Wolfgang", 20);
        plan.AddCreditCommand.Execute(null);
        Check(Person(plan, "Jennifer") == 90 && Person(plan, "Wolfgang") == 80, "Alle vier Geldempfänger unterstützt");
        model.SaveProjectCommand.Execute(null);
        Check(!model.HasError && !model.IsDirty, "Projekt und Gutschriften gespeichert");
        var saved = store.Load().Single();
        Check(saved.Credits.SequenceEqual(plan.Credits), "Gutschriftprotokoll dauerhaft gespeichert");
        Expect<InvalidDataException>(() => store.Save(saved with { Revision = Guid.NewGuid(), Credits = [] }, saved.Revision));
        sales.AllowReversal = true;
        var original = plan.Credits[0];
        plan.ReverseCreditCommand.Execute(original);
        Check(plan.Credits.Count == 5 && plan.Income == 70 && plan.NetPaid == 430 && plan.Credits[4].ReversesId == original.Id, "Storno hebt Gutschrift mit weiterem Protokolleintrag auf");
        Check(!plan.ReverseCreditCommand.CanExecute(original) && !plan.ReverseCreditCommand.CanExecute(plan.Credits[4]), "Kein doppelter Storno");
        sales.Next = Credit("Lea", 1000);
        plan.AddCreditCommand.Execute(null);
        Check(Person(plan, "Lea") == -1000 && plan.NetPaid < 0 && !plan.ReimburseCommand.CanExecute(plan.People[0]), "Einnahmenüberschuss wird negativ ausgewiesen, nicht ausgezahlt");
        Check(plan.People.Sum(p => p.Amount) == plan.NetPaid, "Personensummen entsprechen den Nettoausgaben");
        model.SaveProjectCommand.Execute(null);
        var reopened = new MainWindowViewModel(store, new TestPrompt(), editor, repayments, sales);
        reopened.OpenProjectCommand.Execute(reopened.Projects.Single());
        Check(reopened.CostPlan.Credits.Count == 6 && reopened.CostPlan.NetPaid == plan.NetPaid, "Neustart erhält Einnahmen und Stornos");
        var draft = new SalesCreditDraft { Description = "Verkauf" };
        foreach (var amount in new[] { "0", "-1", "1,001", "1.00", "1000000001" })
        { draft.AmountText = amount; Check(draft.Build() is null, "Ungültige Gutschrift abgewiesen"); }
        draft.AmountText = "80,00";
        draft.Date = DateTime.Today.AddDays(-2);
        Check(draft.Build() is { Amount: 80, Recipient: "Tobias" }, "Gutschrift rückdatierbar");
        draft.Date = DateTime.Today.AddDays(1);
        Check(draft.Build() is null, "Keine Einnahme in der Zukunft");
        var legacy = new RenovationProject(Guid.NewGuid(), Guid.NewGuid(), "Altes Projekt", "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        store.Save(legacy, null);
        var path = Path.Combine(folder, $"{legacy.Id:D}.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["SchemaVersion"] = 3;
        json["Project"]!.AsObject().Remove("Credits");
        File.WriteAllText(path, json.ToJsonString());
        Check(store.Load().Single(p => p.Id == legacy.Id).Credits.Length == 0, "Version 0.4 startet ohne erfundene Gutschriften");
    }

    private static void TestCreditWindow(Window owner, string? screenshot)
    {
        var dialog = new SalesCreditWindow { Owner = owner };
        Exception? failure = null;
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                ((TextBox)dialog.FindName("DescriptionInput")).Text = "Übriges Material verkauft";
                var amount = (TextBox)dialog.FindName("AmountInput");
                amount.Focus();
                Check(amount.SelectionLength == amount.Text.Length, "Gutschriftbetrag sofort überschreibbar");
                amount.SelectedText = "80,00";
                ((ComboBox)dialog.FindName("RecipientInput")).SelectedItem = "Tobias";
                if (screenshot is not null) Capture(dialog, Path.ChangeExtension(screenshot, ".credit.png"));
                Click(dialog, "ApplyButton");
            }
            catch (Exception error) { failure = error; dialog.Hide(); }
        });
        dialog.ShowDialog();
        if (failure is not null) throw failure;
        Check(dialog.Result is { Amount: 80, Recipient: "Tobias" }, "Gutschrift im echten Windows-Formular");
        var model = (MainWindowViewModel)owner.DataContext;
        model.CostPlan.Credits.Add(dialog.Result!);
        model.CostPlan.BudgetText = "200,00";
        model.SaveProjectCommand.Execute(null);
        ((TabControl)owner.FindName("ProjectTabs")).SelectedIndex = 1;
        Pump(owner);
        var panel = Visuals<Expander>(owner).First(e => Equals(e.Header, "Gutschriften und Verkäufe"));
        panel.IsExpanded = true;
        Pump(owner);
        panel.BringIntoView();
        if (screenshot is not null) Capture(owner, Path.ChangeExtension(screenshot, ".credits.png"));
        Check(Visuals<TextBlock>(panel).Any(t => t.Text.StartsWith("+80")), "Gutschrift mit Pluszeichen im Protokoll");
        ((TabControl)owner.FindName("ProjectTabs")).SelectedIndex = 0;
    }

    private sealed class TestSalesCreditEditor : ISalesCreditEditor
    {
        public SalesCredit? Next { get; set; }
        public bool AllowReversal { get; set; }
        public SalesCredit? Record() => Next;
        public bool ConfirmReversal(SalesCredit entry) => AllowReversal;
    }
}
