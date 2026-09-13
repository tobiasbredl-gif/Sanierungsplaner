using Sanierungsplaner.Desktop.Models;
namespace Sanierungsplaner.Desktop.Services;

public interface IProjectStore
{
    string FolderPath { get; }
    IReadOnlyList<RenovationProject> Load();
    void Save(RenovationProject project, Guid? expectedRevision);
}
