using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.Services;

public interface ISalesCreditEditor
{
    SalesCredit? Record();
    bool ConfirmReversal(SalesCredit entry);
}
