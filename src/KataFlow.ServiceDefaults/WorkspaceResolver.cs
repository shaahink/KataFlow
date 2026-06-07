using KataFlow.Core;

namespace KataFlow.ServiceDefaults;

public static class WorkspaceResolver
{
    public static string ResolveRoot(string? startDir = null)
    {
        var dir = new DirectoryInfo(startDir ?? Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (dir.GetFiles("KataFlow.slnx").Length > 0 || dir.GetFiles("KataFlow.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return startDir ?? Directory.GetCurrentDirectory();
    }
}
