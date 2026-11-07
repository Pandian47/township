using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder.Tests
{
    public sealed class SaveSystemTests
    {
        private string _dir;
        private JsonSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "TownBuilderTests", System.Guid.NewGuid().ToString("N"));
            _store = new JsonSaveStore(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static SaveData Sample()
        {
            var data = NewGameFactory.Create(1_700_000_000_000, 1234, 7);
            data.player.level = 3;
            data.player.xp = 450;
            data.buildings.Add(new BuildingSaveData { instanceId = "abc", definitionId = "barn", x = 4, y = 5 });
            data.inventory.Add(new ItemStackSaveData { itemId = "wheat", amount = 12 });
            data.cropPlots.Add(new CropPlotSaveData { plotInstanceId = "p1", cropId = "wheat", plantedAtUnixMs = 1_700_000_100_000 });
            return data;
        }

        [Test]
        public void NoFile_ReturnsNoSave()
        {
            Assert.IsFalse(_store.HasSave);
            Assert.AreEqual(LoadStatus.NoSave, _store.Load().Status);
        }

        [Test]
        public void SaveThenLoad_RoundTripsAllFields()
        {
            _store.Save(Sample());
            var result = _store.Load();

            Assert.AreEqual(LoadStatus.Loaded, result.Status);
            var d = result.Data;
            Assert.AreEqual(SaveMigrator.CurrentVersion, d.schemaVersion);
            Assert.AreEqual(1234, d.player.coins);
            Assert.AreEqual(7, d.player.gems);
            Assert.AreEqual(3, d.player.level);
            Assert.AreEqual(450, d.player.xp);
            Assert.AreEqual(1, d.buildings.Count);
            Assert.AreEqual("barn", d.buildings[0].definitionId);
            Assert.AreEqual(4, d.buildings[0].x);
            Assert.AreEqual(5, d.buildings[0].y);
            Assert.AreEqual(12, d.inventory[0].amount);
            Assert.AreEqual(1_700_000_100_000, d.cropPlots[0].plantedAtUnixMs);
        }

        [Test]
        public void SecondSave_KeepsPreviousAsBackup()
        {
            var data = Sample();
            _store.Save(data);
            data.player.coins = 99;
            _store.Save(data);

            Assert.IsTrue(File.Exists(_store.BackupPath));
            Assert.IsFalse(File.Exists(_store.TempPath));
            Assert.AreEqual(99, _store.Load().Data.player.coins);
        }

        [Test]
        public void CorruptedMain_FallsBackToBackup()
        {
            var data = Sample();
            _store.Save(data);
            data.player.coins = 99;
            _store.Save(data); // backup now holds coins = 1234

            File.WriteAllText(_store.MainPath, "{ this is not json");
            var result = _store.Load();

            Assert.AreEqual(LoadStatus.LoadedFromBackup, result.Status);
            Assert.AreEqual(1234, result.Data.player.coins);
        }

        [Test]
        public void CorruptedMainWithoutBackup_ReportsCorrupted()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_store.MainPath, "");
            Assert.AreEqual(LoadStatus.Corrupted, _store.Load().Status);
        }

        [Test]
        public void SaveFromNewerVersion_IsNotLoaded()
        {
            var data = Sample();
            Directory.CreateDirectory(_dir);
            data.schemaVersion = SaveMigrator.CurrentVersion + 1;
            File.WriteAllText(_store.MainPath, JsonUtility.ToJson(data));
            Assert.AreEqual(LoadStatus.FromNewerVersion, _store.Load().Status);
        }

        [Test]
        public void Delete_RemovesAllFiles()
        {
            _store.Save(Sample());
            _store.Save(Sample());
            _store.Delete();
            Assert.IsFalse(_store.HasSave);
        }

        [Test]
        public void Sanitize_RepairsInvalidValues()
        {
            var data = Sample();
            data.player.coins = -50;
            data.player.level = 0;
            data.buildings.Add(new BuildingSaveData { instanceId = "abc", definitionId = "barn" }); // duplicate id
            data.buildings.Add(new BuildingSaveData { instanceId = "x", definitionId = "" });       // no definition
            data.buildings.Add(new BuildingSaveData { instanceId = "", definitionId = "silo" });    // gets a new id
            data.inventory.Add(new ItemStackSaveData { itemId = "corn", amount = 0 });

            var warnings = new List<string>();
            Assert.AreEqual(MigrationStatus.UpToDate, SaveMigrator.Migrate(data, warnings));

            Assert.AreEqual(0, data.player.coins);
            Assert.AreEqual(1, data.player.level);
            Assert.AreEqual(2, data.buildings.Count);
            Assert.IsFalse(string.IsNullOrEmpty(data.buildings[1].instanceId));
            Assert.AreEqual(1, data.inventory.Count);
            Assert.IsNotEmpty(warnings);
        }

        [Test]
        public void MissingListsInJson_AreRestored()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_store.MainPath, "{\"schemaVersion\":1,\"player\":{\"level\":2,\"coins\":10}}");
            var result = _store.Load();
            Assert.AreEqual(LoadStatus.Loaded, result.Status);
            Assert.NotNull(result.Data.buildings);
            Assert.NotNull(result.Data.inventory);
            Assert.AreEqual(2, result.Data.player.level);
        }

        [Test]
        public void TownMapper_RoundTripsLayoutAndWallet()
        {
            var town = new Town(new GridRect(0, 0, 20, 20));
            town.TryAdd("b1", "barn", new GridCoord(3, 4), new GridCoord(2, 2), out _);
            var wallet = new Wallet(500, 5);
            var progress = new PlayerProgress(2, 10);
            var save = NewGameFactory.Create(0, 0, 0);

            TownSaveMapper.WriteTo(save, town, wallet, progress);

            Assert.AreEqual(500, save.player.coins);
            Assert.AreEqual(2, save.player.level);
            Assert.AreEqual(1, save.buildings.Count);
            Assert.AreEqual("b1", save.buildings[0].instanceId);
            Assert.AreEqual(3, save.buildings[0].x);
        }
    }
}
