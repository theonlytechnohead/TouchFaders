using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using System.Threading.Tasks;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		public static MainWindow instance;
		public AppConfiguration.appconfig config;

		ObservableCollection<oscDevice> oscDevices;

		OutputDevice LS9_in;
		InputDevice LS9_out;
		Timer queueTimer;

		public SendsToMix sendsToMix;
		public ChannelNames channelNames;
		public ChannelFaders channelFaders;
		public LinkedChannels linkedChannels;

		public Queue<NormalSysExEvent> queue = new Queue<NormalSysExEvent>();

		#region WindowEvents
		public MainWindow () {
			InitializeComponent();

			//linkedChannels.links.Add(new LinkedChannel(10, 11)); //Click?
			instance = this;
			Title = "TouchFaders MIDI - MIDI not started";

			config = AppConfiguration.Load();
			linkedChannels = config.linkedChannels;

			Task.Run(() => { DataLoaded(HandleIO.LoadAll()); });

			this.KeyDown += MainWindow_KeyDown;
		}

		protected override async void OnClosed (EventArgs e) {
			Console.WriteLine("Closing...");
			stopMIDIButton_Click(null, null);
			await AppConfiguration.Save(config);
			HandleIO.FileData fileData = new HandleIO.FileData() {
				oscDevices = this.oscDevices,
				sendsToMix = this.sendsToMix,
				channelNames = this.channelNames,
				channelFaders = this.channelFaders
			};
			await HandleIO.SaveAll(fileData);
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
		void DataLoaded (HandleIO.FileData fileData) {
			Dispatcher.Invoke(() => {
				oscDevices = fileData.oscDevices;
				deviceListBox.ItemsSource = oscDevices;
			});
			Dispatcher.Invoke(() => { sendsToMix = fileData.sendsToMix; });
			Dispatcher.Invoke(() => { channelNames = fileData.channelNames; });
			Dispatcher.Invoke(() => { channelFaders = fileData.channelFaders; });

			Task.Run(async () => await RefreshOSCDevices());
			Dispatcher.Invoke(() => displayMIDIDevices());
		}
		#endregion

		#region Device UI management
		async Task RefreshOSCDevices () {
			List<Task> tasks = new List<Task>();
			foreach (oscDevice device in oscDevices) {
				tasks.Add(Task.Run(() => {
					device.Refresh();
					Thread.Sleep(5);
					device.ResendAllFaders();
					Thread.Sleep(5);
					device.ResendAllNames(channelNames.names);
				}));
			}
			await Task.WhenAll(tasks);
			Dispatcher.Invoke(() => {
				refreshOSCButton.IsEnabled = true;
			});
		}

		void displayMIDIDevices () {
			Dispatcher.Invoke(() => { inputMIDIComboBox.IsEnabled = false; });
			inputMIDIComboBox.Items.Clear();
			foreach (InputDevice inputDevice in InputDevice.GetAll()) {
				inputMIDIComboBox.Items.Add(inputDevice.Name);
				Dispatcher.Invoke(() => { inputMIDIComboBox.IsEnabled = true; });
			}
			Dispatcher.Invoke(() => { outputMIDIComboBox.IsEnabled = false; });
			outputMIDIComboBox.Items.Clear();
			foreach (OutputDevice outputDevice in OutputDevice.GetAll()) {
				outputMIDIComboBox.Items.Add(outputDevice.Name);
				Dispatcher.Invoke(() => { outputMIDIComboBox.IsEnabled = true; });
			}
			Dispatcher.Invoke(() => { startMIDIButton.IsEnabled = true; });
		}
		#endregion

		#region MIDI management
		public async Task InitializeMIDI () {
			try {
				Dispatcher.Invoke(() => { LS9_in = OutputDevice.GetByName(inputMIDIComboBox.SelectedItem.ToString()); });
				Dispatcher.Invoke(() => { LS9_out = InputDevice.GetByName(outputMIDIComboBox.SelectedItem.ToString()); });
			} catch (ArgumentException ex) {
				MessageBox.Show($"Can't initialize {inputMIDIComboBox.SelectedItem} and {outputMIDIComboBox.SelectedItem} MIDI ports!\n{ex.Message}");
				Console.WriteLine(ex.Message);
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
				Dispatcher.Invoke(() => {
					inputMIDIComboBox.IsEnabled = false;
					outputMIDIComboBox.IsEnabled = false;
					Title = "TouchFaders MIDI - MIDI started";
					Console.WriteLine("Started MIDI");
				});
				queueTimer = new Timer(sendQueueItem, null, 0, 20);
				await GetAllFaderValues();
				await GetChannelFaders();         // Channel faders to STEREO
												  //await GetChannelNames();
			}
		}

		void sendQueueItem (object state) {
			if (queue.Count > 0) {
				try {
					LS9_in.SendEvent(queue.Dequeue());
				} catch (MidiDeviceException ex) {
					Console.WriteLine($"Well shucks, {LS9_in.Name} don't work no more...");
					Console.WriteLine(ex.Message);
					MessageBox.Show(ex.Message);
				} catch (ObjectDisposedException) {
					Console.WriteLine($"Tried to use {LS9_in.Name} without initializing MIDI!");
					MessageBox.Show("Initialize MIDI first!");
				} catch (NullReferenceException) {
					Console.WriteLine($"Tried to use MIDI device without initializing MIDI!");
					MessageBox.Show("Initialize MIDI first!");
				}
			}
		}

		async Task GetAllFaderValues () {
			await GetFaderValuesForMix(0x05); // Mix 1 level...
			await GetFaderValuesForMix(0x08);
			await GetFaderValuesForMix(0x0B);
			await GetFaderValuesForMix(0x0E);
			await GetFaderValuesForMix(0x11);
			await GetFaderValuesForMix(0x14); // Mix 6
			await GetFaderValuesForMix(0x17);
			await GetFaderValuesForMix(0x1A); // Mix8
			await GetFaderValuesForMix(0x1D);
			await GetFaderValuesForMix(0x20);
			await GetFaderValuesForMix(0x23);
			await GetFaderValuesForMix(0x26);
			await GetFaderValuesForMix(0x29);
			await GetFaderValuesForMix(0x2C);
			await GetFaderValuesForMix(0x2F);
			await GetFaderValuesForMix(0x32); // Mix 16
		}

		async Task GetFaderValuesForMix (byte mix) {
			for (int channel = 0; channel < config.NUM_CHANNELS; channel++) {
				NormalSysExEvent sysExEvent = new NormalSysExEvent();
				byte[] data = { 0x43, 0x30, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, mix, 0x00, Convert.ToByte(channel), 0xF7 };
				sysExEvent.Data = data;
				await SendSysEx(sysExEvent);
			}
		}

		async Task GetChannelFaders () {
			for (int channel = 0; channel < config.NUM_CHANNELS; channel++) {
				NormalSysExEvent kFader = new NormalSysExEvent();
				byte[] data = { 0x43, 0x30, 0x3E, 0x12, 0x01, 0x00, 0x33, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
				kFader.Data = data;
				await SendSysEx(kFader);
			}
		}

		async Task GetChannelNames () {
			for (int channel = 0; channel < config.NUM_CHANNELS; channel++) {
				NormalSysExEvent kNameShort1 = new NormalSysExEvent();
				byte[] data1 = { 0x43, 0x30, 0x3E, 0x12, 0x01, 0x01, 0x14, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
				kNameShort1.Data = data1;
				await SendSysEx(kNameShort1);
				NormalSysExEvent kNameShort2 = new NormalSysExEvent();
				byte[] data2 = { 0x43, 0x30, 0x3E, 0x12, 0x01, 0x01, 0x14, 0x00, 0x01, 0x00, Convert.ToByte(channel), 0xF7 };
				kNameShort2.Data = data2;
				await SendSysEx(kNameShort2);
			}
		}
		#endregion

		#region SysExMIDIHelpers
		bool CheckSysEx (byte[] bytes) {
			if (bytes.Length != 17) {
				return false;
			}
			byte manufacturerID = bytes[0]; // YAMAHA is 0x43
			byte deviceNumber = bytes[1];   // device number is 0x1n where n is 0-15
			byte groupID = bytes[2];        // Digital mixer is 0x3E
			byte modelID = bytes[3];        // LS9 is 0x12
			byte dataCategory = bytes[4];
			byte elementMSB = bytes[5];
			byte elementLSB = bytes[6];
			byte indexMSB = bytes[7];
			byte indexLSB = bytes[8];
			byte channelMSB = bytes[9];     // Channel MSB per channel
			byte channelLSB = bytes[10];    // Channel LSB with a 0 in the 8th bit
			byte data5 = bytes[11];         // Data bytes start
			byte data4 = bytes[12];
			byte data3 = bytes[13];
			byte data2 = bytes[14];
			byte data1 = bytes[15];

			if (manufacturerID == 0x43 &&   // YAMAHA
				deviceNumber == 0x10 &&     // 1 = parameter send; 3 = parameter request, device ID 0
				groupID == 0x3E &&          // Digital mixer
				modelID == 0x12) {          // LS9
				return true;
			}
			return false;
		}

		(int, int, int) ConvertByteArray (byte[] bytes) {
			byte mixMSB = bytes[7];         // mix number MSB
			byte mixLSB = bytes[8];         // mix number LSB
			ushort mixHex = (ushort)(mixMSB << 7);       // Convert MSB to int in the right place
			mixHex += mixLSB;               // Add LSB

			byte channelMSB = bytes[9];    // channel number MSB
			byte channelLSB = bytes[10];    // channel number LSB
			ushort channel = (ushort)(channelMSB << 7);  // Convert MSB to int in the right place
			channel += channelLSB;          // Add LSB
			channel++;                      // LS9 has 0-indexed channel numbers over MIDI

			byte valueMSB = bytes[14];      // value MSB (for up to 14-bit value)
			byte valueLSB = bytes[15];      // value LSB
			ushort value = (ushort)(valueMSB << 7);      // Convert MSB to int in the right place
			value += valueLSB;              // Add LSB
			int mix = mixHex switch {
				0x05 => 1,
				0x08 => 2,
				0x0B => 3,
				0x0E => 4,
				0x11 => 5,
				0x14 => 6,
				0x17 => 7,
				0x1A => 8,
				0x1D => 9,
				0x20 => 10,
				0x23 => 11,
				0x26 => 12,
				0x29 => 13,
				0x2C => 14,
				0x2F => 15,
				0x32 => 16,
				_ => throw new NotImplementedException()
			};
			return (mix, channel, value);
		}

		void HandleMixSendMIDI (SysExEvent midiEvent) {
			(int mix, int channel, int value) = ConvertByteArray(midiEvent.Data);
			int linkedIndex = linkedChannels.getIndex(channel - 1);
			if (linkedIndex != -1) {
				sendsToMix[mix - 1, linkedIndex] = value;
			}
			sendsToMix[mix - 1, channel - 1] = value;
			Console.WriteLine($"Received level for mix {mix}, channel {channel}, value {value}");
			foreach (oscDevice device in oscDevices) {
				if (linkedIndex != -1) {
					if (device.LegacyApp) {
						device.sendOSCMessage(mix, linkedIndex + 1, value / 1023f);
					} else {
						device.sendOSCMessage(mix, linkedIndex + 1, value);
					}
				}
				if (device.LegacyApp) {
					device.sendOSCMessage(mix, channel, value / 1023f);
				} else {
					device.sendOSCMessage(mix, channel, value);
				}
			}
		}

		void HandleChannelName (byte[] bytes) {
			byte indexMSB = bytes[7];       // kNameShort1 is 0x00, 0x00
			byte indexLSB = bytes[8];       // kNameShort2 is 0x00, 0x01
			byte channelMSB = bytes[9];    // Channel MSB per channel
			byte channelLSB = bytes[10];    // Channel LSB with a 0 in the 8th bit
			byte data5 = bytes[11];         // Data bytes start
			byte data4 = bytes[12];
			byte data3 = bytes[13];
			byte data2 = bytes[14];
			byte data1 = bytes[15];

			ushort index = (ushort)(indexMSB << 7);
			index += indexLSB;

			ushort channel = (ushort)(channelMSB << 7);
			channel += channelLSB;

			byte[] data = { data5, data4, data3, data2, data1 };

			switch (index) { // the index number is either for kNameShort 1 or 2
				case 0x00: // kNameShort1
					channelNames[channel] = BitConverter.ToString(data);
					break;
				case 0x01: // kNameShort2
					channelNames[channel] += " " + BitConverter.ToString(data);
					break;
			}
		}

		void HandleChannelFader (byte[] bytes) {
			byte channelMSB = bytes[9];     // Channel MSB per channel
			byte channelLSB = bytes[10];    // Channel LSB with a 0 in the 8th bit
			byte data2 = bytes[14];
			byte data1 = bytes[15];

			ushort channel = (ushort)(channelMSB << 7);
			channel += channelLSB;

			ushort level = (ushort)(data2 << 7);
			level += data1;

			int linkedIndex = linkedChannels.getIndex(channel);
			if (linkedIndex != -1) {
				channelFaders[linkedIndex] = level;
			}
			channelFaders[channel] = level;
		}
		#endregion

		#region MIDI I/O
		void LS9_out_EventReceived (object sender, MidiEventReceivedEventArgs e) {
			var LS9_device = (MidiDevice)sender;
			if (e.Event.EventType != MidiEventType.NormalSysEx)
				return;
			SysExEvent midiEvent = (SysExEvent)e.Event;
			byte[] bytes = midiEvent.Data;
			//string byte_string = BitConverter.ToString(bytes).Replace("-", ", ");
			//Console.WriteLine($"Event received from '{LS9_device.Name}' date: {byte_string}");
			if (CheckSysEx(bytes)) {
				byte dataCategory = bytes[4];   // kInputToMix is in 0x01
				byte elementMSB = bytes[5];     // kInputToMix has MSB 0x00
				byte elementLSB = bytes[6];     // kInputToMix has LSB 0x43
				byte indexMSB = bytes[7];       // index MSB is for the Mix ...
				byte indexLSB = bytes[8];       // ... as on the desk, MIX 0-5
				byte channelMSB = bytes[9];     // Channel MSB per channel
				byte channelLSB = bytes[10];    // Channel LSB with a 0 in the 8th bit

				ushort channel = (ushort)(channelMSB << 7);
				channel += channelLSB;

				if (dataCategory == 0x01 &&     // kInput
					elementMSB == 0x00 &&       // kInputToMix
					elementLSB == 0x43 &&       // kInputToMix
					0 <= channel &&
					channel < config.NUM_CHANNELS) {
					ushort index = (ushort)(indexMSB << 7);
					index += indexLSB;
					switch (index) { // the index number must be for Mix1-6 send level
						case 0x05:  // Mix 1 ...
						case 0x08:
						case 0x0B:
						case 0x0E:
						case 0x11:
						case 0x14:  // Mix 6
						case 0x17:
						case 0x1A:  // Mix 8
						case 0x1D:
						case 0x20:
						case 0x23:
						case 0x26:
						case 0x29:
						case 0x2C:
						case 0x2F:
						case 0x32:  // Mix 16
							HandleMixSendMIDI(midiEvent);
							return;
					}
				} else if (dataCategory == 0x01 &&  // kNameInputChannel
						   elementMSB == 0x01 &&    // kNameShort
						   elementLSB == 0x14 &&    // kNameShort
						   0 <= channel &&
						   channel < config.NUM_CHANNELS) {
					HandleChannelName(bytes);
				} else if (dataCategory == 0x01 &&  // kInput
						   elementMSB == 0x00 &&    // kFader
						   elementLSB == 0x33 &&    // kFader
						   0 <= channel &&
						   channel < config.NUM_CHANNELS) {
					HandleChannelFader(bytes);
				}
			}
		}

		void TestMixerOutputHigh () {
			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //		Mix1		Ch 1					0 db  0 dB
			byte[] data = { 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x37, 0xF7 };
			sysExEvent.Data = data;
			_ = SendSysEx(sysExEvent);
		}

		void TestMixerOutputLow () {
			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //		Mix1		Ch 1					-inf dB
			byte[] data = { 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF7 };
			sysExEvent.Data = data;
			_ = SendSysEx(sysExEvent);
		}

		public void SendFaderValue (int mix, int channel, int value, oscDevice sender) {
			sendsToMix[mix - 1, channel - 1] = value;
			SendOSCValue(mix, channel, value, sender);
			byte mixLSB = mix switch {
				1 => 0x05,
				2 => 0x08,
				3 => 0x0B,
				4 => 0x0E,
				5 => 0x11,
				6 => 0x14,
				7 => 0x17,
				8 => 0x1A,
				9 => 0x1D,
				10 => 0x20,
				11 => 0x23,
				12 => 0x26,
				13 => 0x29,
				14 => 0x2C,
				15 => 0x2F,
				16 => 0x32,
				_ => throw new NotImplementedException()
			};
			channel--; // LS9 channels are 0-indexed, OSC is 1-indexed
			ushort channel_int = Convert.ToUInt16(channel);
			byte channelLSB = (byte)(channel_int & 0x7Fu);
			ushort shiftedChannel = (ushort)(channel_int >> 7);
			byte channelMSB = (byte)(shiftedChannel & 0x7Fu);

			ushort value_int = Convert.ToUInt16(value); // There are 1023 fader levels as per the LS9 manual
			byte valueLSB = (byte)(value_int & 0x7Fu);
			ushort shiftedValue = (ushort)(value_int >> 7);
			byte valueMSB = (byte)(shiftedValue & 0x7Fu);

			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //		Mix					Ch							  db		dB
			byte[] data = { 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, mixLSB, channelMSB, channelLSB, 0x00, 0x00, 0x00, valueMSB, valueLSB, 0xF7 };
			sysExEvent.Data = data;
			bool enabled = false;
			Dispatcher.Invoke(() => { enabled = stopMIDIButton.IsEnabled; });
			if (enabled)
				_ = SendSysEx(sysExEvent);
		}

		private void SendOSCValue (int mix, int channel, int value, oscDevice sender) {
			Task.Run(() => {
				foreach (oscDevice device in oscDevices) {
					if (device != sender) { // Avoid feedback loop!
						if (device.LegacyApp) {
							device.sendOSCMessage(mix, channel, value / 1023f);
						} else {
							device.sendOSCMessage(mix, channel, value);
						}
					}
				}
			});
		}

		public async Task SendSysEx (NormalSysExEvent normalSysExEvent) {
			queue.Enqueue(normalSysExEvent);
			await Task.Run(() => {
				Thread.Sleep(25);
			});
		}

		void LS9_in_EventSent (object sender, MidiEventSentEventArgs e) {
			var LS9_device = (MidiDevice)sender;
			NormalSysExEvent sysExEvent = e.Event as NormalSysExEvent;
			//string byte_string = BitConverter.ToString(sysExEvent.Data).Replace("-", ", ");
			//Console.WriteLine($"{DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()} Event sent with data: {byte_string}");
		}
		#endregion

		#region UIEvents
		void startMIDIButton_Click (object sender, RoutedEventArgs e) {
			if (inputMIDIComboBox.SelectedItem != null && outputMIDIComboBox.SelectedItem != null) {
				Dispatcher.Invoke(() => { startMIDIButton.IsEnabled = false; });
				Task.Run(async () => {
					await InitializeMIDI();
					Dispatcher.Invoke(() => {
						refreshMIDIButton.IsEnabled = true;
						stopMIDIButton.IsEnabled = true;
					});
				});
			} else {
				MessageBox.Show("Please select a MIDI input and output first!");
			}
		}

		void stopMIDIButton_Click (object sender, RoutedEventArgs e) {
			(queueTimer as IDisposable)?.Dispose();
			Console.WriteLine("Stopped MIDI");
			Dispatcher.Invoke(() => {
				Title = "TouchFaders MIDI - MIDI not started";
				refreshMIDIButton.IsEnabled = false;
				startMIDIButton.IsEnabled = true;
				stopMIDIButton.IsEnabled = false;
			});
			(LS9_in as IDisposable)?.Dispose();
			(LS9_out as IDisposable)?.Dispose();
			displayMIDIDevices();
		}

		void refreshOSCButton_Click (object sender, RoutedEventArgs e) {
			Dispatcher.Invoke(() => { refreshOSCButton.IsEnabled = false; });
			_ = RefreshOSCDevices();
		}

		void refreshMIDIButton_Click (object sender, RoutedEventArgs e) {
			Dispatcher.Invoke(new Action(() => { refreshMIDIButton.IsEnabled = false; }));
			bool enabled = stopMIDIButton.IsEnabled;
			Task.Run(async () => {
				if (enabled) {
					await GetAllFaderValues();
					await GetChannelFaders();
					await GetChannelNames();
				}
				Dispatcher.Invoke(new Action(() => { refreshMIDIButton.IsEnabled = true; }));
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
			infoWindow.KeyDown += MainWindow_KeyDown;
			infoWindow.DataContext = this.DataContext;
			infoWindow.Show();
		}

		private void configWindowButton_Click (object sender, RoutedEventArgs e) {
			ConfigWindow configWindow = new ConfigWindow();
			configWindow.Owner = this;
			configWindow.DataContext = this.DataContext;
			configWindow.config = config;
			configWindow.ShowDialog();
		}

		private void MainWindow_KeyDown (object sender, System.Windows.Input.KeyEventArgs e) {
			switch (e.Key) {
				case System.Windows.Input.Key.R:
					displayMIDIDevices();
					break;
				case System.Windows.Input.Key.M:
					if (refreshMIDIButton.IsEnabled)
						refreshMIDIButton_Click(this, new RoutedEventArgs());
					break;
				case System.Windows.Input.Key.O:
					if (refreshOSCButton.IsEnabled)
						refreshOSCButton_Click(this, new RoutedEventArgs());
					break;
				case System.Windows.Input.Key.S:
					if (startMIDIButton.IsEnabled)
						startMIDIButton_Click(this, new RoutedEventArgs());
					if (stopMIDIButton.IsEnabled)
						stopMIDIButton_Click(this, new RoutedEventArgs());
					break;
				case System.Windows.Input.Key.I:
					if (sender is InfoWindow) {
						InfoWindow infoWindow = sender as InfoWindow;
						infoWindow.Close();
						break;
					} else {
						infoWindowButton_Click(this, new RoutedEventArgs());
						break;
					}
				case System.Windows.Input.Key.T:
					if (stopMIDIButton.IsEnabled) {
						if (sendsToMix[0, 0] != 0f) {
							TestMixerOutputHigh();
							break;
						} else {
							TestMixerOutputLow();
							break;
						}
					}
					break;
				case System.Windows.Input.Key.Q:
					this.Close();
					break;
			}
		}
		#endregion

	}
}