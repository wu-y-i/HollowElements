using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HollowElements.Game
{
    // Keep recent decoded illustrations across page changes and inventory closes.
    // The current page stays pinned; the rest share a 32 MiB LRU budget.
    internal sealed class ElementGuideImageCache : IDisposable
    {
        private const long Budget = 32L * 1024 * 1024;
        private readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private long clock;
        internal int Decodes { get; private set; }
        internal int Count => items.Count;
        internal long Bytes => items.Values.Sum(e => e.Bytes);
        private sealed class Entry
        {
            internal Texture2D Texture;
            internal long Length, Written, Used;
            internal long Bytes => (long)Texture.width * Texture.height * 4;
        }
        private static bool Matches(string path, Entry entry)
        {
            var file = new FileInfo(path);
            return file.Exists && file.Length == entry.Length && file.LastWriteTimeUtc.Ticks == entry.Written;
        }
        internal bool Changed(IEnumerable<Texture2D> current)
        {
            var textures = new HashSet<Texture2D>(current);
            return items.Any(pair => textures.Contains(pair.Value.Texture) && !Matches(pair.Key, pair.Value));
        }
        internal Texture2D Get(string path)
        {
            Entry entry;
            if (items.TryGetValue(path, out entry))
            {
                if (Matches(path, entry)) { entry.Used = ++clock; return entry.Texture; }
                UnityEngine.Object.Destroy(entry.Texture); items.Remove(path);
            }
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 16 * 1024 * 1024) throw new InvalidDataException("图片不存在或超过 16MB。");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "HollowElements.ElementGuideImage", wrapMode = TextureWrapMode.Clamp };
            try
            {
                // ElementGuide pictures never need CPU pixel access after upload.
                if (!texture.LoadImage(File.ReadAllBytes(path), true) || texture.width > 4096 || texture.height > 4096)
                    throw new InvalidDataException("图片无效或边长超过 4096。");
                items[path] = new Entry { Texture = texture, Length = file.Length, Written = file.LastWriteTimeUtc.Ticks, Used = ++clock };
                Decodes++;
                return texture;
            }
            catch { UnityEngine.Object.Destroy(texture); throw; }
        }
        internal void Trim(IEnumerable<Texture2D> current)
        {
            var pinned = new HashSet<Texture2D>(current);
            long bytes = Bytes;
            foreach (var pair in items.Where(p => !pinned.Contains(p.Value.Texture)).OrderBy(p => p.Value.Used).ToArray())
            {
                if (bytes <= Budget) break;
                bytes -= pair.Value.Bytes; UnityEngine.Object.Destroy(pair.Value.Texture); items.Remove(pair.Key);
            }
        }
        public void Dispose()
        {
            foreach (var entry in items.Values) if (entry.Texture) UnityEngine.Object.Destroy(entry.Texture);
            items.Clear();
        }
    }
}
