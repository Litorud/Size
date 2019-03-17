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
        delegate void WindowPlacementFieldSetter(ref int left, ref int top, ref int right, ref int bottom);
        delegate void WindowStateSetter(ref uint showCmd);

        const int SW_MAXIMIZE = 3;
        const int SW_MINIMIZE = 6;
        const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern int GetWindowRect(IntPtr hWnd, out RECT lpRECT);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool SetWindowPlacement(IntPtr hWnd, [In]ref WINDOWPLACEMENT lpwndpl);

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
            var maximizeOption = commandLineApplication.Option("-x|--max", "ウィンドウを最大化します。", CommandOptionType.NoValue);
            var minimizeOption = commandLineApplication.Option("-n|--min", "ウィンドウを最小化します。", CommandOptionType.NoValue);
            var restoreOption = commandLineApplication.Option("-s|--restore", "最大化/最小化状態のウィンドウを元のサイズに戻します。", CommandOptionType.NoValue);
            var showsHelpOption = commandLineApplication.Option("-h|--help|-?", "ヘルプを表示します。", CommandOptionType.NoValue);
            var showsListOption = commandLineApplication.Option("-l|--list", "ウィンドウの一覧を出力します。", CommandOptionType.NoValue);

            commandLineApplication.OnExecute(() =>
            {
                var 何もしてない = true;

                if (!string.IsNullOrEmpty(titleArgument.Value))
                {
                    SetSize(titleArgument.Value, commandLineApplication.RemainingArguments, isRegexOption.HasValue(), maximizeOption.HasValue(), minimizeOption.HasValue(), restoreOption.HasValue());
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

        public void SetSize(string title, IList<string> remainingArguments, bool isRegex, bool maximize, bool minimize, bool restore)
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

            WindowPlacementFieldSetter setWindowPlacementField;
            try
            {
                setWindowPlacementField = GetWindowPlacementFieldSetter(remainingArguments);
            }
            catch (FormatException)
            {
                Console.WriteLine("位置またはサイズの値が正しくありません。");
                return;
            }

            WindowStateSetter setWindowState = GetWindowStateSetter(maximize, minimize, restore);

            int windowPlacementLength = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            foreach (Process process in targetProcesses)
            {
                WINDOWPLACEMENT wp;
                // https://docs.microsoft.com/en-us/windows/desktop/api/winuser/ns-winuser-tagwindowplacement
                // には、GetWindowPlacement を呼び出す前にも length をセットせよと書いてある。
                wp.length = windowPlacementLength;

                GetWindowPlacement(process.MainWindowHandle, out wp);

                setWindowPlacementField(ref wp.rcNormalPosition.left, ref wp.rcNormalPosition.top, ref wp.rcNormalPosition.right, ref wp.rcNormalPosition.bottom);
                setWindowState(ref wp.showCmd);

                SetWindowPlacement(process.MainWindowHandle, ref wp);
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

        private WindowPlacementFieldSetter GetWindowPlacementFieldSetter(IList<string> remainingArguments)
        {
            var argumentsEnumerator = remainingArguments.GetEnumerator();

            if (!argumentsEnumerator.MoveNext())
            {
                return (ref int left, ref int top, ref int right, ref int bottom) => { };
            }

            int l = int.Parse(argumentsEnumerator.Current);

            if (!argumentsEnumerator.MoveNext())
            {
                return (ref int left, ref int top, ref int right, ref int bottom) =>
                {
                    left = l;
                };
            }

            int t = int.Parse(argumentsEnumerator.Current);

            if (!argumentsEnumerator.MoveNext())
            {
                return (ref int left, ref int top, ref int right, ref int bottom) =>
                {
                    left = l;
                    top = t;
                };
            }

            int r = l + int.Parse(argumentsEnumerator.Current);

            if (!argumentsEnumerator.MoveNext())
            {
                return (ref int left, ref int top, ref int right, ref int bottom) =>
                {
                    left = l;
                    top = t;
                    right = r;
                };
            }

            int b = t + int.Parse(argumentsEnumerator.Current);

            return (ref int left, ref int top, ref int right, ref int bottom) =>
            {
                left = l;
                top = t;
                right = r;
                bottom = b;
            };
        }

        private WindowStateSetter GetWindowStateSetter(bool maximize, bool minimize, bool restore)
        {
            if (minimize)
            {
                return (ref uint showCmd) =>
                {
                    showCmd = SW_MINIMIZE;
                };
            }
            else if (maximize)
            {
                return (ref uint showCmd) =>
                {
                    showCmd = SW_MAXIMIZE;
                };
            }
            else if (restore)
            {
                return (ref uint showCmd) =>
                {
                    showCmd = SW_RESTORE;
                };
            }
            else
            {
                return (ref uint showCmd) => { };
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
        public struct WINDOWPLACEMENT
        {
            public int length;
            public uint flags;
            public uint showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
            // https://docs.microsoft.com/en-us/windows/desktop/api/winuser/ns-winuser-tagwindowplacement
            // には RECT rcDevice というフィールドも載っているが、このフィールドは不要。
            // 参考: https://ja.stackoverflow.com/questions/49492/c-2010-%E6%9C%80%E5%B0%8F%E5%8C%96%E6%99%82%E3%81%AE%E3%83%95%E3%82%A9%E3%83%BC%E3%83%A0%E5%BA%A7%E6%A8%99%E3%82%92%E5%8F%96%E5%BE%97
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
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
