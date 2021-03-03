using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TouchFaders_MIDI {
	class DataStructures {
	}

	public class AppConfiguration {
		// Constants and stuff goes here
		public class appconfig {
			public int? config_version { get; set; }
			public int sendsToMix_version { get; set; }

			public int? channelConfig_version { get; set; } // Replaces channelNames_version and channelFaders_version

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
					channelConfig_version = 2,
					mixer = Mixer.LS932,
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

	public class Mixer {
		public Mixer () { model = "NONE"; channelCount = 0; mixCount = 0; }
		private Mixer (string value, int channels, int mixes) { model = value; channelCount = channels; mixCount = mixes; }

		public string model { get; set; }
		public int channelCount { get; set; }
		public int mixCount { get; set; }

		public override string ToString () {
			return $"{model}, ch{channelCount}×{mixCount}";
		}

		public override bool Equals (object obj) {
			if (obj.GetType() != typeof(Mixer)) return false;
			Mixer other = obj as Mixer;
			if (model != other.model) return false;
			if (channelCount != other.channelCount) return false;
			if (mixCount != other.mixCount) return false;
			return true;
		}

		public override int GetHashCode () {
			int hashCode = -316074491;
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(model);
			hashCode = hashCode * -1521134295 + channelCount.GetHashCode();
			hashCode = hashCode * -1521134295 + mixCount.GetHashCode();
			return hashCode;
		}

		public static Mixer LS932 { get { return new Mixer("LS9-32", 64, 16); } }
		public static Mixer LS916 { get { return new Mixer("LS9-16", 32, 16); } }

	}

	public class SendsToMix {
		public event EventHandler sendsChanged;

		public int this[int mix, int channel] {
			get { return sendLevel[mix][channel]; }
			set { sendLevel[mix][channel] = value; sendsChanged?.Invoke(this, new EventArgs()); }
		}

		private List<List<int>> levels = (from mix in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select (from channel in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select 623).ToList()).ToList(); // Initalized to -10dB

		public List<List<int>> sendLevel {
			get { return levels; }
			set { levels = value; sendsChanged?.Invoke(this, new EventArgs()); }
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

	public class ChannelConfig {
		public class Channel {
			public event EventHandler channelLevelChanged;
			private int fader;

			public class ChannelLevelChangedEventArgs : EventArgs {
				public char linkGroup;
			}

			public string name { get; set; }
			public int level { get { return fader; } set { fader = value; channelLevelChanged?.Invoke(this, new ChannelLevelChangedEventArgs() { linkGroup = linkGroup }); } }
			public char linkGroup { get; set; }

			public static string kNameShortToString (byte[] kNameShort) {
				string raw_MIDI = string.Concat(kNameShort.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))); // convert the byte array to a string of 0's and 1's
				string decoded_MIDI = "";
				for (int i = 0; i < raw_MIDI.Length; i++) {
					if (i % 8 != 0) { // if it's not the 8th bit in a byte
						decoded_MIDI += raw_MIDI[i]; // add it to the new string, reforming the original 8-bit encoding
					}
				}
				decoded_MIDI = decoded_MIDI.Substring(3); // skip the first nibble-ish, it's a leftover artifact of the 7b/8b encoding
				List<byte> list = new List<byte>();
				for (int i = 0; i < decoded_MIDI.Length; i += 8) {
					list.Add(Convert.ToByte(decoded_MIDI.Substring(i, 8), 2)); // convert segments 8 bits to a byte, and put in a list
				}
				return Encoding.ASCII.GetString(list.ToArray());
			}
		}

		public class SelectedChannel {
			private Channel currentChannel { get; set; }

			public int channelIndex { get; set; }
			public string name {
				get {
					if (kNameShort1 != null && kNameShort2 != null) {
						return Channel.kNameShortToString(kNameShort1) + Channel.kNameShortToString(kNameShort2);
					} else {
						return currentChannel.name;
					}
				}
				set {
					currentChannel.name = value;
				}
			}
			public byte[] kNameShort1 { get; set; }
			public byte[] kNameShort2 { get; set; }

			public int iconID { get; set; }
			public static List<Uri> iconURIs = new List<Uri>() {
				new Uri("Resources/00_kick.png", UriKind.Relative),
				new Uri("Resources/01_snare.png", UriKind.Relative),
				new Uri("Resources/02_hi-hat.png", UriKind.Relative),
				new Uri("Resources/03_tom.png", UriKind.Relative),
				new Uri("Resources/04_drumkit.png", UriKind.Relative),
				new Uri("Resources/05_perc.png", UriKind.Relative),
				new Uri("Resources/06_a.bass.png", UriKind.Relative),
				new Uri("Resources/07_strings.png", UriKind.Relative),
				new Uri("Resources/08_e.bass.png", UriKind.Relative),
				new Uri("Resources/09_a.guitar.png", UriKind.Relative),
				new Uri("Resources/10_e.guitar.png", UriKind.Relative),
				new Uri("Resources/11_bassamp.png", UriKind.Relative),
				new Uri("Resources/12_guitaramp.png", UriKind.Relative),
				new Uri("Resources/13_trumpet.png", UriKind.Relative),
				new Uri("Resources/14_trombone.png", UriKind.Relative),
				new Uri("Resources/15_saxophone.png", UriKind.Relative),
				new Uri("Resources/16_piano.png", UriKind.Relative),
				new Uri("Resources/17_organ.png", UriKind.Relative),
				new Uri("Resources/18_keyboard.png", UriKind.Relative),
				new Uri("Resources/19_male.png", UriKind.Relative),
				new Uri("Resources/20_female.png", UriKind.Relative),
				new Uri("Resources/21_choir.png", UriKind.Relative),
				new Uri("Resources/22_dynamic.png", UriKind.Relative),
				new Uri("Resources/23_condenser.png", UriKind.Relative),
				new Uri("Resources/24_wireless.png", UriKind.Relative),
				new Uri("Resources/25_podium.png", UriKind.Relative),
				new Uri("Resources/26_wedge.png", UriKind.Relative),
				new Uri("Resources/27_2way.png", UriKind.Relative),
				new Uri("Resources/28_in-ear.png", UriKind.Relative),
				new Uri("Resources/29_effector.png", UriKind.Relative),
				new Uri("Resources/30_media1.png", UriKind.Relative),
				new Uri("Resources/31_media2.png", UriKind.Relative),
				new Uri("Resources/32_vtr.png", UriKind.Relative),
				new Uri("Resources/33_mixer.png", UriKind.Relative),
				new Uri("Resources/34_pc.png", UriKind.Relative),
				new Uri("Resources/35_processor.png", UriKind.Relative),
				new Uri("Resources/36_audience.png", UriKind.Relative),
				new Uri("Resources/37_star1.png", UriKind.Relative),
				new Uri("Resources/38_star2.png", UriKind.Relative),
				new Uri("Resources/39_blank.png", UriKind.Relative)
			};
			public int bgColourID { get; set; }
			public static List<SolidColorBrush> bgColours = new List<SolidColorBrush>() {
				new SolidColorBrush(Color.FromRgb(1, 1, 253)), // blue, default
				new SolidColorBrush(Color.FromRgb(255, 102, 1)), // orange
				new SolidColorBrush(Color.FromRgb(153, 102, 1)), // brown
				new SolidColorBrush(Color.FromRgb(102, 1, 153)), // purple
				new SolidColorBrush(Color.FromRgb(1, 153, 255)), // cyan
				new SolidColorBrush(Color.FromRgb(255, 102, 153)), // pink
				new SolidColorBrush(Color.FromRgb(102, 1, 1)), // bergundy
				new SolidColorBrush(Color.FromRgb(1, 102, 51)) // green
			};

			public int level { get { return currentChannel.level; } set { currentChannel.level = value; } }

			public SelectedChannel () {
				currentChannel = new Channel();
				channelIndex = 0;
				name = "Ch 1";
				level = 823;
				iconID = 22;
				bgColourID = 0;
			}
		}

		public List<Channel> channels { get; set; }

		public List<string> GetChannelNames () {
			List<string> names = new List<string>();
			foreach (Channel channel in channels) {
				names.Add(channel.name);
			}
			return names;
		}

		public List<int> GetFaderLevels () {
			List<int> levels = new List<int>();
			foreach (Channel channel in channels) {
				levels.Add(channel.level);
			}
			return levels;
		}

		/*
		public void UpdateLinkedFaderLevels (object sender, EventArgs eventArgs) {
			Channel senderChannel = sender as Channel;
			Channel.ChannelLevelChangedEventArgs args = eventArgs as Channel.ChannelLevelChangedEventArgs;
			List<Channel> groupChannels = GetGroup(senderChannel, args.linkGroup);
			foreach (Channel channel in groupChannels) {
				int index = MainWindow.instance.channelConfig.channels.IndexOf(channel);
				MainWindow.instance.channelConfig.channels[index].level = channel.level;
			}
		}
		*/

		public static ObservableCollection<char> ChannelGroupChars = new ObservableCollection<char>() {
			' ',
			'A',
			'B',
			'C',
			'D',
			'E',
			'F',
			'G',
			'H',
			'I',
			'J',
			'K',
			'L',
			'M',
			'N',
			'O',
			'P',
			'Q',
			'R',
			'S',
			'T',
			'U',
			'V',
			'W',
			'X',
			'Y',
			'Z',
			'a',
			'b',
			'c',
			'd',
			'e',
			'f',
			'g',
			'h'
		};

		public ChannelConfig () {
			channels = new List<Channel>();
		}


		public List<Channel> GetGroup (Channel senderChannel, char group) {
			List<Channel> channels = new List<Channel>();
			foreach (Channel channel in this.channels) {
				if (channel.linkGroup == group) {
					channels.Add(channel);
				}
			}
			channels.Remove(senderChannel);
			return channels;
		}
	}

}
