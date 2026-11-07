using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    public enum LoadStatus
    {
        Loaded,
        LoadedFromBackup,
        NoSave,
        Corrupted,
        FromNewerVersion,
    }

    public readonly struct LoadResult
    {
        public LoadResult(LoadStatus status, SaveData data, IReadOnlyList<string> warnings)
        {
            Status = status;
            Data = data;
            Warnings = warnings ?? Array.Empty<string>();
        }

        public LoadStatus Status { get; }
        public SaveData Data { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Status == LoadStatus.Loaded || Status == LoadStatus.LoadedFromBackup;
    }

    /// <summary>
    /// Offline JSON save file with crash-safe writes:
    ///   1. write the new save to town.json.tmp and flush it to disk
    ///   2. atomically swap it into town.json, keeping the previous file as town.json.bak
    /// If town.json is unreadable on load, town.json.bak is used instead.
    /// </summary>
    public sealed class JsonSaveStore
    {
        public const string FileName = "town.json";

        public JsonSaveStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Save directory is required.", nameof(directory));
            Directory = directory;
            MainPath = Path.Combine(directory, FileName);
            BackupPath = MainPath + ".bak";
            TempPath = MainPath + ".tmp";
        }

        public string Directory { get; }
        public string MainPath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }

        public bool HasSave => File.Exists(MainPath) || File.Exists(BackupPath);

        public void Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.schemaVersion = SaveMigrator.CurrentVersion;

            System.IO.Directory.CreateDirectory(Directory);
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (!File.Exists(MainPath))
            {
                File.Move(TempPath, MainPath);
                return;
            }

            try
            {
                File.Replace(TempPath, MainPath, BackupPath, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceFallback();
            }
            catch (IOException)
            {
                ReplaceFallback();
            }
        }

        public LoadResult Load()
        {
            bool mainExists = File.Exists(MainPath);
            bool backupExists = File.Exists(BackupPath);
            if (!mainExists && !backupExists) return new LoadResult(LoadStatus.NoSave, null, null);

            var warnings = new List<string>();
            if (mainExists)
            {
                var status = TryRead(MainPath, warnings, out var data);
                if (status == MigrationStatus.UpToDate || status == MigrationStatus.Migrated)
                    return new LoadResult(LoadStatus.Loaded, data, warnings);
                if (status == MigrationStatus.FromNewerVersion)
                    return new LoadResult(LoadStatus.FromNewerVersion, null, warnings);
                warnings.Add($"Main save unreadable, trying backup.");
            }

            if (backupExists)
            {
                var status = TryRead(BackupPath, warnings, out var data);
                if (status == MigrationStatus.UpToDate || status == MigrationStatus.Migrated)
                    return new LoadResult(LoadStatus.LoadedFromBackup, data, warnings);
                if (status == MigrationStatus.FromNewerVersion)
                    return new LoadResult(LoadStatus.FromNewerVersion, null, warnings);
            }

            return new LoadResult(LoadStatus.Corrupted, null, warnings);
        }

        public void Delete()
        {
            foreach (var path in new[] { MainPath, BackupPath, TempPath })
                if (File.Exists(path)) File.Delete(path);
        }

        private static MigrationStatus TryRead(string path, List<string> warnings, out SaveData data)
        {
            data = null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return MigrationStatus.Invalid;
                data = JsonUtility.FromJson<SaveData>(json);
                return SaveMigrator.Migrate(data, warnings);
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                warnings.Add($"Failed to read {Path.GetFileName(path)}: {e.Message}");
                data = null;
                return MigrationStatus.Invalid;
            }
        }

        private void ReplaceFallback()
        {
            File.Copy(MainPath, BackupPath, overwrite: true);
            File.Delete(MainPath);
            File.Move(TempPath, MainPath);
        }
    }
}
