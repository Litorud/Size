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
    /// <remarks>
    /// このクラスが Application を継承しているのは、ジャンプリストを使用するため。
    /// JumpList.AddToRecentCategory() は、
    /// </remarks>
    class Sizer
    {
        [DllImport("user32.dll")]
        private static extern int MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, int bRepaint);

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

            // ジャンプリストに登録
            // 参考: http://www.atmarkit.co.jp/ait/articles/1509/09/news025.html
            var jumpTask = new JumpTask
            {
                Title = arguments,
                Arguments = arguments,
            };

            JumpList.AddToRecentCategory(jumpTask);
        }
    }
}
