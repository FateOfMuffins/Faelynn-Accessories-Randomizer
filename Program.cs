using System;
using System.Reflection;
using System.Windows;

[assembly: AssemblyTitle("Faelynn Accessories Randomizer")]
[assembly: AssemblyProduct("Faelynn Accessories Randomizer")]
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]

namespace FaelynnRandomizer
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var installation = new Installation();
            try
            {
                if (args.Length == 1 && args[0] == "--watch")
                {
                    Watcher.Run(installation);
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--self-test")
                {
                    Watcher.SelfTest();
                    Installation.SelfTest(args[1]);
                    SetupForm.SelfTest(args[1]);
                    return 0;
                }
                int value;
                if ((args.Length == 2 || args.Length == 3) && args[0] == "--install" &&
                    Int32.TryParse(args[1], out value) &&
                    (args.Length == 2 || args[2] == "--start-at-sign-in"))
                {
                    installation.Apply(value, args.Length == 3);
                    return 0;
                }
                if (args.Length == 1 && args[0] == "--uninstall")
                {
                    installation.Remove();
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--preview" &&
                    Int32.TryParse(args[1], out value) && value >= 1 && value <= 28)
                {
                    Watcher.Preview(value);
                    return 0;
                }
                if (args.Length != 0) return 2;

                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                return app.Run(new SetupForm(installation));
            }
            catch (Exception error)
            {
                if (args.Length == 0)
                    MessageBox.Show(error.Message, Installation.Product, MessageBoxButton.OK, MessageBoxImage.Error);
                else if (args[0] == "--self-test")
                {
                    System.IO.Directory.CreateDirectory(args[1]);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(args[1], "failure.txt"), error.ToString());
                }
                else
                    installation.LogError("Randomizer stopped", error);
                return 1;
            }
        }
    }
}
