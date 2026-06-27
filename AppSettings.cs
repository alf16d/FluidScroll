using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace FluidScroll
{
    internal sealed class AppSettings
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public bool SmoothScrollEnabled { get; set; } = true;
        public double AccelDeltaMs { get; set; } = 70.0;
        public double AccelMaxX { get; set; } = 7.0;
        public double ScrollBoost { get; set; } = 2.0;
        public double CatchupPerMs { get; set; } = 0.005;
        public double FrictionPerMs { get; set; } = 0.998;

        public static string SettingsPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "FluidScroll", "settings.json");
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load settings: {ex.Message}");
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                SmoothScrollEnabled = SmoothScrollEnabled,
                AccelDeltaMs = AccelDeltaMs,
                AccelMaxX = AccelMaxX,
                ScrollBoost = ScrollBoost,
                CatchupPerMs = CatchupPerMs,
                FrictionPerMs = FrictionPerMs
            };
        }

        public void ApplyTo(FluidScrollEngine engine)
        {
            engine.IsEnabled = SmoothScrollEnabled;
            engine.AccelDeltaMs = AccelDeltaMs;
            engine.AccelMaxX = AccelMaxX;
            engine.ScrollBoost = ScrollBoost;
            engine.CatchupPerMs = CatchupPerMs;
            engine.FrictionPerMs = FrictionPerMs;
        }
    }
}
