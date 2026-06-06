using KataFlow.Core.Abstractions;

namespace KataFlow.Infrastructure;

public class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default)
        => File.ReadAllTextAsync(path, ct);
    public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
        => File.WriteAllTextAsync(path, content, ct);
    public void WriteAllText(string path, string content)
        => File.WriteAllText(path, content);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
    public string[] GetFiles(string directory, string pattern)
        => Directory.GetFiles(directory, pattern);
    public string[] GetDirectories(string directory)
        => Directory.GetDirectories(directory);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public Stream OpenReadWrite(string path)
        => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    public void DeleteFile(string path) => File.Delete(path);
    public string Combine(string path1, string path2) => Path.Combine(path1, path2);
    public string Combine(params string[] paths) => Path.Combine(paths);
    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
    public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
}
