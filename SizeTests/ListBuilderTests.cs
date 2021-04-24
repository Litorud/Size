using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Size.Tests
{
    [TestClass()]
    public class ListBuilderTests
    {
        [TestMethod()]
        public void BuildTest()
        {
            // ウィンドウ幅いっぱい - 1 までは列を揃えて出力すること。
            // それを超えると空白1文字区切りで出力すること。
            var builder = new ListBuilder();
            builder.Add("abcdefg", -32000, -32000, 1, 1);
            builder.Add("あ", 1, -1, 1, 1);
            builder.Add("abcdefあ", -1, -32000, 1, 9999);

            var results = builder.Build(29).ToArray();
            Assert.AreEqual("abcdefg -32000 -32000 1    1", results[0]);
            Assert.AreEqual("あ           1     -1 1    1", results[1]);
            Assert.AreEqual("abcdefあ     -1 -32000 1 9999", results[2]);
        }
    }
}