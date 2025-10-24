using System;
using System.IO;
using Xunit;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Tests
{
    public class ConfigManagerTests : IDisposable
    {
        private readonly string _originalCwd;
        private readonly string _tempDir;

        public ConfigManagerTests()
        {
            _originalCwd = Environment.CurrentDirectory;
            _tempDir = Path.Combine(Path.GetTempPath(), "ConfigManagerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            Environment.CurrentDirectory = _tempDir;
        }

        public void Dispose()
        {
            Environment.CurrentDirectory = _originalCwd;
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void Load_WhenNoFile_CreatesDefaultAndSaves()
        {
            // ensure no config in test folder
            Assert.False(File.Exists("config.json"));

            var cfg = ConfigManager.Load();

            Assert.NotNull(cfg);
            Assert.True(File.Exists("config.json"));

            // load again and compare some expected default values
            var cfg2 = ConfigManager.Load();
            Assert.Equal(cfg.AimBot, cfg2.AimBot);
            Assert.Equal(cfg.Esp.Box.Enabled, cfg2.Esp.Box.Enabled);
        }

        [Fact]
        public void Load_InvalidJson_FallsBackToDefault()
        {
            File.WriteAllText("config.json", "{ invalid json ");

            var cfg = ConfigManager.Load();

            Assert.NotNull(cfg);
            // Default AimBot is true in Default()
            Assert.True(cfg.AimBot);
        }

        [Fact]
        public void Save_WritesFile_WithExpectedProperties()
        {
            var cfg = ConfigManager.Default();
            cfg.AimBot = false;
            ConfigManager.Save(cfg);

            Assert.True(File.Exists("config.json"));
            var json = File.ReadAllText("config.json");
            Assert.Contains("\"aimBot\": false", json);
        }
    }
}
