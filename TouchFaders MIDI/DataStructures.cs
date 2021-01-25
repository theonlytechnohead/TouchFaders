using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TouchFaders_MIDI {
	class DataStructures {
	}

	public class AppConfiguration {
		// Constants and stuff goes here
		public class appconfig {
			public int config_version { get; set; }
			public int oscDevices_version { get; set; }
			public int sendsToMix_version { get; set; }
			public int channelNames_version { get; set; }
			public int channelFaders_version { get; set; }
			public int mixNames_version { get; set; }
			public int mixFaders_version { get; set; }
			public int device_ID { get; set; }
			public int NUM_CHANNELS { get; set; }
			public int NUM_MIXES { get; set; }
			public LinkedChannels linkedChannels { get; set; }

			public override string ToString () {
				return $"config_version: {config_version}";
			}

			public static appconfig defaultValues () {
				return new appconfig() {
					config_version = 3,
					oscDevices_version = 1,
					sendsToMix_version = 1,
					channelNames_version = 1,
					channelFaders_version = 1,
					mixNames_version = 0,
					mixFaders_version = 0,
					device_ID = 1,
					NUM_MIXES = 8,
					NUM_CHANNELS = 32,
					linkedChannels = new LinkedChannels() { links = new List<LinkedChannel>() { new LinkedChannel() { leftChannel = 4, rightChannel = 5 } } }
				};
			}

		}

		public static appconfig Load () {
			appconfig config;
			_ = Directory.CreateDirectory("config");
			if (File.Exists("config/config.txt")) {
				string configFile = File.ReadAllText("config/config.txt");
				config = JsonSerializer.Deserialize<appconfig>(configFile);
				if (config.config_version == 0) {
					config.config_version = appconfig.defaultValues().config_version;
				}
				if (config.oscDevices_version == 0) {
					config.oscDevices_version = appconfig.defaultValues().config_version;
				}
				if (config.sendsToMix_version == 0) {
					config.sendsToMix_version = appconfig.defaultValues().sendsToMix_version;
				}
				if (config.channelNames_version == 0) {
					config.channelNames_version = appconfig.defaultValues().channelNames_version;
				}
				if (config.channelFaders_version == 0) {
					config.channelFaders_version = appconfig.defaultValues().channelFaders_version;
				}
				if (config.mixNames_version == 0) {
					config.mixNames_version = appconfig.defaultValues().mixNames_version;
				}
				if (config.mixFaders_version == 0) {
					config.mixFaders_version = appconfig.defaultValues().mixFaders_version;
				}
				if (config.device_ID == 0) {
					config.device_ID = appconfig.defaultValues().device_ID;
				}
				if (config.NUM_MIXES == 0) {
					config.NUM_MIXES = appconfig.defaultValues().NUM_MIXES;
				}
				if (config.NUM_CHANNELS == 0) {
					config.NUM_CHANNELS = appconfig.defaultValues().NUM_CHANNELS;
				}
			} else {
				config = appconfig.defaultValues();
			}
			return config;
		}

		public static async Task Save (appconfig config) {
			JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, };
			_ = Directory.CreateDirectory("config");
			using (FileStream fs = File.Create("config/config.txt")) {
				await JsonSerializer.SerializeAsync(fs, config, jsonSerializerOptions);
			}
		}
	}

	public class SendsToMix {
		public event EventHandler sendsChanged;

		public int this[int mix, int channel] {
			get { return sendLevel[mix][channel]; }
			set { sendLevel[mix][channel] = value; sendsChanged?.Invoke(this, new EventArgs()); }
		}

		private List<List<int>> levels = (from mix in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select (from channel in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select 823).ToList()).ToList(); // Initalized to 0dB

		public List<List<int>> sendLevel {
			get { return levels; }
			set { levels = value; sendsChanged?.Invoke(this, new EventArgs()); }
		}
	}

	public class ChannelNames {
		public event EventHandler channelNamesChanged;

		private List<string> channelNames = (from channel in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select $"CH {channel}").ToList();

		public string this[int index] {
			get { return channelNames[index]; }
			set { channelNames[index] = value; channelNamesChanged?.Invoke(this, new EventArgs()); }
		}

		public List<string> names {
			get => channelNames; set {
				channelNames = value;
				channelNamesChanged?.Invoke(this, new EventArgs());
			}
		}
	}

	public class ChannelFaders {
		public event EventHandler channelFadersChanged;

		private List<int> channelFaders = (from channel in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select 823).ToList();

		public int this[int index] {
			get { return channelFaders[index]; }
			set { channelFaders[index] = value; channelFadersChanged?.Invoke(this, new EventArgs()); }
		}

		public List<int> faders {
			get { return channelFaders; }
			set { channelFaders = value; channelFadersChanged?.Invoke(this, new EventArgs()); }
		}
	}

	public class MixNames {
		public event EventHandler mixNamesChanged;

		private List<string> mixNames = (from mix in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select $"MIX {mix}").ToList();

		public string this[int index] {
			get { return mixNames[index]; }
			set { mixNames[index] = value; mixNamesChanged?.Invoke(this, new EventArgs()); }
		}

		public List<string> names {
			get => mixNames; set {
				mixNames = value;
				mixNamesChanged?.Invoke(this, new EventArgs());
			}
		}
	}

	public class MixFaders {
		public event EventHandler mixFadersChanged;

		private List<int> mixFaders = (from mix in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select 823).ToList();

		public int this[int index] {
			get { return mixFaders[index]; }
			set { mixFaders[index] = value; mixFadersChanged?.Invoke(this, new EventArgs()); }
		}

		public List<int> faders {
			get { return mixFaders; }
			set { mixFaders = value; mixFadersChanged?.Invoke(this, new EventArgs()); }
		}
	}

	public class LinkedChannel {
		public int leftChannel { get; set; }
		public int rightChannel { get; set; }

		public bool isLinked (int index) {
			if (index == leftChannel || index == rightChannel) {
				return true;
			}
			return false;
		}
	}

	public class LinkedChannels {
		public List<LinkedChannel> links { get; set; }

		public int getIndex (int index) {
			foreach (LinkedChannel linkedChannel in links) {
				if (linkedChannel.isLinked(index)) {
					if (index == linkedChannel.leftChannel) {
						return linkedChannel.rightChannel;
					}
					return linkedChannel.leftChannel;
				}
			}
			return -1;
		}

		public LinkedChannels () {
			links = new List<LinkedChannel>();
		}
	}
}
