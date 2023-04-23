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
        }
    }
}