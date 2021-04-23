using Microsoft.VisualStudio.TestTools.UnitTesting;
using Size;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Size.Tests
{
    [TestClass()]
    public class BoundsCalculatorTests
    {
        [TestMethod()]
        public void CalculateTest()
        {
            // -a を付けない場合のテスト
            // X=5、Y=10、幅=100 にする。
            var calculator1 = new BoundsCalculator(new int[] { 5, 10, 100 });
            // 現在の透明部を除いたウィンドウは、(20, 20)、200×200 とする。
            var extendedFrameBounds = new Rect
            {
                left = 20,
                top = 20,
                right = 20 + 200,
                bottom = 20 + 200
            };
            // ウィンドウの左と上には 10 の透明部、右と下には透明部なしとする。
            var windowRect = new Rect
            {
                left = extendedFrameBounds.left - 10,
                top = extendedFrameBounds.top - 10,
                right = extendedFrameBounds.right,
                bottom = extendedFrameBounds.bottom
            };
            var (x, y, width, height) = calculator1.Calculate(windowRect, extendedFrameBounds);
            // X=5 が指定されており、透明部が 10 あるので、X = -5。
            Assert.AreEqual(x, -5);
            // Y=10 が指定されており、透明部が 10 あるので、Y = 0。
            Assert.AreEqual(y, 0);
            // 幅=100 が指定されており、右透明部は無いので、幅 = 左透明部 + 指定幅 = 110
            Assert.AreEqual(width, 110);
            // 高さが指定されておらず、下透明部は無いので、高さ = 上透明部 + 現在高さ = 210
            Assert.AreEqual(height, 210);
        }
    }
}