using System;
using System.IO;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// Reads and writes the save file.
    ///
    /// JSON in persistentDataPath, written atomically. Not because JSON is fast
    /// but because a corrupted save you can open in a text editor is a bug report
    /// you can actually action, and a binary one is not.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "save.json";
        private const string BackupName = "save.backup.json";

        private static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);
        private static string BackupPath => System.IO.Path.Combine(Application.persistentDataPath, BackupName);

        public static SaveData Load()
        {
            SaveData data = TryRead(Path);

            // A crash mid-write leaves a truncated primary file. The backup is the
            // previous known-good save, which is a far better outcome than a
            // player losing everything to one bad shutdown.
            if (data == null)
            {
                data = TryRead(BackupPath);
                if (data != null) Debug.LogWarning("Primary save was unreadable; recovered from backup.");
            }

            if (data == null) return new SaveData();

            Migrate(data);
            return data;
        }

        public static bool Save(SaveData data)
        {
            if (data == null) return false;

            data.Version = SaveData.CurrentVersion;

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string temp = Path + ".tmp";

                // Write to a temp file, keep the old file as the backup, then move
                // the temp into place. A crash at any point leaves either the old
                // save or the new one intact - never a half-written file.
                File.WriteAllText(temp, json);

                if (File.Exists(Path))
                {
                    File.Copy(Path, BackupPath, overwrite: true);
                    File.Delete(Path);
                }

                File.Move(temp, Path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write save: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete save: {e.Message}");
            }
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;

                SaveData data = JsonUtility.FromJson<SaveData>(json);

                // JsonUtility returns a default-constructed object for malformed
                // input rather than throwing, so an unset version is the tell.
                return data != null && data.Version > 0 ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save at {path} could not be read: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Brings an older save forward. Each step handles exactly one version
        /// bump, so a save from any past version walks up to the current one.
        /// </summary>
        private static void Migrate(SaveData data)
        {
            if (data.Version > SaveData.CurrentVersion)
            {
                // A save from a newer build. Loading it would silently drop the
                // fields this build does not know about, so refuse and keep it.
                Debug.LogWarning(
                    $"Save version {data.Version} is newer than this build ({SaveData.CurrentVersion}). " +
                    "Leaving it untouched.");
                return;
            }

            // while (data.Version < SaveData.CurrentVersion) { ... data.Version++; }
            data.Version = SaveData.CurrentVersion;
        }
    }
}
