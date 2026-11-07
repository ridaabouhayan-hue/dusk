using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Dusk
{
    public enum PowerAction { Sleep = 0, Hibernate = 1, Restart = 2, Shutdown = 3, Lock = 4, Logoff = 5 }

    // Wat kan deze pc? Uitgelezen met GetPwrCapabilities (SYSTEM_POWER_CAPABILITIES).
    public struct PowerCaps
    {
        public bool S1, S2, S3, HiberFile, ModernStandby;
        public bool CanSleep { get { return S1 || S2 || S3 || ModernStandby; } }
        public bool CanHibernate { get { return HiberFile; } }

        // Indeling van de structuur: losse BOOLEAN-bytes. S1..S3 op 3..5, HiberFilePresent op 8, AoAc op 20.
        public static PowerCaps FromBytes(byte[] b)
        {
            PowerCaps c = new PowerCaps();
            if (b == null || b.Length < 21) return c;
            c.S1 = b[3] != 0; c.S2 = b[4] != 0; c.S3 = b[5] != 0;
            c.HiberFile = b[8] != 0;
            c.ModernStandby = b[20] != 0;
            return c;
        }
    }

    static class Power
    {
        public const uint EWX_LOGOFF = 0, EWX_SHUTDOWN = 1, EWX_REBOOT = 2, EWX_FORCE = 4, EWX_POWEROFF = 8,
            EWX_FORCEIFHUNG = 0x10, EWX_HYBRID_SHUTDOWN = 0x00400000;
        public const uint SHTDN_REASON_FLAG_PLANNED = 0x80000000;
        const int ScreenOff = 73;   // POWER_INFORMATION_LEVEL, zo doet het startmenu het bij Modern Standby

        public static IntPtr MessageHandle;   // het onzichtbare venstertje van App
        static PowerCaps cached;
        static DateTime cachedAt = DateTime.MinValue;

        public static PowerCaps Caps
        {
            get
            {
                if ((DateTime.UtcNow - cachedAt).TotalSeconds > 30)
                {
                    cached = Read();
                    cachedAt = DateTime.UtcNow;
                }
                return cached;
            }
        }

        static PowerCaps Read()
        {
            try
            {
                byte[] b = new byte[128];
                if (W.GetPwrCapabilities(b)) return PowerCaps.FromBytes(b);
            }
            catch (Exception e) { Log.Error("Stroomopties uitlezen mislukt", e); }
            PowerCaps c = new PowerCaps();
            c.S3 = true; c.HiberFile = true;   // niet te bepalen: niets uitschakelen
            return c;
        }

        // Snel opstarten van Windows aan? Dan sluiten we af zoals het startmenu dat doet (hybride).
        public static bool HiberbootEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power"))
                {
                    object v = k != null ? k.GetValue("HiberbootEnabled") : null;
                    return v is int && (int)v == 1;
                }
            }
            catch { return false; }
        }

        // Welke vlaggen krijgt ExitWindowsEx? Los gezet zodat de tests het kunnen controleren.
        public static uint ExitFlags(PowerAction a, bool forced, bool hiberboot)
        {
            uint f;
            switch (a)
            {
                case PowerAction.Shutdown: f = EWX_SHUTDOWN | EWX_POWEROFF; if (hiberboot) f |= EWX_HYBRID_SHUTDOWN; break;
                case PowerAction.Restart: f = EWX_REBOOT; break;
                case PowerAction.Logoff: f = EWX_LOGOFF; break;
                default: throw new ArgumentException("Geen afsluitactie: " + a);
            }
            // Netjes: programma's mogen vragen of je wilt opslaan. Geforceerd: niet wachten.
            // Een programma dat vastzit houdt het afsluiten in beide gevallen niet tegen.
            f |= forced ? EWX_FORCE : EWX_FORCEIFHUNG;
            return f;
        }

        public static bool Available(PowerAction a)
        {
            if (a == PowerAction.Sleep) return Caps.CanSleep;
            if (a == PowerAction.Hibernate) return Caps.CanHibernate;
            return true;
        }

        // Voert de actie uit. Geeft null bij succes, anders de sleutel van een foutmelding (Lang).
        public static string Run(PowerAction a, bool forced)
        {
            Log.Info("Actie: " + a + (forced ? " (geforceerd)" : ""));
            if (Program.SelfTest) return null;
            try
            {
                switch (a)
                {
                    case PowerAction.Sleep: return Sleep();
                    case PowerAction.Hibernate:
                        if (!Caps.CanHibernate) return "err.hibernate";
                        if (!W.SetSuspendState(true, false, false)) return Fail("SetSuspendState(sluimer)", "err.hibernate");
                        return null;
                    case PowerAction.Lock:
                        if (!W.LockWorkStation()) return Fail("LockWorkStation", "err.generic");
                        return null;
                    default:
                        if (a != PowerAction.Logoff && !EnableShutdownPrivilege()) return "err.privilege";
                        uint flags = ExitFlags(a, forced, a == PowerAction.Shutdown && HiberbootEnabled());
                        if (!W.ExitWindowsEx(flags, SHTDN_REASON_FLAG_PLANNED)) return Fail("ExitWindowsEx", "err.generic");
                        return null;
                }
            }
            catch (Exception e)
            {
                Log.Error("Actie " + a + " mislukt", e);
                return "err.generic";
            }
        }

        static string Sleep()
        {
            PowerCaps c = Caps;
            if (c.ModernStandby)
            {
                // Modern Standby: SetSuspendState werkt hier niet. Het startmenu zet dan het scherm uit,
                // waarna Windows zelf in slaapstand gaat.
                try { if (W.NtPowerInformation(ScreenOff, IntPtr.Zero, 0, IntPtr.Zero, 0) == 0) return null; }
                catch (Exception e) { Log.Error("ScreenOff mislukt", e); }
                // Reserve: het scherm uitzetten via ons eigen venstertje (Windows regelt de rest)
                if (MessageHandle != IntPtr.Zero) W.PostMessage(MessageHandle, W.WM_SYSCOMMAND, new IntPtr(W.SC_MONITORPOWER), new IntPtr(2));
                return null;
            }
            if (!c.CanSleep) return "err.sleep";
            // Let op: nooit via "rundll32 powrprof.dll,SetSuspendState", dat kan in sluimerstand eindigen.
            if (!W.SetSuspendState(false, false, false)) return Fail("SetSuspendState(slaap)", "err.sleep");
            return null;
        }

        static string Fail(string what, string key)
        {
            int err = Marshal.GetLastWin32Error();
            Log.Info(what + " mislukt: " + new Win32Exception(err).Message + " (" + err + ")");
            return key;
        }

        static bool EnableShutdownPrivilege()
        {
            IntPtr token;
            if (!W.OpenProcessToken(W.GetCurrentProcess(), 0x20 | 0x8, out token)) return false;   // ADJUST_PRIVILEGES | QUERY
            try
            {
                W.TOKEN_PRIVILEGES tp = new W.TOKEN_PRIVILEGES();
                tp.Count = 1; tp.Attr = 2;   // SE_PRIVILEGE_ENABLED
                if (!W.LookupPrivilegeValue(null, "SeShutdownPrivilege", out tp.Luid)) return false;
                if (!W.AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)) return false;
                return Marshal.GetLastWin32Error() == 0;   // 1300 = niet toegewezen
            }
            finally { W.CloseHandle(token); }
        }
    }
}
