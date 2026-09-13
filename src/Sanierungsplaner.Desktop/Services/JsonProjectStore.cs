using System.IO;
using System.Text.Json;
using Sanierungsplaner.Desktop.Models;
namespace Sanierungsplaner.Desktop.Services;

public sealed class JsonProjectStore(string folderPath) : IProjectStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string FolderPath { get; } = Path.GetFullPath(folderPath);
    public static JsonProjectStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sanierungsplaner", "Projects"));

    public IReadOnlyList<RenovationProject> Load()
    {
        if (!Directory.Exists(FolderPath)) return [];
        using var guard = AcquireLock();
        return Directory.EnumerateFiles(FolderPath, "*.json")
            .Select(Read).OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Name).ToArray();
    }

    public void Save(RenovationProject project, Guid? expectedRevision) => Write(project, expectedRevision, false);
    // Authoritative pulls are used only by the pinned sync client after clean sync or explicit conflict resolution.
    public void ReplaceFromSync(RenovationProject project, Guid? expectedRevision) => Write(project, expectedRevision, true);
    private void Write(RenovationProject project, Guid? expectedRevision, bool authoritativePull)
    {
        project.Validate();
        Directory.CreateDirectory(FolderPath);
        using var guard = AcquireLock();
        var target = Path.Combine(FolderPath, $"{project.Id:D}.json");
        if(File.Exists(Path.Combine(FolderPath,"Deleted",$"{project.Id:D}.json")))throw new IOException("Dieses Projekt wurde von Tobias gelöscht und kann nicht erneut übertragen werden.");
        var existing = File.Exists(target) ? Read(target) : null;
        if (existing?.Revision != expectedRevision)
            throw new IOException("Das Projekt wurde außerhalb dieses Fensters geändert. Kopiere deine Änderungen und lade die Projekte erneut.");
        if (!authoritativePull && existing is not null && !project.Reimbursements.Take(existing.Reimbursements.Length).SequenceEqual(existing.Reimbursements))
            throw new InvalidDataException("Gespeicherte Rückzahlungen dürfen nicht entfernt oder verändert werden. Bitte einen Stornoeintrag verwenden.");
        if (!authoritativePull && existing is not null && !project.Credits.Take(existing.Credits.Length).SequenceEqual(existing.Credits))
            throw new InvalidDataException("Gespeicherte Gutschriften dürfen nicht entfernt oder verändert werden. Bitte einen Stornoeintrag verwenden.");
        if (!authoritativePull && existing is not null && !project.IncomingRepayments.Take(existing.IncomingRepayments.Length).SequenceEqual(existing.IncomingRepayments)) throw new InvalidDataException("Gespeicherte Rückzahlungen an Tobias dürfen nur storniert werden.");
        var temporary = Path.Combine(FolderPath, $"{project.Id:D}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new ProjectDocument(6, project), Options);
                stream.Flush(flushToDisk: true);
            }
            // The original remains untouched until the complete new document has been flushed.
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { /* A leftover .tmp is ignored by Load. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    public IReadOnlyList<Guid> DeletedIds()
    {
        var deleted=Path.Combine(FolderPath,"Deleted");
        return Directory.Exists(deleted) ? Directory.EnumerateFiles(deleted,"*.json").Select(p=>Guid.Parse(Path.GetFileNameWithoutExtension(p))).ToArray() : [];
    }
    public void Delete(Guid id, Guid? expectedRevision, bool authoritative = false)
    {
        if(id==Guid.Empty)throw new InvalidDataException("Ungültige Projektkennung.");
        Directory.CreateDirectory(FolderPath);
        using var guard=AcquireLock();
        var deleted=Path.Combine(FolderPath,"Deleted");Directory.CreateDirectory(deleted);
        var target=Path.Combine(deleted,$"{id:D}.json");
        if(File.Exists(target))return;
        var source=Path.Combine(FolderPath,$"{id:D}.json");
        var current=File.Exists(source)?Read(source):null;
        if(!authoritative&&current?.Revision!=expectedRevision)throw new IOException("Projekt inzwischen geändert. Bitte erneut abgleichen und die Löschung prüfen.");
        // Atomic move preserves the complete journal and also acts as a durable deletion marker.
        if(current!=null)File.Move(source,target);
        else
        {
            var temp=target+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            {JsonSerializer.Serialize(stream,new {DeletedProject=id});stream.Flush(true);}
            File.Move(temp,target);
        }
    }
    private FileStream AcquireLock() => new(Path.Combine(FolderPath, ".write.lock"),
        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private static RenovationProject Read(string path)
    {
        try
        {
            var document = JsonSerializer.Deserialize<ProjectDocument>(File.ReadAllText(path));
            if (document is not { SchemaVersion: 1 or 2 or 3 or 4 or 5 or 6, Project: not null })
                throw new InvalidDataException("Unbekanntes Projektformat.");
            document.Project.Validate();
            if (Path.GetFileNameWithoutExtension(path) != document.Project.Id.ToString("D"))
                throw new InvalidDataException("Projektkennung und Dateiname stimmen nicht überein.");
            return document.Project;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            throw new InvalidDataException($"Die Datei {Path.GetFileName(path)} kann nicht geladen werden. {error.Message}", error);
        }
    }
    private sealed record ProjectDocument(int SchemaVersion, RenovationProject Project);
}
