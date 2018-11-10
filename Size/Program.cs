using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;

namespace Size
{
    public partial class Program : Application
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowRect(IntPtr hWnd, out RECT lpRECT);

        [DllImport("user32.dll")]
        private static extern int MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, int bRepaint);

        public static string PowerShellArguments { get; } = $"-noexit -Command Set-Location '{AppDomain.CurrentDomain.BaseDirectory}'";

        protected override void OnStartup(StartupEventArgs e)
        {
            switch (e.Args.Length)
            {
                case 0:
                    ShowSizes();
                    break;
                case 5:
                    SetSize(e.Args);
                    break;
                default:
                    ShowHelp();
                    break;
            }

#if !DEBUG
            Shutdown();
#endif
        }

        private static void ShowSizes()
        {
            /* 参考:
             * https://dobon.net/vb/dotnet/system/displaysize.html
             * https://mseeeen.msen.jp/get-screen-bounds-with-multiple-monitors-in-wpf/
             * http://sliceof-it.blogspot.com/2012/02/systemparameters-screen-resolutions-wpf.html
             **/
            WriteBounds("(PrimaryScreen)", 0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            WriteBounds(
                "(VirtualScreen)",
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            var workArea = SystemParameters.WorkArea;
            WriteBounds(
                "(WorkArea)",
                workArea.X,
                workArea.Y,
                workArea.Width,
                workArea.Height);

            var targetProcesses = Process.GetProcesses()
                .Where(p => p.MainWindowHandle.ToInt64() > 0 && !string.IsNullOrEmpty(p.MainWindowTitle));

            foreach (var process in targetProcesses)
            {
                GetWindowRect(process.MainWindowHandle, out var rect);

                WriteBounds(
                    process.MainWindowTitle,
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top);
            }
        }

        private static void WriteBounds(string title, double x, double y, double width, double height)
        {
            Console.WriteLine($"{title}: ({x}, {y}) {width}×{height}");
        }

        public void SetSize(IList<string> args)
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

            UpdateJumpList(args);
        }

        private void UpdateJumpList(IEnumerable<string> args)
        {
            var arguments = ArgumentEscaper.EscapeAndConcatenate(args);

            JumpList.AddToRecentCategory(new JumpTask
            {
                Title = arguments,
                Arguments = arguments
            });
        }

        private static void ShowHelp()
        {
            Console.WriteLine(@"Size

指定したタイトルを持つウィンドウを、指定した位置とサイズに変更します。

構文
    Size.exe <title> <x> <y> <width> <height>

    title : 正規表現で指定します。これに該当するタイトルを持つウィンドウが変更対象です。
    x     : 変更後のウィンドウ左上の X 座標。
    y     : 変更後のウィンドウ左上の Y 座標。
    width : 変更後のウィンドウの幅。
    height: 変更後のウィンドウの高さ。

注意
    変更するウィンドウは最初に該当した1つだけです。");
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
