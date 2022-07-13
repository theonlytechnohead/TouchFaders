using SharpOSC;
using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace TouchFaders_MIDI {
    public class oscDevice {

		public const string CONNECT = "test";
		public const string DISCONNECT = "disconnect";

		public const string CHANNEL = "channel";
		public const string MIX = "mix";
		public const string NAME = "name";
		public const string PATCH = "patch";
		public const string MUTE = "mute";

		public const string COLOUR = "colour";

		public string deviceName;
		private int currentMix;

		private UDPListener input = null;
		private UDPSender output = null;

		public oscDevice (string name, IPAddress address, int sendPort, int receivePort) {
			deviceName = name;
			(input as IDisposable)?.Dispose(); // TODO: I don't need to do this, right?
			(output as IDisposable)?.Dispose();
			input = new UDPListener(receivePort, parseOSCMessage);
			output = new UDPSender(address.ToString(), sendPort);
		}

		~oscDevice () {
			Close();
		}

		public void Close () {
			(input as IDisposable)?.Dispose();
			(output as IDisposable)?.Dispose();
		}

		void parseOSCMessage (OscPacket packet) {
			if (packet is OscBundle) {
				OscBundle messageBundle = (OscBundle)packet;
				foreach (OscMessage message in messageBundle.Messages) {
					try {
						handleOSCMessage(message);
                    } catch (NullReferenceException) {
                        Console.WriteLine("Got a null reference exception whilst reading/handling an OSC bundle");
                    }
				}
			} else {
				OscMessage message = (OscMessage)packet;
				try {
					handleOSCMessage(message);
				} catch (NullReferenceException) {
                    Console.WriteLine("Got a null reference exception whilst reading/handling an OSC mesage");
					return;
				}
			}
		}

		void handleOSCMessage (OscMessage message) {
			//Console.WriteLine($"OSC from {deviceName}: {message.Address} {message.Arguments[0]}");
			if (message.Address == $"/{CONNECT}") {
				output.Send(new OscMessage($"/{CONNECT}/{CONNECT}", 1));
				//output.Send(new OscMessage("/mix1/fader1", 823));
            }
            if (message.Address.Contains($"/{MIX}")) {
				string[] address = message.Address.Split('/');
				address = address.Skip(1).ToArray(); // remove the empty string before the leading '/'
				if (address.Length > 1) {
					int mix = int.Parse(String.Join("", address[0].Where(char.IsDigit)));
					int channel = int.Parse(String.Join("", address[1].Where(char.IsDigit)));
					if (address.Length == 2) {
						if (message.Arguments[0] is int) {
							int value = (int)message.Arguments[0];
							/*int linkedIndex = MainWindow.instance.linkedChannels.getIndex(channel - 1); // TODO: fix this
							if (linkedIndex != -1) {
								sendOSCMessage(mix, linkedIndex + 1, value);
								MainWindow.instance.SendFaderValue(mix, linkedIndex + 1, value, this);
							}*/
							value = Math.Max(0, Math.Min(value, 1023));
                            MainWindow.instance.SendFaderValue(mix, channel, value, this);
                        }
					} else if (address.Length == 3 && message.Arguments[0] is int) {
						bool muted = false;
						if ((int)message.Arguments[0] == 1) {
							muted = true;
                        }
						MainWindow.instance.SendChannelMute(mix, channel, muted, this);
					}
				} else {
					int mix = int.Parse(String.Join("", address[0].Where(char.IsDigit)));
					if (message.Arguments[0].ToString() == "1") {
						currentMix = mix;
						Refresh();
					}
				}
			}
		}

		public void Refresh () {
			SendChannelStrips();
		}

		public void SendChannelStrips() {
			for (int channel = 1; channel <= MainWindow.instance.config.NUM_CHANNELS; channel++) {
				SendChannelStrip(channel);
				Thread.Sleep(3);
			}
        }

		public void SendChannelStrip(int channel) {
			int level = MainWindow.instance.data.channels[channel - 1].sends[currentMix - 1].level;
			bool sendMuted = MainWindow.instance.data.channels[channel - 1].sends[currentMix - 1].muted;
			string name = MainWindow.instance.data.channels[channel - 1].name;
			bool channelMuted = MainWindow.instance.data.channels[channel - 1].muted;
			string patch = "IN " + MainWindow.instance.data.channels[channel - 1].patch;
			int colourIndex = MainWindow.instance.data.channels[channel - 1].bgColourId;
			OscMessage message = new OscMessage($"/{MIX}{currentMix}/{CHANNEL}{channel}", level, sendMuted, name, channelMuted, patch, colourIndex);
			output.Send(message);
		}

		public void SendSendLevels () {
			for (int channel = 1; channel <= MainWindow.instance.config.NUM_CHANNELS; channel++) {
				int level = MainWindow.instance.data.channels[channel - 1].sends[currentMix - 1].level;
				sendOSCMessage(currentMix, channel, level);
				Thread.Sleep(3);
			}
		}

		public void SendChannelNames () {
            for (int channel = 1; channel <= MainWindow.instance.data.channels.Count; channel++) {
				SendChannelName(channel, MainWindow.instance.data.channels[channel - 1].name);
				Thread.Sleep(3);
			}
		}

		public void SendChannelName (int channel, string name) {
			OscMessage message = new OscMessage($"/{NAME}{channel}", name);
			output.Send(message);
		}

		public void SendChannelPatches () {
			for (int patch = 1; patch <= MainWindow.instance.data.channels.Count; patch++) {
				SendChannelPatch(patch, patch);
				Thread.Sleep(3);
			}
		}

		public void SendChannelPatch (int channel, int patch) {
			string patchIn = "IN " + MainWindow.instance.data.channels[patch - 1].patch;
			OscMessage message = new OscMessage($"/{PATCH}{channel}", patchIn);
			output.Send(message);
		}

		public void SendSendMutes () {
			for (int channel = 1; channel <= MainWindow.instance.data.channels.Count; channel++) {
				SendSendMute(currentMix, channel);
			}
        }

		public void SendSendMute (int mix, int channel) {
			bool muted = MainWindow.instance.data.channels[channel - 1].sends[mix - 1].muted;
			SendSendMute(mix, channel, muted);
		}

		public void SendSendMute (int mix, int channel, bool muted) {
			OscMessage message = new OscMessage($"/{MIX}{mix}/{CHANNEL}{channel}/{MUTE}", muted ? 1 : 0);
			output.Send(message);
        }

		public void SendChannelMutes () {
			for (int channel = 1; channel <= MainWindow.instance.data.channels.Count; channel++) {
				bool muted = MainWindow.instance.data.channels[channel - 1].muted;
				SendChannelMute(channel, muted);
            }
        }

		public void SendChannelMute (int channel, bool muted) {
			OscMessage message = new OscMessage($"/{CHANNEL}{channel}/{MUTE}", muted);
			output.Send(message);
		}

		public void SendChannelColours () {
			for (int channel = 1; channel <= MainWindow.instance.data.channels.Count; channel++) {
				int colourIndex = MainWindow.instance.data.channels[channel - 1].bgColourId;
				SendChannelColour(channel, colourIndex);
			}
		}

		public void SendChannelColour (int channel, int colourIndex) {
			OscMessage message = new OscMessage($"/{CHANNEL}{channel}/{COLOUR}", colourIndex);
			output.Send(message);
		}

		public void SendDisconnect () {
			OscMessage message = new OscMessage($"/{DISCONNECT}");
			output.Send(message);
        }

		public void sendOSCMessage (int mix, int channel, int value) {
			//Console.WriteLine($"Sending OSC: /mix{mix}/fader{channel} {value}");
            OscMessage message = new OscMessage($"/{MIX}{mix}/{CHANNEL}{channel}", value);
			output.Send(message);
		}
	}
}
