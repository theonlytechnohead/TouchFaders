using System;
using System.Collections.Generic;
using System.Linq;

namespace YAMAHA_MIDI {
	class DataStructures {
	}

	public class SendsToMix {
		public event EventHandler sendsChanged;

		public float this[int mix, int channel] {
			get { return sendLevel[mix][channel]; }
			set { sendLevel[mix][channel] = value; sendsChanged?.Invoke(this, new EventArgs()); }
		}

		private List<List<float>> levels = (from mix in Enumerable.Range(1, 6) select (from channel in Enumerable.Range(1, 16) select 823f / 1023f).ToList()).ToList(); // Initalized to 0dB

		public List<List<float>> sendLevel {
			get { return levels; }
			set { levels = value; sendsChanged?.Invoke(this, new EventArgs()); }
		}
	}

	public class ChannelNames {
		private List<string> channelNames = new List<string>() {
			"CH 1",
			"CH 2",
			"CH 3",
			"CH 4",
			"CH 5",
			"CH 6",
			"CH 7",
			"CH 8",
			"CH 9",
			"CH 10",
			"CH 11",
			"CH 12",
			"CH 13",
			"CH 14",
			"CH 15",
			"CH 16"
		};

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

		public float this[int index] {
			get { return channelFaders[index]; }
			set { channelFaders[index] = value; channelFadersChanged?.Invoke(this, new EventArgs()); }
		}

		private List<float> channelFaders = new List<float>() {
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f,
			823f / 1023f
		};

		public List<float> faders {
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
