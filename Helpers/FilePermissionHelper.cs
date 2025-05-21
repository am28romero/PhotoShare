using System.Diagnostics;

namespace PhotoShare.Helpers;

public static class FilePermissionHelper
{
    public static void SetFilePermissions(string path, string mode = "700")
    {
        var chmod = new ProcessStartInfo
        {
            FileName = "/bin/chmod",
            ArgumentList = { mode, path },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(chmod);
        process.WaitForExit();
    }
    
    public static void SetFileOwnership(string path, string user = "photoshare", string group = "photoshare")
    {
        var chown = new ProcessStartInfo
        {
            FileName = "/bin/chown",
            ArgumentList = { $"{user}:{group}", path },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(chown);
        process.WaitForExit();
    }


    public static void SetDirectoryPermissions(string path, string mode = "700")
    {
        SetFilePermissions(path, mode);
    }
    
    public static void SetDirectoryOwnership(string path, string user = "photoshare", string group = "photoshare")
    {
        SetFileOwnership(path, user, group);
    }
}