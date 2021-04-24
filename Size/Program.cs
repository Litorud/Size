using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Shell;

namespace Size
{
    public partial class Program : Application
    {
        public static string PowerShellArguments { get; } = $"-noexit -Command Set-Location '{AppDomain.CurrentDomain.BaseDirectory}'";

        protected override void OnStartup(StartupEventArgs e)
        {
            var commandLineApplication = new CommandLineApplication()
            {
                OptionsComparison = StringComparison.CurrentCultureIgnoreCase,
                UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue
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

            Shutdown();
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

            var calculator = new BoundsCalculator(args);
            if (adjust)
            {
                calculator.AddAdjustProcess(
                    SystemParameters.PrimaryScreenWidth,
                    SystemParameters.PrimaryScreenHeight,
                    SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth,
                    SystemParameters.VirtualScreenHeight,
                    SystemParameters.WorkArea.X,
                    SystemParameters.WorkArea.Y,
                    SystemParameters.WorkArea.Width,
                    SystemParameters.WorkArea.Height);
            }

            foreach (var process in targetProcesses)
            {
                var windowRect = Api.GetWindowRect(process.MainWindowHandle);
                // 次の呼び出しは、args.Length が 0 かつ adjust == false の場合、無駄な処理になる。
                // が、めったにないケースなので気にしない。
                var extendedFrameBounds = Api.GetExtendedFrameBounds(process.MainWindowHandle);

                var (x, y, width, height) = calculator.Calculate(windowRect, extendedFrameBounds);
                Api.MoveWindow(process.MainWindowHandle, x, y, width, height);
            }
        }

        private IEnumerable<Process> GetTargetProcesses(string title, bool isRegex)
        {
            Func<string, bool> titleFilter;
            if (isRegex)
            {
                var regex = new Regex(title, RegexOptions.Compiled);
                titleFilter = t => regex.IsMatch(t);
            }
            else
            {
                titleFilter = t => t.Contains(title, StringComparison.CurrentCultureIgnoreCase);
            }

            return Process.GetProcesses()
                .Where(p => p.MainWindowHandle.ToInt64() != 0 && titleFilter(p.MainWindowTitle) && !Api.IsCloaked(p.MainWindowHandle));
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
             */
            var builder = new ListBuilder();

            // PrimaryScreen、VirtualScreen、WorkArea、および各ウィンドウから、各値の最大値を探す。
            builder.Add("(PrimaryScreen)", 0, 0,
                SystemParameters.PrimaryScreenWidth,
                SystemParameters.PrimaryScreenHeight);
            builder.Add("(VirtualScreen)",
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            builder.Add("(WorkArea)",
                SystemParameters.WorkArea.X,
                SystemParameters.WorkArea.Y,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height);

            var processes = Process.GetProcesses()
                .Where(p => p.MainWindowHandle.ToInt64() != 0 && !string.IsNullOrEmpty(p.MainWindowTitle) && !Api.IsCloaked(p.MainWindowHandle));
            foreach (var process in processes)
            {
                var extendedFrameBounds = Api.GetExtendedFrameBounds(process.MainWindowHandle);
                builder.Add(process.MainWindowTitle,
                    extendedFrameBounds.left,
                    extendedFrameBounds.top,
                    extendedFrameBounds.right - extendedFrameBounds.left,
                    extendedFrameBounds.bottom - extendedFrameBounds.top);
            }

            // Console.BufferWidth ではなく Console.WindowWidth を使う。
            // BufferWidth > WindowWidth の場合、横スクロールバーが表示される。
            var results = builder.Build(Console.WindowWidth);
            foreach (var result in results)
            {
                Console.WriteLine(result);
            }
        }
    }
}
