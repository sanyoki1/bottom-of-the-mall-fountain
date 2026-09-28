// JSON save in persistentDataPath with an atomic write and a .bak fallback.
using System;
using System.IO;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.Game
{
    public static class SaveSystem
    {
        public static string FileName = "wishextractor_save.json";
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Load()
        {
            foreach (var p in new[] { FilePath, FilePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    var json = File.ReadAllText(p);
                    var d = JsonUtility.FromJson<SaveData>(json);
                    if (d != null) return d;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WishExtractor] could not read save {p}: {e.Message}");
                }
            }
            return null;
        }

        public static void Save(SaveData d)
        {
            try
            {
                d.lastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var json = JsonUtility.ToJson(d);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WishExtractor] save failed: {e.Message}");
            }
        }

        public static void Erase()
        {
            foreach (var p in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp" })
                try { if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }
}
