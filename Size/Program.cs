﻿using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
            var rows = new List<Row>
            {
                new Row("(PrimaryScreen)", 0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                new Row(
                    "(VirtualScreen)",
                    SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth,
                    SystemParameters.VirtualScreenHeight),
                new Row(
                    "(WorkArea)",
                    SystemParameters.WorkArea.X,
                    SystemParameters.WorkArea.Y,
                    SystemParameters.WorkArea.Width,
                    SystemParameters.WorkArea.Height)
            };

            rows.AddRange(Process.GetProcesses()
                .Where(p => p.MainWindowHandle.ToInt64() != 0 && !string.IsNullOrEmpty(p.MainWindowTitle) && !Api.IsCloaked(p.MainWindowHandle))
                .Select(p =>
                {
                    var extendedFrameBounds = Api.GetExtendedFrameBounds(p.MainWindowHandle);
                    return new Row(
                        p.MainWindowTitle,
                        extendedFrameBounds.left,
                        extendedFrameBounds.top,
                        extendedFrameBounds.right - extendedFrameBounds.left,
                        extendedFrameBounds.bottom - extendedFrameBounds.top);
                }));

            // PrimaryScreen、VirtualScreen、WorkArea、および各ウィンドウから、各値の最大値を探す。
            int maxX = 0, maxY = 0, maxWidth = 0, maxHeight = 0;
            foreach (var row in rows)
            {
                if (row.X.Length > maxX) maxX = row.X.Length;
                if (row.Y.Length > maxY) maxY = row.Y.Length;
                if (row.Width.Length > maxWidth) maxWidth = row.Width.Length;
                if (row.Height.Length > maxHeight) maxHeight = row.Height.Length;
            }

            // 以下の式で、最後のひく数を5ではなく4にすると、Windows PowerShell を起動したときに開くウィンドウで実行したときのみ、
            // 不要な折り返しが発生する（幅ちょうどに収まっているのに折り返しが発生する）。
            // ジャンプリストでは Windows PowerShell が開くため、これに最適化して引く数を5としている。
            // また、Console.BufferWidth ではなく Console.WindowWidth を使う。
            // BufferWidth > WindowWidth の場合、横スクロールバーが表示される。
            int limitTitleWidth = Console.WindowWidth - maxX - maxY - maxWidth - maxHeight - 5;
            int maxTitleWidth = rows.Where(r => r.TitleWidth <= limitTitleWidth).Max(r => r.TitleWidth);

            foreach (var row in rows)
            {
                var titlePadding = maxTitleWidth > row.TitleWidth ? new string(' ', maxTitleWidth - row.TitleWidth) : string.Empty;
                var xPadding = new string(' ', maxX - row.X.Length);
                var yPadding = new string(' ', maxY - row.Y.Length);
                var widthPadding = new string(' ', maxWidth - row.Width.Length);
                var heightPadding = new string(' ', maxHeight - row.Height.Length);

                Console.WriteLine($"{row.Title}{titlePadding} {xPadding}{row.X} {yPadding}{row.Y} {widthPadding}{row.Width} {heightPadding}{row.Height}");
            }
        }

        private class Row
        {
            private static readonly Encoding encoding;

            public string Title { get; private set; }
            public int TitleWidth { get; private set; }
            public string X { get; private set; }
            public string Y { get; private set; }
            public string Width { get; private set; }
            public string Height { get; private set; }

            static Row()
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                encoding = Encoding.GetEncoding(932);
            }

            public Row(string title, double x, double y, double width, double height)
            {
                Title = title;
                TitleWidth = encoding.GetByteCount(title);
                X = x.ToString();
                Y = y.ToString();
                Width = width.ToString();
                Height = height.ToString();
            }
        }
    }
}
