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
			public int NUM_CHANNELS { get; set; }
			public int NUM_MIXES { get; set; }

			public override string ToString () {
				return $"config_version: {config_version}";
			}
		}

		public static appconfig Load () {
			appconfig config;
			_ = Directory.CreateDirectory("config");
			if (File.Exists("config/config.txt")) {
				string configFile = File.ReadAllText("config/config.txt");
				config = JsonSerializer.Deserialize<appconfig>(configFile);
			} else {
				config = new appconfig {
					config_version = 1,
					oscDevices_version = 1,
					sendsToMix_version = 1,
					channelNames_version = 1,
					channelFaders_version = 1,
					NUM_MIXES = 16,
					NUM_CHANNELS = 64
				};
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
		private List<string> channelNames = (from channel in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select $"CH {channel}").ToList();

		public event EventHandler channelNamesChanged;

		public string this[int index] {
			get { return names[index]; }
			set { names[index] = value; channelNamesChanged?.Invoke(this, new EventArgs()); }
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

		public int this[int index] {
			get { return channelFaders[index]; }
			set { channelFaders[index] = value; channelFadersChanged?.Invoke(this, new EventArgs()); }
		}

		private List<int> channelFaders = (from channel in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select 823).ToList();

		public List<int> faders {
			get { return channelFaders; }
			set { channelFaders = value; channelFadersChanged?.Invoke(this, new EventArgs()); }
		}
	}

	public class LinkedChannel {
		public int leftChannel, rightChannel;

		public bool isLinked (int index) {
			if (index == leftChannel || index == rightChannel) {
				return true;
			}
			return false;
		}

		public LinkedChannel (int leftIndex, int rightIndex) {
			this.leftChannel = leftIndex;
			this.rightChannel = rightIndex;
		}
	}

	public class LinkedChannels {
		public List<LinkedChannel> links;

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
