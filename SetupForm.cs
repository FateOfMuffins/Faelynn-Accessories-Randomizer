using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FaelynnRandomizer
{
    internal sealed class SetupForm : Window
    {
        private static readonly Brush Ink = Brushes.WhiteSmoke;
        private static readonly Brush Dark = new SolidColorBrush(Color.FromArgb(245, 29, 31, 33));
        private static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(255, 157, 94));

        private readonly Installation installation;
        private readonly TextBox delay;
        private readonly CheckBox startAtSignIn;
        private readonly TextBlock status;
        private readonly Border statusBadge;
        private readonly Button apply, close, uninstall;

        internal SetupForm(Installation installation)
        {
            this.installation = installation;
            Title = Installation.Product;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 14;
            Foreground = Ink;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = Math.Min(880, Math.Min(SystemParameters.WorkArea.Width * .94,
                SystemParameters.WorkArea.Height * 1.60));
            Height = Width * 941 / 1672;

            // The PNG is the entire window. Only the controls have their own backing.
            var surface = new Grid();
            Content = surface;
            var art = new Image { Source = LoadSplash(), Stretch = Stretch.Uniform };
            art.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            surface.Children.Add(art);

            var controls = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(80, 0, 0, Height * .105)
            };
            surface.Children.Add(controls);

            status = new TextBlock { Foreground = Ink };
            statusBadge = new Border {
                Background = Dark, CornerRadius = new CornerRadius(4), Padding = new Thickness(9, 4, 9, 4),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 5),
                Child = status, Visibility = Visibility.Collapsed
            };
            controls.Children.Add(statusBadge);

            delay = new TextBox {
                Text = installation.ReadDelay().ToString(CultureInfo.InvariantCulture), Width = 48, Height = 25,
                TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 16, 0), MaxLength = 3
            };
            System.Windows.Automation.AutomationProperties.SetName(delay, "Delay in seconds");
            startAtSignIn = new CheckBox {
                Content = "Start at sign-in", Foreground = Ink, VerticalAlignment = VerticalAlignment.Center,
                IsChecked = !installation.IsInstalled || installation.StartsAtSignIn
            };
            var settings = new StackPanel { Orientation = Orientation.Horizontal };
            settings.Children.Add(new TextBlock { Text = "Delay (s)", VerticalAlignment = VerticalAlignment.Center });
            settings.Children.Add(delay);
            settings.Children.Add(startAtSignIn);
            controls.Children.Add(new Border {
                Background = Dark, CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 6, 10, 6),
                HorizontalAlignment = HorizontalAlignment.Center, Child = settings
            });

            var actions = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            uninstall = ActionButton("Uninstall", 86, false);
            uninstall.Click += Remove;
            close = ActionButton("Close", 70, false);
            close.Click += delegate { Close(); };
            apply = ActionButton("", 96, true);
            apply.IsDefault = true;
            apply.Click += Save;
            actions.Children.Add(uninstall);
            actions.Children.Add(close);
            actions.Children.Add(apply);
            controls.Children.Add(actions);

            PreviewKeyDown += delegate(object sender, KeyEventArgs args) {
                if (args.Key == Key.Escape) { args.Handled = true; Close(); }
            };
            Refresh();
        }

        private static BitmapImage LoadSplash()
        {
            using (Stream stream = typeof(SetupForm).Assembly.GetManifestResourceStream("GF2-Splash.png"))
            {
                if (stream == null) throw new InvalidOperationException("Missing Faelynn splash.");
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private static Button ActionButton(string text, double width, bool primary)
        {
            return new Button {
                Content = text, Width = width, Height = 33, Margin = new Thickness(2, 0, 2, 0),
                Background = primary ? Orange : Dark, Foreground = primary ? Brushes.Black : Ink,
                BorderBrush = primary ? Orange : Brushes.DimGray, BorderThickness = new Thickness(1)
            };
        }

        private void Save(object sender, RoutedEventArgs args)
        {
            int seconds;
            if (!Int32.TryParse(delay.Text, out seconds) || seconds < Installation.MinDelay ||
                seconds > Installation.MaxDelay)
            {
                SetStatus("Enter 1 to 600 seconds.");
                delay.Focus();
                return;
            }
            try
            {
                bool wasInstalled = installation.IsInstalled;
                installation.Apply(seconds, startAtSignIn.IsChecked == true);
                Refresh();
                SetStatus(wasInstalled ? "Saved." : "Installed.");
            }
            catch (Exception error) { ShowError(error); }
        }

        private void Remove(object sender, RoutedEventArgs args)
        {
            if (MessageBox.Show(this, "Uninstall the randomizer?", Installation.Product,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                if (installation.Remove()) { Close(); return; }
                Refresh();
                SetStatus("Uninstalled.");
            }
            catch (Exception error) { ShowError(error); }
        }

        private void Refresh()
        {
            apply.Content = installation.IsInstalled ? "Save" : "Install";
            uninstall.Visibility = installation.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetStatus(string message)
        {
            status.Text = message;
            statusBadge.Visibility = Visibility.Visible;
        }

        private void ShowError(Exception error)
        {
            MessageBox.Show(this, error.Message, Installation.Product, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        internal static void SelfTest(string scratch)
        {
            var window = new SetupForm(new Installation()) {
                ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000
            };
            bool closed = false;
            window.Closed += delegate { closed = true; };
            window.Show();
            try
            {
                window.UpdateLayout();
                var visual = (FrameworkElement)window.Content;
                int width = (int)Math.Ceiling(visual.ActualWidth);
                int height = (int)Math.Ceiling(visual.ActualHeight);
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var corner = new byte[4];
                bitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), corner, 4, 0);
                if (!window.AllowsTransparency || corner[3] != 0)
                    throw new InvalidOperationException("Setup must have no opaque background.");
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(Path.Combine(scratch, "setup-preview.png"))) png.Save(file);
                window.close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!closed) throw new InvalidOperationException("Close did not dismiss setup.");
            }
            finally { if (!closed) window.Close(); }
        }
    }
}
