using Newtonsoft.Json;
using SigmabotSync.Domain.Config;
using System;
using System.IO;

namespace SigmabotSync.Infrastructure.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            _settingsPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "settings.json");
        }

        public AconexSettings Load()
        {
            if (!File.Exists(_settingsPath))
                return new AconexSettings(); // vacío por defecto

            var json = File.ReadAllText(_settingsPath);
            return JsonConvert.DeserializeObject<AconexSettings>(json);
        }

        public void Save(AconexSettings settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
        }
    }
}
