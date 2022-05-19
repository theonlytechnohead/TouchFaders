using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TouchFaders_MIDI {
	public class AppConfiguration {
		// Constants and stuff goes here
		public class appconfig {
			public int? config_version { get; set; }
			public int sendsToMix_version { get; set; }
			public int mutesToMix_version { get; set; }

			public int? channelConfig_version { get; set; } // Replaces channelNames_version and channelFaders_version
			public int? mixConfig_version { get; set; } // Replaces mixNames_version and mixFaders_version - eventually

			public Mixer mixer { get; set; }
			public int mixNames_version { get; set; }
			public int mixFaders_version { get; set; }
			public int device_ID { get; set; }
			public int NUM_CHANNELS { get; set; }
			public int NUM_MIXES { get; set; }

			public static appconfig defaultValues () {
				return new appconfig() {
					config_version = 5,
					sendsToMix_version = 1,
					mutesToMix_version = 1,
					channelConfig_version = 2,
					mixer = Mixer.LS932,
					mixConfig_version = 0,
					mixNames_version = 0,
					mixFaders_version = 0,
					device_ID = 1,
					NUM_MIXES = 8,
					NUM_CHANNELS = 32
				};
			}
		}

		public static appconfig Load () {
			appconfig config;
			_ = Directory.CreateDirectory("config");
			if (File.Exists("config/config.txt")) {
				string configFile = File.ReadAllText("config/config.txt");
				config = JsonSerializer.Deserialize<appconfig>(configFile);
				if (config.config_version == null) {
					config.config_version = appconfig.defaultValues().config_version;
				}
				if (config.config_version >= 1) {
					if (config.sendsToMix_version == 0) {
						config.sendsToMix_version = appconfig.defaultValues().sendsToMix_version;
					}
					if (config.mutesToMix_version == 0) {
						config.mutesToMix_version = appconfig.defaultValues().mutesToMix_version;
                    }
					if (config.channelConfig_version == null) {
						config.channelConfig_version = appconfig.defaultValues().channelConfig_version;
					}
				}
				if (config.config_version >= 2) {
					if (config.NUM_MIXES == 0) {
						config.NUM_MIXES = appconfig.defaultValues().NUM_MIXES;
					}
					if (config.NUM_CHANNELS == 0) {
						config.NUM_CHANNELS = appconfig.defaultValues().NUM_CHANNELS;
					}
				}
				if (config.config_version >= 3) {
					if (config.device_ID == 0) {
						config.device_ID = appconfig.defaultValues().device_ID;
					}
				}
				if (config.config_version >= 4) {
					if (config.mixer == null) {
						config.mixer = appconfig.defaultValues().mixer;
					}
				}
				if (config.config_version >= 6) {
					if (config.mixNames_version == 0) {
						config.mixNames_version = appconfig.defaultValues().mixNames_version;
					}
					if (config.mixFaders_version == 0) {
						config.mixFaders_version = appconfig.defaultValues().mixFaders_version;
					}
				}
			} else {
				config = appconfig.defaultValues();
			}
			return config;
		}

		public static async Task Save (appconfig config) {
			if (config == null) return;
			JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, };
			_ = Directory.CreateDirectory("config");
			using (FileStream fs = File.Create("config/config.txt")) {
				await JsonSerializer.SerializeAsync(fs, config, jsonSerializerOptions);
			}
		}
	}

}
