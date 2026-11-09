using System;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Faelynn Accessories Randomizer")]
[assembly: AssemblyProduct("Faelynn Accessories Randomizer")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

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
                    return 0;
                }
                int roll;
                if (args.Length == 2 && args[0] == "--preview" &&
                    Int32.TryParse(args[1], out roll) && roll >= 1 && roll <= 28)
                {
                    Watcher.Preview(roll);
                    return 0;
                }
                if (args.Length != 0) return 2;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var setup = new SetupForm(installation)) Application.Run(setup);
                return 0;
            }
            catch (Exception error)
            {
                if (args.Length == 0)
                    MessageBox.Show(error.Message, Installation.Product, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
