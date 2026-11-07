using System;
using System.IO;
using System.Text;

namespace Dusk
{
    // Klein logboek in %APPDATA%\Dusk\log.txt. Alleen fouten en belangrijke stappen, nooit persoonlijke gegevens.
    static class Log
    {
        static readonly object Gate = new object();

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dusk"); }
        }

        public static void Info(string msg) { Write("INFO ", msg); }

        public static void Error(string msg, Exception e)
        {
            Write("FOUT ", msg + (e != null ? ": " + e : ""));
        }

        static void Write(string kind, string msg)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Dir);
                    string file = Path.Combine(Dir, "log.txt");
                    FileInfo fi = new FileInfo(file);
                    if (fi.Exists && fi.Length > 256 * 1024)
                    {
                        string old = Path.Combine(Dir, "log-oud.txt");
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(file, old);
                    }
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + kind + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
