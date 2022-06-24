using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace TouchFaders_MIDI {
	class DataStructures {

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

		public static ObservableCollection<string> bgColourNames = new ObservableCollection<string>() {
			"blue",
			"orange",
			"brown",
			"purple",
			"cyan",
			"pink",
			"bergundy",
			"green"
		};

		public static Dictionary<string, SolidColorBrush> bgColourMap = Enumerable.Range(0, bgColourNames.Count).ToDictionary(i => bgColourNames[i], i => bgColours[i]);

	}

	public class Data {
		public static event EventHandler sendLevelChanged;
		public static event EventHandler sendMuteChanged;
		public static event EventHandler channelLevelChanged;
		public static event EventHandler channelMuteChanged;
		public static event EventHandler mixLevelChanged;
		public static event EventHandler mixMuteChanged;

		public static string kNameShortToString (byte[] kNameShort) {
			string raw = string.Concat(kNameShort.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))); // convert the byte array to a string of 0's and 1's
			string decoded = "";
			for (int i = 0; i < raw.Length; i++) {
				if (i % 8 != 0) { // if it's not the 8th bit in a byte
					decoded += raw[i]; // add it to the new string, reforming the original 8-bit encoding
				}
			}
			decoded = decoded.Substring(3); // skip the first nibble-ish, it's a leftover artifact of the 7b/8b encoding
			List<byte> name = new List<byte>();
			for (int i = 0; i < decoded.Length; i += 8) {
				name.Add(Convert.ToByte(decoded.Substring(i, 8), 2)); // convert segments 8 bits to a byte, and put in a list
			}
			return Encoding.ASCII.GetString(name.ToArray());
		}

		public List<Channel> channels { get; set; }
		public List<Mix> mixes { get; set; }

		public Data() {
			channels = (from channel in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select new Channel(channel)).ToList();
			mixes = (from mix in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select new Mix(mix)).ToList();
        }

		public class Channel {
			private int fader;
			private bool mute;

			public class ChannelLevelArgs : EventArgs {
				public char linkGroup;
			}

			public string name { get; set; }
			public int level { get => fader; set { fader = value; channelLevelChanged?.Invoke(this, new ChannelLevelArgs() { linkGroup = linkGroup }); } }
            public bool muted { get => mute; set { mute = value; channelMuteChanged?.Invoke(this, new EventArgs()); } }
			public int bgColourId { get; set; }
			public char linkGroup { get; set; }
			public int patch { get; set; }

			public List<Send> sends { get; set; }

			public Channel(int channel) {
				name = $"ch{channel}";
				// Initialised to 0dB
				level = 823;
				// Initialised to unmuted (ON)
				muted = false;
				bgColourId = 0;
				linkGroup = ' ';
				patch = channel;
				sends = (from send in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select new Send()).ToList();
			}
		}

		public class Send {
			private int fader;
			private bool mute;

			public int level { get => fader; set { fader = value; sendLevelChanged?.Invoke(this, new EventArgs()); } }
			public bool muted { get => mute; set { mute = value; sendMuteChanged?.Invoke(this, new EventArgs()); } }

			public Send() {
				// Initalised to -10dB
				level = 623;
				// Initialised to unmuted (ON)
				muted = false;
            }
		}

		public class Mix {
			private int fader;
			private bool mute;

			public string name { get; set; }
			public int level { get => fader; set { fader = value; mixLevelChanged?.Invoke(this, new EventArgs()); } }
			public bool muted { get => mute; set { mute = value; mixMuteChanged?.Invoke(this, new EventArgs()); } }
			public int bgColourId { get; set; }

			public Mix(int mix) {
				name = $"MX{mix}";
				// Initialised to 0dB
				level = 823;
				// Initialised to unmuted (ON)
				muted = false;
				bgColourId = 0;
            }
		}

	}

	public class SendsToMix {
		public event EventHandler sendsChanged;
		

		public int this[int mix, int channel] {
			get { return sendLevel[mix][channel]; }
			set { sendLevel[mix][channel] = value; sendsChanged?.Invoke(this, new EventArgs()); }
		}

		private List<List<int>> levels = (from mix in Enumerable.Range(1, 16) select (from channel in Enumerable.Range(1, 64) select 623).ToList()).ToList(); // Initalized to -10dB
		

		public List<List<int>> sendLevel {
			get { return levels; }
			set { levels = value; sendsChanged?.Invoke(this, new EventArgs()); }
		}
	}

	public class MutesToMix {
		public event EventHandler mutesChanged;

		public bool this[int mix, int channel] {
			get { return mutes[mix][channel]; }
			set { mutes[mix][channel] = value; mutesChanged?.Invoke(this, new EventArgs()); }
		}

		private List<List<bool>> mutes = (from mix in Enumerable.Range(1, 16) select (from channel in Enumerable.Range(1, 64) select false).ToList()).ToList(); // Initalized to ON (unmuted)

		public List<List<bool>> sendMute {
			get { return mutes; }
			set { mutes = value; mutesChanged?.Invoke(this, new EventArgs()); }
		}
	}

	public class MixConfig {
		public class Mix {
			public event EventHandler mixLevelChanged;
			private int fader;

			public string name { get; set; }
			public int level {
                get => fader;
                set {
					fader = value;
					mixLevelChanged?.Invoke(this, new EventArgs());
				}
            }
			public int bgColourId { get; set; }

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

		public List<Mix> mixes { get; set; }

		public MixConfig () {
			mixes = new List<Mix>();
		}

		public void Initialise(AppConfiguration.appconfig appConfig) {
			for (int i = 1; i <= appConfig.NUM_MIXES; i++) {
				mixes.Add(new Mix() { name = $"MIX {i}", level = 823 });
			}
        }

		//public event EventHandler mixNamesChanged;
		//public event EventHandler mixFadersChanged;

		//private List<string> mixNames = (from mix in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select $"MIX {mix}").ToList();
		//private List<int> mixFaders = (from mix in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select 823).ToList();

		//public List<string> names {
		//	get => mixNames; set {
		//		mixNames = value;
		//		mixNamesChanged?.Invoke(this, new EventArgs());
		//	}
		//}

		//public List<int> faders {
		//	get { return mixFaders; }
		//	set { mixFaders = value; mixFadersChanged?.Invoke(this, new EventArgs()); }
		//}
	}

	// deprecated
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

	// deprecated
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
			public bool muted { get; set; }
			public int bgColourId { get; set; }
			public char linkGroup { get; set; }
			public int patch { get; set; }

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
