using System;
using System.Collections.Generic;

namespace Size
{
    public class BoundsCalculator
    {
        Func<Rect, Rect, double, double, double, (double, double, double, double)> calculate;

        public BoundsCalculator(IList<int> args)
        {
            calculate = args.Count switch
            {
                0 => (windowRect, extendedFrameBounds, monitorDpiX, monitorDpiY, windowDpi) => (windowRect.left, windowRect.top, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top),
                1 => (windowRect, extendedFrameBounds, monitorDpiX, monitorDpiY, windowDpi) =>
                {
                    var extendedFrameBoundsLeft = extendedFrameBounds.left * monitorDpiX / windowDpi;

                    var 左透明部 = extendedFrameBoundsLeft - windowRect.left;

                    var x = (args[0] - 左透明部) * monitorDpiX / 96;

                    return (x, windowRect.top, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top);
                }
                ,
                2 => (windowRect, extendedFrameBounds, monitorDpiX, monitorDpiY, windowDpi) =>
                {
                    var extendedFrameBoundsLeft = extendedFrameBounds.left * monitorDpiX / windowDpi;
                    var extendedFrameBoundsTop = extendedFrameBounds.top * monitorDpiY / windowDpi;

                    var 左透明部 = extendedFrameBoundsLeft - windowRect.left;
                    var 上透明部 = extendedFrameBoundsTop - windowRect.top;

                    var x = (args[0] - 左透明部) * monitorDpiX / 96;
                    var y = (args[1] - 上透明部) * monitorDpiY / 96;

                    return (x, y, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top);
                }
                ,
                3 => (windowRect, extendedFrameBounds, monitorDpiX, monitorDpiY, windowDpi) =>
                {
                    var extendedFrameBoundsLeft = extendedFrameBounds.left * monitorDpiX / windowDpi;
                    var extendedFrameBoundsTop = extendedFrameBounds.top * monitorDpiY / windowDpi;
                    var extendedFrameBoundsRight = extendedFrameBounds.right * monitorDpiX / windowDpi;

                    var 左透明部 = extendedFrameBoundsLeft - windowRect.left;
                    var 上透明部 = extendedFrameBoundsTop - windowRect.top;
                    var 右透明部 = windowRect.right - extendedFrameBoundsRight;

                    var x = (args[0] - 左透明部) * monitorDpiX / 96;
                    var y = (args[1] - 上透明部) * monitorDpiY / 96;
                    var width = (args[2] + 左透明部 + 右透明部) * monitorDpiX / 96;

                    return (x, y, width, windowRect.bottom - windowRect.top);
                }
                ,
                _ => (windowRect, extendedFrameBounds, monitorDpiX, monitorDpiY, windowDpi) =>
                {
                    // 透明部を求める。windowRect と extendedFrameBounds の差で求めるが、windowRect は仮想化された DPI であるため、
                    // extendedFrameBounds にはモニターの DPI と ウィンドウの DPI に応じた倍率を掛ける。
                    var extendedFrameBoundsLeft = extendedFrameBounds.left * monitorDpiX / windowDpi;
                    var extendedFrameBoundsTop = extendedFrameBounds.top * monitorDpiY / windowDpi;
                    var extendedFrameBoundsRight = extendedFrameBounds.right * monitorDpiX / windowDpi;
                    var extendedFrameBoundsBottom = extendedFrameBounds.bottom * monitorDpiY / windowDpi;

                    var 左透明部 = extendedFrameBoundsLeft - windowRect.left;
                    var 上透明部 = extendedFrameBoundsTop - windowRect.top;
                    var 右透明部 = windowRect.right - extendedFrameBoundsRight;
                    var 下透明部 = windowRect.bottom - extendedFrameBoundsBottom;

                    // 位置とサイズを求める。引数に、モニターの DPI に応じた倍率を掛け、さらに透明部を考慮する。透明部にも DPI に応じた倍率を掛ける。
                    var x = (args[0] - 左透明部) * monitorDpiX / 96;
                    var y = (args[1] - 上透明部) * monitorDpiY / 96;
                    var width = (args[2] + 左透明部 + 右透明部) * monitorDpiX / 96;
                    var height = (args[3] + 上透明部 + 下透明部) * monitorDpiY / 96;
                    // x と y は現在の DPI だけ考慮すればよいが、幅と高さは本来、移動後の DPI も考慮する必要がある。
                    // 例えばメインディスプレイが 144dpi、サブディスプレイが 96dpi の場合、
                    // モニター DPI は常に 144 だが、ウィンドウ DPI はメインディスプレイにあるときは 144、サブディスプレイにあるときは 96 となる。
                    // この場合、ウィンドウが DPI の異なるディスプレイに移動すると、サイズが期待どおりにならない。
                    // メイン→メイン …… OK
                    // メイン→サブ　 …… サイズが小さい。1.5を掛ける、つまり windowDpi / 96 または monitorDpi / 96 を掛けると期待どおりになる。
                    // 　サブ→メイン …… サイズが大きい。1.5で割る、つまり windowDpi / monitorDpi または 96 / monitorDpi を掛けると期待どおりになる。
                    // 　サブ→サブ　 …… OK
                    // これは移動後のディスプレイの DPI を得る必要があるため、現在のところ対応していない。

                    return (x, y, width, height);
                }
            };
        }

        public void AddAdjustProcess(
            double primaryScreenWidth,
            double primaryScreenHeight,
            double virtualScreenLeft,
            double virtualScreenTop,
            double virtualScreenWidth,
            double virtualScreenHeight,
            double workAreaX,
            double workAreaY,
            double workAreaWidth,
            double workAreaHeight)
        {
            var calculateMain = calculate;
            calculate = (windowRect, extendedFrameBounds, monitorDpiX, monitorDpiY, windowDpi) =>
            {
                var (x, y, width, height) = calculateMain(windowRect, extendedFrameBounds, monitorDpiX, monitorDpiY, windowDpi);

                var extendedFrameBoundsLeft = extendedFrameBounds.left * monitorDpiX / windowDpi;
                var extendedFrameBoundsTop = extendedFrameBounds.top * monitorDpiY / windowDpi;
                var extendedFrameBoundsRight = extendedFrameBounds.right * monitorDpiX / windowDpi;
                var extendedFrameBoundsBottom = extendedFrameBounds.bottom * monitorDpiY / windowDpi;

                var vLeft = virtualScreenLeft * monitorDpiX / 96;
                var vTop = virtualScreenTop * monitorDpiY / 96;
                var vRight = vLeft + virtualScreenWidth * monitorDpiX / 96;
                var vBottom = vTop + virtualScreenHeight * monitorDpiY / 96;

                var 下透明部 = windowRect.bottom - extendedFrameBoundsBottom;
                var bottom = y + height - 下透明部;
                if (bottom > vBottom)
                {
                    y -= bottom - vBottom;
                }

                var 右透明部 = windowRect.right - extendedFrameBoundsRight;
                var right = x + width - 右透明部;
                if (right > vRight)
                {
                    x -= right - vRight;
                }

                var 上透明部 = extendedFrameBoundsTop - windowRect.top;
                var top = y + 上透明部;
                if (top < vTop)
                {
                    y = vTop - 上透明部;
                }

                var 左透明部 = extendedFrameBoundsLeft - windowRect.left;
                var left = x + 左透明部;
                if (left < vLeft)
                {
                    x = vLeft - 左透明部;
                }

                // プライマリスクリーンのタスクバーに重ならないように調整する。
                var pX = 0;
                var pY = 0;
                var pW = primaryScreenWidth * monitorDpiX / 96;
                var pH = primaryScreenHeight * monitorDpiY / 96;
                var wX = workAreaX * monitorDpiX / 96;
                var wY = workAreaY * monitorDpiY / 96;
                var wW = workAreaWidth * monitorDpiX / 96;
                var wH = workAreaHeight * monitorDpiY / 96;

                // PrimaryScreen の X と WorkArea の X が異なるなら、タスクバーは左にある。
                // X が同じで Width が異なるなら、タスクバーは右にある。
                // PrimaryScreen の Y と WorkArea の Y が異なるなら、タスクバーは上にある。
                // Y が同じで Height が異なるなら、タスクバーは下にある。
                // どれも同じならタスクバーは非表示。
                if (pY == wY && pH != wH)
                {
                    // ウィンドウの底辺がタスクバーに被っているか?
                    //   0 1 2 3 4 5 6 7 8 9
                    // 0　　　　　　　　　　 例: プライマリスクリーンは10×10、タスクバーは下、
                    // 1　　　　　　　　　　 　  ワークエリアは10×7、ウィンドウは3×5。
                    // 2
                    // 3　口口口　　　　　　 ウィンドウY(=3) + ウィンドウ高さ(=5) > ワークエリア高さ(=7)
                    // 4　口口口　　　　　　 のとき、タスクバーに重なる。
                    // 5　口口口　　　　　　 →y + height > wH のとき、タスクバーに重なる。
                    // 6　口口口　　口口口
                    // 7田口口口田田口口口田 ウィンドウY(=6) + ウィンドウ高さ(=5) > スクリーン高さ(=10)
                    // 8田田田田田田口口口田 のときは、タスクバーに重ならない。
                    // 9田田田田田田口口口田 →y + height <= pH のとき、タスクバーに重なる。
                    // 0　　　　　　口口口
                    bottom = y + height - 下透明部;
                    if (bottom > wH && bottom <= pH && x + width > pX && x < pW)
                    {
                        // 重なっていたら、重なっている分だけ上にずらす。
                        // ただし、上にずらした結果 VirtualScreen からはみ出さないように、最小でも virtualScreenTop になるようにする。
                        y -= Math.Min(bottom - wH, y + 上透明部 - vTop);
                    }
                }
                else if (pY != wY)
                {
                    top = y + 上透明部;
                    if (top >= pY && top < wY && x + width > pX && x < pW)
                    {
                        // ウィンドウの上辺が、タスクバーに重なっていたら、重なっている分だけ下にずらす。
                        // 下にずらした結果 VirtualScreen からはみ出しても構わない。
                        y += wY - top;
                    }
                }
                else if (pX == wX && pW != wW)
                {
                    right = x + width - 右透明部;
                    if (right > wW && right <= pW && y + height > pY && y < pH)
                    {
                        x -= Math.Min(right - wW, x + 左透明部 - vLeft);
                    }
                }
                else if (pX != wX)
                {
                    left = x + 左透明部;
                    if (left >= pX && left < wX && y + height > pY && y < pH)
                    {
                        x += wX - left;
                    }
                }

                return (x, y, width, height);
            };
        }

        public (double, double, double, double) Calculate(Rect windowRect, Rect extendedFrameBounds, uint monitorDpiX, uint monitorDpiY, uint windowDpi)
        {
            return calculate(windowRect, extendedFrameBounds, monitorDpiX, monitorDpiY, windowDpi);
        }
    }
}
