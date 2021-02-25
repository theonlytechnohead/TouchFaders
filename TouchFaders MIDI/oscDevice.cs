using SharpOSC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;

namespace TouchFaders_MIDI {
	public class oscDevice {
		public string deviceName;

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
					handleOSCMessage(message);
				}
			} else {
				OscMessage message = (OscMessage)packet;
				try {
					handleOSCMessage(message);
				} catch (NullReferenceException) {
					return;
				}
			}
		}

		void handleOSCMessage (OscMessage message) {
			//Console.WriteLine($"OSC from {DeviceName}: {message.Address} {message.Arguments[0]}");
			if (message.Address.Contains("/mix")) {
				string[] address = message.Address.Split('/');
				address = address.Skip(1).ToArray(); // remove the empty string before the leading '/'
				if (address.Length > 1) {
					int mix = int.Parse(String.Join("", address[0].Where(char.IsDigit)));
					int channel = int.Parse(String.Join("", address[1].Where(char.IsDigit)));
					if (message.Arguments[0] is int) { // TouchFaders OSC clients use 1:1 mapping ints for fader values (can be passed directly to the console)
						int value = (int)message.Arguments[0];
						/*int linkedIndex = MainWindow.instance.linkedChannels.getIndex(channel - 1); // TODO: fix this
						if (linkedIndex != -1) {
							sendOSCMessage(mix, linkedIndex + 1, value);
							MainWindow.instance.SendFaderValue(mix, linkedIndex + 1, value, this);
						}*/
						MainWindow.instance.SendFaderValue(mix, channel, value, this);
					}
				} else {
					int mix = int.Parse(String.Join("", address[0].Where(char.IsDigit)));
					if (message.Arguments[0].ToString() == "1") {
						ResendMixFaders(mix);
						//ResendMixNames(mix, MainWindow.instance.channelConfig.GetChannelNames());
					}
				}
			}
		}

		void ResendMixFaders (int mix) {
			for (int channel = 1; channel <= MainWindow.instance.config.NUM_CHANNELS; channel++) {
				int level = MainWindow.instance.sendsToMix[mix - 1, channel - 1];
				sendOSCMessage(mix, channel, level);
				Thread.Sleep(3);
			}
		}

		public void ResendAllFaders () {
			for (int mix = 1; mix <= MainWindow.instance.config.NUM_MIXES; mix++) {
				ResendMixFaders(mix);
			}
		}

		public void ResendMixNames (int mix, List<string> channelNames) {
			for (int label = 1; label <= MainWindow.instance.config.NUM_CHANNELS; label++) {
				OscMessage message = new OscMessage($"/mix{mix}/label{label}", channelNames[label - 1]);
				output.Send(message);
			}
		}

		public void ResendAllNames (List<string> channelNames) {
			for (int mix = 1; mix <= MainWindow.instance.config.NUM_MIXES; mix++) {
				ResendMixNames(mix, channelNames);
				Thread.Sleep(3);
			}
		}

		public void sendOSCMessage (int mix, int channel, int value) {
			//Console.WriteLine($"Sending OSC: /mix{mix}/fader{channel} {value}");
			OscMessage message = new OscMessage($"/mix{mix}/fader{channel}", value);
			output.Send(message);
		}
	}
}
