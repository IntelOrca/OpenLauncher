using System;
using System.IO;
using System.Text.Json;

namespace IntelOrca.OpenLauncher.Core
{
    public class ConfigService
    {
        private static string ConfigFileDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenLauncher");
        private static string ConfigFilePath => Path.Combine(ConfigFileDir, "config.json");

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private Config _config;

        public ConfigService()
        {
            if (!File.Exists(ConfigFilePath))
            {
                SaveConfig();
            }
            else
            {
                try
                {
                    var configJson = File.ReadAllText(ConfigFilePath);
                    _config = JsonSerializer.Deserialize<Config>(configJson, _serializerOptions);
                }
                catch
                {
                }
            }
        }

        private void SaveConfig()
        {
            try
            {
                var configJson = JsonSerializer.Serialize(_config, _serializerOptions);
                Directory.CreateDirectory(ConfigFileDir);
                File.WriteAllText(ConfigFilePath, configJson);
            }
            catch
            {
            }
        }

        public bool PreReleaseChecked
        {
            get => _config.PreReleaseChecked;
            set
            {
                _config.PreReleaseChecked = value;
                SaveConfig();
            }
        }

        public int SelectingGame
        {
            get => _config.SelectingGame;
            set
            {
                _config.SelectingGame = value;
                SaveConfig();
            }
        }
    }

    internal struct Config
    {
        public bool PreReleaseChecked { get; set; }
        public int SelectingGame { get; set; }
    }
}
