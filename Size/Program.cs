using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
            var adjustOption = commandLineApplication.Option("-a|--adjust", "ウィンドウがスクリーン内に収まるように調整します。", CommandOptionType.NoValue);
            var showsHelpOption = commandLineApplication.Option("-h|--help|-?", "ヘルプを表示します。", CommandOptionType.NoValue);
            var showsListOption = commandLineApplication.Option("-l|--list", "ウィンドウの一覧を出力します。", CommandOptionType.NoValue);

            commandLineApplication.OnExecute(() =>
            {
                var 何もしてない = true;

                if (!string.IsNullOrEmpty(titleArgument.Value))
                {
                    SetSize(titleArgument.Value, commandLineApplication.RemainingArguments, isRegexOption.HasValue(), adjustOption.HasValue());
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

        public void SetSize(string title, IList<string> remainingArguments, bool isRegex, bool adjust)
        {
            IEnumerable<Process> targetProcesses;
            try
            {
                targetProcesses = GetTargetProcesses(title, isRegex);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("正規表現が正しくありません。");
                return;
            }

            if (!targetProcesses.Any())
            {
                Console.WriteLine("対象ウィンドウが見つかりません。");
                return;
            }

            Func<IntPtr, (int, int, int, int)> getBounds;
            try
            {
                getBounds = GetBoundsGetter(remainingArguments);
            }
            catch (FormatException)
            {
                Console.WriteLine("位置またはサイズの値が正しくありません。");
                return;
            }

            var getActualBounds = adjust ? handle =>
            {
                (var x, var y, var width, var height) = getBounds(handle);
                return Adjust(x, y, width, height);
            }
            : getBounds;

            foreach (Process process in targetProcesses)
            {
                (var x, var y, var width, var height) = getActualBounds(process.MainWindowHandle);
                MoveWindow(process.MainWindowHandle, x, y, width, height, 1);
            }
        }

        private IEnumerable<Process> GetTargetProcesses(string title, bool isRegex)
        {
            var processes = Process.GetProcesses().Where(p => p.MainWindowHandle.ToInt64() > 0);

            if (isRegex)
            {
                var regex = new Regex(title, RegexOptions.Compiled);
                return processes.Where(p => regex.IsMatch(p.MainWindowTitle));
            }
            else
            {
                return processes.Where(p => p.MainWindowTitle.IndexOf(title, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }
        }

        private Func<IntPtr, (int, int, int, int)> GetBoundsGetter(IList<string> remainingArguments)
        {
            switch (remainingArguments.Count)
            {
                case 0:
                    return handle =>
                    {
                        GetWindowRect(handle, out var rect);
                        return (rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
                    };
                case 1:
                    int x1 = int.Parse(remainingArguments[0]);
                    return handle =>
                    {
                        GetWindowRect(handle, out var rect);
                        return (x1, rect.top, rect.right - rect.left, rect.bottom - rect.top);
                    };
                case 2:
                    int x2 = int.Parse(remainingArguments[0]);
                    int y2 = int.Parse(remainingArguments[1]);
                    return handle =>
                    {
                        GetWindowRect(handle, out var rect);
                        return (x2, y2, rect.right - rect.left, rect.bottom - rect.top);
                    };
                case 3:
                    int x3 = int.Parse(remainingArguments[0]);
                    int y3 = int.Parse(remainingArguments[1]);
                    int w3 = int.Parse(remainingArguments[2]);
                    return handle =>
                    {
                        GetWindowRect(handle, out var rect);
                        return (x3, y3, w3, rect.bottom - rect.top);
                    };
                default:
                    int x4 = int.Parse(remainingArguments[0]);
                    int y4 = int.Parse(remainingArguments[1]);
                    int w4 = int.Parse(remainingArguments[2]);
                    int h4 = int.Parse(remainingArguments[3]);
                    return handle => (x4, y4, w4, h4);
            }
        }

        private (int, int, int, int) Adjust(int x, int y, int width, int height)
        {
            var virtualScreenLeft = SystemParameters.VirtualScreenLeft;
            var virtualScreenTop = SystemParameters.VirtualScreenTop;
            var virtualScreenWidth = SystemParameters.VirtualScreenWidth;
            var virtualScreenHeight = SystemParameters.VirtualScreenHeight;

            if (x < virtualScreenLeft)
            {
                x = (int)virtualScreenLeft;
            }

            if (y < virtualScreenTop)
            {
                y = (int)virtualScreenTop;
            }

            if (x + width > virtualScreenLeft + virtualScreenWidth)
            {
                if (width > virtualScreenWidth)
                {
                    x = 0;
                    width = (int)virtualScreenWidth;
                }
                else
                {
                    x = (int)(virtualScreenLeft + virtualScreenWidth) - width;
                }
            }

            if (y + height > virtualScreenTop + virtualScreenHeight)
            {
                if (height > virtualScreenHeight)
                {
                    y = 0;
                    height = (int)virtualScreenHeight;
                }
                else
                {
                    y = (int)(virtualScreenTop + virtualScreenHeight) - height;
                }
            }

            return (x, y, width, height);
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
                    rect.left,
                    rect.top,
                    rect.right - rect.left,
                    rect.bottom - rect.top);
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
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
    }
}
