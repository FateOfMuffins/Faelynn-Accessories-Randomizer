using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FaelynnRandomizer
{
    internal sealed class SetupForm : Window
    {
        private static readonly Brush Ink = Brushes.WhiteSmoke;
        private static readonly Brush Dark = new SolidColorBrush(Color.FromArgb(245, 29, 31, 33));
        private static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(255, 157, 94));

        private readonly Installation installation;
        private readonly Func<bool> watcherRunning;
        private readonly TextBox delay;
        private readonly CheckBox startAtSignIn;
        private readonly TextBlock status;
        private readonly Border statusBadge;
        private readonly Button apply, close, uninstall, start;
        private readonly StackPanel controls;
        private int savedDelay;
        private bool updateAvailable, restoringSignIn;

        internal SetupForm(Installation installation) : this(installation, () => installation.WatcherRunning) { }

        private SetupForm(Installation installation, Func<bool> watcherRunning)
        {
            this.installation = installation;
            this.watcherRunning = watcherRunning;
            savedDelay = installation.ReadDelay();
            updateAvailable = installation.HasUpdate;
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

            var surface = new Grid();
            Content = surface;
            var art = new Image { Source = LoadSplash(), Stretch = Stretch.Uniform };
            art.MouseLeftButtonDown += delegate {
                CommitDelay();
                if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove();
            };
            surface.Children.Add(art);

            controls = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 14, 14, 0)
            };
            surface.Children.Add(controls);
            SizeChanged += delegate {
                // Keep the controls off the artwork on narrow screens.
                art.Margin = ActualWidth < 680 ? new Thickness(0, 120, 0, 0) : new Thickness(0);
                controls.Margin = new Thickness(0, ActualWidth < 680 ? 14 : 40,
                    Math.Max(14, ActualWidth - 752), 0);
            };

            status = new TextBlock { Foreground = Ink };
            statusBadge = new Border {
                Background = Dark, CornerRadius = new CornerRadius(4), Padding = new Thickness(9, 4, 9, 4),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 5),
                Child = status, Visibility = Visibility.Collapsed
            };
            controls.Children.Add(statusBadge);

            delay = new TextBox {
                Text = savedDelay.ToString(CultureInfo.InvariantCulture), Width = 48, Height = 25,
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
            uninstall = ActionButton("Uninstall", 72, false);
            uninstall.Click += Remove;
            close = ActionButton("Close", 70, false);
            close.Click += delegate { Close(); };
            start = ActionButton("Start", 68, false);
            start.Click += StartWatcher;
            apply = ActionButton("", 96, true);
            apply.IsDefault = true;
            apply.Click += InstallOrUpdate;
            actions.Children.Add(uninstall);
            actions.Children.Add(close);
            actions.Children.Add(start);
            actions.Children.Add(apply);
            controls.Children.Add(actions);

            PreviewKeyDown += delegate(object sender, KeyEventArgs args) {
                if (args.Key == Key.Escape) { args.Handled = true; Close(); }
            };
            startAtSignIn.Checked += SignInChanged;
            startAtSignIn.Unchecked += SignInChanged;
            delay.LostKeyboardFocus += delegate { CommitDelay(); };
            delay.PreviewKeyDown += delegate(object sender, KeyEventArgs args) {
                if (args.Key != Key.Enter || !installation.IsInstalled) return;
                args.Handled = true;
                CommitDelay();
            };
            var watchStatus = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            watchStatus.Tick += delegate { Refresh(); };
            Loaded += delegate { watchStatus.Start(); };
            Closed += delegate { watchStatus.Stop(); };
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

        private bool TryReadDelay(out int seconds)
        {
            if (Int32.TryParse(delay.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) &&
                seconds >= Installation.MinDelay && seconds <= Installation.MaxDelay) return true;
            SetStatus("Enter 1 to 600 seconds.");
            return false;
        }

        private void InstallOrUpdate(object sender, RoutedEventArgs args)
        {
            int seconds;
            if (!TryReadDelay(out seconds)) { delay.Focus(); return; }
            try
            {
                bool wasInstalled = installation.IsInstalled;
                installation.Apply(seconds, startAtSignIn.IsChecked == true);
                savedDelay = seconds;
                updateAvailable = false;
                Refresh();
                SetStatus(wasInstalled ? "Updated." : "Installed.");
            }
            catch (Exception error) { ShowError(error); }
        }

        private void CommitDelay()
        {
            int seconds;
            if (!installation.IsInstalled || !TryReadDelay(out seconds)) return;
            try
            {
                if (seconds != savedDelay)
                {
                    installation.SaveDelay(seconds);
                    savedDelay = seconds;
                }
                if (status.Text == "Enter 1 to 600 seconds." || status.Text == "Couldn't save the delay.")
                    statusBadge.Visibility = Visibility.Collapsed;
            }
            catch (Exception error)
            {
                installation.LogError("Delay", error);
                SetStatus("Couldn't save the delay.");
            }
        }

        private void SignInChanged(object sender, RoutedEventArgs args)
        {
            if (restoringSignIn) return;
            if (installation.IsInstalled)
            {
                try { installation.SaveSignIn(startAtSignIn.IsChecked == true); }
                catch (Exception error)
                {
                    installation.LogError("Sign-in", error);
                    restoringSignIn = true;
                    try { startAtSignIn.IsChecked = installation.StartsAtSignIn; }
                    finally { restoringSignIn = false; }
                    SetStatus("Couldn't change sign-in startup.");
                }
            }
            Refresh();
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

        private void StartWatcher(object sender, RoutedEventArgs args)
        {
            try
            {
                installation.StartWatcher();
                start.Content = "Starting";
                start.IsEnabled = false;
            }
            catch (Exception error) { ShowError(error); }
        }

        private void Refresh()
        {
            bool installed = installation.IsInstalled;
            RefreshControls(installed, installed && watcherRunning());
        }

        private void RefreshControls(bool installed, bool running)
        {
            apply.Content = installed ? "Update" : "Install";
            apply.Visibility = !installed || updateAvailable ? Visibility.Visible : Visibility.Collapsed;
            apply.Width = installed ? 64 : 96;
            close.Width = installed ? 52 : 70;
            uninstall.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
            start.Visibility = installed && (startAtSignIn.IsChecked != true || !running)
                ? Visibility.Visible : Visibility.Collapsed;
            start.Content = running ? "Running" : "Start";
            start.IsEnabled = !running;
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
            var test = new Installation(Path.Combine(scratch, "setup-app"),
                Path.Combine(scratch, "setup-startup"), Path.Combine(scratch, "setup-programs"));
            test.Apply(Installation.DefaultDelay, true, false);
            bool running = true;
            var window = new SetupForm(test, () => running) {
                ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000
            };
            bool closed = false;
            window.Closed += delegate { closed = true; };
            window.Show();
            try
            {
                double widthBefore = window.Width, heightBefore = window.Height;
                window.startAtSignIn.IsChecked = false;
                if (test.StartsAtSignIn || window.apply.Visibility != Visibility.Collapsed)
                    throw new InvalidOperationException("The sign-in setting must save without a Save button.");
                window.startAtSignIn.IsChecked = true;
                if (!test.StartsAtSignIn)
                    throw new InvalidOperationException("Turning sign-in startup back on must save immediately.");
                window.startAtSignIn.IsChecked = false;
                if (window.start.IsEnabled || (string)window.start.Content != "Running")
                    throw new InvalidOperationException("The Start button must show Running when the watcher is running.");
                running = false;
                window.Refresh();
                if (!window.start.IsEnabled || window.start.Visibility != Visibility.Visible)
                    throw new InvalidOperationException("The stopped watcher must have a Start button.");

                var lostFocus = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, window.delay, window.close)
                    { RoutedEvent = Keyboard.LostKeyboardFocusEvent };
                window.delay.Text = "47";
                window.delay.RaiseEvent(lostFocus);
                if (test.ReadDelay() != 47)
                    throw new InvalidOperationException("Leaving the delay field must save the new value.");
                window.delay.Text = "0";
                window.delay.RaiseEvent(lostFocus);
                if (test.ReadDelay() != 47 || window.statusBadge.Visibility != Visibility.Visible)
                    throw new InvalidOperationException("An invalid delay must leave the saved value alone.");
                window.delay.Text = "30";
                var enter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window.delay),
                    0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                window.delay.RaiseEvent(enter);
                if (test.ReadDelay() != 30 || !enter.Handled)
                    throw new InvalidOperationException("Enter must save the delay.");

                window.updateAvailable = true;
                window.Refresh();
                if (window.apply.Visibility != Visibility.Visible || (string)window.apply.Content != "Update")
                    throw new InvalidOperationException("A newer setup build must offer Update.");
                window.SetStatus("Enter 1 to 600 seconds.");
                window.CheckArtClear();
                window.Width = 620;
                window.Height = 620.0 * 941 / 1672;
                window.CheckArtClear();
                window.Width = widthBefore;
                window.Height = heightBefore;
                window.updateAvailable = false;
                window.Refresh();
                window.statusBadge.Visibility = Visibility.Collapsed;
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
            finally
            {
                if (!closed) window.Close();
                test.Remove(false);
            }
        }

        private void CheckArtClear()
        {
            UpdateLayout();
            var surface = (FrameworkElement)Content;
            var bounds = controls.TransformToAncestor(surface).TransformBounds(
                new Rect(0, 0, controls.ActualWidth, controls.ActualHeight));
            var artOnly = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth),
                (int)Math.Ceiling(surface.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            controls.Opacity = 0;
            try { artOnly.Render(surface); }
            finally { controls.Opacity = 1; }

            int left = (int)Math.Floor(bounds.Left), top = (int)Math.Floor(bounds.Top);
            int width = (int)Math.Ceiling(bounds.Right) - left;
            int height = (int)Math.Ceiling(bounds.Bottom) - top;
            var pixels = new byte[width * height * 4];
            artOnly.CopyPixels(new Int32Rect(left, top, width, height), pixels, width * 4, 0);
            for (int i = 3; i < pixels.Length; i += 4)
                if (pixels[i] > 8)
                    throw new InvalidOperationException("Setup controls overlap the splash.");
        }
    }
}
