using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TouchFaders_MIDI {
	public class AppConfiguration {
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
			_ = Directory.CreateDirectory("config");
			if (File.Exists("config/config.json")) {
				string configFile = File.ReadAllText("config/config.json");
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
			}
			return config;
		}

		public static async Task Save (appconfig config) {
			if (config == null) return;
			JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true };
			_ = Directory.CreateDirectory("config");
			using (FileStream fs = File.Create("config/config.json")) {
				await JsonSerializer.SerializeAsync(fs, config, jsonSerializerOptions);
			}
		}
	}

}
