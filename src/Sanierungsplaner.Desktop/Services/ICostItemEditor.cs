using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.Services;

public interface ICostItemEditor
{
    CostItem? Edit(CostItem? existing);
    bool ConfirmRemoval(CostItem existing);
}
