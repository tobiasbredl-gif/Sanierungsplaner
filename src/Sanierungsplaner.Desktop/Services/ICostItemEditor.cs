using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.Services;

public interface ICostItemEditor
{
    CostItem? Edit(CostItem? existing, bool planned = false);
    bool ConfirmRemoval(CostItem existing);
    MatchDecision ChooseSimilar(CostItem incoming, IReadOnlyList<CostItem> candidates);
}

public sealed record MatchDecision(Guid? MatchId = null, bool Cancelled = false);
