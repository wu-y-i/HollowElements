using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace HollowElements.ElementGuide
{
    public sealed class ElementGuideIndex
    {
        public int Version = 1;
        public string Title = "元素系统";
        public ElementGuideLayout Layout = new ElementGuideLayout();
        public List<ElementGuideCategory> Categories = new List<ElementGuideCategory>();
    }
    public sealed class ElementGuideLayout
    {
        public int FontSize = 24;
        public int SidebarWidth = 290;
        public int ImageMaxHeight = 240;
        public int Spacing = 14;
    }
    public sealed class ElementGuideCategory
    {
        public string Title = "";
        public List<ElementGuideEntry> Entries = new List<ElementGuideEntry>();
    }
    public sealed class ElementGuideEntry
    {
        public string Id = "", Title = "", File = "";
        public string[] Scenes = Array.Empty<string>();
        public ElementGuidePageLayout Layout;
        [JsonIgnore] public string Category, Source;
    }
    public sealed class ElementGuidePageLayout
    {
        public int? FontSize, ImageMaxHeight, Spacing;
    }
    public sealed class ElementGuideBlock
    {
        public string Kind, Text, Path;
        public int Level;
        public List<string[]> Rows;
    }
    public sealed class ElementGuideBook
    {
        public readonly string Root;
        public ElementGuideIndex Index { get; private set; }
        public List<ElementGuideEntry> Entries { get; private set; }
        private Dictionary<string, FileStamp> files = new Dictionary<string, FileStamp>();
        public ElementGuideBook(string root) { Root = Path.GetFullPath(root); }
        public void Load()
        {
            var candidateFiles = new Dictionary<string, FileStamp>();
            candidateFiles[Resolve("index.json")] = Stamp(Resolve("index.json"));
            string text = Read("index.json", 1024 * 1024);
            var candidate = JsonConvert.DeserializeObject<ElementGuideIndex>(text, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
            if (candidate == null || candidate.Version != 1 || candidate.Categories == null || candidate.Layout == null) throw new InvalidDataException("图鉴目录版本或结构无效。");
            ValidateLayout(candidate.Layout);
            var entries = new List<ElementGuideEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var scenes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var category in candidate.Categories)
            {
                if (category == null || category.Entries == null) throw new InvalidDataException("图鉴分类为空。");
                foreach (var entry in category.Entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id) || string.IsNullOrWhiteSpace(entry.Title)) throw new InvalidDataException("图鉴条目 ID 重复或缺少标题。");
                    Resolve(entry.File); if (entry.Layout != null) ValidateLayout(LayoutFor(candidate.Layout, entry.Layout));
                    if(entry.Scenes == null || entry.Scenes.Any(s=>string.IsNullOrWhiteSpace(s) || !scenes.Add(s))) throw new InvalidDataException("图鉴场景映射为空或重复。");
                    candidateFiles[Resolve(entry.File)] = Stamp(Resolve(entry.File));
                    entry.Category = category.Title; entry.Source = Read(entry.File, 512 * 1024); entries.Add(entry);
                    if (entries.Count > 500) throw new InvalidDataException("图鉴条目超过 500 项。");
                }
            }
            if (entries.Count == 0) throw new InvalidDataException("图鉴没有条目。");
            Index = candidate; Entries = entries; files = candidateFiles; // Replace only after the complete book validates.
        }
        // Normal opens only stat the loaded documents. Explicit reload still reads
        // everything, including edits that deliberately preserve timestamps.
        public bool HasChanges()
        {
            try { return Index == null || files.Any(f => !f.Value.Equals(Stamp(f.Key))); }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }
        private static FileStamp Stamp(string path)
        {
            var file = new FileInfo(path);
            return new FileStamp { Length = file.Exists ? file.Length : -1, Written = file.LastWriteTimeUtc.Ticks };
        }
        private struct FileStamp { public long Length, Written; }
        public string Resolve(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0) throw new InvalidDataException("图鉴路径必须是相对路径。");
            string path = Path.GetFullPath(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("图鉴路径超出 ElementGuide 文件夹。");
            return path;
        }
        public int EntryForScene(string scene) => Entries == null || scene == null || !scene.StartsWith("GG_", StringComparison.Ordinal) ? -1 : Entries.FindIndex(e=>e.Scenes.Contains(scene));
        private string Read(string relative, int limit)
        {
            string path = Resolve(relative);
            if (new FileInfo(path).Length > limit) throw new InvalidDataException("图鉴文件过大：" + relative);
            return File.ReadAllText(path, Encoding.UTF8);
        }
        private static void ValidateLayout(ElementGuideLayout l)
        {
            if (l.FontSize < 18 || l.FontSize > 40 || l.SidebarWidth < 200 || l.SidebarWidth > 480 || l.ImageMaxHeight < 64 || l.ImageMaxHeight > 700 || l.Spacing < 4 || l.Spacing > 40) throw new InvalidDataException("图鉴布局参数超出支持范围。");
        }
        public static ElementGuideLayout LayoutFor(ElementGuideLayout defaults, ElementGuidePageLayout page)
        {
            return new ElementGuideLayout { FontSize = page?.FontSize ?? defaults.FontSize, SidebarWidth = defaults.SidebarWidth,
                ImageMaxHeight = page?.ImageMaxHeight ?? defaults.ImageMaxHeight, Spacing = page?.Spacing ?? defaults.Spacing };
        }
        public static List<ElementGuideBlock> Parse(string source)
        {
            var blocks = new List<ElementGuideBlock>(); var paragraph = new List<string>();
            Action flush = () => { if (paragraph.Count > 0) { blocks.Add(new ElementGuideBlock { Kind = "text", Text = string.Join(" ", paragraph) }); paragraph.Clear(); } };
            var lines = source.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) { flush(); continue; }
                var image = Regex.Match(line, @"^!\[(.*?)\]\(([^)]+)\)$");
                if (image.Success) { flush(); blocks.Add(new ElementGuideBlock { Kind = "image", Text = image.Groups[1].Value, Path = image.Groups[2].Value }); continue; }
                var heading = Regex.Match(line, @"^(#{1,3})\s+(.+)$");
                if (heading.Success) { flush(); blocks.Add(new ElementGuideBlock { Kind = "heading", Text = heading.Groups[2].Value, Level = heading.Groups[1].Length }); continue; }
                if (line.StartsWith("|") && i + 1 < lines.Length && Regex.IsMatch(lines[i + 1].Trim(), @"^\|?[\s:|\-]+\|?$"))
                {
                    flush(); var rows = new List<string[]> { Cells(line) }; i++;
                    while (i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("|")) rows.Add(Cells(lines[++i]));
                    blocks.Add(new ElementGuideBlock { Kind = "table", Rows = rows }); continue;
                }
                if (line == "---") { flush(); blocks.Add(new ElementGuideBlock { Kind = "rule" }); continue; }
                if (line.StartsWith("- ") || line.StartsWith("* ") || Regex.IsMatch(line, @"^\d+\.\s"))
                { flush(); blocks.Add(new ElementGuideBlock { Kind = "list", Text = line.StartsWith("- ") || line.StartsWith("* ") ? "· " + line.Substring(2) : line }); continue; }
                if (line.StartsWith(">")) { flush(); blocks.Add(new ElementGuideBlock { Kind = "note", Text = line.Substring(1).Trim() }); continue; }
                paragraph.Add(line);
            }
            flush(); return blocks;
        }
        private static string[] Cells(string line) { return line.Trim().Trim('|').Split('|').Select(s => s.Trim()).ToArray(); }
        public static string Inline(string text)
        {
            text = text.Replace("<", "‹").Replace(">", "›");
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
            text = Regex.Replace(text, @"`([^`]+)`", "$1");
            return Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");
        }
    }
}
