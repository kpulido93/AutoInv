using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace AutoInventario.ModelsUserSystem;

//public static class ResponsibleResolver
//{
//    public static string? GetResponsible()
//    {

//        var current = WindowsIdentity.GetCurrent()?.Name;
//        if (!string.IsNullOrWhiteSpace(current) && !IsSystemIdentity(current))
//            return FormatDisplayAndAccount(Normalize(current)); var wtsUser = GetUserFromActiveWtsSession();
//        if (!string.IsNullOrWhiteSpace(wtsUser))
//            return FormatDisplayAndAccount(Normalize(wtsUser)); var explorerUser = GetInteractiveUserFromExplorerActiveSession();
//        if (!string.IsNullOrWhiteSpace(explorerUser))
//            return FormatDisplayAndAccount(Normalize(explorerUser)); var regUser = GetLastLoggedOnFromRegistry();
//        if (!string.IsNullOrWhiteSpace(regUser))
//            return FormatDisplayAndAccount(regUser.Trim()); var wmiUser = GetWmiUserName();
//        if (!string.IsNullOrWhiteSpace(wmiUser))
//            return FormatDisplayAndAccount(Normalize(wmiUser));

//        return null;
//    }

//    private static string FormatDisplayAndAccount(string accountOrDisplay)
//    {
//        if (string.IsNullOrWhiteSpace(accountOrDisplay))
//            return accountOrDisplay;

//        var input = accountOrDisplay.Trim();
//        if (!input.Contains('\\') && input.Contains(' '))
//            return input;

//        var display = TryGetDisplayName(input);
//        if (!string.IsNullOrWhiteSpace(display))
//        {

//            if (string.Equals(display, input, StringComparison.OrdinalIgnoreCase))
//                return display;

//            return $"{display} | {input}";
//        }

//        return input;
//    }

//    private static string? TryGetDisplayName(string account)
//    {
//        try
//        {
//            var (domain, user) = SplitAccount(account);
//            if (string.IsNullOrWhiteSpace(user))
//                return null;

//            PrincipalContext? ctx = null;
//            if (!string.IsNullOrWhiteSpace(domain) &&
//                domain != "." &&
//                !domain.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase) &&
//                !domain.Equals("AZUREAD", StringComparison.OrdinalIgnoreCase) &&
//                !domain.Equals("AZUREADDS", StringComparison.OrdinalIgnoreCase))
//            {
//                try { ctx = new PrincipalContext(ContextType.Domain, domain); }
//                catch { ctx = null; }
//            }
//            ctx ??= new PrincipalContext(ContextType.Machine);

//            UserPrincipal? p = null; if (user.Contains('@'))
//                p = UserPrincipal.FindByIdentity(ctx, IdentityType.UserPrincipalName, user);
//            p ??= UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, user);
//            p ??= UserPrincipal.FindByIdentity(ctx, IdentityType.Name, user);

//            var display = p?.DisplayName;
//            if (string.IsNullOrWhiteSpace(display)) display = p?.Name;

//            return string.IsNullOrWhiteSpace(display) ? null : display.Trim();
//        }
//        catch
//        {
//            return null;
//        }
//    }

//    private static (string domain, string user) SplitAccount(string account)
//    {
//        var a = account.Trim();

//        if (a.StartsWith(@".\", StringComparison.Ordinal))
//            return (".", a.Substring(2));

//        var idx = a.IndexOf('\\');
//        if (idx > 0 && idx < a.Length - 1)
//            return (a.Substring(0, idx), a.Substring(idx + 1));

//        return ("", a);
//    }

//    private static bool IsSystemIdentity(string name)
//        => name.Equals(@"NT AUTHORITY\SYSTEM", StringComparison.OrdinalIgnoreCase)
//           || name.EndsWith(@"\SYSTEM", StringComparison.OrdinalIgnoreCase);

//    private static string Normalize(string name) => name.Trim();

//    private static string? GetUserFromActiveWtsSession()
//    {
//        try
//        {
//            var activeSessionId = GetActiveSessionId();
//            if (activeSessionId < 0) return null;

//            var user = WtsQueryString(activeSessionId, WTS_INFO_CLASS.WTSUserName);
//            if (string.IsNullOrWhiteSpace(user)) return null;

//            var domain = WtsQueryString(activeSessionId, WTS_INFO_CLASS.WTSDomainName);
//            if (string.IsNullOrWhiteSpace(domain)) domain = "UNKNOWN";

//            return $"{domain}\\{user}";
//        }
//        catch
//        {
//            return null;
//        }
//    }

//    private static int GetActiveSessionId()
//    {

//        if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, out var pSessions, out var count) && pSessions != IntPtr.Zero)
//        {
//            try
//            {
//                var dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
//                for (int i = 0; i < count; i++)
//                {
//                    var si = Marshal.PtrToStructure<WTS_SESSION_INFO>(pSessions + i * dataSize);
//                    if (si.State == WTS_CONNECTSTATE_CLASS.WTSActive)
//                        return si.SessionID;
//                }
//            }
//            finally
//            {
//                WTSFreeMemory(pSessions);
//            }
//        }
//        var console = (int)WTSGetActiveConsoleSessionId();
//        return console == unchecked((int)0xFFFFFFFF) ? -1 : console;
//    }

//    private static string? WtsQueryString(int sessionId, WTS_INFO_CLASS infoClass)
//    {
//        if (!WTSQuerySessionInformation(IntPtr.Zero, sessionId, infoClass, out var buffer, out _))
//            return null;

//        try
//        {

//            return Marshal.PtrToStringAnsi(buffer);
//        }
//        finally
//        {
//            WTSFreeMemory(buffer);
//        }
//    }

//    private static string? GetInteractiveUserFromExplorerActiveSession()
//    {
//        try
//        {
//            var activeSessionId = GetActiveSessionId();

//            var explorer = Process.GetProcessesByName("explorer")
//                .FirstOrDefault(p => p.SessionId == activeSessionId);

//            if (explorer == null) return null;

//            using var mo = new ManagementObject($"win32_process.handle='{explorer.Id}'");
//            using var outParams = mo.InvokeMethod("GetOwner", null, null);

//            var user = outParams?["User"]?.ToString();
//            var domain = outParams?["Domain"]?.ToString();

//            return string.IsNullOrWhiteSpace(user) ? null : $"{domain}\\{user}";
//        }
//        catch
//        {
//            return null;
//        }
//    }

//    private static string? GetLastLoggedOnFromRegistry()
//    {
//        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI";
//        try
//        {
//            using var k = Registry.LocalMachine.OpenSubKey(keyPath);
//            if (k == null) return null;

//            var sam = k.GetValue("LastLoggedOnSAMUser")?.ToString();
//            if (!string.IsNullOrWhiteSpace(sam)) return sam;

//            var user = k.GetValue("LastLoggedOnUser")?.ToString();
//            if (!string.IsNullOrWhiteSpace(user)) return user;

//            var display = k.GetValue("LastLoggedOnDisplayName")?.ToString();
//            if (!string.IsNullOrWhiteSpace(display)) return display;

//            return null;
//        }
//        catch
//        {
//            return null;
//        }
//    }

//    private static string? GetWmiUserName()
//    {
//        try
//        {
//            using var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
//            foreach (ManagementObject mo in searcher.Get())
//                return mo["UserName"]?.ToString();
//        }
//        catch { }
//        return null;
//    }

//    private enum WTS_INFO_CLASS
//    {
//        WTSUserName = 5,
//        WTSDomainName = 7
//    }

//    private enum WTS_CONNECTSTATE_CLASS
//    {
//        WTSActive = 0,
//        WTSConnected = 1,
//        WTSConnectQuery = 2,
//        WTSShadow = 3,
//        WTSDisconnected = 4,
//        WTSIdle = 5,
//        WTSListen = 6,
//        WTSReset = 7,
//        WTSDown = 8,
//        WTSInit = 9
//    }

//    [StructLayout(LayoutKind.Sequential)]
//    private struct WTS_SESSION_INFO
//    {
//        public int SessionID;
//        public IntPtr pWinStationName;
//        public WTS_CONNECTSTATE_CLASS State;
//    }

//    [DllImport("kernel32.dll")]
//    private static extern uint WTSGetActiveConsoleSessionId();

//    [DllImport("wtsapi32.dll", SetLastError = true)]
//    private static extern bool WTSQuerySessionInformation(
//        IntPtr hServer,
//        int sessionId,
//        WTS_INFO_CLASS wtsInfoClass,
//        out IntPtr ppBuffer,
//        out int pBytesReturned);

//    [DllImport("wtsapi32.dll", SetLastError = true)]
//    private static extern bool WTSEnumerateSessions(
//        IntPtr hServer,
//        int reserved,
//        int version,
//        out IntPtr ppSessionInfo,
//        out int pCount);

//    [DllImport("wtsapi32.dll")]
//    private static extern void WTSFreeMemory(IntPtr pMemory);
//}


public static class ResponsibleResolver
{
    public static string? GetResponsible()
    {
        // 1) ✅ Mejor opción: usuario de sesión activa -> Display Name
        var displayFromSession = GetDisplayNameFromActiveSession();
        if (!string.IsNullOrWhiteSpace(displayFromSession))
            return displayFromSession;

        // 2) Fallbacks: obtengo cuenta y trato de convertir a DisplayName
        var current = WindowsIdentity.GetCurrent()?.Name;
        if (!string.IsNullOrWhiteSpace(current) && !IsSystemIdentity(current))
            return OnlyDisplayNameOrUserPart(current);

        var wtsUser = GetUserFromActiveWtsSession();
        if (!string.IsNullOrWhiteSpace(wtsUser))
            return OnlyDisplayNameOrUserPart(wtsUser);

        var explorerUser = GetInteractiveUserFromExplorerActiveSession();
        if (!string.IsNullOrWhiteSpace(explorerUser))
            return OnlyDisplayNameOrUserPart(explorerUser);

        var regUser = GetLastLoggedOnFromRegistry();
        if (!string.IsNullOrWhiteSpace(regUser))
            return OnlyDisplayNameOrUserPart(regUser.Trim());

        var wmiUser = GetWmiUserName();
        if (!string.IsNullOrWhiteSpace(wmiUser))
            return OnlyDisplayNameOrUserPart(wmiUser);

        return null;
    }

    private static string OnlyDisplayNameOrUserPart(string accountOrDisplay)
    {
        var input = (accountOrDisplay ?? "").Trim();
        if (string.IsNullOrWhiteSpace(input)) return null;

        // si ya parece nombre (tiene espacios y no tiene \)
        if (!input.Contains('\\') && input.Contains(' '))
            return input;

        var display = TryGetDisplayName(input);
        if (!string.IsNullOrWhiteSpace(display))
            return display.Trim();

        // si no se pudo resolver, devuelve al menos la parte "usuario" sin dominio
        return ExtractUserPart(input);
    }

    private static string ExtractUserPart(string s)
    {
        var t = s.Trim();
        if (t.StartsWith(@".\", StringComparison.Ordinal)) t = t.Substring(2);
        var idx = t.IndexOf('\\');
        if (idx >= 0 && idx < t.Length - 1) return t.Substring(idx + 1);
        return t;
    }

    // --------- DISPLAY NAME desde sesión activa (SYSTEM) ---------

    private static string? GetDisplayNameFromActiveSession()
    {
        var sessionId = GetActiveSessionId();
        if (sessionId < 0) return null;

        if (!WTSQueryUserToken((uint)sessionId, out var hPrimaryToken) || hPrimaryToken == IntPtr.Zero)
            return null;

        try
        {
            if (!DuplicateTokenEx(
                    hPrimaryToken,
                    TOKEN_QUERY | TOKEN_DUPLICATE | TOKEN_IMPERSONATE,
                    IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenImpersonation,
                    out var hImpersonationToken))
            {
                return null;
            }

            using var safeToken = new SafeAccessTokenHandle(hImpersonationToken);

            string? result = null;
            WindowsIdentity.RunImpersonated(safeToken, () =>
            {
                result = GetUserNameExString(EXTENDED_NAME_FORMAT.NameDisplay)
                      ?? GetUserNameExString(EXTENDED_NAME_FORMAT.NameSamCompatible)
                      ?? WindowsIdentity.GetCurrent().Name;
            });

            // Si vuelve tipo "DOMINIO\usuario", quita dominio (porque quieres solo nombre)
            if (!string.IsNullOrWhiteSpace(result) && result.Contains('\\') && !result.Contains(' '))
                return ExtractUserPart(result);

            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        }
        finally
        {
            CloseHandle(hPrimaryToken);
        }
    }

    private static string? GetUserNameExString(EXTENDED_NAME_FORMAT fmt)
    {
        uint size = 0;
        GetUserNameEx(fmt, null, ref size);
        if (size == 0) return null;

        var sb = new StringBuilder((int)size);
        if (!GetUserNameEx(fmt, sb, ref size)) return null;

        var s = sb.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    // --------- Tu código existente (solo dejo lo necesario) ---------

    private static bool IsSystemIdentity(string name)
        => name.Equals(@"NT AUTHORITY\SYSTEM", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(@"\SYSTEM", StringComparison.OrdinalIgnoreCase);

    private static string? GetUserFromActiveWtsSession()
    {
        try
        {
            var activeSessionId = GetActiveSessionId();
            if (activeSessionId < 0) return null;

            var user = WtsQueryString(activeSessionId, WTS_INFO_CLASS.WTSUserName);
            if (string.IsNullOrWhiteSpace(user)) return null;

            var domain = WtsQueryString(activeSessionId, WTS_INFO_CLASS.WTSDomainName);
            if (string.IsNullOrWhiteSpace(domain)) domain = "UNKNOWN";

            return $"{domain}\\{user}";
        }
        catch { return null; }
    }

    private static int GetActiveSessionId()
    {
        // En workstation normalmente basta consola activa
        var console = (int)WTSGetActiveConsoleSessionId();
        return console == unchecked((int)0xFFFFFFFF) ? -1 : console;
    }

    private static string? WtsQueryString(int sessionId, WTS_INFO_CLASS infoClass)
    {
        if (!WTSQuerySessionInformation(IntPtr.Zero, sessionId, infoClass, out var buffer, out _))
            return null;

        try
        {
            // ✅ Unicode
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static string? GetInteractiveUserFromExplorerActiveSession()
    {
        try
        {
            var activeSessionId = GetActiveSessionId();
            var explorer = Process.GetProcessesByName("explorer")
                .FirstOrDefault(p => p.SessionId == activeSessionId);

            if (explorer == null) return null;

            using var mo = new ManagementObject($"win32_process.handle='{explorer.Id}'");
            using var outParams = mo.InvokeMethod("GetOwner", null, null);

            var user = outParams?["User"]?.ToString();
            var domain = outParams?["Domain"]?.ToString();

            return string.IsNullOrWhiteSpace(user) ? null : $"{domain}\\{user}";
        }
        catch { return null; }
    }

    private static string? GetLastLoggedOnFromRegistry()
    {
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI";
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(keyPath);
            if (k == null) return null;

            return k.GetValue("LastLoggedOnDisplayName")?.ToString()
                ?? k.GetValue("LastLoggedOnUser")?.ToString()
                ?? k.GetValue("LastLoggedOnSAMUser")?.ToString();
        }
        catch { return null; }
    }

    private static string? GetWmiUserName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
            foreach (ManagementObject mo in searcher.Get())
                return mo["UserName"]?.ToString();
        }
        catch { }
        return null;
    }

    // ✅ Aquí puedes dejar tu TryGetDisplayName actual o mejorarlo luego.
    // Por ahora solo lo “declaro” para que compile si ya lo tienes.
    private static string? TryGetDisplayName(string account) => null;

    // --------- P/Invokes ---------

    private enum WTS_INFO_CLASS { WTSUserName = 5, WTSDomainName = 7 }

    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_IMPERSONATE = 0x0004;

    private enum TOKEN_TYPE { TokenPrimary = 1, TokenImpersonation = 2 }
    private enum SECURITY_IMPERSONATION_LEVEL { SecurityAnonymous = 0, SecurityIdentification = 1, SecurityImpersonation = 2, SecurityDelegation = 3 }
    private enum EXTENDED_NAME_FORMAT { NameUnknown = 0, NameFullyQualifiedDN = 1, NameSamCompatible = 2, NameDisplay = 3, NameUserPrincipal = 8 }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint SessionId, out IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
        TOKEN_TYPE TokenType,
        out IntPtr phNewToken);

    [DllImport("secur32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserNameEx(EXTENDED_NAME_FORMAT nameFormat, StringBuilder? lpNameBuffer, ref uint nSize);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        int sessionId,
        WTS_INFO_CLASS wtsInfoClass,
        out IntPtr ppBuffer,
        out int pBytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
