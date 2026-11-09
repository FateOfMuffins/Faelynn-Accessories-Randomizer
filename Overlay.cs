using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FaelynnRandomizer
{
    internal sealed class LayeredSurface : IDisposable
    {
        private IntPtr dc, dib, original;
        private readonly int width, height;

        internal LayeredSurface(Bitmap bitmap)
        {
            width = bitmap.Width;
            height = bitmap.Height;
            try
            {
                dc = Native.CreateCompatibleDC(IntPtr.Zero);
                if (dc == IntPtr.Zero) throw new Win32Exception();
                var info = new Native.BitmapInfo {
                    Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32
                };
                IntPtr pixels;
                dib = Native.CreateDIBSection(dc, ref info, 0, out pixels, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || pixels == IntPtr.Zero) throw new Win32Exception();

                // Clear near-zero alpha so invisible pixels don't catch clicks.
                var data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    int length = width * 4;
                    var row = new byte[length];
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, length);
                        for (int i = 0; i < length; i += 4)
                            if (row[i + 3] <= 2) Array.Clear(row, i, 4);
                        Marshal.Copy(row, 0, IntPtr.Add(pixels, y * length), length);
                    }
                }
                finally { bitmap.UnlockBits(data); }

                original = Native.SelectObject(dc, dib);
                if (original == IntPtr.Zero || original == new IntPtr(-1))
                {
                    original = IntPtr.Zero;
                    throw new Win32Exception();
                }
            }
            catch { Dispose(); throw; }
        }

        internal void Present(IntPtr window, Rectangle bounds, byte opacity)
        {
            var dest = new Native.Point(bounds.Left, bounds.Top);
            var size = new Native.Point(width, height);
            var src = new Native.Point(0, 0);
            var blend = new Native.Blend { Opacity = opacity, AlphaFormat = 1 };
            if (!Native.UpdateLayeredWindow(window, IntPtr.Zero, ref dest, ref size, dc,
                ref src, 0, ref blend, 2)) // ULW_ALPHA
                throw new Win32Exception();
        }

        public void Dispose()
        {
            if (original != IntPtr.Zero) { Native.SelectObject(dc, original); original = IntPtr.Zero; }
            if (dib != IntPtr.Zero) { Native.DeleteObject(dib); dib = IntPtr.Zero; }
            if (dc != IntPtr.Zero) { Native.DeleteDC(dc); dc = IntPtr.Zero; }
        }
    }

    internal sealed class Overlay : Form
    {
        private const double FadeMs = 350;
        private readonly IntPtr game;
        private readonly int previewMs;
        private readonly EventWaitHandle stop;
        private readonly LayeredSurface art;
        private readonly Rectangle placement;
        private readonly Stopwatch elapsed = new Stopwatch();
        private readonly System.Windows.Forms.Timer frame = new System.Windows.Forms.Timer();
        private double fadeOutAt = -1;
        private byte alpha = 1, fadeFrom;
        private bool raisedAgain;
        internal bool Dismissed { get; private set; }
        internal Exception Failure { get; private set; }

        internal Overlay(IntPtr game, int roll, int previewMs, EventWaitHandle stop)
        {
            this.game = game;
            this.previewMs = previewMs;
            this.stop = stop;
            Rectangle area = Screen.FromHandle(game).Bounds;
            Native.Bounds gameBounds;
            if (Native.GetWindowRect(game, out gameBounds))
            {
                Rectangle clipped = Rectangle.Intersect(area, Rectangle.FromLTRB(
                    gameBounds.Left, gameBounds.Top, gameBounds.Right, gameBounds.Bottom));
                if (clipped.Width > 200 && clipped.Height > 200) area = clipped;
            }

            using (Bitmap image = Art.Render(roll, area.Size))
            {
                placement = new Rectangle(area.Left + (area.Width - image.Width) / 2,
                    area.Top + (area.Height - image.Height) / 2, image.Width, image.Height);
                art = new LayeredSurface(image);
            }
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = placement;
            ShowInTaskbar = false;
            TopMost = true;
            Cursor = Cursors.Hand;
            Text = "GF2 overlay";
            frame.Interval = 16;
            frame.Tick += Animate;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= 0x08000000 | 0x00080000 | 0x00000080; // NOACTIVATE | LAYERED | TOOLWINDOW
                return p;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0021 || message.Msg == 0x024B) // WM_MOUSEACTIVATE or WM_POINTERACTIVATE
            {
                message.Result = new IntPtr(3); // MA_NOACTIVATE / PA_NOACTIVATE
                return;
            }
            base.WndProc(ref message);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Native.GetForegroundWindow() != game) { Close(); return; }
            try
            {
                art.Present(Handle, placement, alpha);
                RaiseTopmost(false);
                elapsed.Start();
                frame.Start();
                Console.WriteLine("Overlay shown; GF2 foreground: {0}",
                    Native.GetForegroundWindow() == game);
            }
            catch (Exception error) { Failure = error; Close(); }
        }

        private void RaiseTopmost(bool reset)
        {
            const uint flags = 0x0001 | 0x0002 | 0x0010 | 0x0200; // NOSIZE | NOMOVE | NOACTIVATE | NOOWNERZORDER
            if (reset) Native.SetWindowPos(Handle, new IntPtr(-2), 0, 0, 0, 0, flags);
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, flags);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && fadeOutAt < 0)
            {
                Dismissed = true;
                Console.WriteLine("Dismiss click received; GF2 foreground: {0}",
                    Native.GetForegroundWindow() == game);
                BeginFadeOut();
            }
            base.OnMouseUp(e);
        }

        private void BeginFadeOut()
        {
            fadeOutAt = elapsed.Elapsed.TotalMilliseconds;
            fadeFrom = alpha;
            frame.Interval = 16;
        }

        private void Animate(object sender, EventArgs e)
        {
            if (Native.GetForegroundWindow() != game || (stop != null && stop.WaitOne(0)))
            {
                Close();
                return;
            }
            try
            {
                double now = elapsed.Elapsed.TotalMilliseconds;
                if (previewMs > 0 && fadeOutAt < 0 && now >= previewMs) BeginFadeOut();
                if (fadeOutAt >= 0 && now >= fadeOutAt + FadeMs)
                {
                    Console.WriteLine("Overlay closed; GF2 foreground: True");
                    Close();
                    return;
                }
                double fraction = fadeOutAt < 0 ? Math.Min(now / FadeMs, 1) :
                    Math.Max(1 - (now - fadeOutAt) / FadeMs, 0);
                byte next = (byte)Math.Max(1, Math.Round(fraction * (fadeOutAt < 0 ? 255 : fadeFrom)));
                if (next != alpha) { art.Present(Handle, placement, next); alpha = next; }
                if (!raisedAgain && fadeOutAt < 0 && now >= 500)
                {
                    // GF2 can cover the overlay during fade-in.
                    RaiseTopmost(true);
                    raisedAgain = true;
                    frame.Interval = 125;
                }
            }
            catch (Exception error) { Failure = error; Close(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { frame.Dispose(); if (art != null) art.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
