using KataFlow.Core.Abstractions;
using KataFlow.Core;

namespace KataFlow.Engine.Gates;

public class ApprovalFileSignal
{
    private readonly IFileSystem _fileSystem;

    public ApprovalFileSignal(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    private string GetPendingPath(string sessionDir) => _fileSystem.Combine(sessionDir, Constants.PendingApprovalFile);
    private string GetApprovedPath(string sessionDir) => _fileSystem.Combine(sessionDir, Constants.ApprovedFile);
    private string GetRejectedPath(string sessionDir) => _fileSystem.Combine(sessionDir, Constants.RejectedFile);

    public void WritePending(string sessionDir, string stepName)
    {
        _fileSystem.CreateDirectory(sessionDir);
        _fileSystem.WriteAllText(GetPendingPath(sessionDir), stepName);
    }

    public bool HasApproval(string sessionDir)
        => _fileSystem.FileExists(GetApprovedPath(sessionDir));

    public bool HasRejection(string sessionDir)
        => _fileSystem.FileExists(GetRejectedPath(sessionDir));

    public void ClearApproval(string sessionDir)
    {
        if (_fileSystem.FileExists(GetApprovedPath(sessionDir)))
            _fileSystem.DeleteFile(GetApprovedPath(sessionDir));
        if (_fileSystem.FileExists(GetPendingPath(sessionDir)))
            _fileSystem.DeleteFile(GetPendingPath(sessionDir));
    }

    public void ClearRejection(string sessionDir)
    {
        if (_fileSystem.FileExists(GetRejectedPath(sessionDir)))
            _fileSystem.DeleteFile(GetRejectedPath(sessionDir));
        if (_fileSystem.FileExists(GetPendingPath(sessionDir)))
            _fileSystem.DeleteFile(GetPendingPath(sessionDir));
    }
}
