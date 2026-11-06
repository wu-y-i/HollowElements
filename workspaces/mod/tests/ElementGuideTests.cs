using System;
using System.IO;
using System.Linq;
using HollowElements.ElementGuide;

internal static class ElementGuideTests
{
    internal static void Documents()
    {
        string directory = Path.Combine(Path.GetTempPath(), "HollowElements-ElementGuide-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "page.md"), "# 龘与中文\n\n正文 **强调**。\n\n![说明](images/test.png)\n\n|甲|乙|\n|---|---|\n|1|2|\n\n- 列表\n\n> 提示");
            string index = "{\"Version\":1,\"Layout\":{\"FontSize\":30},\"Categories\":[{\"Title\":\"分类\",\"Entries\":[{\"Id\":\"test\",\"Title\":\"测试\",\"File\":\"page.md\",\"Scenes\":[\"GG_Test\"],\"Layout\":{\"ImageMaxHeight\":300}}]}]}";
            string path = Path.Combine(directory, "index.json");File.WriteAllText(path, index);
            var book = new ElementGuideBook(directory);book.Load();
            if (book.HasChanges()) throw new Exception("未修改的图鉴不应重新加载。");
            string original = File.ReadAllText(Path.Combine(directory, "page.md"));
            File.AppendAllText(Path.Combine(directory, "page.md"), "\n外部修改");
            if (!book.HasChanges()) throw new Exception("正文修改未被检测。");
            book.Load();
            if (book.HasChanges() || !book.Entries[0].Source.Contains("外部修改")) throw new Exception("正文缓存没有更新。");
            File.WriteAllText(Path.Combine(directory, "page.md"), original); book.Load();
            var blocks = ElementGuideBook.Parse(book.Entries[0].Source);
            if(book.EntryForScene("GG_Test")!=0 || book.EntryForScene("GG_Atrium")!=-1 || book.EntryForScene("Crossroads_01")!=-1) throw new Exception("Boss 场景自动定位错误。");
            if (!blocks.Any(b => b.Kind == "image") || blocks.Single(b => b.Kind == "table").Rows.Count != 2 || !blocks.Any(b => b.Kind == "note")) throw new Exception("Markdown 块解析失败。");
            var layout = ElementGuideBook.LayoutFor(book.Index.Layout, book.Entries[0].Layout);
            if (layout.FontSize != 30 || layout.ImageMaxHeight != 300) throw new Exception("页面布局未继承默认参数。");
            var previous = book.Index;
            foreach (string invalid in new[] { "{broken", index.Replace("page.md", "../outside.md"), index.Replace("\"Version\":1", "\"Version\":2"), index.Replace("\"FontSize\":30", "\"FontSize\":0"), index.Replace("page.md", "missing.md") })
            {
                File.WriteAllText(path, invalid);bool rejected = false;
                try { book.Load(); } catch { rejected = true; }
                if (!rejected || !ReferenceEquals(previous, book.Index)) throw new Exception("无效图鉴没有保留上次有效内容。");
                if (!book.HasChanges()) throw new Exception("失败重载不应吞掉文档变更。");
            }
            File.WriteAllText(path, index); book.Load();
            File.Delete(Path.Combine(directory, "page.md"));
            if (!book.HasChanges()) throw new Exception("正文删除未被检测。");
        }
        finally { Directory.Delete(directory, true); }
    }
}
