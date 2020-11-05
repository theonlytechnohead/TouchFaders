using SharpOSC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace TouchFaders_MIDI {
	public class oscDevice : INotifyPropertyChanged {
		public string name;
		[System.Text.Json.Serialization.JsonIgnore]
		public string Name { // Display name for ObservableCollection in UI
			get {
				if (input == null && output == null) {
					return DeviceName + " is not configured";
				} else {
					return DeviceName + " at " + Address + ":" + SendPort + ", " + ListenPort;
				}
			}
			set {
				DeviceName = value;
			}
		}

		#region JsonProperties
		public string DeviceName { get { return name; } set { name = value; } }

		public bool LegacyApp { get; set; } = false;

		string address;
		public string Address {
			get {
				if (output != null)
					return output.Address;
				else
					return address;
			}
			set {
				address = value;
			}
		}

		int sendPort;
		public int? SendPort {
			get {
				if (output != null)
					return output.Port;
				else
					return sendPort;
			}
			set {
				sendPort = value.Value;
			}
		}

		int listenPort;
		public int? ListenPort {
			get {
				if (input != null)
					return input.Port;
				else
					return listenPort;
			}
			set {
				listenPort = value.Value;
			}
		}

		/*
		List<float> faders;
		public List<float> Faders {
			get {
				if (faders != null)
					return faders;
				else
					return null;
			}
			set {
				faders = value;
			}
		}
		*/
		#endregion

		public UDPListener input = null;
		public UDPSender output = null;

		public event PropertyChangedEventHandler PropertyChanged;

		public oscDevice () {
			Name = "Unnamed device";
		}

		public void Refresh () {
			if (DeviceName != null)
				SetDeviceName(DeviceName);
			if (Address != null && ListenPort != null && SendPort != null)
				InitializeIO(Address, SendPort.Value, ListenPort.Value);
		}

		public void SetDeviceName (string value) {
			Name = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("name"));
		}

		public void InitializeIO (string address, int port, int localPort) {
			Address = address;
			SendPort = port;
			ListenPort = localPort;
			(input as IDisposable)?.Dispose();
			(output as IDisposable)?.Dispose();
			input = new UDPListener(localPort, parseOSCMessage);
			output = new UDPSender(address, port);
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("name"));
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
					if (message.Arguments[0] is float) { // Legacy TouchOSC clients use floats for fader values
						int value = Convert.ToInt32((float)message.Arguments[0] * 1023);
						int linkedIndex = MainWindow.instance.linkedChannels.getIndex(channel - 1);
						if (linkedIndex != -1) {
							sendOSCMessage(mix, linkedIndex + 1, (float)message.Arguments[0]);
							MainWindow.instance.SendFaderValue(mix, linkedIndex + 1, value, this);
						}
						MainWindow.instance.SendFaderValue(mix, channel, value, this);
					}
					if (message.Arguments[0] is int) { // TouchFaders OSC clients use 1:1 mapping ints for fader values (can be passed directly to the console)
						int value = (int)message.Arguments[0];
						int linkedIndex = MainWindow.instance.linkedChannels.getIndex(channel - 1);
						if (linkedIndex != -1) {
							sendOSCMessage(mix, linkedIndex + 1, value);
							MainWindow.instance.SendFaderValue(mix, linkedIndex + 1, value, this);
						}
						MainWindow.instance.SendFaderValue(mix, channel, value, this);
					}
				} else {
					int mix = int.Parse(String.Join("", address[0].Where(char.IsDigit)));
					if (message.Arguments[0].ToString() == "1") {
						ResendMixFaders(mix);
						ResendMixNames(mix, MainWindow.instance.channelNames.names);
					}
				}
			}
		}

		void ResendMixFaders (int mix) {
			for (int channel = 1; channel < MainWindow.NUM_CHANNELS; channel++) {
				int level = MainWindow.instance.sendsToMix[mix - 1, channel - 1];
				if (LegacyApp) {
					sendOSCMessage(mix, channel, level / 1023f);
				} else {
					sendOSCMessage(mix, channel, level);
				}
				Thread.Sleep(3);
			}
		}

		public void ResendAllFaders () {
			for (int mix = 1; mix <= MainWindow.NUM_MIXES; mix++) {
				ResendMixFaders(mix);
			}
		}

		public void ResendMixNames (int mix, List<string> channelNames) {
			for (int label = 1; label <= MainWindow.NUM_CHANNELS; label++) {
				OscMessage message = new OscMessage($"/mix{mix}/label{label}", channelNames[label - 1]);
				output.Send(message);
			}
		}

		public void ResendAllNames (List<string> channelNames) {
			for (int mix = 1; mix <= MainWindow.NUM_MIXES; mix++) {
				ResendMixNames(mix, channelNames);
				Thread.Sleep(3);
			}
		}

		public void sendOSCMessage (int mix, int channel, float value) {
			//Console.WriteLine($"Sending OSC: /mix{mix}/fader{channel} {value}");
			OscMessage message = new OscMessage($"/mix{mix}/fader{channel}", value);
			output.Send(message);
		}

		public void sendOSCMessage (int mix, int channel, int value) {
			//Console.WriteLine($"Sending OSC: /mix{mix}/fader{channel} {value}");
			OscMessage message = new OscMessage($"/mix{mix}/fader{channel}", value);
			output.Send(message);
		}
	}
}
