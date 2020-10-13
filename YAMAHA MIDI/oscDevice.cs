using SharpOSC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace YAMAHA_MIDI {
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
			//faders = (from number in Enumerable.Range(1, 96) select 823f / 1023f).ToList(); // 0dB is at 823 when value is 10-bit, therefore 823/1023
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
					float value = (float)message.Arguments[0];
					int linkedIndex = MainWindow.instance.linkedChannels.getIndex(channel - 1);
					if (linkedIndex != -1) {
						sendOSCMessage(mix, linkedIndex + 1, value);
						//MainWindow.instance.sendsToMix[mix - 1, linkedIndex] = value;
						MainWindow.instance.SendFaderValue(mix, linkedIndex + 1, value, this);
					}
					MainWindow.instance.SendFaderValue(mix, channel, value, this);
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
			for (int channel = 1; channel <= MainWindow.instance.sendsToMix.sendLevel[mix - 1].Count; channel++) {
				sendOSCMessage(mix, channel, MainWindow.instance.sendsToMix[mix - 1, channel - 1]);
				Thread.Sleep(5);
			}
		}

		public void ResendAllFaders () {
			for (int mix = 1; mix <= MainWindow.instance.sendsToMix.sendLevel.Count; mix++) {
				ResendMixFaders(mix);
			}
		}

		public void ResendMixNames (int mix, List<string> channelNames) {
			for (int label = 1; label <= 16; label++) {
				OscMessage message = new OscMessage($"/mix{mix}/label{label}", channelNames[label - 1]);
				output.Send(message);
			}
		}

		public void ResendAllNames (List<string> channelNames) {
			for (int mix = 1; mix < 6; mix++) {
				ResendMixNames(mix, channelNames);
				Thread.Sleep(2);
			}
		}

		public void sendOSCMessage (int mix, int channel, float value) {
			//Console.WriteLine($"Sending OSC: /mix{mix}/fader{channel} {value}");
			OscMessage message = new OscMessage($"/mix{mix}/fader{channel}", value);
			output.Send(message);
		}
	}
}
