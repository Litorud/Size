using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Size
{
    public class ListBuilder
    {
        private readonly List<Entry> entries = new();

        public void Add(string title, double x, double y, double width, double height)
        {
            entries.Add(new Entry(title, x, y, width, height));
        }

        public IEnumerable<string> Build(int windowWidth)
        {
            int maxX = 0, maxY = 0, maxWidth = 0, maxHeight = 0;
            foreach (var entry in entries)
            {
                if (entry.X.Length > maxX) maxX = entry.X.Length;
                if (entry.Y.Length > maxY) maxY = entry.Y.Length;
                if (entry.Width.Length > maxWidth) maxWidth = entry.Width.Length;
                if (entry.Height.Length > maxHeight) maxHeight = entry.Height.Length;
            }

            // 以下の式で、最後のひく数を5ではなく4にすると、Windows PowerShell を起動したときに開くウィンドウで実行したときのみ、
            // 不要な折り返しが発生する（幅ちょうどに収まっているのに折り返しが発生する）。
            // ジャンプリストでは Windows PowerShell が開くため、これに最適化して引く数を5としている。
            int limitTitleWidth = windowWidth - maxX - maxY - maxWidth - maxHeight - 5;
            int maxTitleWidth = entries.Where(r => r.TitleWidth <= limitTitleWidth).Max(r => r.TitleWidth);

            foreach (var entry in entries)
            {
                var titlePadding = entry.TitleWidth < maxTitleWidth ? new string(' ', maxTitleWidth - entry.TitleWidth) : string.Empty;
                var xPadding = new string(' ', maxX - entry.X.Length);
                var yPadding = new string(' ', maxY - entry.Y.Length);
                var widthPadding = new string(' ', maxWidth - entry.Width.Length);
                var heightPadding = new string(' ', maxHeight - entry.Height.Length);

                yield return $"{entry.Title}{titlePadding} {xPadding}{entry.X} {yPadding}{entry.Y} {widthPadding}{entry.Width} {heightPadding}{entry.Height}";
            }
        }

        class Entry
        {
            private static readonly Encoding encoding;

            public string Title { get; private set; }
            public int TitleWidth { get; private set; }
            public string X { get; private set; }
            public string Y { get; private set; }
            public string Width { get; private set; }
            public string Height { get; private set; }

            static Entry()
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                encoding = Encoding.GetEncoding(932);
            }

            public Entry(string title, double x, double y, double width, double height)
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
