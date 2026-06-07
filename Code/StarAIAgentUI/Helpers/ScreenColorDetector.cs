using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace StarAIAgentUI.Helpers
{

    public static class ScreenColorDetector
    {
        // Native Win32 GDI structures to read screen pixels safely
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        private const int SRCCOPY = 0x00CC0020;

        public static bool IsBackgroundBright(int x, int y)
        {
            try
            {
                // Sample a 1x1 pixel slightly offset or directly behind the top-left boundary
                // We use a small 2-pixel offset to avoid catching the window's own native frame border
                using (var bitmap = new System.Drawing.Bitmap(1, 1))
                {
                    using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(x - 2, y + 20, 0, 0, new System.Drawing.Size(1, 1));
                    }

                    var color = bitmap.GetPixel(0, 0);

                    // Standard luminosity formula (Y = 0.299R + 0.587G + 0.114B)
                    double luma = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);

                    // Returns true if the sampled pixel color behind the window is bright
                    return luma > 140;
                }
            }
            catch
            {
                return false; // Fallback safely if coordinates are off-screen
            }
        }
    }
}
