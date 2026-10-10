// shot — screenshot the primary display for an AI agent.
// Downscales so the long edge fits MAX_EDGE (Claude resizes larger images, which would break
// click coordinates) and writes the factor to %TEMP%\dc-scale.txt so `dc` lands 1:1.
//
// Usage:
//   shot              (writes %TEMP%\dc-shot.png)
//   shot path.png

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

static class Shot
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);

    const int MAX_EDGE = 1568;

    static void Main(string[] a)
    {
        SetProcessDPIAware();
        string path = a.Length > 0 ? a[0] : Path.Combine(Path.GetTempPath(), "dc-shot.png");
        int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
        double scale = Math.Max(1.0, (double)Math.Max(w, h) / MAX_EDGE);
        int ow = (int)Math.Round(w / scale), oh = (int)Math.Round(h / scale);

        using (var full = new Bitmap(w, h, PixelFormat.Format24bppRgb))
        using (var outImg = new Bitmap(ow, oh, PixelFormat.Format24bppRgb))
        {
            using (var g = Graphics.FromImage(full)) g.CopyFromScreen(0, 0, 0, 0, full.Size);
            using (var g = Graphics.FromImage(outImg))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(full, 0, 0, ow, oh);
            }
            outImg.Save(path, ImageFormat.Png);
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "dc-scale.txt"), scale.ToString("R", CultureInfo.InvariantCulture));
        Console.WriteLine("{0} ({1}x{2})", path, ow, oh);
    }
}
