using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

namespace FaelynnRandomizer
{
    internal sealed class Installation
    {
        internal const string Product = "Faelynn Accessories Randomizer";
        internal const int DefaultDelay = 30;
        internal const int MinDelay = 1;
        internal const int MaxDelay = 600;
        private const string ShortcutName = Product + ".lnk";
        private readonly string directory, startup, programs;
        private string Executable { get { return Path.Combine(directory, "FaelynnRandomizer.exe"); } }
        private string DelayFile { get { return Path.Combine(directory, "delay-seconds.txt"); } }
        private string StartupLink { get { return Path.Combine(startup, ShortcutName); } }
        private string ProgramsLink { get { return Path.Combine(programs, ShortcutName); } }
        private string ErrorLog { get { return Path.Combine(directory, "errors.log"); } }
        internal bool IsInstalled { get { return File.Exists(Executable); } }
        internal bool StartsAtSignIn { get { return File.Exists(StartupLink); } }
        internal bool WatcherRunning { get { return HasSignal(Watcher.StopEvent); } }

        internal Installation() : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FaelynnAccessoriesRandomizer"),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs)) { }

        internal Installation(string directory, string startup, string programs)
        {
            this.directory = directory;
            this.startup = startup;
            this.programs = programs;
        }

        internal int ReadDelay()
        {
            try { return ParseDelay(File.ReadAllText(DelayFile)); }
            catch (IOException) { return DefaultDelay; }
            catch (UnauthorizedAccessException) { return DefaultDelay; }
        }

        private static int ParseDelay(string value)
        {
            int seconds;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)
                && seconds >= MinDelay && seconds <= MaxDelay ? seconds : DefaultDelay;
        }

        internal void Apply(int delay, bool atSignIn, bool manageWatcher = true)
        {
            ValidateDelay(delay);
            if (manageWatcher) CheckForLegacyWatcher();
            bool wasInstalled = IsInstalled;
            bool wasRunning = manageWatcher && WatcherRunning;

            Directory.CreateDirectory(directory);
            string source = System.Windows.Forms.Application.ExecutablePath;
            bool sameFile = String.Equals(source, Executable, StringComparison.OrdinalIgnoreCase);
            if (!sameFile && (!wasInstalled || HasUpdate))
            {
                if (manageWatcher) StopWatcher();
                string staged = Executable + ".new";
                try { File.Copy(source, staged, true); Replace(staged, Executable); }
                finally { File.Delete(staged); }
            }

            SaveDelay(delay);
            WriteShortcut(ProgramsLink, "");
            SaveSignIn(atSignIn);

            if (manageWatcher && (!wasInstalled || wasRunning)) StartWatcher();
        }

        internal bool HasUpdate
        {
            get
            {
                string source = System.Windows.Forms.Application.ExecutablePath;
                return IsInstalled && !String.Equals(source, Executable, StringComparison.OrdinalIgnoreCase)
                    && !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(Executable));
            }
        }

        private static void ValidateDelay(int delay)
        {
            if (delay < MinDelay || delay > MaxDelay) throw new ArgumentOutOfRangeException("delay");
        }

        internal void SaveDelay(int delay)
        {
            ValidateDelay(delay);
            string pending = DelayFile + ".new";
            try
            {
                File.WriteAllText(pending, delay.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
                Replace(pending, DelayFile);
            }
            finally { File.Delete(pending); }
        }

        internal void SaveSignIn(bool atSignIn)
        {
            if (atSignIn) WriteShortcut(StartupLink, "--watch");
            else File.Delete(StartupLink);
        }

        internal void StartWatcher()
        {
            if (!IsInstalled) throw new InvalidOperationException("Install the randomizer first.");
            CheckForLegacyWatcher();
            if (WatcherRunning) return;
            using (Process.Start(new ProcessStartInfo(Executable, "--watch") {
                UseShellExecute = false, WorkingDirectory = directory })) { }
        }

        private void CheckForLegacyWatcher()
        {
            if (HasSignal(@"Local\GF2AccessoryOverlay.Stop") ||
                File.Exists(Path.Combine(startup, "GF2 Overlay Watcher.lnk")))
                throw new InvalidOperationException("Stop the old GF2 watcher and remove its Startup shortcut first.");
        }

        internal bool Remove(bool stopWatcher = true)
        {
            File.Delete(StartupLink);
            File.Delete(ProgramsLink);
            if (stopWatcher) StopWatcher();
            File.Delete(DelayFile);
            File.Delete(ErrorLog);
            if (!IsInstalled) return false;
            if (!String.Equals(System.Windows.Forms.Application.ExecutablePath, Executable,
                StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(Executable);
                return false;
            }

            // The installed executable can only be deleted after it exits.
            int pid;
            using (var current = Process.GetCurrentProcess()) pid = current.Id;
            string command = "Wait-Process -Id " + pid.ToString(CultureInfo.InvariantCulture)
                + " -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '"
                + Executable.Replace("'", "''") + "' -ErrorAction Stop";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            using (Process.Start(new ProcessStartInfo(powershell, "-NoProfile -NonInteractive -EncodedCommand " + encoded)
                { UseShellExecute = false, CreateNoWindow = true })) { }
            return true;
        }

        private static void Replace(string staged, string target)
        {
            if (File.Exists(target)) File.Replace(staged, target, null);
            else File.Move(staged, target);
        }

        private void WriteShortcut(string path, string arguments)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic shortcut = shell.CreateShortcut(path);
                try
                {
                    shortcut.TargetPath = Executable;
                    shortcut.WorkingDirectory = directory;
                    shortcut.Arguments = arguments;
                    shortcut.Description = Product;
                    shortcut.Save();
                }
                finally { Marshal.FinalReleaseComObject(shortcut); }
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }

        private static bool HasSignal(string name)
        {
            EventWaitHandle signal;
            if (!EventWaitHandle.TryOpenExisting(name, out signal)) return false;
            signal.Dispose();
            return true;
        }

        private static void StopWatcher()
        {
            EventWaitHandle signal;
            if (!EventWaitHandle.TryOpenExisting(Watcher.StopEvent, out signal)) return;
            using (signal) signal.Set();

            var wait = Stopwatch.StartNew();
            while (HasSignal(Watcher.StopEvent))
            {
                if (wait.ElapsedMilliseconds >= 5000)
                    throw new InvalidOperationException("The overlay watcher did not stop. Try again in a moment.");
                Thread.Sleep(50);
            }
        }

        internal void LogError(string context, Exception error)
        {
            try
            {
                Directory.CreateDirectory(directory);
                if (File.Exists(ErrorLog) && new FileInfo(ErrorLog).Length >= 65536) File.Delete(ErrorLog);
                File.AppendAllText(ErrorLog, DateTime.UtcNow.ToString("O") + " " + context + ": "
                    + error.Message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static void SelfTest(string scratch)
        {
            var test = new Installation(Path.Combine(scratch, "app"),
                Path.Combine(scratch, "startup"), Path.Combine(scratch, "programs"));
            if (test.ReadDelay() != 30 || ParseDelay("0") != 30 || ParseDelay("invalid") != 30)
                throw new InvalidOperationException("Default and invalid timer settings must return 30 seconds.");
            test.Apply(30, true, false);
            if (!test.IsInstalled || !test.StartsAtSignIn || !File.Exists(test.ProgramsLink))
                throw new InvalidOperationException("Install did not create the executable and shortcuts.");
            test.Apply(47, false, false);
            if (test.ReadDelay() != 47 || test.StartsAtSignIn || !File.Exists(test.ProgramsLink))
                throw new InvalidOperationException("Saving settings did not update the delay and Startup shortcut.");
            test.Remove(false);
            if (test.IsInstalled || test.StartsAtSignIn || File.Exists(test.ProgramsLink) || File.Exists(test.DelayFile))
                throw new InvalidOperationException("Uninstall did not remove the installed files.");
        }
    }
}
