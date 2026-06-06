namespace KataFlow.Engine.Gates;

public class ApprovalFileSignal
{
    private const string PendingFile = ".pending-approval";
    private const string ApprovedFile = ".approved";
    private const string RejectedFile = ".rejected";

    public string GetPendingPath(string sessionDir) => Path.Combine(sessionDir, PendingFile);
    public string GetApprovedPath(string sessionDir) => Path.Combine(sessionDir, ApprovedFile);
    public string GetRejectedPath(string sessionDir) => Path.Combine(sessionDir, RejectedFile);

    public void WritePending(string sessionDir, string stepName)
    {
        Directory.CreateDirectory(sessionDir);
        File.WriteAllText(GetPendingPath(sessionDir), stepName);
    }

    public bool HasApproval(string sessionDir)
    {
        return File.Exists(GetApprovedPath(sessionDir));
    }

    public bool HasRejection(string sessionDir)
    {
        return File.Exists(GetRejectedPath(sessionDir));
    }

    public void ClearApproval(string sessionDir)
    {
        if (File.Exists(GetApprovedPath(sessionDir)))
            File.Delete(GetApprovedPath(sessionDir));
        if (File.Exists(GetPendingPath(sessionDir)))
            File.Delete(GetPendingPath(sessionDir));
    }

    public void ClearRejection(string sessionDir)
    {
        if (File.Exists(GetRejectedPath(sessionDir)))
            File.Delete(GetRejectedPath(sessionDir));
        if (File.Exists(GetPendingPath(sessionDir)))
            File.Delete(GetPendingPath(sessionDir));
    }
}
