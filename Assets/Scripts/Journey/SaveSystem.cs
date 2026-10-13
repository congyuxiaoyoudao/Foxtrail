using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Hiking.Journey
{
    // 进度档的读写。
    //
    // 关键在"原子写入"：先写临时文件 → 读回来验证 → 才替换正式档，旧档转成 .bak。
    // 如果直接往正式档上写，写一半崩溃或断电，玩家这一局就没了。
    // 读档时正式档坏掉就自动回退备份。
    public static class SaveSystem
    {
        public const string FileName = "journey_save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        public static string BackupPath => SavePath + ".bak";
        public static string TempPath => SavePath + ".tmp";

        public static bool Exists => File.Exists(SavePath) || File.Exists(BackupPath);

        public static bool Write(SaveData data)
        {
            if (data == null) return false;
            try
            {
                data.schemaVersion = SaveData.CurrentVersion;
                data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
                File.WriteAllText(TempPath, JsonUtility.ToJson(data, true), new UTF8Encoding(false));

                // 读不回来就当作这次没存成功，宁可丢这一次，也不能毁掉已有的档
                if (ReadFrom(TempPath) == null)
                {
                    Debug.LogWarning("存档校验失败，已放弃本次写入：" + TempPath);
                    return false;
                }

                if (File.Exists(SavePath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(SavePath, BackupPath);
                }
                File.Move(TempPath, SavePath);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("存档失败：" + error.Message);
                return false;
            }
        }

        public static SaveData Read()
        {
            return ReadFrom(SavePath) ?? ReadFrom(BackupPath);
        }

        static SaveData ReadFrom(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null) return null;
                if (data.schemaVersion != 1 && data.schemaVersion != SaveData.CurrentVersion)
                {
                    Debug.LogWarning("存档版本不匹配，已忽略：" + data.schemaVersion);
                    return null;
                }
                return data;
            }
            catch (Exception error)
            {
                Debug.LogWarning("读档失败：" + path + " · " + error.Message);
                return null;
            }
        }

        // 保存时间（UTC），离线推进要用它算间隔
        public static DateTime SavedAtUtc(SaveData data) =>
            new DateTime(data == null ? 0L : data.savedAtUtcTicks, DateTimeKind.Utc);

        public static void Delete()
        {
            foreach (var path in new[] { SavePath, BackupPath, TempPath })
                if (File.Exists(path)) File.Delete(path);
        }
    }
}
