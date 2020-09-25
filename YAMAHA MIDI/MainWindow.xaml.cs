using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using SharpOSC;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace YAMAHA_MIDI {

	public class oscDevice : INotifyPropertyChanged {
		public string name;
		[JsonIgnore]
		public string Name {
			get {
				if (input == null && output == null) {
					return DeviceName + " with " + faders.Count + " faders";
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
		#endregion

		public UDPListener input = null;
		public UDPSender output = null;

		public event PropertyChangedEventHandler PropertyChanged;

		public oscDevice () {
			Name = "Unnamed device";
			faders = (from number in Enumerable.Range(1, 96) select 823f / 1023f).ToList(); // 0dB is at 823 when value is 10-bit, therefore 823/1023
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
			Console.WriteLine($"OSC from {DeviceName}: {message.Address} {message.Arguments[0]}");
			if (message.Address.Contains("/mix")) {
				string[] address = message.Address.Split('/');
				address = address.Skip(1).ToArray(); // remove the empty string before the leading '/'
				if (address.Length > 1) {
					int mix = int.Parse(String.Join("", address[0].Where(char.IsDigit)));
					int channel = int.Parse(String.Join("", address[1].Where(char.IsDigit)));
					float value = (float)message.Arguments[0];
					MainWindow.instance.SendFaderValue(mix, channel, value, this);
					faders[channel - 1] = value;
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
			int startChannel = 16 * (mix - 1);
			for (int i = startChannel; i < startChannel + 16; i++) {
				sendOSCMessage(i / 16 + 1, i % 16, faders[i]);
			}
		}

		public void ResendAllFaders () {
			for (int i = 0; i < faders.Count; i++) {
				sendOSCMessage(i / 16 + 1, i % 16, faders[i]);
				Thread.Sleep(2);
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
			//Console.WriteLine($"Sending OSC: /mix{mix}/fader{channel + 1}");
			OscMessage message = new OscMessage($"/mix{mix}/fader{channel + 1}", value);
			output.Send(message);
		}
	}

	public class SendsToMix {
		public event EventHandler sendsChanged;

		public float this[int mix, int channel] {
			get { return sendLevel[mix][channel]; }
			set { sendLevel[mix][channel] = value; sendsChanged?.Invoke(this, new EventArgs()); }
		}

		private List<List<float>> levels = (from mix in Enumerable.Range(1, 6) select (from channel in Enumerable.Range(1, 16) select 823f / 1023f).ToList()).ToList();

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

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		public static MainWindow instance;

		ObservableCollection<oscDevice> oscDevices = new ObservableCollection<oscDevice>();

		OutputDevice LS9_in;
		InputDevice LS9_out;
		Timer activeSensingTimer;

		public SendsToMix sendsToMix;
		public ChannelNames channelNames;
		public ChannelFaders channelFaders;

		#region WindowEvents
		public MainWindow () {
			InitializeComponent();
			sendsToMix = new SendsToMix();
			channelNames = new ChannelNames();
			channelFaders = new ChannelFaders();
			instance = this;
			Title = "YAMAHA MIDI - MIDI not started";
			deviceListBox.ItemsSource = oscDevices;
			Task.Run(() => LoadAll());
			displayMIDIDevices();
		}

		protected override async void OnClosed (EventArgs e) {
			stopMIDIButton_Click(null, null);
			await SaveAll();
			base.OnClosed(e);
		}
		#endregion

		#region Scaling
		// This section smoothly scales everything within the mainGrid
		public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
			typeof(double),
			typeof(MainWindow),
			new UIPropertyMetadata(1.0,
				new PropertyChangedCallback(OnScaleValueChanged),
				new CoerceValueCallback(OnCoerceScaleValue)));

		private static object OnCoerceScaleValue (DependencyObject o, object value) {
			MainWindow mainWindow = o as MainWindow;
			if (mainWindow != null)
				return mainWindow.OnCoerceScaleValue((double)value);
			else
				return value;
		}

		private static void OnScaleValueChanged (DependencyObject o, DependencyPropertyChangedEventArgs e) {
			MainWindow mainWindow = o as MainWindow;
			if (mainWindow != null)
				mainWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
		}

		protected virtual double OnCoerceScaleValue (double value) {
			if (double.IsNaN(value))
				return 1.0f;

			value = Math.Max(1f, value);
			return value;
		}

		protected virtual void OnScaleValueChanged (double oldValue, double newValue) {
			// Don't need to do anything
		}

		public double ScaleValue {
			get {
				return (double)GetValue(ScaleValueProperty);
			}
			set {
				SetValue(ScaleValueProperty, value);
			}
		}

		private void mainGrid_SizeChanged (object sender, EventArgs e) {
			CalculateScale();
		}

		private void CalculateScale () {
			double xScale = ActualWidth / 600f; // must be set to initial window sizing for proper scaling!!!
			double yScale = ActualHeight / 300f; // must be set to initial window sizing for proper scaling!!!
			double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
			ScaleValue = (double)OnCoerceScaleValue(mainWindow, value); // Update the actual scale for the main window
		}

		#endregion

		#region File I/O
		async Task LoadAll () {
			try {
				JsonSerializerOptions jsonDeserializerOptions = new JsonSerializerOptions { IgnoreNullValues = true, };

				ObservableCollection<oscDevice> loadDevices = await JsonSerializer.DeserializeAsync<ObservableCollection<oscDevice>>(File.Open("oscDevices.txt", FileMode.Open), jsonDeserializerOptions);
				await Dispatcher.BeginInvoke(new Action(() => {
					foreach (oscDevice device in loadDevices) {
						oscDevices.Add(device);
					}
				}));
				sendsToMix.sendLevel = await JsonSerializer.DeserializeAsync<List<List<float>>>(File.Open("sendsToMix.txt", FileMode.Open), jsonDeserializerOptions);
				channelNames.names = await JsonSerializer.DeserializeAsync<List<string>>(File.Open("channelNames.txt", FileMode.Open), jsonDeserializerOptions);
				channelFaders.faders = await JsonSerializer.DeserializeAsync<List<float>>(File.Open("channelFaders.txt", FileMode.Open), jsonDeserializerOptions);
			} catch (FileNotFoundException) {
				await SaveAll();
			}
			await RefreshOSCDevices();
		}

		async Task SaveAll () {
			using (FileStream fs = File.Create("oscDevices.txt")) {
				await JsonSerializer.SerializeAsync(fs, oscDevices, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, });
			}
			using (FileStream fs = File.Create("sendsToMix.txt")) {
				await JsonSerializer.SerializeAsync(fs, sendsToMix.sendLevel, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, });
			}
			using (FileStream fs = File.Create("channelNames.txt")) {
				await JsonSerializer.SerializeAsync(fs, channelNames.names, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, });
			}
			using (FileStream fs = File.Create("channelFaders.txt")) {
				await JsonSerializer.SerializeAsync(fs, channelFaders.faders, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, });
			}
		}
		#endregion

		async Task RefreshOSCDevices () {
			await Task.Run(() => {
				foreach (oscDevice device in oscDevices) {
					device.Refresh();
					Thread.Sleep(5);
					device.ResendAllFaders();
					Thread.Sleep(5);
					device.ResendAllNames(channelNames.names);
				}
			});
			refreshOSCButton.IsEnabled = true;
		}

		void displayMIDIDevices () {
			inputMIDIComboBox.IsEnabled = false;
			inputMIDIComboBox.Items.Clear();
			foreach (InputDevice inputDevice in InputDevice.GetAll()) {
				inputMIDIComboBox.Items.Add(inputDevice.Name);
				inputMIDIComboBox.IsEnabled = true;
			}
			outputMIDIComboBox.IsEnabled = false;
			outputMIDIComboBox.Items.Clear();
			foreach (OutputDevice outputDevice in OutputDevice.GetAll()) {
				outputMIDIComboBox.Items.Add(outputDevice.Name);
				outputMIDIComboBox.IsEnabled = true;
			}
		}

		#region setupMIDI
		public void InitializeIO () {
			try {
				LS9_in = OutputDevice.GetByName(inputMIDIComboBox.SelectedItem.ToString());
				LS9_out = InputDevice.GetByName(outputMIDIComboBox.SelectedItem.ToString());
			} catch (ArgumentException ex) {
				MessageBox.Show($"Can't initialize {inputMIDIComboBox.SelectedItem} and {outputMIDIComboBox.SelectedItem} MIDI ports!\n{ex.Message}");
				Console.WriteLine(ex.Message);
				return;
			} catch (NullReferenceException) {
				MessageBox.Show("Please select a MIDI input and output first!");
				return;
			}
			LS9_in.EventSent += LS9_in_EventSent;
			LS9_out.EventReceived += LS9_out_EventReceived;
			try {
				LS9_out.StartEventsListening();
			} catch (MidiDeviceException ex) {
				Console.WriteLine($"Couldn't start listening to {outputMIDIComboBox.SelectedItem}");
				Console.WriteLine(ex.Message);
				return;
			}
			if (LS9_out.IsListeningForEvents) {
				ResetEvent systemReset = new ResetEvent();
				try {
					LS9_in.SendEvent(systemReset);
				} catch (MidiDeviceException ex) {
					Console.WriteLine($"Couldn't send system reset MIDI event to {inputMIDIComboBox.SelectedItem}");
					Console.WriteLine(ex.Message);
					return;
				}
				inputMIDIComboBox.IsEnabled = false;
				outputMIDIComboBox.IsEnabled = false;
				activeSensingTimer = new Timer(sendActiveSense, null, 0, 350);
				Title = "YAMAHA MIDI - MIDI running (active sensing)";
				GetAllFaderValues();
				GetChannelNames();
			}
		}

		void sendActiveSense (object state) {
			ActiveSensingEvent activeSense = new ActiveSensingEvent();
			try {
				LS9_in.SendEvent(activeSense); // MIDI should now be initialized with the desk
			} catch (MidiDeviceException e) {
				Title = "YAMAHA MIDI - Couldn't active sense";
				Console.WriteLine("Couldn't send active sensing MIDI event to LS9");
				Console.WriteLine(e.Message);
			}
		}

		void GetAllFaderValues () {
			GetFaderValuesForMix(0x05); // Mix 1 level...
			GetFaderValuesForMix(0x08);
			GetFaderValuesForMix(0x0B);
			GetFaderValuesForMix(0x0E);
			GetFaderValuesForMix(0x11);
			GetFaderValuesForMix(0x14); // Mix 6
			GetChannelFaders();         // Channel faders to STEREO
		}

		void GetFaderValuesForMix (byte mix) {
			for (int channel = 0; channel <= 15; channel++) {
				Thread.Sleep(2);
				NormalSysExEvent sysExEvent = new NormalSysExEvent();
				byte[] data = { 0xF0, 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, mix, 0x00, Convert.ToByte(channel), 0xF7 };
				sysExEvent.Data = data;
				SendSysEx(sysExEvent);
			}
		}

		void GetChannelNames () {
			for (int channel = 0; channel <= 15; channel++) {
				Thread.Sleep(2);
				NormalSysExEvent kNameShort1 = new NormalSysExEvent();
				byte[] data1 = { 0xF0, 0x43, 0x30, 0x3E, 0x12, 0x01, 0x01, 0x14, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
				kNameShort1.Data = data1;
				SendSysEx(kNameShort1);
				Thread.Sleep(2);
				NormalSysExEvent kNameShort2 = new NormalSysExEvent();
				byte[] data2 = { 0xF0, 0x43, 0x30, 0x3E, 0x12, 0x01, 0x01, 0x14, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
				kNameShort2.Data = data2;
				SendSysEx(kNameShort2);
			}
		}

		void GetChannelFaders () {
			for (int channel = 0; channel <= 15; channel++) {
				Thread.Sleep(2);
				NormalSysExEvent kFader = new NormalSysExEvent();
				byte[] data = { 0xF0, 0x43, 0x30, 0x3E, 0x12, 0x01, 0x00, 0x33, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
				kFader.Data = data;
				SendSysEx(kFader);
			}
		}
		#endregion

		#region SysExMIDIHelpers
		bool CheckSysEx (byte[] bytes) {
			if (bytes.Length != 18) {
				return false;
			}
			byte sysExStart = bytes[0];     // Signals start of SysEx, 0xF0
			byte manufacturerID = bytes[1]; // YAMAHA is 0x43
			byte deviceNumber = bytes[2];   // device number is 0x1n where n is 0-15
			byte groupID = bytes[3];        // Digital mixer is 0x3E
			byte modelID = bytes[4];        // LS9 is 0x12
			byte dataCategory = bytes[5];
			byte elementMSB = bytes[6];
			byte elementLSB = bytes[7];
			byte indexMSB = bytes[8];
			byte indexLSB = bytes[9];
			byte channelMSB = bytes[10];    // Channel MSB per channel
			byte channelLSB = bytes[11];    // Channel LSB with a 0 in the 8th bit
			byte data5 = bytes[12];         // Data bytes start
			byte data4 = bytes[13];
			byte data3 = bytes[14];
			byte data2 = bytes[15];
			byte data1 = bytes[16];
			byte sysExEnd = bytes[17];      // End of SysEx message, 0xF7

			if (manufacturerID == 0x43 &&   // YAMAHA
				deviceNumber == 0x10 &&     // 1 = parameter send; 3 = parameter request, device ID 0
				groupID == 0x3E &&          // Digital mixer
				modelID == 0x12) {          // LS9
				return true;
			}
			return false;
		}

		(int, int, int) ConvertByteArray (byte[] bytes) {
			byte mixMSB = bytes[8];         // mix number MSB
			byte mixLSB = bytes[9];         // mix number LSB
			int mixHex = mixMSB << 7;       // Convert MSB to int in the right place
			mixHex += mixLSB;               // Add LSB

			byte channelMSB = bytes[10];    // channel number MSB
			byte channelLSB = bytes[11];    // channel number LSB
			int channel = channelMSB << 7;  // Convert MSB to int in the right place
			channel += channelLSB;          // Add LSB
			channel++;                      // LS9 has 0-indexed channel numbers over MIDI

			byte valueMSB = bytes[15];      // value MSB (for up to 14-bit value)
			byte valueLSB = bytes[16];      // value LSB
			int value = valueMSB << 7;      // Convert MSB to int in the right place
			value += valueLSB;              // Add LSB
			int mix = mixHex switch
			{
				0x05 => 1,
				0x08 => 2,
				0x0B => 3,
				0x0E => 4,
				0x11 => 5,
				0x14 => 6,
				_ => throw new NotImplementedException()
			};
			return (mix, channel, value);
		}

		void HandleMixSendMIDI (SysExEvent midiEvent) {
			(int mix, int channel, int value) = ConvertByteArray(midiEvent.Data);
			foreach (oscDevice device in oscDevices) {
				device.Faders[16 * (mix - 1) + channel] = value;
				device.sendOSCMessage(mix, channel, value);
			}
		}

		void HandleChannelName (byte[] bytes) {
			byte indexMSB = bytes[8];       // kNameShort1 is 0x00, 0x00
			byte indexLSB = bytes[9];       // kNameShort2 is 0x00, 0x01
			byte channelMSB = bytes[10];    // Channel MSB per channel
			byte channelLSB = bytes[11];    // Channel LSB with a 0 in the 8th bit
			byte data5 = bytes[12];         // Data bytes start
			byte data4 = bytes[13];
			byte data3 = bytes[14];
			byte data2 = bytes[15];
			byte data1 = bytes[16];

			int index = indexMSB << 7;
			index += indexLSB;

			int channel = channelMSB << 7;
			channel += channelLSB;

			byte[] data = { data5, data4, data3, data2, data1 };

			switch (index) { // the index number is either for kNameShort 1 or 2
				case 0x00: // kNameShort1
					channelNames[channel] = "1" + Encoding.ASCII.GetString(data);
					break;
				case 0x01: // kNameShort2
					channelNames[channel] = "2" + Encoding.ASCII.GetString(data);
					break;
			}
		}

		void HandleChannelFader (byte[] bytes) {
			byte channelMSB = bytes[10];    // Channel MSB per channel
			byte channelLSB = bytes[11];    // Channel LSB with a 0 in the 8th bit
			byte data2 = bytes[15];
			byte data1 = bytes[16];

			int channel = channelMSB << 7;
			channel += channelLSB;

			int level = data2 << 7;
			level += data1;
			float value = level / 1023f;
			channelFaders[channel] = value;

		}
		#endregion

		#region MIDI
		void LS9_out_EventReceived (object sender, MidiEventReceivedEventArgs e) {
			var LS9_device = (MidiDevice)sender;
			if (e.Event.EventType != MidiEventType.NormalSysEx)
				return;
			SysExEvent midiEvent = (SysExEvent)e.Event;
			byte[] bytes = midiEvent.Data;
			Console.WriteLine($"Event received from '{LS9_device.Name}' as: {e.Event}");
			if (CheckSysEx(bytes)) {
				byte dataCategory = bytes[5];   // kInputToMix is in 0x01
				byte elementMSB = bytes[6];     // kInputToMix has MSB 0x00
				byte elementLSB = bytes[7];     // kInputToMix has LSB 0x43
				byte indexMSB = bytes[8];       // index MSB is for the Mix ...
				byte indexLSB = bytes[9];       // ... as on the desk, MIX 0-5
				byte channelMSB = bytes[10];    // Channel MSB per channel
				byte channelLSB = bytes[11];    // Channel LSB with a 0 in the 8th bit

				int channel = channelMSB << 7;
				channel += channelLSB;

				if (dataCategory == 0x01 &&     // kInput
					elementMSB == 0x00 &&       // kInputToMix
					elementLSB == 0x43 &&       // kInputToMix
					0 <= channel &&
					channel <= 16) {
					int index = indexMSB << 7;
					index += indexLSB;
					switch (index) { // the index number must be for Mix1-6 send level
						case 0x05:  // Mix 1 ...
						case 0x08:
						case 0x0B:
						case 0x0E:
						case 0x11:
						case 0x14:  // Mix 6
							HandleMixSendMIDI(midiEvent);
							return;
					}
				} else if (dataCategory == 0x01 &&  // kNameInputChannel
						   elementMSB == 0x01 &&    // kNameShort
						   elementLSB == 0x14 &&    // kNameShort
						   0 <= channel &&
						   channel <= 16) {
					HandleChannelName(bytes);
				} else if (dataCategory == 0x01 &&  // kInput
						   elementMSB == 0x00 &&    // kFader
						   elementLSB == 0x33 &&    // kFader
						   0 <= channel &&
						   channel <= 16) {
					HandleChannelFader(bytes);
				}
			}
		}

		void TestMixerOutput () {
			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //			  Mix1		  Ch 1					  0 db  0 dB
			byte[] data = { 0xF0, 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x37, 0xF7 };
			sysExEvent.Data = data;
			SendSysEx(sysExEvent);
		}

		public void SendFaderValue (int mix, int channel, float value, oscDevice sender) {
			byte mixLSB = mix switch
			{
				1 => 0x05,
				2 => 0x08,
				3 => 0x0B,
				4 => 0x0E,
				5 => 0x11,
				6 => 0x14,
				_ => throw new NotImplementedException()
			};
			channel--; // LS9 channels are 0-indexed, OSC is 1-indexed
			byte channelLSB = (byte)(channel);
			byte channelMSB = (byte)(channel >> 8);

			int value_int = Convert.ToInt32(value * 1023); // There are 1023 fader levels as per the LS9 manual, hence remapping 0-1f to 0-1023
			byte valueMSB = (byte)(value_int);
			byte valueLSB = (byte)(value_int >> 8);

			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //				Mix					Ch							  0 db		0 dB
			byte[] data = { 0xF0, 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, mixLSB, channelMSB, channelLSB, 0x00, 0x00, 0x00, valueMSB, valueLSB, 0xF7 };
			sysExEvent.Data = data;

			if (activeSensingTimer != null)
				SendSysEx(sysExEvent);
			foreach (oscDevice device in oscDevices) {
				if (device != sender) { // Avoid feedback loop!
					device.Faders[16 * (mix - 1) + channel] = value;
					device.sendOSCMessage(mix, channel, value);
				}
			}
		}

		public void SendSysEx (NormalSysExEvent normalSysExEvent) {
			try {
				LS9_in.SendEvent(normalSysExEvent);
			} catch (MidiDeviceException ex) {
				Console.WriteLine($"Well shucks, {LS9_in.Name} don't work no more...");
				Console.WriteLine(ex.Message);
			} catch (ObjectDisposedException) {
				Console.WriteLine($"Tried to use {LS9_in.Name} without initializing MIDI!");
				MessageBox.Show("Initialize MIDI first!");
			} catch (NullReferenceException) {
				Console.WriteLine($"Tried to use MIDI device without initializing MIDI!");
				MessageBox.Show("Initialize MIDI first!");
			}
		}

		void LS9_in_EventSent (object sender, MidiEventSentEventArgs e) {
			var LS9_device = (MidiDevice)sender;
			Console.WriteLine($"Event sent to '{LS9_device.Name}' as: {e.Event}");
		}
		#endregion

		#region UIEvents
		void startMIDIButton_Click (object sender, RoutedEventArgs e) {
			startMIDIButton.IsEnabled = false;
			InitializeIO();
			startMIDIButton.IsEnabled = true;
		}

		void refreshOSCButton_Click (object sender, RoutedEventArgs e) {
			refreshOSCButton.IsEnabled = false;
			_ = RefreshOSCDevices();
		}

		void stopMIDIButton_Click (object sender, RoutedEventArgs e) {
			(activeSensingTimer as IDisposable)?.Dispose();
			if (activeSensingTimer != null)
				Title = "YAMAHA MIDI - MIDI not started";
			Console.WriteLine("Disposed active sensing timer");
			(LS9_in as IDisposable)?.Dispose();
			(LS9_out as IDisposable)?.Dispose();
			displayMIDIDevices();
		}

		void refreshFadersButton_Click (object sender, RoutedEventArgs e) {
			refreshFadersButton.IsEnabled = false;
			Task.Run(() => {
				if (activeSensingTimer != null) {
					GetAllFaderValues();
					GetChannelNames();
				}
				Dispatcher.Invoke(new Action(() => { refreshFadersButton.IsEnabled = true; }));
			});
		}

		void deviceListBox_MouseDoubleClick (object sender, System.Windows.Input.MouseButtonEventArgs e) {
			if (deviceListBox.SelectedItem != null) {
				int index = deviceListBox.SelectedIndex;
				oscDevice device = deviceListBox.SelectedItem as oscDevice;
				CreateOSCDevice editOSCdevice = new CreateOSCDevice();
				editOSCdevice.Owner = this;
				editOSCdevice.DataContext = DataContext;
				editOSCdevice.name.Text = device.name;
				if (device.input != null)
					editOSCdevice.listenPort.Text = device.input.Port.ToString();
				if (device.output != null) {
					editOSCdevice.addressIPTextBox.Address = device.output.Address.ToString();
					editOSCdevice.sendPort.Text = device.output.Port.ToString();
				}
				editOSCdevice.addButton.Content = "Save OSC device";
				editOSCdevice.Title = "Edit OSC device";
				editOSCdevice.ShowDialog();
				if (editOSCdevice.DialogResult.Value) {
					string address = editOSCdevice.addressIPTextBox.Address;
					int sendPort = int.Parse(editOSCdevice.sendPort.Text);
					int listenPort = int.Parse(editOSCdevice.listenPort.Text);
					oscDevices[index].SetDeviceName(editOSCdevice.name.Text);
					oscDevices[index].InitializeIO(address, sendPort, listenPort);
				}
			}
		}

		private void MenuItem_Click (object sender, RoutedEventArgs e) {
			if (deviceListBox.SelectedIndex == -1) {
				return;
			}
			oscDevices.RemoveAt(deviceListBox.SelectedIndex);
		}

		private void deviceListBox_MouseDown (object sender, System.Windows.Input.MouseButtonEventArgs e) {
			deviceListBox.UnselectAll();
		}

		void addDeviceButton_Click (object sender, RoutedEventArgs e) {
			CreateOSCDevice createOSCDevice = new CreateOSCDevice();
			createOSCDevice.Owner = this;
			createOSCDevice.DataContext = this.DataContext;
			createOSCDevice.ShowDialog();
			if (createOSCDevice.DialogResult.Value) {
				string address = createOSCDevice.addressIPTextBox.Address;
				int sendPort = int.Parse(createOSCDevice.sendPort.Text);
				int listenPort = int.Parse(createOSCDevice.listenPort.Text);
				oscDevice device = new oscDevice();
				device.name = createOSCDevice.name.Text;
				device.InitializeIO(address, sendPort, listenPort);
				oscDevices.Add(device);
			}
		}

		private void infoWindowButton_Click (object sender, RoutedEventArgs e) {
			InfoWindow infoWindow = new InfoWindow();
			infoWindow.Owner = this;
			infoWindow.DataContext = this.DataContext;
			infoWindow.Show();
		}
		#endregion

	}
}