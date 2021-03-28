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
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [DllImport("user32.dll")]
        private static extern int GetWindowRect(IntPtr hWnd, out RECT lpRECT);

        [DllImport("dwmapi.dll")]
        private static extern long DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out RECT rect, int cbAttribute);

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
        }

        public void SetSize(string title, List<string> remainingArguments, bool isRegex, bool adjust)
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

            List<int> args;
            try
            {
                // int.Parse(a, NumberStyles.AllowDecimalPoint)
                // では不十分。int.Parse では、小数部は0しか認められないため（それ以外は OverflowException になる）。
                args = remainingArguments.ConvertAll(a => (int)float.Parse(a));
            }
            catch (FormatException)
            {
                Console.WriteLine("位置とサイズには数値を指定してください。");
                return;
            }

            foreach (Process process in targetProcesses)
            {
                GetWindowRect(process.MainWindowHandle, out var rect);
                // 次の5行は、args.Length が 0 かつ adjust == false の場合、無駄な処理になる。
                DwmGetWindowAttribute(process.MainWindowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out var rect2, Marshal.SizeOf(typeof(Rect)));
                var 左透明部 = rect2.left - rect.left;
                var 上透明部 = rect2.top - rect.top;
                var 右透明部 = rect.right - rect2.right;
                var 下透明部 = rect.bottom - rect2.bottom;

                int newX, newY, newW, newH;
                switch (args.Count)
                {
                    case 0:
                        newX = rect.left;
                        newY = rect.top;
                        newW = rect.right - rect.left;
                        newH = rect.bottom - rect.top;
                        break;
                    case 1:
                        newX = args[0] - 左透明部;
                        newY = rect.top;
                        newW = rect.right - rect.left;
                        newH = rect.bottom - rect.top;
                        break;
                    case 2:
                        newX = args[0] - 左透明部;
                        newY = args[1] - 上透明部;
                        newW = rect.right - rect.left;
                        newH = rect.bottom - rect.top;
                        break;
                    case 3:
                        newX = args[0] - 左透明部;
                        newY = args[1] - 上透明部;
                        newW = args[2] + 左透明部 + 右透明部;
                        newH = rect.bottom - rect.top;
                        break;
                    default:
                        newX = args[0] - 左透明部;
                        newY = args[1] - 上透明部;
                        newW = args[2] + 左透明部 + 右透明部;
                        newH = args[3] + 上透明部 + 下透明部;
                        break;
                }

                if (adjust)
                {
                    var virtualScreenLeft = SystemParameters.VirtualScreenLeft;
                    var virtualScreenTop = SystemParameters.VirtualScreenTop;
                    var virtualScreenWidth = SystemParameters.VirtualScreenWidth;
                    var virtualScreenHeight = SystemParameters.VirtualScreenHeight;

                    var newBottom = newY + newH - 下透明部;
                    var virtualScreenBottom = (int)(virtualScreenTop + virtualScreenHeight);
                    if (newBottom > virtualScreenBottom)
                    {
                        newY -= newBottom - virtualScreenBottom;
                    }

                    var newRight = newX + newW - 右透明部;
                    var virtualScreenRight = (int)(virtualScreenLeft + virtualScreenWidth);
                    if (newRight > virtualScreenRight)
                    {
                        newX -= newRight - virtualScreenRight;
                    }

                    if (newY + 上透明部 < virtualScreenTop)
                    {
                        newY = (int)virtualScreenTop - 上透明部;
                    }

                    if (newX + 左透明部 < virtualScreenLeft)
                    {
                        newX = (int)virtualScreenLeft - 左透明部;
                    }
                }

                MoveWindow(process.MainWindowHandle, newX, newY, newW, newH, 1);
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
    Size.exe [-r] [-a] [-l] [-h] <title> <x> <y> <width> <height>

    title : この文字列を含むタイトルを持つウィンドウが変更対象です。
    x     : 変更後のウィンドウ左上の X 座標。
    y     : 変更後のウィンドウ左上の Y 座標。
    width : 変更後のウィンドウの幅。
    height: 変更後のウィンドウの高さ。

    以下のオプションがあります。

    -r    : title を正規表現として解釈します。
    -a    : ウィンドウがスクリーン内に収まるように調整します。
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
