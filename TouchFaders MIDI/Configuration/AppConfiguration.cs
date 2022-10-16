using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TouchFaders_MIDI {
	public class AppConfiguration {

		public const string CONFIG_DIR = "config";
		public const string CONFIG_FILE = "config";
		public const string DATA_FILE = "data";

		// Constants and stuff goes here
		public class Config {
			public Mixer MIXER { get; set; }
			public int DEVICE_ID { get; set; }
			public int NUM_CHANNELS { get; set; }
			public int NUM_MIXES { get; set; }

			public static Config defaultValues () {
				return new Config() {
					MIXER = Mixer.LS932,
					DEVICE_ID = 1,
					NUM_MIXES = 8,
					NUM_CHANNELS = 32
				};
			}
		}

		public static Config LoadConfig () {
			Config config;
			_ = Directory.CreateDirectory(CONFIG_DIR);
			if (File.Exists($"{CONFIG_DIR}/{CONFIG_FILE}.json")) {
				string configFile = File.ReadAllText($"{CONFIG_DIR}/{CONFIG_FILE}.json");
				config = JsonSerializer.Deserialize<Config>(configFile);
				if (config.NUM_MIXES == 0) {
					config.NUM_MIXES = Config.defaultValues().NUM_MIXES;
				}
				if (config.NUM_CHANNELS == 0) {
					config.NUM_CHANNELS = Config.defaultValues().NUM_CHANNELS;
				}
				if (config.DEVICE_ID == 0) {
					config.DEVICE_ID = Config.defaultValues().DEVICE_ID;
				}
				if (config.MIXER == null) {
					config.MIXER = Config.defaultValues().MIXER;
				}
			} else {
				config = Config.defaultValues();
				_ = SaveConfig(config);
			}
			return config;
		}

		public static async Task SaveConfig (Config config) {
			if (config == null) return;
			JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
			_ = Directory.CreateDirectory(CONFIG_DIR);
			using FileStream fs = File.Create($"{CONFIG_DIR}/{CONFIG_FILE}.json");
			await JsonSerializer.SerializeAsync(fs, config, jsonSerializerOptions);
		}

		public static Data LoadData () {
			Data data;
			try {
				string dataFile = File.ReadAllText($"{CONFIG_DIR}/{DATA_FILE}.json");
				data = JsonSerializer.Deserialize<Data>(dataFile);
			} catch (FileNotFoundException) {
				data = new Data();
				_ = SaveData(data);
			} catch (Exception ex) {
				Dispatcher.CurrentDispatcher.Invoke(() => System.Windows.MessageBox.Show(ex.StackTrace, ex.Message));
				data = new Data();
			}
			return data;
		}

		public static async Task SaveData (Data data) {
			JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
			_ = Directory.CreateDirectory(CONFIG_DIR);
			if (data != null) {
				using FileStream fs = File.Create($"{CONFIG_DIR}/{DATA_FILE}.json");
				await JsonSerializer.SerializeAsync(fs, data, serializerOptions);
			}
		}
	}

}
