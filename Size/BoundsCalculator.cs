using System;
using System.Collections.Generic;

namespace Size
{
    public class BoundsCalculator
    {
        Func<Rect, Rect, (int, int, int, int)> calculate;

        public BoundsCalculator(IList<int> args)
        {
            calculate = args.Count switch
            {
                0 => (windowRect, extendedFrameBounds) => (windowRect.left, windowRect.top, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top),
                1 => (windowRect, extendedFrameBounds) =>
                {
                    var x = args[0] - (extendedFrameBounds.left - windowRect.left); // = args[0] - 左透明部
                    return (x, windowRect.top, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top);
                }
                ,
                2 => (windowRect, extendedFrameBounds) =>
                {
                    var x = args[0] - (extendedFrameBounds.left - windowRect.left);
                    var y = args[1] - (extendedFrameBounds.top - windowRect.top);
                    return (x, y, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top);
                }
                ,
                3 => (windowRect, extendedFrameBounds) =>
                {
                    var x = args[0] - (extendedFrameBounds.left - windowRect.left);
                    var y = args[1] - (extendedFrameBounds.top - windowRect.top);
                    var width = args[2] + extendedFrameBounds.left - windowRect.left + windowRect.right - extendedFrameBounds.right; // = args[2] + 左透明部 + 右透明部
                    return (x, y, width, windowRect.bottom - windowRect.top);
                }
                ,
                _ => (windowRect, extendedFrameBounds) =>
                {
                    var x = args[0] - (extendedFrameBounds.left - windowRect.left);
                    var y = args[1] - (extendedFrameBounds.top - windowRect.top);
                    var width = args[2] + extendedFrameBounds.left - windowRect.left + windowRect.right - extendedFrameBounds.right;
                    var height = args[3] + extendedFrameBounds.top - windowRect.top + windowRect.bottom - extendedFrameBounds.bottom;
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
            calculate = (windowRect, extendedFrameBounds) =>
            {
                var (x, y, width, height) = calculateMain(windowRect, extendedFrameBounds);

                var vLeft = (int)virtualScreenLeft;
                var vTop = (int)virtualScreenTop;
                var vRight = (int)(vLeft + virtualScreenWidth);
                var vBottom = (int)(vTop + virtualScreenHeight);

                var 下透明部 = windowRect.bottom - extendedFrameBounds.bottom;
                var bottom = y + height - 下透明部;
                if (bottom > vBottom)
                {
                    y -= bottom - vBottom;
                }

                var 右透明部 = windowRect.right - extendedFrameBounds.right;
                var right = x + width - 右透明部;
                if (right > vRight)
                {
                    x -= right - vRight;
                }

                var 上透明部 = extendedFrameBounds.top - windowRect.top;
                var top = y + 上透明部;
                if (top < vTop)
                {
                    y = vTop - 上透明部;
                }

                var 左透明部 = extendedFrameBounds.left - windowRect.left;
                var left = x + 左透明部;
                if (left < vLeft)
                {
                    x = vLeft - 左透明部;
                }

                // プライマリスクリーンのタスクバーに重ならないように調整する。
                var pX = 0;
                var pY = 0;
                var pW = primaryScreenWidth;
                var pH = primaryScreenHeight;
                var wX = workAreaX;
                var wY = workAreaY;
                var wW = workAreaWidth;
                var wH = workAreaHeight;

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
                        y -= Math.Min(bottom - (int)wH, y + 上透明部 - vTop);
                    }
                }
                else if (pY != wY)
                {
                    top = y + 上透明部;
                    if (top >= pY && top < wY && x + width > pX && x < pW)
                    {
                        // ウィンドウの上辺が、タスクバーに重なっていたら、重なっている分だけ下にずらす。
                        // 下にずらした結果 VirtualScreen からはみ出しても構わない。
                        y += (int)wY - top;
                    }
                }
                else if (pX == wX && pW != wW)
                {
                    right = x + width - 右透明部;
                    if (right > wW && right <= pW && y + height > pY && y < pH)
                    {
                        x -= Math.Min(right - (int)wW, x + 左透明部 - vLeft);
                    }
                }
                else if (pX != wX)
                {
                    left = x + 左透明部;
                    if (left >= pX && left < wX && y + height > pY && y < pH)
                    {
                        x += (int)wX - left;
                    }
                }

                return (x, y, width, height);
            };
        }

        public (int, int, int, int) Calculate(Rect windowRect, Rect extendedFrameBounds)
        {
            return calculate(windowRect, extendedFrameBounds);
        }
    }
}
