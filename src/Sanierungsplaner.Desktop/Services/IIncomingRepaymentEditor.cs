using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.Services;

public interface IIncomingRepaymentEditor
{
    IncomingRepayment? Record();
    bool ConfirmReversal(IncomingRepayment entry);
}
