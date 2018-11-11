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

        private static Encoding encoding = Encoding.GetEncoding(932);

        protected override void OnStartup(StartupEventArgs e)
        {
            var commandLineApplication = new CommandLineApplication(false)
            {
                OptionsComparison = StringComparison.CurrentCultureIgnoreCase
            };

            var titleArgument = commandLineApplication.Argument("title", "この文字列を含むタイトルのウィンドウが変更対象です。");

            var isRegexOption = commandLineApplication.Option("-r|--regex", "title を正規表現として解釈します。", CommandOptionType.NoValue);

            var showsHelpOption = commandLineApplication.Option("-h|--help|-?", "ヘルプを表示します。", CommandOptionType.NoValue);

            var showsListOption = commandLineApplication.Option("-l|--list", "ウィンドウの一覧を出力します。", CommandOptionType.NoValue);

            commandLineApplication.OnExecute(() =>
            {
                var 何もしてない = true;

                if (!string.IsNullOrEmpty(titleArgument.Value))
                {
                    SetSize(titleArgument.Value, commandLineApplication.RemainingArguments, isRegexOption.HasValue());
                    UpdateJumpList(e.Args);
                    何もしてない = false;
                }

                if (showsHelpOption.HasValue())
                {
                    ShowHelp();
                    何もしてない = false;
                }

                if (showsListOption.HasValue())
                {
                    ShowList();
                    何もしてない = false;
                }

                if (何もしてない)
                {
                    ShowHelp();
                    Console.WriteLine();
                    ShowList();
                }
            });

            commandLineApplication.Execute(e.Args);
#if !DEBUG
            Shutdown();
#endif
        }

        public void SetSize(string title, IList<string> remainingArguments, bool isRegex)
        {
            var targetProcess = GetTargetProcess(title, isRegex);

            if (targetProcess == null)
            {
                Console.WriteLine("対象ウィンドウが見つかりません。");
                return;
            }

            try
            {
                (int x, int y, int width, int height) = GetArguments(remainingArguments, targetProcess.MainWindowHandle);
                MoveWindow(targetProcess.MainWindowHandle, x, y, width, height, 1);
            }
            catch (FormatException)
            {
                Console.WriteLine("位置またはサイズの値が正しくありません。");
            }
        }

        private Process GetTargetProcess(string title, bool isRegex)
        {
            var processes = Process.GetProcesses().Where(p => p.MainWindowHandle.ToInt64() > 0);

            if (isRegex)
            {
                var regex = new Regex(title, RegexOptions.Compiled);
                return processes.FirstOrDefault(p => regex.IsMatch(p.MainWindowTitle));
            }
            else
            {
                return processes.FirstOrDefault(p => p.MainWindowTitle.IndexOf(title, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }
        }

        private (int x, int y, int width, int height) GetArguments(IList<string> args, IntPtr handle)
        {
            switch (args.Count)
            {
                case 0:
                    GetWindowRect(handle, out var rect0);
                    return (rect0.Left, rect0.Top, rect0.Right - rect0.Left, rect0.Bottom - rect0.Top);
                case 1:
                    GetWindowRect(handle, out var rect1);
                    return (int.Parse(args[0]), rect1.Top, rect1.Right - rect1.Left, rect1.Bottom - rect1.Top);
                case 2:
                    GetWindowRect(handle, out var rect2);
                    return (int.Parse(args[0]), int.Parse(args[1]), rect2.Right - rect2.Left, rect2.Bottom - rect2.Top);
                case 3:
                    GetWindowRect(handle, out var rect3);
                    return (int.Parse(args[0]), int.Parse(args[1]), int.Parse(args[2]), rect3.Bottom - rect3.Top);
                default:
                    return (int.Parse(args[0]), int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]));
            }
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
    Size.exe [-r] [-l] [-h] <title> <x> <y> <width> <height>

    title : この文字列を含むタイトルを持つウィンドウが変更対象です。
    x     : 変更後のウィンドウ左上の X 座標。
    y     : 変更後のウィンドウ左上の Y 座標。
    width : 変更後のウィンドウの幅。
    height: 変更後のウィンドウの高さ。

    以下のオプションがあります。

    -r    : title を正規表現として解釈します。
    -l    : ウィンドウの一覧を出力します。
    -h    : このヘルプを表示します。

注意
    変更するウィンドウは最初に該当した1つだけです。
    オプションは x より前に指定する必要があります。");
        }

        private static void ShowList()
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
            var byteCount = encoding.GetByteCount(title);

            foreach (var limit in new[] { 28, 56 })
            {
                if (byteCount <= limit)
                {
                    var padding = new string(' ', limit - byteCount);
                    Console.WriteLine($"{title}{padding} {x,5} {y,5} {width,5} {height,5}");
                    return;
                }
            }

            Console.WriteLine($"{title} {x} {y} {width} {height}");
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
