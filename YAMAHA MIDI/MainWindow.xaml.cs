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

namespace YAMAHA_MIDI {

	public class oscDevice : INotifyPropertyChanged {
		public string name;
		[JsonIgnore]
		public string Name {
			get {
				if (input == null && output == null) {
					return name + " with " + faders.Count + " faders";
				} else {
					return name + " at " + output.Address + ":" + output.Port + ", " + input.Port;
				}
			}
			set {
				name = value;
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
		public List<float> faders;

		public event PropertyChangedEventHandler PropertyChanged;

		public oscDevice () {
			Name = "Unnamed device";
			faders = (from number in Enumerable.Range(1, 96) select 823f / 1023f).ToList(); // 0dB is at 823 when value is 10-bit, therefore 823/1023
		}

		public void refresh () {
			if (DeviceName != null)
				setName(DeviceName);
			if (Address != null && ListenPort != null && SendPort != null)
				InitializeIO(Address, SendPort.Value, ListenPort.Value);
		}

		public void setName (string value) {
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

		public void parseOSCMessage (OscPacket packet) {
			if (packet is OscBundle) {
				OscBundle messageBundle = (OscBundle)packet;
				foreach (OscMessage message in messageBundle.Messages) {
					handleOSCMessage(message);
				}
			} else {
				OscMessage message = (OscMessage)packet;
				handleOSCMessage(message);
			}
		}

		private void handleOSCMessage (OscMessage message) {
			Console.WriteLine($"Received a message: {message.Address} {message.Arguments[0]}");
			if (message.Address.Contains("/iem/fader")) {
				int fader = int.Parse(String.Join("", message.Address.Where(char.IsDigit)));
				int channel = fader % 16;
				int mix = (fader - channel) / 16;
				float value = (float)message.Arguments[0];
				MainWindow.instance.SendSysEx(mix, channel, value);
				faders[fader - 1] = value;
			} else if (message.Address.Contains("/action/41743")) {
				if (message.Arguments[0].ToString() == "1")
					resendAllFaders();
			}
		}

		public void resendAllFaders () {
			for (int i = 0; i < faders.Count; i++) {
				sendOSCMessage(i + 1, faders[i]);
				Thread.Sleep(5);
			}
		}

		public void sendOSCMessage (int channel, float value) {
			OscMessage message = new OscMessage($"/iem/fader{channel}", value);
			output.Send(message);
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

		public MainWindow () {
			InitializeComponent();
			instance = this;
			LoadAll();
			deviceListBox.ItemsSource = oscDevices;
		}

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

		private void Window_Loaded (object sender, RoutedEventArgs e) {
			//InitializeIO();
			CalculateScale();
		}

		void LoadAll () {
			try {
				string file = File.ReadAllText("oscDevices.txt");
				oscDevices = JsonSerializer.Deserialize<ObservableCollection<oscDevice>>(file, new JsonSerializerOptions { IgnoreNullValues = true, });
			} catch (FileNotFoundException) {
				SaveAll();
			}
			foreach (oscDevice device in oscDevices)
				device.refresh();
		}

		#region setupMIDI
		public void InitializeIO () {
			try {
				LS9_in = OutputDevice.GetByName("LS9");
				LS9_out = InputDevice.GetByName("LS9");
			} catch (ArgumentException ex) {
				MessageBox.Show($"Can't initialize LS9 MIDI ports!\n{ex.Message}");
				Console.WriteLine(ex.Message);
				return;
			}
			LS9_in.EventSent += LS9_in_EventSent;
			LS9_out.EventReceived += LS9_out_EventReceived;
			try {
				LS9_out.StartEventsListening();
			} catch (MidiDeviceException ex) {
				Console.WriteLine("Couldn't start listening to LS9");
				Console.WriteLine(ex.Message);
				return;
			}
			if (LS9_out.IsListeningForEvents) {
				ResetEvent systemReset = new ResetEvent();
				try {
					LS9_in.SendEvent(systemReset);
				} catch (MidiDeviceException ex) {
					Console.WriteLine("Couldn't send system reset MIDI event to LS9");
					Console.WriteLine(ex.Message);
					return;
				}
				activeSensingTimer = new Timer(sendActiveSense, null, 0, 350);
			}
		}

		void sendActiveSense (object state) {
			ActiveSensingEvent activeSense = new ActiveSensingEvent();
			try {
				LS9_in.SendEvent(activeSense); // MIDI should now be initialized with the desk
			} catch (MidiDeviceException e) {
				Console.WriteLine("Couldn't send active sensing MIDI event to LS9");
				Console.WriteLine(e.Message);
			}
		}
		#endregion

		#region SysExMIDIHelpers
		bool CheckSysEx (byte[] bytes) {
			if (bytes.Length == 18) {
				return false;
			}
			byte sysExStart = bytes[0];     // Signals start of SysEx, 0xF0
			byte manufacturerID = bytes[1]; // YAMAHA is 0x43
			byte deviceNumber = bytes[2];   // device number is 0x1n where n is 0-15
			byte groupID = bytes[3];        // Digital mixer is 0x3E
			byte modelID = bytes[4];        // LS9 is 0x12
			byte dataCategory = bytes[5];   // kInputToMix is in 0x01
			byte elementMSB = bytes[6];     // kInputToMix has MSB 0x00
			byte elementLSB = bytes[7];     // kInputToMix has LSB 0x43
			byte indexMSB = bytes[8];       // index MSB is for the Mix ...
			byte indexLSB = bytes[9];       // ... as on the desk, MIX 0-5
			byte channelMSB = bytes[10];    // Channel MSB per channel
			byte channelLSB = bytes[11];    // Channel LSB with a 0 in the 8th bit
			byte data5 = bytes[12];         // Data bytes start
			byte data4 = bytes[13];         // ''
			byte data3 = bytes[14];         // ''
			byte data2 = bytes[15];         // ''
			byte data1 = bytes[16];         // ''
			byte sysExEnd = bytes[17];      // End of SysEx message, 0xF7

			if (manufacturerID == 0x43 &&   // YAMAHA
				deviceNumber == 0x10 &&     // Device 0
				groupID == 0x3E &&          // Digital mixer
				modelID == 0x12 &&          // LS9
				dataCategory == 0x01 &&     // kInput
				elementMSB == 0x00 &&       // kInputToMix
				elementLSB == 0x43) {       // kInputToMix
				int index = indexMSB << 7;
				index += indexLSB;
				if (index == 0x05 || index == 0x08 || index == 0x0E || index == 0x11 || index == 0x14) { // the index number must be for Mix1-6 send level
					return true;
				}
			}
			return false;
		}

		(int, int, int) ConvertByteArray (byte[] bytes) {
			byte mixMSB = bytes[8];         // mix number MSB
			byte mixLSB = bytes[9];         // mix number LSB
			int mix = mixMSB << 7;          // Convert MSB to int in the right place
			mix += mixLSB;                  // Add LSB

			byte channelMSB = bytes[10];    // channel number MSB
			byte channelLSB = bytes[11];    // channel number LSB
			int channel = channelMSB << 7;  // Convert MSB to int in the right place
			channel += channelLSB;          // Add LSB
			channel++;                      // LS9 has 0-indexed channel numbers over MIDI

			byte valueMSB = bytes[15];      // value MSB (for up to 14-bit value)
			byte valueLSB = bytes[16];      // value LSB
			int value = valueMSB << 7;      // Convert MSB to int in the right place
			value += valueLSB;              // Add LSB

			return (mix, channel, value);
		}
		#endregion

		#region MIDI
		void LS9_out_EventReceived (object sender, MidiEventReceivedEventArgs e) {
			var LS9_device = (MidiDevice)sender;
			if (e.Event.EventType != MidiEventType.NormalSysEx)
				return;
			SysExEvent midiEvent = (SysExEvent)e.Event;
			Console.WriteLine($"Event received from '{LS9_device.Name}' as: {e.Event}");
			if (CheckSysEx(midiEvent.Data)) {
				(int mix, int channel, int value) = ConvertByteArray(midiEvent.Data);
				foreach (oscDevice device in oscDevices) {
					device.sendOSCMessage(16 * mix + channel, value);
				}
			}
		}

		void TestMixerOutput () {
			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //			  Mix1		  Ch 1					  0 db  0 dB
			byte[] data = { 0xF0, 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x37, 0xF7 };
			sysExEvent.Data = data;
			try {
				LS9_in.SendEvent(sysExEvent);
			} catch (MidiDeviceException ex) {
				Console.WriteLine("Well shucks, LS9_in don't work no more...");
				Console.WriteLine(ex.Message);
			} catch (NullReferenceException ex) {
				Console.WriteLine("LS9_in isn't available right now");
				Console.WriteLine(ex.Message);
			}
		}

		public void SendSysEx (int mix, int channel, float value) {
			byte mixLSB = (byte)(mix);
			byte mixMSB = (byte)(mix >> 8);

			channel--; // LS9 channels are 0-indexed, OSC/REAPER is 1-indexed
			byte channelLSB = (byte)(channel);
			byte channelMSB = (byte)(channel >> 8);

			int value_int = Convert.ToInt32(value * 16383);
			byte valueMSB = (byte)(value_int);
			byte valueLSB = (byte)(value_int >> 8);

			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //				Mix					Ch							  0 db		0 dB
			byte[] data = { 0xF0, 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, mixMSB, mixLSB, channelMSB, channelLSB, 0x00, 0x00, 0x00, valueMSB, valueLSB, 0xF7 };
			sysExEvent.Data = data;

			try {
				LS9_in.SendEvent(sysExEvent);
			} catch (MidiDeviceException ex) {
				Console.WriteLine("Well shucks, LS9_in don't work no more...");
				Console.WriteLine(ex.Message);
			} catch (NullReferenceException) {
				Console.WriteLine("LS9 don't exist...");
			}
		}

		void LS9_in_EventSent (object sender, MidiEventSentEventArgs e) {
			var LS9_device = (MidiDevice)sender;
			Console.WriteLine($"Event sent to '{LS9_device.Name}' as: {e.Event}");
		}
		#endregion

		#region UIEvents
		void initializeIOButton_Click (object sender, RoutedEventArgs e) {
			InitializeIO();
		}

		void refreshOSCButton_Click (object sender, RoutedEventArgs e) {
			foreach (oscDevice device in oscDevices)
				device.refresh();
		}

		void disposeSensingTimerButton_Click (object sender, RoutedEventArgs e) {
			(activeSensingTimer as IDisposable)?.Dispose();
			if (activeSensingTimer != null)
				Console.WriteLine("Disposed active sensing timer");
			(LS9_in as IDisposable)?.Dispose();
			(LS9_out as IDisposable)?.Dispose();
		}

		void sendSysEx_Click (object sender, RoutedEventArgs e) {
			TestMixerOutput();
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
					oscDevices[index].setName(editOSCdevice.name.Text);
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
		#endregion

		async void SaveAll () {
			using (FileStream fs = File.Create("oscDevices.txt")) {
				await JsonSerializer.SerializeAsync(fs, oscDevices, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, });
			}
		}

		protected override void OnClosed (EventArgs e) {
			(activeSensingTimer as IDisposable)?.Dispose();
			(LS9_in as IDisposable)?.Dispose();
			(LS9_out as IDisposable)?.Dispose();
			SaveAll();
			base.OnClosed(e);
		}


	}
}