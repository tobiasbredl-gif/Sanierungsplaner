using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.Services;

public interface IIncomingRepaymentEditor
{
    IncomingRepayment? Record(string? payer = null);
    bool ConfirmReversal(IncomingRepayment entry);
}
