using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Size
{
    class Program
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowRect(IntPtr hWnd, out RECT lpRECT);

        [DllImport("user32.dll")]
        private static extern int MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, int bRepaint);

        static void Main(string[] args)
        {
            Debug.WriteLine("起動しました。");

            switch (args.Length)
            {
                case 0:
                    ShowSizes();
                    break;
                case 5:
                    SetSize(args);
                    break;
            }
        }

        private static void ShowSizes()
        {
            var targetProcesses = Process.GetProcesses()
                .Where(p => p.MainWindowHandle.ToInt64() > 0 && !string.IsNullOrEmpty(p.MainWindowTitle));

            foreach (var process in targetProcesses)
            {
                RECT rect;
                GetWindowRect(process.MainWindowHandle, out rect);

                var title = process.MainWindowTitle;
                var x = rect.Left;
                var y = rect.Top;
                var width = rect.Right - x;
                var height = rect.Bottom - y;
                Console.WriteLine($"{title}: {x}, {y}, {width}, {height}");
            }
        }

        private static void SetSize(string[] args)
        {
            var title = args[0];
            var regex = new Regex(title, RegexOptions.Compiled);

            var targetProcess = Process.GetProcesses()
                .Where(p => p.MainWindowHandle.ToInt64() > 0 && regex.IsMatch(p.MainWindowTitle))
                .FirstOrDefault();

            if (targetProcess == null)
            {
                Console.WriteLine("対象ウィンドウが見つかりません。");
                return;
            }

            int x = int.Parse(args[1]);
            int y = int.Parse(args[2]);
            int width = int.Parse(args[3]);
            int height = int.Parse(args[4]);
            MoveWindow(targetProcess.MainWindowHandle, x, y, width, height, 1);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
