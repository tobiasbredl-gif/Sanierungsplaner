using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.ViewModels;
var folder=Path.Combine(Path.GetTempPath(),"Sanierungsplaner-AndroidTests",Guid.NewGuid().ToString("N"));
void Check(bool value,string label){if(!value)throw new Exception(label);}
try
{
 var costs=new CostItemEditor();var sales=new SalesCreditEditor();var incoming=new IncomingRepaymentEditor();var repayments=new ReimbursementEditor();var prompt=new NativePrompt();
 var model=new MainWindowViewModel(new JsonProjectStore(folder),prompt,costs,repayments,sales,incoming);
 model.NewProjectCommand.Execute(null);model.Name="Handytest";
 var draft=new CostItemDraft(null);draft["Material"]="Estrich";draft["UnitPrice"]="100";draft["Quantity"]="10";draft.AddVat=true;draft.FillAmountCommand.Execute("Tobias");
 costs.Pending=draft.Build();model.CostPlan.AddCommand.Execute(null);
 Check(costs.Pending==null && model.CostPlan.Total==1190 && model.CostPlan.Paid==1190,"MwSt. und einmalige Formularübernahme");
 incoming.Pending=new IncomingRepayment(Guid.NewGuid(),"Kostenanteil","Lea",1000,DateOnly.FromDateTime(DateTime.Today),DateTimeOffset.UtcNow,"");
 model.CostPlan.PayTobiasCommand.Execute(model.CostPlan.People[0]);
 Check(model.CostPlan.People[0].Amount==0 && model.CostPlan.People[3].Amount==190,"Kostenanteil bei 0 offen");
 sales.Pending=new SalesCredit(Guid.NewGuid(),"Restmaterial","Tobias",50,DateOnly.FromDateTime(DateTime.Today),DateTimeOffset.UtcNow,"");model.CostPlan.AddCreditCommand.Execute(null);
 Check(model.CostPlan.NetPaid==140,"Verkäufe senken Netto");
 var planned=new CostItemDraft(null,true);planned["Material"]="Fenster";planned["UnitPrice"]="200";planned.Date=DateTime.Today.AddDays(10);costs.Pending=planned.Build();model.CostPlan.AddPlannedCommand.Execute(null);
 Check(model.CostPlan.PlannedItems.Count==1 && model.CostPlan.Paid==1190,"Planung ohne Zahlung");
 var repeat=new CostItemDraft(null);repeat["Material"]="estrich";repeat["UnitPrice"]="10";repeat.FillAmountCommand.Execute("Jennifer");costs.Pending=repeat.Build();model.CostPlan.AddCommand.Execute(null);
 Check(model.CostPlan.Groups.Count==2 && model.CostPlan.Groups[0].Entries.Count==2,"Wiederholte Einkäufe gruppieren");
 var typo=repeat.Build()! with {Material="Estrcih"};Check(CostItemMatching.Similar(typo,model.CostPlan.Items).Count==1,"Tippfehler erkannt");
 var protectedEntry=new Reimbursement(Guid.NewGuid(),"Jennifer",10,DateOnly.FromDateTime(DateTime.Today),DateTimeOffset.UtcNow,"");
 repayments.Pending=protectedEntry;Check(repayments.Record("Jennifer",10)==null&&repayments.Pending==null,"Default role denies and clears pending reimbursement");
 repayments.IsAuthorized=()=>true;repayments.Pending=protectedEntry;Check(repayments.Record("Jennifer",10)==null,"Role alone does not replace device confirmation");
 repayments.AuthorizeOnce();Check(repayments.ConfirmReversal(protectedEntry)&&!repayments.ConfirmReversal(protectedEntry),"Device approval is consumed once");
 repayments.AuthorizeOnce();repayments.IsAuthorized=()=>false;Check(!repayments.ConfirmReversal(protectedEntry),"Revoked role invalidates pending approval");
 repayments.IsAuthorized=()=>true;repayments.AuthorizeOnce();repayments.Pending=new Reimbursement(Guid.NewGuid(),"Jennifer",10,DateOnly.FromDateTime(DateTime.Today),DateTimeOffset.UtcNow,"");model.CostPlan.ReimburseCommand.Execute(model.CostPlan.People[2]);
 Check(model.CostPlan.People[2].Amount==0 && model.CostPlan.People[3].Amount==150,"Rückzahlung überträgt Ausgaben");
 model.SelectedProjectTab=2;model.SaveProjectCommand.Execute(null);Check(!model.IsDirty && model.SelectedProjectTab==2,"Speichern erhält Ansicht");
 var reloaded=new MainWindowViewModel(new JsonProjectStore(folder),prompt,costs,repayments,sales,incoming);reloaded.OpenProjectCommand.Execute(reloaded.Projects[0]);Check(reloaded.SelectedProjectTab==1 && reloaded.CostPlan.NetPaid==150 && reloaded.CostPlan.IncomingTotal==1000,"Neustart und Navigation");
 reloaded.CostPlan.ReverseIncomingCommand.Execute(reloaded.CostPlan.IncomingRepayments[0]);Check(reloaded.CostPlan.NetPaid==1150 && reloaded.CostPlan.People[0].Amount==0,"Storno ohne Belastung von Lea");
 reloaded.SaveProjectCommand.Execute(null);Check(!reloaded.IsDirty,"Journal gespeichert");
 Console.WriteLine("PASS Android shared logic: VAT, form adapters, payments, incoming contributions, refunds, sales, grouping, typo matching, planning, persistence, reversals and navigation.");
}
finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
