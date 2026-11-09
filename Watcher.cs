using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace FaelynnRandomizer
{
    internal sealed class Launch
    {
        internal readonly DateTime Started;
        private readonly DateTime due;
        internal int Roll;
        internal bool Finished;

        internal Launch(DateTime started, DateTime watchingSince, int delaySeconds)
        {
            Started = started;
            due = started.AddSeconds(delaySeconds);
            Finished = due < watchingSince;
        }

        internal void RollWhenDue(DateTime now, Func<int> roll)
        {
            if (!Finished && Roll == 0 && now >= due)
                Roll = roll();
        }
    }

    internal static class Watcher
    {
        private const string GameName = "GF2_Exilium";
        internal const string StopEvent = @"Local\FaelynnAccessoriesRandomizer.Stop";

        internal static void Run(Installation installation)
        {
            bool first;
            using (var single = new Mutex(true, @"Local\FaelynnAccessoriesRandomizer.Watcher", out first))
            {
                if (!first) { Console.WriteLine("GF2 overlay watcher is already running."); return; }
                try
                {
                    using (var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEvent))
                        Watch(installation, stop);
                }
                finally { single.ReleaseMutex(); }
            }
        }

        private static void Watch(Installation installation, EventWaitHandle stop)
        {
            Native.SetProcessDPIAware();
            var watched = new Dictionary<int, Launch>();
            var random = new Random();
            DateTime watchingSince = DateTime.UtcNow;
            int session;
            using (var current = Process.GetCurrentProcess()) session = current.SessionId;
            while (!stop.WaitOne(0))
            {
                IntPtr foreground = Native.GetForegroundWindow();
                int activePid;
                Native.GetWindowThreadProcessId(foreground, out activePid);
                var present = new HashSet<int>();
                Launch ready = null;
                foreach (Process process in Process.GetProcessesByName(GameName))
                {
                    using (process)
                    {
                        DateTime started;
                        int pid;
                        try
                        {
                            // HasExited can return Access Denied for GF2.
                            if (process.SessionId != session) continue;
                            pid = process.Id;
                            started = process.StartTime.ToUniversalTime();
                        }
                        catch (Win32Exception) { continue; }
                        catch (InvalidOperationException) { continue; }
                        present.Add(pid);

                        Launch launch;
                        if (!watched.TryGetValue(pid, out launch) || launch.Started != started)
                        {
                            // Delay changes apply to the next launch.
                            watched[pid] = launch = new Launch(started, watchingSince, installation.ReadDelay());
                        }
                        launch.RollWhenDue(DateTime.UtcNow, delegate { return random.Next(1, 29); });
                        if (activePid == pid && launch.Roll != 0 && !launch.Finished) ready = launch;
                    }
                }

                foreach (int pid in new List<int>(watched.Keys))
                    if (!present.Contains(pid)) watched.Remove(pid);
                if (ready != null && !stop.WaitOne(0))
                {
                    try { ready.Finished = Show(foreground, ready.Roll, 0, stop); }
                    catch (Exception error)
                    {
                        ready.Finished = true;
                        installation.LogError("Could not show the overlay", error);
                    }
                }
                if (stop.WaitOne(1000)) break;
            }
        }

        private static bool Show(IntPtr game, int roll, int previewMs, EventWaitHandle stop)
        {
            if (game == IntPtr.Zero || Native.GetForegroundWindow() != game) return false;
            using (var overlay = new Overlay(game, roll, previewMs, stop))
            {
                if (Native.GetForegroundWindow() != game) return false;
                Application.Run(overlay);
                if (overlay.Failure != null) throw overlay.Failure;
                return overlay.Dismissed;
            }
        }

        internal static void Preview(int roll)
        {
            Native.SetProcessDPIAware();
            var wait = Stopwatch.StartNew();
            Console.WriteLine("Preview roll {0}: waiting for GF2 to be in the foreground.", roll);
            while (wait.Elapsed < TimeSpan.FromSeconds(35))
            {
                IntPtr game = Native.GetForegroundWindow();
                int pid;
                if (Native.GetWindowThreadProcessId(game, out pid) != 0 && IsGame(pid))
                {
                    Show(game, roll, 18000, null);
                    return;
                }
                Thread.Sleep(100);
            }
            throw new InvalidOperationException("GF2 did not come to the foreground. No overlay was shown.");
        }

        private static bool IsGame(int pid)
        {
            try { using (var process = Process.GetProcessById(pid)) return process.ProcessName == GameName; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (Win32Exception) { return false; }
        }

        internal static void SelfTest()
        {
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
            for (int z = 0; z < 3; z++)
            {
                int roll = 1 + x * 9 + y * 3 + z;
                Check(Art.Column(roll, 0) == x && Art.Column(roll, 1) == y && Art.Column(roll, 2) == z,
                    "grid mapping " + roll);
            }

            DateTime start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var launch = new Launch(start, start.AddSeconds(3), Installation.DefaultDelay);
            int rolls = 0;
            Func<int> rollOnce = delegate { rolls++; return 28; };
            launch.RollWhenDue(start.AddMilliseconds(29999), rollOnce);
            Check(rolls == 0, "no roll before 30 seconds");
            launch.RollWhenDue(start.AddSeconds(30), rollOnce);
            launch.RollWhenDue(start.AddMinutes(2), rollOnce);
            Check(rolls == 1 && launch.Roll == 28, "roll exactly once and retain a pending result");
            var old = new Launch(start, start.AddSeconds(31), Installation.DefaultDelay);
            old.RollWhenDue(start.AddMinutes(2), rollOnce);
            Check(old.Finished && old.Roll == 0 && rolls == 1, "ignore a game already past its minute on startup");
            var fresh = new Launch(start.AddMinutes(4), start.AddMinutes(1), 45);
            fresh.RollWhenDue(start.AddMinutes(4).AddSeconds(44), rollOnce);
            Check(fresh.Roll == 0, "custom delay does not use the 30-second default");
            fresh.RollWhenDue(start.AddMinutes(4).AddSeconds(45), rollOnce);
            Check(fresh.Roll == 28 && rolls == 2, "new launch honors its custom delay");

            using (Bitmap grid = Art.Render(4, new Size(1920, 1080)))
            using (Bitmap splash = Art.Render(28, new Size(1920, 1080)))
            {
                Check(grid.Height > grid.Width && splash.Width > splash.Height, "panel and splash aspect ratios");
                Check(grid.GetPixel(0, 0).A <= 2 && splash.GetPixel(0, 0).A <= 2, "transparent asset corners");
                using (var gridSurface = new LayeredSurface(grid))
                using (var splashSurface = new LayeredSurface(splash)) { }
            }
            Console.WriteLine("Passed: 27 mappings, launch timing and one-roll behavior, both transparent art surfaces.");
        }

        private static void Check(bool passed, string test)
        {
            if (!passed) throw new InvalidOperationException("Self-test failed: " + test);
        }
    }
}
