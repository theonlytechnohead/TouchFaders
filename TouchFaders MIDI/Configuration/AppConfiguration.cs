using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TouchFaders_MIDI {
	public class AppConfiguration {

		public const string CONFIG_DIR = "config";
		public const string CONFIG_FILE = "config";
		public const string DATA_FILE = "data";

		// Constants and stuff goes here
		public class appconfig {
			public Mixer mixer { get; set; }
			public int device_ID { get; set; }
			public int NUM_CHANNELS { get; set; }
			public int NUM_MIXES { get; set; }

			public static appconfig defaultValues () {
				return new appconfig() {
					mixer = Mixer.LS932,
					device_ID = 1,
					NUM_MIXES = 8,
					NUM_CHANNELS = 32
				};
			}
		}

		public static appconfig Load () {
			appconfig config;
			_ = Directory.CreateDirectory(CONFIG_DIR);
			if (File.Exists($"{CONFIG_DIR}/{CONFIG_FILE}.json")) {
				string configFile = File.ReadAllText($"{CONFIG_DIR}/{CONFIG_FILE}.json");
				config = JsonSerializer.Deserialize<appconfig>(configFile);
				if (config.NUM_MIXES == 0) {
					config.NUM_MIXES = appconfig.defaultValues().NUM_MIXES;
				}
				if (config.NUM_CHANNELS == 0) {
					config.NUM_CHANNELS = appconfig.defaultValues().NUM_CHANNELS;
				}
				if (config.device_ID == 0) {
					config.device_ID = appconfig.defaultValues().device_ID;
				}
				if (config.mixer == null) {
					config.mixer = appconfig.defaultValues().mixer;
				}
			} else {
				config = appconfig.defaultValues();
				_ = Save(config);
			}
			return config;
		}

		public static async Task Save (appconfig config) {
			if (config == null) return;
			JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true };
			_ = Directory.CreateDirectory(CONFIG_DIR);
            using FileStream fs = File.Create($"{CONFIG_DIR}/{CONFIG_FILE}.json");
            await JsonSerializer.SerializeAsync(fs, config, jsonSerializerOptions);
        }
	}

}
