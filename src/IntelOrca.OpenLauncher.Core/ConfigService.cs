using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IntelOrca.OpenLauncher.Core
{
    public class ConfigService
    {
        private static string ConfigFileDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenLauncher");
        private static string ConfigFilePath => Path.Combine(ConfigFileDir, "config.json");

        private static readonly JsonSerializerOptions _serializerOptions = new ()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private Config _config = new();


        public ConfigService()
        {
            if (!File.Exists(ConfigFilePath))
            {
                SaveConfig();
            }
            else
            {
                var configJson = File.ReadAllText(ConfigFilePath);
                _config = JsonSerializer.Deserialize<Config>(configJson, _serializerOptions);
            }
        }

        private void SaveConfig()
        {
            var configJson = JsonSerializer.Serialize(_config, _serializerOptions);
            Directory.CreateDirectory(ConfigFileDir);
            File.WriteAllText(ConfigFilePath, configJson);
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
        public Config()
        {
        }
        
        public bool PreReleaseChecked { get; set; } = false;
        public int SelectingGame { get; set; } = 0;
    }
}
