using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Afterimage;

static class Admin
{
    public static bool IsElevated
    {
        get
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool NeedsAdminFor(int pid)
    {
        if (IsElevated || pid <= 0) return false;
        try
        {
            var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (handle == 0) return true;
            try
            {
                if (!OpenProcessToken(handle, TokenQuery, out var token)) return true;
                try
                {
                    var info = new TokenElevation();
                    if (!GetTokenInformation(token, TokenElevationClass, out info, Marshal.SizeOf<TokenElevation>(), out _))
                        return true;
                    return info.TokenIsElevated != 0;
                }
                finally
                {
                    CloseHandle(token);
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }
        catch
        {
            return true;
        }
    }

    public static bool TryRelaunchElevated()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;
        try
        {
            Program.ReleaseInstance();
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Line("elevate: " + ex.Message);
            return false;
        }
    }

    const int ProcessQueryLimitedInformation = 0x1000;
    const int TokenQuery = 0x0008;
    const int TokenElevationClass = 20;

    [StructLayout(LayoutKind.Sequential)]
    struct TokenElevation
    {
        public int TokenIsElevated;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern nint OpenProcess(int access, bool inherit, int pid);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool OpenProcessToken(nint process, int access, out nint token);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool GetTokenInformation(nint token, int classId, out TokenElevation info, int length, out int returned);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(nint handle);
}
