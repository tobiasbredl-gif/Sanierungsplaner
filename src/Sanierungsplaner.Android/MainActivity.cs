using global::Android.App;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.Graphics;
using global::Android.OS;
using global::Android.Views;
using global::Android.Widget;
using System.Text.Json;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.ViewModels;
using AColor = global::Android.Graphics.Color;
namespace Sanierungsplaner.AndroidApp;

[Activity(Label = "Sanierungsplaner", MainLauncher = true, Exported = true,
 ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden,
 WindowSoftInputMode = SoftInput.AdjustResize)]
public partial class MainActivity : Activity
{
 readonly CostItemEditor costs = new();
 readonly ReimbursementEditor repayments = new();
 readonly SalesCreditEditor sales = new();
 readonly IncomingRepaymentEditor incoming = new();
 readonly NativePrompt prompt = new();
 MainWindowViewModel model = null!;
 LinearLayout page = null!;
 Guid? openedId;
 bool rendering;
 readonly List<Action> detach = new();
 string DraftPath => System.IO.Path.Combine(FilesDir!.AbsolutePath, "draft.json");
 CostPlanViewModel Plan => model.CostPlan;
 protected override void OnCreate(Bundle? state)
 {
  base.OnCreate(state); LoadIdentity();
  model = new MainWindowViewModel(new JsonProjectStore(System.IO.Path.Combine(FilesDir!.AbsolutePath, "Projects")), prompt, costs, repayments, sales, incoming);
  RestoreDraft();
  Render();
 }
 readonly Handler autoSyncHandler=new(Looper.MainLooper!);
 Action? autoSyncTick;
 protected override void OnResume()
 {
  base.OnResume();
  autoSyncTick ??= () => { if(model!=null)Run(()=>Synchronize(true));autoSyncHandler.PostDelayed(autoSyncTick!,60000); };
  autoSyncHandler.PostDelayed(autoSyncTick,1500);
 }
 protected override void OnPause() { if(autoSyncTick!=null)autoSyncHandler.RemoveCallbacks(autoSyncTick);PersistDraft(); base.OnPause(); }
 public override void OnBackPressed() => Run(Back);
 void Run(Func<Task> action) => _ = Safe(action);
 async Task Safe(Func<Task> action)
 {
  try { await action(); }
  catch (Exception ex) { await Message("Das hat nicht geklappt", ex.Message); }
 }
 int Dp(int n) => (int)(n * Resources!.DisplayMetrics!.Density);
 LinearLayout Stack(LinearLayout? parent = null, bool card = false)
 {
  var box = new LinearLayout(this) { Orientation = Orientation.Vertical };
  box.SetPadding(Dp(card ? 16 : 0), Dp(card ? 12 : 0), Dp(card ? 16 : 0), Dp(card ? 12 : 0));
  if(card) { var bg = new global::Android.Graphics.Drawables.GradientDrawable(); bg.SetColor(AColor.White); bg.SetCornerRadius(Dp(12)); box.Background = bg; }
  if(parent != null) { var lp = new LinearLayout.LayoutParams(-1, -2); lp.BottomMargin = Dp(12); parent.AddView(box, lp); }
  return box;
 }
 TextView Text(LinearLayout parent, string value, int size = 15, bool bold = false)
 {
  var text = new TextView(this) { Text = value, TextSize = size };
  text.SetTextColor(AColor.ParseColor("#153E33"));
  if(bold) text.SetTypeface(null, TypefaceStyle.Bold);
  text.SetPadding(0,Dp(4),0,Dp(8)); parent.AddView(text); return text;
 }
 Button Button(LinearLayout parent, string label, Func<Task> action, bool enabled = true)
 {
  var b = new Button(this) { Text = label, Enabled = enabled }; b.SetAllCaps(false);
  b.SetTextColor(AColor.White); b.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(AColor.ParseColor("#176B56"));
  b.Click += (_,_) => Run(action); parent.AddView(b, new LinearLayout.LayoutParams(-1,-2)); return b;
 }
 EditText Field(LinearLayout parent, string label, string value, Action<string> changed, bool number=false, int max=200, bool multiline=false)
 {
  Text(parent,label,13,true);
  var e = new EditText(this) { Text=value, TextSize=16 };
  e.SetTextColor(AColor.ParseColor("#153E33")); e.SetSelectAllOnFocus(number);
  e.InputType = number ? global::Android.Text.InputTypes.ClassNumber | global::Android.Text.InputTypes.NumberFlagDecimal : global::Android.Text.InputTypes.ClassText | (multiline ? global::Android.Text.InputTypes.TextFlagMultiLine : global::Android.Text.InputTypes.TextFlagCapSentences);
  // Accept both decimal keyboard separators; validation still follows the desktop's explicit comma convention.
  if(number) e.KeyListener = global::Android.Text.Method.DigitsKeyListener.GetInstance("0123456789,.");
  e.SetFilters([new global::Android.Text.InputFilterLengthFilter(max)]);
  e.TextChanged += (_,_) => { if(number && (e.Text ?? "").Contains(".")) { e.Text=e.Text!.Replace(".",",");e.SetSelection(e.Text.Length);return;} if(!rendering) changed(e.Text ?? ""); };
  parent.AddView(e,new LinearLayout.LayoutParams(-1,-2)); return e;
 }
 void Render()
 {
  foreach(var remove in detach) remove(); detach.Clear();
  rendering = true;
  var scroll = new ScrollView(this) { FillViewport = true };
  page = Stack(); page.SetPadding(Dp(18),Dp(28),Dp(18),Dp(28)); page.SetBackgroundColor(AColor.ParseColor("#F4F6F3"));
  scroll.AddView(page); SetContentView(scroll);
  Text(page,"S /  Sanierungsplaner",26,true);
  Text(page,"Android 0.10.0 · Offline und sicheres Heimnetz",12);
  if(model.ShowAbout) About();
  else if(!model.IsEditing) Home();
  else Project();
  if(model.Error.Length>0) Text(page,model.Error);
  if(model.Status.Length>0) Text(page,model.Status.Replace("Computer","Handy"),13);
  rendering = false;
 }
 void Home()
 {
  Text(page,"Deine Projekte",23,true);
  Button(page,"PC koppeln / WLAN-Abgleich",()=>{model.ShowAboutCommand.Execute(null);Render();return Task.CompletedTask;});
  Button(page,"+ Neues Projekt",()=> { model.NewProjectCommand.Execute(null); openedId=null; Render(); return Task.CompletedTask; }, model.NewProjectCommand.CanExecute(null));
  foreach(var p in model.Projects)
  {
   var card=Stack(page,true); Text(card,p.Name,20,true); Text(card,p.AddressLabel); Text(card,p.UpdatedLabel,12);
   Button(card,"Projekt öffnen",()=> {model.OpenProjectCommand.Execute(p); openedId=p.Id; Render(); return Task.CompletedTask;}, model.OpenProjectCommand.CanExecute(p));
  }
  if(model.IsEmpty) Text(page,"Noch kein Projekt. Lege dein erstes Sanierungsprojekt an.");
  Button(page,"Erneut laden",()=> {model.ReloadCommand.Execute(null); Render(); return Task.CompletedTask;});
  Button(page,"Über die App",()=> {model.ShowAboutCommand.Execute(null); Render(); return Task.CompletedTask;});
 }
 void About()
 {
  SyncOptions(page);
  Text(page,"Deine Daten bleiben auf deinem Handy",22,true);
  Text(page,"Alle Funktionen der aktuellen Desktop-Version: Projekte, Einkäufe, Planung, 19 % MwSt., Personenzahlungen, Rückzahlungen, Beiträge an Tobias und Verkäufe mit Stornoprotokollen.");
  Text(page,"Nach Freigabe am PC werden Projekte verschlüsselt im Heimnetz abgeglichen. Widerrufene Geräte werden beim nächsten Verbindungsversuch abgewiesen. Offline bleibt der zuletzt bestätigte Rollenstand bestehen.");
  Text(page,"Android 8 oder neuer. Daten liegen im privaten App-Speicher. Beim Deinstallieren werden sie gelöscht. Ein APK-Update über die bestehende Installation erhält sie.");
  Button(page,"Zurück",()=> {model.ShowHomeCommand.Execute(null);Render();return Task.CompletedTask;});
 }
 void Project()
 {
  Button(page,"← Alle Projekte",Back);
  Button(page,"Jetzt mit PC abgleichen",()=>Synchronize(false),binding!=null);
  Text(page,binding?.Role is string role ? "Geräterolle: "+role : "Nicht freigegeben · Erstattungen gesperrt",12);
  Text(page,string.IsNullOrWhiteSpace(model.Name)?"Neues Projekt":model.Name,23,true);
  var nav = new LinearLayout(this) { Orientation=Orientation.Horizontal };
  page.AddView(nav);
  foreach(var (title,index) in new[]{("Projektdaten",0),("Kosten & Zahlungen",1),("Geplant",2)})
  {
   var b=new Button(this){Text=title,TextSize=12}; b.SetAllCaps(false); b.SetTextColor(AColor.ParseColor(index==model.SelectedProjectTab?"#176B56":"#596B62"));
   nav.AddView(b,new LinearLayout.LayoutParams(0,-2,1)); b.Click+=(_,_)=>{model.SelectedProjectTab=index;Render();};
  }
  if(model.SelectedProjectTab==0) Details(); else if(model.SelectedProjectTab==1) Costs(); else Planned();
  Text(page,model.DraftStatus,13);
  Button(page,"Projekt speichern",Save);
 }
 void Details()
 {
  Field(page,"Projektname *",model.Name,v=>model.Name=v,max:120);
  Field(page,"Objektadresse",model.Address,v=>model.Address=v,max:300);
  Field(page,"Notizen",model.Notes,v=>model.Notes=v,max:10000,multiline:true);
 }
 void Costs()
 {
  var summary=Stack(page,true);
  Text(summary,"Aktuelle Gesamtausgaben",18,true);Text(summary,Plan.NetPaidLabel,28,true);
  Text(summary,"Netto nach Gutschriften und Beiträgen an Tobias",12);Text(summary,"Ursprünglich bezahlt: "+Plan.PaidLabel,13);
  Text(summary,"Geplante Ausgaben",18,true);Text(summary,Plan.PlannedTotalLabel,26,true);
  Text(summary,"Geplante Positionen einschließlich gewählter MwSt.",12);
  Button(page,"+ Einkauf / Position erfassen",()=>EditCost(null,false));
  Button(page,"+ Verkauf / Gutschrift",RecordSale);
  if(Plan.Error.Length>0) Text(page,Plan.Error);
  if(Plan.Notice.Length>0) Text(page,Plan.Notice);
  Text(page,"Wer hat was bezahlt?",22,true);
  foreach(var person in Plan.People)
  {
   var card=Stack(page,true); Text(card,person.Person,21,true); Text(card,person.AmountCaption+": "+person.AmountLabel,19,true); Text(card,person.DetailLabel,12);
   Text(card,(person.Person=="Tobias"?"Von den anderen erhalten: ":"An Tobias bezahlt: ")+person.IncomingLabel);
   if(person.Person!="Tobias")
   {
    Button(card,"An Tobias zahlen",()=>RecordIncoming(person.Person));
    if(IsTobias) Button(card,"Tobias erstattet an "+person.Person,()=>RecordRefund(person.Person), Plan.ReimburseCommand.CanExecute(person));
   }
  }
  Text(page,"Beiträge an Tobias senken nur seine Nettoausgaben. Die Ausgaben der zahlenden Person bleiben unverändert.",13);
  Expand(page,"Budget und Gesamtkosten",Budget);
  Expand(page,"Rückzahlungen an Tobias · "+Plan.IncomingTotalLabel,IncomingHistory);
  Expand(page,"Verkäufe / Gutschriften · "+Plan.IncomeLabel,SalesHistory);
  Expand(page,"Rückzahlungsprotokoll",RefundHistory);
  Text(page,"Alle Kostenpositionen",22,true);
  Button(page,"+ Einkauf / Position erfassen",()=>EditCost(null,false));
  foreach(var group in Plan.Groups)
  {
   var card=Stack(page,true); Text(card,group.Material,20,true);Text(card,group.QuantityLabel); Text(card,group.DetailLabel,12); Text(card,"Kosten: "+group.TotalLabel+" · Bezahlt: "+group.PaidLabel,16,true);
   Expand(card,"Einzelne Einkäufe mit Datum",box=> {foreach(var item in group.Entries) Item(box,item);});
  }
 }
 void Expand(LinearLayout parent,string label,Action<LinearLayout> fill)
 {
  var box=Stack(parent); box.Visibility=ViewStates.Gone;
  var button=new Button(this){Text="▸ "+label};button.SetAllCaps(false);
  parent.AddView(button,parent.IndexOfChild(box));
  bool loaded=false;
  button.Click+=(_,_)=>{if(!loaded){fill(box);loaded=true;}box.Visibility=box.Visibility==ViewStates.Gone?ViewStates.Visible:ViewStates.Gone;button.Text=(box.Visibility==ViewStates.Visible?"▾ ":"▸ ")+label;};
 }
 void Budget(LinearLayout box)
 {
  Field(box,"Projektbudget (€)",Plan.BudgetText,v=>Plan.BudgetText=v,true,max:16);
  Text(box,"Kalkulierte Kosten: "+Plan.TotalLabel);Text(box,"Tatsächlich bezahlt: "+Plan.PaidLabel);Text(box,"Verkäufe: "+Plan.IncomeLabel);Text(box,"Beiträge an Tobias: "+Plan.IncomingTotalLabel);Text(box,"Nettoausgaben: "+Plan.NetPaidLabel);
  Text(box,"Noch zu bezahlen: "+Plan.OutstandingLabel);
  var remain=Text(box,"Restbudget: "+Plan.RemainingLabel,20,true);var forecast=Text(box,"Planungsspielraum: "+Plan.ForecastLabel);var error=Text(box,Plan.BudgetError);
  System.ComponentModel.PropertyChangedEventHandler handler=(_,_)=>{remain.Text="Restbudget: "+Plan.RemainingLabel;forecast.Text="Planungsspielraum: "+Plan.ForecastLabel;error.Text=Plan.BudgetError;}; Plan.PropertyChanged+=handler;detach.Add(()=>Plan.PropertyChanged-=handler);
 }
 void Planned()
 {
  Text(page,"Zukünftige / geplante Ausgaben",22,true);Text(page,"Geplante Ausgaben zählen zur Kalkulation, aber noch nicht als bezahlt. Beim Kauf den vorhandenen Eintrag ändern.");
  Button(page,"+ Geplante Ausgabe erfassen",()=>EditCost(null,true)); Text(page,Plan.PlannedHint);Text(page,Plan.PlannedTotalLabel,24,true);
  foreach(var item in Plan.PlannedItems) Item(Stack(page,true),item);
 }
 void Item(LinearLayout box,CostItem item)
 {
  Text(box,item.Material,18,true);Text(box,item.PurchaseLabel);Text(box,"Kosten: "+item.TotalLabel+" · Bezahlt: "+item.PaidLabel);Text(box,item.PaymentsLabel,12);
  Button(box,"Eintrag bearbeiten",()=>EditCost(item,false));
  Button(box,"Entfernen",async()=>{if(await Confirm("Eintrag entfernen?",item.Material+" vom "+item.DateLabel+" einschließlich Zahlungen entfernen?")){Plan.RemoveCommand.Execute(item);Render();}});
 }
 async Task Save()
 {
  model.SaveProjectCommand.Execute(null);
  if(!model.IsDirty && !model.HasError){openedId=model.Projects.FirstOrDefault()?.Id;ClearDraft();}
  else if(model.HasError && string.IsNullOrWhiteSpace(model.Name)) model.SelectedProjectTab=0;
  Render(); await Task.CompletedTask;
 }
 async Task Back()
 {
  if(model.ShowAbout){model.ShowHomeCommand.Execute(null);Render();return;}
  if(!model.IsEditing){Finish();return;}
  if(model.IsDirty)
  {
   var choice=await Choose("Ungespeicherte Änderungen",new[]{"Speichern","Verwerfen","Weiterbearbeiten"});
   if(choice==0){await Save();if(model.IsDirty||model.HasError)return;}
   else if(choice!=1)return;
  }
  prompt.Choice=UnsavedChangesChoice.Discard; model.BackCommand.Execute(null);prompt.Choice=UnsavedChangesChoice.Cancel;openedId=null;ClearDraft();Render();
 }
 record DraftState(Guid? OriginalId,RenovationProject Project,int Tab,string BudgetText);
 void PersistDraft()
 {
  if(model==null||!model.IsEditing||!model.IsDirty)return;
  try
  {
   var now=DateTimeOffset.UtcNow;
   var p=new RenovationProject(Guid.NewGuid(),Guid.NewGuid(),model.Name,model.Address,model.Notes,now,now){Budget=Plan.TryBudget(out var b)?b:0,Items=Plan.Items.ToArray(),Credits=Plan.Credits.ToArray(),Reimbursements=Plan.Reimbursements.ToArray(),IncomingRepayments=Plan.IncomingRepayments.ToArray()};
   var temp=DraftPath+".tmp";System.IO.File.WriteAllText(temp,JsonSerializer.Serialize(new DraftState(openedId,p,model.SelectedProjectTab,Plan.BudgetText)));System.IO.File.Move(temp,DraftPath,true);
  }
  catch(Exception ex){global::Android.Util.Log.Warn("Sanierungsplaner",ex.Message);}
 }
 void ClearDraft(){if(System.IO.File.Exists(DraftPath))System.IO.File.Delete(DraftPath);}
 void RestoreDraft()
 {
  if(!System.IO.File.Exists(DraftPath))return;
  try
  {
   var state=JsonSerializer.Deserialize<DraftState>(System.IO.File.ReadAllText(DraftPath));if(state==null)return;
   var original=model.Projects.FirstOrDefault(p=>p.Id==state.OriginalId);
   if(original!=null){model.OpenProjectCommand.Execute(original);openedId=original.Id;}else model.NewProjectCommand.Execute(null);
   if(!model.IsEditing)return;
   model.Name=state.Project.Name;model.Address=state.Project.Address;model.Notes=state.Project.Notes;
   Plan.Items.Clear();foreach(var i in state.Project.Items)Plan.Items.Add(i);
   Plan.Credits.Clear();foreach(var i in state.Project.Credits)Plan.Credits.Add(i);
   Plan.Reimbursements.Clear();foreach(var i in state.Project.Reimbursements)Plan.Reimbursements.Add(i);
   Plan.IncomingRepayments.Clear();foreach(var i in state.Project.IncomingRepayments)Plan.IncomingRepayments.Add(i);
   Plan.BudgetText=state.BudgetText;model.SelectedProjectTab=state.Tab;
  }catch(Exception ex){global::Android.Util.Log.Warn("Sanierungsplaner",ex.Message);}
 }
}
