using Sanierungsplaner.Desktop.Models;
namespace Sanierungsplaner.Desktop.Services;
// Native dialogs complete asynchronously before the shared synchronous command consumes the result.
public sealed class CostItemEditor : ICostItemEditor
{
 public CostItem? Pending { get; set; }
 public MatchDecision Match { get; set; } = new();
 public CostItem? Edit(CostItem? existing, bool planned = false) { var value = Pending; Pending = null; return value; }
 public bool ConfirmRemoval(CostItem existing) => true;
 public MatchDecision ChooseSimilar(CostItem incoming, IReadOnlyList<CostItem> candidates) => Match;
}
public sealed class ReimbursementEditor : IReimbursementEditor
{
 public Reimbursement? Pending { get; set; }
 public Reimbursement? Record(string recipient, decimal outstanding) { var value = Pending; Pending = null; return value; }
 public bool ConfirmReversal(Reimbursement entry) => true;
}
public sealed class SalesCreditEditor : ISalesCreditEditor
{
 public SalesCredit? Pending { get; set; }
 public SalesCredit? Record() { var value = Pending; Pending = null; return value; }
 public bool ConfirmReversal(SalesCredit entry) => true;
}
public sealed class IncomingRepaymentEditor : IIncomingRepaymentEditor
{
 public IncomingRepayment? Pending { get; set; }
 public IncomingRepayment? Record(string? payer = null) { var value = Pending; Pending = null; return value; }
 public bool ConfirmReversal(IncomingRepayment entry) => true;
}
public sealed class NativePrompt : IUnsavedChangesPrompt
{
 public UnsavedChangesChoice Choice { get; set; } = UnsavedChangesChoice.Cancel;
 public UnsavedChangesChoice Ask() => Choice;
}
