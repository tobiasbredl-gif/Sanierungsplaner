using global::Android.Content;
using global::Android.App;
using global::Android.Views;
using global::Android.Widget;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.ViewModels;
namespace Sanierungsplaner.AndroidApp;
public partial class MainActivity
{
 Task<int> Choose(string title,string[] options)
 {
  var done=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
  var dialog=new AlertDialog.Builder(this)!.SetTitle(title)!.SetItems(options,(_,e)=>done.TrySetResult(e.Which))!.SetNegativeButton("Abbrechen",(_,_)=>done.TrySetResult(-1))!.Create()!;
  dialog.CancelEvent+=(_,_)=>done.TrySetResult(-1); dialog.Show();return done.Task;
 }
 async Task<bool> Confirm(string title,string message,string confirmLabel="Ja")
 {
  var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  var d=new AlertDialog.Builder(this)!.SetTitle(title)!.SetMessage(message)!.SetPositiveButton(confirmLabel,(_,_)=>done.TrySetResult(true))!.SetNegativeButton("Abbrechen",(_,_)=>done.TrySetResult(false))!.Create()!;
  d.CancelEvent+=(_,_)=>done.TrySetResult(false);d.Show();return await done.Task;
 }
 Task Message(string title,string message)
 {
  var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var d=new AlertDialog.Builder(this)!.SetTitle(title)!.SetMessage(message)!.SetPositiveButton("OK",(_,_)=>done.TrySetResult())!.Create()!;
  d.CancelEvent+=(_,_)=>done.TrySetResult();d.Show();return done.Task;
 }
 async Task<T?> Form<T>(string title,Action<LinearLayout> fill,Func<T?> build,Func<string> error) where T:class
 {
  var done=new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);
  var scroll=new ScrollView(this);var box=Stack();box.SetPadding(Dp(18),Dp(8),Dp(18),Dp(12));scroll.AddView(box);
  fill(box);var errorView=Text(box,"");errorView.SetTextColor(global::Android.Graphics.Color.ParseColor("#852D14"));
  var dialog=new AlertDialog.Builder(this)!.SetTitle(title)!.SetView(scroll)!.SetPositiveButton("Übernehmen",(_,_)=>{})!.SetNegativeButton("Abbrechen",(_,_)=>{})!.Create()!;
  dialog.SetCancelable(false);dialog.Show();
  dialog.GetButton((int)DialogButtonType.Positive)!.Click+=(_,_)=>{var result=build();if(result==null){errorView.Text=error();scroll.FullScroll(FocusSearchDirection.Down);return;}dialog.Dismiss();done.TrySetResult(result);};
  dialog.GetButton((int)DialogButtonType.Negative)!.Click+=(_,_)=>Run(async()=>{if(await Confirm("Eingabe verwerfen?","Die Eingaben in diesem Fenster werden nicht übernommen.")){dialog.Dismiss();done.TrySetResult(null);}});
  formOpen=true;try{return await done.Task;}finally{formOpen=false;}
 }
 void Select(LinearLayout box,string label,IReadOnlyList<string> values,string selected,Action<string> changed)
 {
  Text(box,label,13,true);var spinner=new Spinner(this);
  var adapter=new ArrayAdapter<string>(this,global::Android.Resource.Layout.SimpleSpinnerItem,values.ToArray());adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
  spinner.Adapter=adapter;spinner.SetSelection(Math.Max(0,values.ToList().IndexOf(selected)));spinner.ItemSelected+=(_,e)=>changed(values[e.Position]);box.AddView(spinner);
 }
 void DateField(LinearLayout box,DateTime? date,Action<DateTime?> changed)
 {
  var current=date??DateTime.Today;
  var b=Button(box,date==null?"Datum unbekannt · Datum auswählen":current.ToString("dd.MM.yyyy"),()=>Task.CompletedTask);
  b.Click+=(_,_)=>{var picker=new DatePickerDialog(this,(_,e)=>{current=e.Date;changed(current);b.Text=current.ToString("dd.MM.yyyy");},current.Year,current.Month-1,current.Day);picker.Show();};
 }
 async Task EditCost(CostItem? existing,bool planned)
 {
  var draft=new CostItemDraft(existing,planned);
  var result=await Form("Einkauf / Position",box=>
  {
   Field(box,"Material / Beschreibung *",draft["Material"],v=>draft["Material"]=v);
   Text(box,planned?"Geplantes Datum":"Einkaufsdatum");DateField(box,draft.Date,v=>draft.Date=v);
   Field(box,"Etage (optional)",draft["Floor"],v=>draft["Floor"]=v,max:100);Field(box,"Raum (optional)",draft["Room"],v=>draft["Room"]=v,max:100);
   Field(box,"Menge *",draft["Quantity"],v=>draft["Quantity"]=v,true,max:14);
   Field(box,"Einheit *",draft["Unit"],v=>draft["Unit"]=v,max:30);
   Field(box,"Einzelpreis (€) *",draft["UnitPrice"],v=>draft["UnitPrice"]=v,true,max:14);
   var vat=new CheckBox(this){Text="19 % MwSt. hinzufügen",Checked=draft.AddVat};vat.CheckedChange+=(_,e)=>draft.AddVat=e.IsChecked;box.AddView(vat);
   Text(box,"Bei MwSt.-Auswahl den Einzelpreis ohne Steuer eingeben. Zahlen mit Dezimalkomma.",12);
   var total=Text(box,draft.TotalLabel,24,true);
   Spinner? status=null;
   Text(box,"Status");status=new Spinner(this);var adapter=new ArrayAdapter<string>(this,global::Android.Resource.Layout.SimpleSpinnerItem,draft.Statuses.ToArray());adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);status.Adapter=adapter;status.SetSelection(draft.Statuses.ToList().IndexOf(draft["Status"]));status.ItemSelected+=(_,e)=>draft["Status"]=draft.Statuses[e.Position];box.AddView(status);
   Text(box,"Wer hat was bezahlt?",20,true);var fields=new Dictionary<string,EditText>();var updating=false;
   foreach(var person in SalesCredit.Recipients)
   {
    fields[person]=Field(box,person+" (€)",draft[person],v=>{if(!updating)draft[person]=v;},true,max:16);
    Button(box,"Gesamtbetrag für "+person,()=>{draft.FillAmountCommand.Execute(person);return Task.CompletedTask;});
   }
   var inlineError=Text(box,"");
   draft.PropertyChanged+=(_,_)=>
   {
    if(updating)return;updating=true;total.Text=draft.TotalLabel;inlineError.Text=draft.Error;
    foreach(var (person,e) in fields)if(e.Text!=draft[person])e.Text=draft[person];
    var index=draft.Statuses.ToList().IndexOf(draft["Status"]);if(status.SelectedItemPosition!=index)status.SetSelection(index);updating=false;
   };
  },draft.Build,()=>draft.Error);
  if(result==null)return;
  costs.Match=new();
  if(existing==null && !Plan.Items.Any(i=>CostItemMatching.Key(i)==CostItemMatching.Key(result)))
  {
   var candidates=CostItemMatching.Similar(result,Plan.Items);
   if(candidates.Count>0)
   {
    var options=candidates.Select(i=>"Zu „"+i.Material+"“ hinzufügen").Append("Als eigene Position anlegen").ToArray();
    var choice=await Choose("Ähnliche Position gefunden",options);if(choice<0)return;
    if(choice<candidates.Count)costs.Match=new(candidates[choice].Id);
   }
  }
  costs.Pending=result;
  if(existing!=null)Plan.EditCommand.Execute(existing);else if(planned)Plan.AddPlannedCommand.Execute(null);else Plan.AddCommand.Execute(null);
  PersistDraft();Render();
 }
 async Task RecordSale()
 {
  var draft=new SalesCreditDraft();
  var result=await Form("Verkauf / Gutschrift",box=>
  {
   Field(box,"Verkaufte Sache *",draft.Description,v=>draft.Description=v);
   Field(box,"Erhaltener Betrag (€) *",draft.AmountText,v=>draft.AmountText=v,true,max:16);
   Select(box,"Wer hat das Geld erhalten?",draft.Recipients,draft.Recipient,v=>draft.Recipient=v);
   Text(box,"Datum der Einnahme");DateField(box,draft.Date,v=>draft.Date=v);
   Field(box,"Notiz",draft.Note,v=>draft.Note=v,max:500,multiline:true);
  },draft.Build,()=>draft.Error);
  if(result==null)return;sales.Pending=result;Plan.AddCreditCommand.Execute(null);PersistDraft();Render();
 }
 async Task RecordIncoming(string? payer=null)
 {
  var draft=new IncomingRepaymentDraft{Payer=payer??"Lea"};
  var result=await Form("An Tobias zahlen",box=>
  {
   Text(box,"Auch bei 0 € offen: Der Beitrag senkt nur Tobias' Nettoausgaben. Die eigenen Ausgaben bleiben gleich.");
   Select(box,"Wer zahlt an Tobias?",draft.Payers,draft.Payer,v=>draft.Payer=v);
   Field(box,"Beschreibung",draft.Description,v=>draft.Description=v);
   Field(box,"Erhaltener Betrag (€)",draft.AmountText,v=>draft.AmountText=v,true,max:16);
   Text(box,"Zahlungsdatum");DateField(box,draft.Date,v=>draft.Date=v);
   Field(box,"Notiz",draft.Note,v=>draft.Note=v,max:500,multiline:true);
  },draft.Build,()=>draft.Error);
  if(result==null)return;incoming.Pending=result;Plan.AddIncomingCommand.Execute(null);PersistDraft();Render();
 }
 async Task RecordRefund(string person)
 {
  var draft=new ReimbursementDraft(person,Plan.People.First(p=>p.Person==person).Amount);
  var result=await Form("Tobias erstattet an "+person,box=>
  {
   Text(box,draft.BalanceLabel);Field(box,"Zurückgezahlter Betrag (€)",draft.AmountText,v=>draft.AmountText=v,true,max:20);
   Text(box,"Rückzahlungsdatum");DateField(box,draft.Date,v=>draft.Date=v);
   Field(box,"Notiz",draft.Note,v=>draft.Note=v,max:500,multiline:true);
  },draft.Build,()=>draft.Error);
  if(result==null||!await ConfirmOwner())return;repayments.AuthorizeOnce();repayments.Pending=result;Plan.ReimburseCommand.Execute(Plan.People.First(p=>p.Person==person));PersistDraft();Render();
 }
 void IncomingHistory(LinearLayout box)
 {
  Button(box,"+ Beitrag an Tobias erfassen",()=>RecordIncoming());
  if(Plan.IncomingHistory.Count==0)Text(box,"Noch keine Beiträge erfasst.");
  foreach(var entry in Plan.IncomingHistory)
  {
   var card=Stack(box,true);Text(card,entry.Title,18,true);Text(card,entry.AmountLabel,22,true);Text(card,entry.DetailLabel);Text(card,entry.RecordedLabel,12);Text(card,entry.Note);
   Button(card,"Stornieren",async()=>{if(await Confirm("Beitrag stornieren?","Der Originaleintrag bleibt erhalten; Tobias' Entlastung wird aufgehoben.")){Plan.ReverseIncomingCommand.Execute(entry);PersistDraft();Render();}},Plan.ReverseIncomingCommand.CanExecute(entry));
  }
 }
 void SalesHistory(LinearLayout box)
 {
  if(Plan.CreditHistory.Count==0)Text(box,"Noch keine Verkäufe erfasst.");
  foreach(var entry in Plan.CreditHistory)
  {
   var card=Stack(box,true);Text(card,entry.Title,18,true);Text(card,entry.AmountLabel,22,true);Text(card,entry.DetailLabel);Text(card,entry.RecordedLabel,12);Text(card,entry.Note);
   Button(card,"Stornieren",async()=>{if(await Confirm("Verkauf stornieren?","Der Originaleintrag bleibt erhalten; die Gutschrift wird aufgehoben.")){Plan.ReverseCreditCommand.Execute(entry);PersistDraft();Render();}},Plan.ReverseCreditCommand.CanExecute(entry));
  }
 }
 void RefundHistory(LinearLayout box)
 {
  if(Plan.History.Count==0)Text(box,"Noch keine Rückzahlungen erfasst.");
  foreach(var entry in Plan.History)
  {
   var card=Stack(box,true);Text(card,entry.Description,18,true);Text(card,entry.RecordedLabel,12);Text(card,entry.Note);
   if(IsTobias) Button(card,"Stornieren",async()=>{if(await Confirm("Rückzahlung stornieren?","Der Originaleintrag bleibt erhalten; die Kostenübernahme wird aufgehoben.") && await ConfirmOwner()){repayments.AuthorizeOnce();Plan.ReverseCommand.Execute(entry);PersistDraft();Render();}},Plan.ReverseCommand.CanExecute(entry));
  }
 }
}
