using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Size.Tests
{
    [TestClass()]
    public class BoundsCalculatorTests
    {
        [TestMethod()]
        public void CalculateTest()
        {
            // 1. 指定した値が4つ未満のときは現在の値を使うこと。
            // 2. 上下左右の透明部を除いて計算すること。

            // (0, -1)、11×12 にしたい。
            var calculator1 = new BoundsCalculator(new int[] { 0, -1, 11 });
            // 現在の透明部を除いたウィンドウは、(0, 1)、10×12 とする。
            var extendedFrameBounds = new Rect
            {
                left = 0,
                top = 1,
                right = 0 + 10,
                bottom = 1 + 12
            };
            // ウィンドウの上と右と下には 2px の透明部、左には透明部なしとする。
            var windowRect = new Rect
            {
                left = extendedFrameBounds.left,
                top = extendedFrameBounds.top - 2,
                right = extendedFrameBounds.right + 2,
                bottom = extendedFrameBounds.bottom + 2
            };
            var (x1, y1, width1, height1) = calculator1.Calculate(windowRect, extendedFrameBounds, 96, 96, 96);
            // 指定X=0、現在X=0、左透明部=0 なので、X=0。
            Assert.AreEqual(0, x1);
            // 指定Y=-1、現在Y=1、上透明部=2 なので、Y = -1 - 2 = -3。
            Assert.AreEqual(-3, y1);
            // 指定幅=11、現在幅=10、左透明部=0、右透明部=2 なので、幅 = 0 + 11 + 2 = 13。
            Assert.AreEqual(13, width1);
            // 指定高さ=なし、現在高さ=12、上透明部=2、下透明部=2 なので、高さ = 2 + 12 + 2 = 16。
            Assert.AreEqual(16, height1);

            // 3. -a オプションを付けたとき、下がスクリーンからはみ出すなら上へずらすこと。
            // 4. -a オプションを付けたとき、右がタスクバーに重なるなら左へずらすこと。

            // PrimaryScreen は (0, 0)、10×10。VirtualScreen は (0, 0)、20×20 とする。
            // タスクバーは PrimaryScreen の右側に 3px とする。したがって、WorkArea は (0, 0)、7×10 となる。
            double pW = 10, pH = 10;
            double vX = 0, vY = 0, vW = 20, vH = 20;
            double wX1 = 0, wY1 = 0, wW1 = 7, wH1 = 10;

            // 透明部を除いて 1×11 のウィンドウを、(7, 10) に配置するよう指定する。
            // しかし、下がスクリーンからはみ出すので、押し込んで (7, 9) になる。
            // さらに、右がタスクバーと重なるので、左へずらして (6, 9) になる。
            var calculator2 = new BoundsCalculator(new int[] { 7, 10, 1, 11 });
            calculator2.AddAdjustProcess(pW, pH, vX, vY, vW, vH, wX1, wY1, wW1, wH1);
            var (x2, y2, width2, height2) = calculator2.Calculate(windowRect, extendedFrameBounds, 96, 96, 96);
            // 左透明部=0 なので、X = 6 のまま。
            Assert.AreEqual(6, x2);
            // 上透明部=2 なので、Y = 9 - 2 = 7。
            Assert.AreEqual(7, y2);
            // 左透明部=0、右透明部=2 なので、幅 = 0 + 1 + 2 = 3。
            Assert.AreEqual(3, width2);
            // 上透明部=2、下透明部=2 なので、高さ = 2 + 11 + 2 = 15。
            Assert.AreEqual(15, height2);

            // 5. 下がスクリーンからはみ出さないようにずらした結果、上がスクリーンからはみ出すことがないこと。
            // 6. 右がタスクバーに重ならないようにずらした結果、左がスクリーンからはみ出すことがないこと。

            // 透明部を除いて 9×21 のウィンドウを、(1, 10) に配置するよう指定する。
            // しかし、下がスクリーンからはみ出すので、押し込んで (1, -1) にしたいが、
            // 今度は上がはみ出すので、結局 (1, 0) になる。
            // さらに、右がタスクバーに重なるので、ずらして (-2, 0) にしたいが、
            // 今度は左がはみ出すので、結局 (0, 0) になる。
            // この結果、右がタスクバーと重なり、下がスクリーンからはみ出すが、これは対処しない。
            var calculator3 = new BoundsCalculator(new int[] { 1, 10, 9, 21 });
            calculator3.AddAdjustProcess(pW, pH, vX, vY, vW, vH, wX1, wY1, wW1, wH1);
            var (x3, y3, width3, height3) = calculator3.Calculate(windowRect, extendedFrameBounds, 96, 96, 96);
            // 左透明部=0 なので、X = 0 のまま。
            Assert.AreEqual(0, x3);
            // 上透明部=2 なので、Y = 0 - 2 = -2。
            Assert.AreEqual(-2, y3);
            // 左透明部=0、右透明部=2 なので、幅 = 0 + 9 + 2 = 11。
            Assert.AreEqual(11, width3);
            // 上透明部=2、下透明部=2 なので、高さ = 2 + 21 + 2 = 25。
            Assert.AreEqual(25, height3);

            // 7. DPI が 96 ではない（144の）ディスプレイで、仮想化された DPI に基づく位置とサイズを返すこと。
            // (-149, -151) 149×151 にする。値は、144 以上の素数として選んだ。
            var calculator4 = new BoundsCalculator(new int[] { -149, -151, 149, 151 });
            // 現在の透明部を除いたウィンドウは、(0, 1)、139×137 とする。
            var extendedFrameBounds2 = new Rect
            {
                left = 0,
                top = 1,
                right = 0 + 139,
                bottom = 1 + 137
            };
            // DpiForMonitor は 144 で、DpiForWindow は 96 とする。
            // モニターとウィンドウの DPI が異なるので、windowRect は 144 / 96 = 1.5倍 + 透明部を含む値となる。
            // 透明部は上下左右に 5px とする。これも 1.5倍するので 7.5 となる。
            var windowRect2 = new Rect
            {
                left = (int)((extendedFrameBounds2.left - 5) * 1.5),    // = (  0 - 5) * 1.5 =  -7.5 ≒ -7
                top = (int)((extendedFrameBounds2.top - 5) * 1.5),      // = (  1 - 5) * 1.5 =  -6
                right = (int)((extendedFrameBounds2.right + 5) * 1.5),  // = (139 + 5) * 1.5 = 216
                bottom = (int)((extendedFrameBounds2.bottom + 5) * 1.5) // = (138 + 5) * 1.5 = 214.5 ≒ 214
            };
            var (x4, y4, w4, h4) = calculator4.Calculate(windowRect2, extendedFrameBounds2, 144, 144, 96);
            // DpiForMonitor が 144 と、標準の 96dpi より高いので、144 / 96 = 1.5倍の値を返す必要がある。透明部も1.5倍して考慮する。
            Assert.AreEqual(-234, x4);    // (-149 - 7.5)       * 1.5 = -234.75 ※0.75のずれがあるのは、extendedFrameBounds2 から windowRect2 を決めたため。
            Assert.AreEqual(-237.75, y4); // (-151 - 7.5)       * 1.5 = -237.75
            Assert.AreEqual(245.25, w4);  // ( 149 + 7.5 + 7.5) * 1.5 =  246    ※〃
            Assert.AreEqual(248.25, h4);  // ( 151 + 7.5 + 7.5) * 1.5 =  249    ※〃

            // 8. DPI が 96 ではない（144の）ディスプレイで、仮想化された DPI に基づく位置とサイズを返すこと。-a の場合は調整すること。
            // PrimaryScreen は (0, 0)、1000×1000。VirtualScreen も同じとする。
            // タスクバーは左側に 3px とする。したがって、WorkArea は (3, 0)、997×1000 となる。
            double pW2 = 1000, pH2 = 1000;
            double vX2 = 0, vY2 = 0, vW2 = 1000, vH2 = 1000;
            double wX2 = 3, wY2 = 0, wW2 = 997, wH2 = 1000;
            calculator4.AddAdjustProcess(pW2, pH2, vX2, vY2, vW2, vH2, wX2, wY2, wW2, wH2);
            // DpiForMonitor は 144 で、DpiForWindow も 144 とする。同じなので1.5倍などは不要。
            var windowRect3 = new Rect
            {
                left = extendedFrameBounds2.left - 5,    // =   0 - 5 =  -5
                top = extendedFrameBounds2.top - 5,      // =   1 - 5 =  -4
                right = extendedFrameBounds2.right + 5,  // = 139 + 5 = 144
                bottom = extendedFrameBounds2.bottom + 5 // = 138 + 5 = 143
            };
            var (x5, y5, w5, h5) = calculator4.Calculate(windowRect3, extendedFrameBounds2, 144, 144, 144);
            // DpiForMonitor が 144 と、標準の 96dpi より高いので、タスクバーも1.5倍して考慮する。
            Assert.AreEqual(-0.5, x5);  // -5 + 3 * 1.5 = -0.5
            Assert.AreEqual(-5, y5);    // -5 + 0 * 1.5 = -5
            Assert.AreEqual(238.5, w5); // (149 + 5 + 5) * 1.5 = 238.5
            Assert.AreEqual(241.5, h5); // (151 + 5 + 5) * 1.5 = 241.5
        }
    }
}