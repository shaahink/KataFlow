namespace KataFlow.Core.Abstractions;

public interface IFileSystem
{
    bool FileExists(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);
    Task WriteAllTextAsync(string path, string content, CancellationToken ct = default);
    void WriteAllText(string path, string content);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string GetCurrentDirectory();
    string[] GetFiles(string directory, string pattern);
    Stream OpenRead(string path);
    Stream OpenReadWrite(string path);
    void DeleteFile(string path);
    string Combine(string path1, string path2);
    string Combine(params string[] paths);
    string? GetDirectoryName(string path);
    string GetFileNameWithoutExtension(string path);
}
