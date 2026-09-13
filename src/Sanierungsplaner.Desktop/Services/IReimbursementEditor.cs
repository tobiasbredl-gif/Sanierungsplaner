using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.Services;

public interface IReimbursementEditor
{
    Reimbursement? Record(string recipient, decimal outstanding);
    bool ConfirmReversal(Reimbursement entry);
}
