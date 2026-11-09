using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace FaelynnRandomizer
{
    internal static class Art
    {
        // These are the gutters in the approved transparent 3x3 sheet.
        private static readonly float[] RowEdges = { .073f, .361f, .660f, .973f };

        internal static int Column(int roll, int row)
        {
            int n = roll - 1;
            return (row == 0 ? n / 9 : row == 1 ? n / 3 : n) % 3;
        }

        internal static Bitmap Render(int roll, Size area)
        {
            bool splash = roll == 28;
            string name = splash ? "GF2-Splash.png" : "GF2-Accessories.png";
            using (Stream stream = typeof(Art).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("Missing bundled art: " + name);
                using (var source = new Bitmap(stream)) return Render(source, splash, roll, area);
            }
        }

        private static Bitmap Render(Bitmap source, bool splash, int roll, Size area)
        {
            float top = splash ? 0 : source.Height * RowEdges[0];
            float bottom = splash ? source.Height : source.Height * RowEdges[3];
            var crop = new RectangleF(0, top, source.Width, bottom - top);
            double scale = Math.Min(area.Width * (splash ? .90 : .62) / crop.Width,
                area.Height * (splash ? .90 : .65) / crop.Height);
            var art = new Bitmap(Math.Max(1, (int)(crop.Width * scale)),
                Math.Max(1, (int)(crop.Height * scale)), PixelFormat.Format32bppPArgb);
            try
            {
                using (Graphics canvas = Graphics.FromImage(art))
                {
                    canvas.Clear(Color.Transparent);
                    canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    canvas.SmoothingMode = SmoothingMode.AntiAlias;
                    var target = new RectangleF(0, 0, art.Width, art.Height);
                    canvas.DrawImage(source, target, crop, GraphicsUnit.Pixel);
                    if (!splash)
                    {
                        for (int row = 0; row < 3; row++)
                        {
                            float left = art.Width * Column(roll, row) / 3f;
                            float y = (RowEdges[row] * source.Height - top) * art.Height / crop.Height;
                            float height = (RowEdges[row + 1] - RowEdges[row]) * source.Height * art.Height / crop.Height;
                            Highlight(canvas, new RectangleF(left, y, art.Width / 3f, height));
                        }
                    }
                }
                return art;
            }
            catch { art.Dispose(); throw; }
        }

        private static void Highlight(Graphics canvas, RectangleF tile)
        {
            float unit = Math.Min(tile.Width, tile.Height);
            tile.Inflate(-unit * .035f, -unit * .035f);
            float arm = unit * .16f;
            using (var path = new GraphicsPath())
            using (var outline = new Pen(Color.FromArgb(210, 27, 18, 13), Math.Max(3, unit * .034f)))
            using (var accent = new Pen(Color.FromArgb(255, 115, 40), Math.Max(2, unit * .020f)))
            {
                Corner(path, tile.Left, tile.Top, arm, arm);
                Corner(path, tile.Right, tile.Top, -arm, arm);
                Corner(path, tile.Left, tile.Bottom, arm, -arm);
                Corner(path, tile.Right, tile.Bottom, -arm, -arm);
                outline.LineJoin = LineJoin.Round;
                accent.LineJoin = LineJoin.Round;
                canvas.DrawPath(outline, path);
                canvas.DrawPath(accent, path);
            }
        }

        private static void Corner(GraphicsPath path, float x, float y, float dx, float dy)
        {
            path.StartFigure();
            path.AddLines(new[] { new PointF(x + dx, y), new PointF(x, y), new PointF(x, y + dy) });
        }
    }
}
