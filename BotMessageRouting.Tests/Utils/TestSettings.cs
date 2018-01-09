using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace BotMessageRouting.Tests.Utils
{
    /// <summary>
    /// Provides the test settings such as the Azure Table Storage connection string.
    /// </summary>
    public class TestSettings
    {
        public static readonly string TestSettingsFilename = "BotMessageRoutingTestSettings.json";
        private JObject _jsonSettings;

        public string this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentNullException("Key cannot be null or empty");
                }

                if (_jsonSettings == null)
                {
                    System.Diagnostics.Debug.WriteLine("No settings available");
                    return string.Empty;
                }

                return _jsonSettings[key].Value<string>();
            }
        }

        public TestSettings()
        {
            Load();
        }

        public bool Load()
        {
            bool wasLoaded = false;

            try
            {
                _jsonSettings = LoadSettingsJson();
                wasLoaded = true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load the test settings: {e.Message}");
            }

            return wasLoaded;
        }

        private JObject LoadSettingsJson()
        {
            string myDocumentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string settingsFilePath = Path.Combine(myDocumentsFolder, TestSettingsFilename);

            if (!File.Exists(settingsFilePath))
            {
                throw new FileNotFoundException($"Unable to find the test settings in {settingsFilePath}");
            }

            return JsonConvert.DeserializeObject<JObject>(File.ReadAllText(settingsFilePath));
        }
    }
}
