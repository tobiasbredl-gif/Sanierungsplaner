using System.IO;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.ViewModels;
internal static partial class Program
{
    private static void TestIncoming(string folder)
    {
        var now = DateTimeOffset.UtcNow;
        var item = Sample() with { Quantity = 1, UnitPrice = 500, Payments = new Payments(100,100,100,200) };
        var project = new RenovationProject(Guid.NewGuid(), Guid.NewGuid(), "Rückzahlungstest", "", "", now, now) { Items = [item], Budget = 1000 };
        var editor = new TestIncomingEditor();
        var model = new CostPlanViewModel(new TestCostEditor(), () => {}, incoming: editor);
        model.Reset(project);
        foreach (var person in IncomingRepayment.Payers)
        {
            editor.Next = new IncomingRepayment(Guid.NewGuid(), "Rückzahlung", person, 20, DateOnly.FromDateTime(DateTime.Today), now, "");
            model.AddIncomingCommand.Execute(null);
            Check(model.People.First(p => p.Person == person).Amount == 100, "Zahlende Person bleibt unverändert");
        }
        Check(model.IncomingTotal == 60 && model.People.Last().Amount == 140 && model.Total == 500 && model.NetPaid == 440, "Nur Tobias entlastet, Einkauf unverändert");
        Check(model.People.Sum(p => p.Amount) == model.NetPaid && model.IsDirty, "Personensumme und Speicherschutz");
        var contribution = new CostPlanViewModel(new TestCostEditor(), () => {}, incoming: editor);
        contribution.Reset(project with { Items = [item with { UnitPrice = 4000, Payments = new Payments(Tobias: 4000) }] });
        var lea = contribution.People.First(p => p.Person == "Lea");
        editor.Next = new IncomingRepayment(Guid.NewGuid(), "Kostenanteil", "Lea", 1000, DateOnly.FromDateTime(DateTime.Today), now, "");
        Check(lea.Amount == 0 && contribution.PayTobiasCommand.CanExecute(lea), "Kostenanteil auch bei null offen möglich");
        contribution.PayTobiasCommand.Execute(lea);
        Check(contribution.People.First().Amount == 0 && contribution.People.First().IncomingByPerson == 1000 && contribution.People.Last().Amount == 3000 && contribution.Total == 4000, "4000 minus Leas 1000 ohne Belastung von Lea");
        var store = new JsonProjectStore(folder);
        project = project with { IncomingRepayments = model.IncomingRepayments.ToArray() };
        store.Save(project, null);
        var loaded = store.Load().Single();
        model.Reset(loaded);
        Check(model.IncomingTotal == 60 && !model.IsDirty, "Rückzahlungen nach Neustart");
        Expect<InvalidDataException>(() => store.Save(loaded with { Revision = Guid.NewGuid(), IncomingRepayments = [] }, loaded.Revision));
        var first = model.IncomingRepayments[0];
        model.ReverseIncomingCommand.Execute(first);
        Check(model.IncomingTotal == 40 && model.People.Last().Amount == 160 && model.People.First().Amount == 100, "Storno entlastet keine andere Person");
        Check(!model.ReverseIncomingCommand.CanExecute(first), "Kein doppeltes Storno");
        Expect<InvalidDataException>(() => IncomingRepayment.ValidateLedger([first with { Payer = "Tobias" }]));
        Expect<InvalidDataException>(() => IncomingRepayment.ValidateLedger([first with { Amount = -1 }]));
        var main = new MainWindowViewModel(store, new TestPrompt());
        main.OpenProjectCommand.Execute(main.Projects.Single());
        Check(main.SelectedProjectTab == 1, "Öffnen wählt Kosten");
        main.SelectedProjectTab = 2; main.Notes = "Notiz"; main.SaveProjectCommand.Execute(null);
        Check(main.SelectedProjectTab == 2 && store.Load().Single().IncomingRepayments.Length == 3, "Speichern erhält Reiter und Protokoll");
    }
    private sealed class TestIncomingEditor : IIncomingRepaymentEditor
    {
        public IncomingRepayment? Next { get; set; }
        public IncomingRepayment? Record(string? payer = null) => Next;
        public bool ConfirmReversal(IncomingRepayment entry) => true;
    }
}
