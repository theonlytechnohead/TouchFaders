using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Windows.UI.ViewManagement;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		public class Device {
			public string Name { get; set; }

			public override string ToString () {
				return Name;
			}
		}

		public static MainWindow instance;
		public AppConfiguration.appconfig config;

		List<oscDevice> devices = new List<oscDevice>();
		ObservableCollection<Device> uiDevices = new ObservableCollection<Device>();

		MIDI_Functions MIDI;
		OutputDevice Console_in;
		InputDevice Console_out;
		Timer queueTimer;
		Timer advertisingTimer;
		Timer meteringTimer;
		public Queue<NormalSysExEvent> midiQueue = new Queue<NormalSysExEvent>();

		public SendsToMix sendsToMix;

		public ChannelConfig channelConfig; // Replaces ChannelNames and ChannelFaders
		public ChannelConfig.SelectedChannel selectedChannel;
		List<ChannelConfig.SelectedChannel> selectedChannelCache = new List<ChannelConfig.SelectedChannel>();
		Stack<int> selectedChannelIndexToGet = new Stack<int>();
		Timer selectedChannelTimer;

		public InfoWindow infoWindow;
		public AudioMixerWindow audioMixerWindow;


		#region WindowEvents
		public MainWindow () {
			InitializeComponent();

			instance = this;
			Title = "TouchFaders MIDI | MIDI not started";

			config = AppConfiguration.Load();
			Task.Run(() => { DataLoaded(HandleIO.LoadAll()); });

			UISettings settings = new UISettings();
			Windows.UI.Color foreground = settings.GetColorValue(UIColorType.Foreground);
			Windows.UI.Color background = settings.GetColorValue(UIColorType.Background);
			Foreground = new SolidColorBrush(Color.FromArgb(foreground.A, foreground.R, foreground.G, foreground.B));
			Background = new SolidColorBrush(Color.FromArgb(background.A, background.R, background.G, background.B));

			if (Application.Current.Resources.Contains("textColour")) {
				Application.Current.Resources["textColour"] = new SolidColorBrush(Color.FromArgb(foreground.A, foreground.R, foreground.G, foreground.B));
			}
			if (Application.Current.Resources.Contains("textBackground")) {
				Application.Current.Resources["textBackground"] = new SolidColorBrush(Color.FromArgb(0, background.R, background.G, background.B));
			}

			this.KeyDown += MainWindow_KeyDown;

        }

		private void mainWindow_Loaded (object sender, RoutedEventArgs e) {
			selectedChannel = new ChannelConfig.SelectedChannel();
			UpdateSelectedChannel();
			devicesListBox.DataContext = this;
			devicesListBox.ItemsSource = uiDevices;
		}

		protected override async void OnClosed (EventArgs e) {
			Console.WriteLine("Closing...");
			infoWindow.Visibility = Visibility.Hidden;
			infoWindow.Close();
			audioMixerWindow.Visibility = Visibility.Hidden;
			audioMixerWindow.Close();
			stopMIDIButton_Click(null, null);
			advertisingTimer?.Dispose();
			await AppConfiguration.Save(config);
			HandleIO.FileData fileData = new HandleIO.FileData() {
				sendsToMix = this.sendsToMix,
				channelConfig = this.channelConfig
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
			double yScale = ActualHeight / 350f; // must be set to initial window sizing for proper scaling!!!
			double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
			ScaleValue = (double)OnCoerceScaleValue(mainWindow, value); // Update the actual scale for the main window
		}

		#endregion

		#region File & network I/O (and setup)
		void DataLoaded (HandleIO.FileData fileData) {
			// Lists and config
			Dispatcher.Invoke(() => { sendsToMix = fileData.sendsToMix; });
			Dispatcher.Invoke(() => { channelConfig = fileData.channelConfig; });
			for (int i = 0; i < config.mixer.channelCount; i++) {
				selectedChannelCache.Add(new ChannelConfig.SelectedChannel() { name = $"Ch {i + 1}", channelIndex = i });
			}

			// MIDI
			Dispatcher.Invoke(() => displayMIDIDevices());

			// Networking
			advertisingTimer = new Timer(UDPAdvertiser, null, 0, 2000);
			Task.Run(() => UDPListener());
			Task.Run(() => TCPListener());

			// Supplementary windows...
			Dispatcher.Invoke(() => {
				infoWindow = new InfoWindow();
				infoWindow.DataContext = this.DataContext;
				audioMixerWindow = new AudioMixerWindow();
				audioMixerWindow.Visibility = Visibility.Hidden;

				infoWindow.KeyDown += MainWindow_KeyDown;
				audioMixerWindow.KeyDown += MainWindow_KeyDown;
			});
		}

		private void UDPAdvertiser (object state) {
			IPAddress localIP;
			using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
				socket.Connect("8.8.8.8", 65530);
				IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
				localIP = endPoint.Address;
			}

			string name = Dns.GetHostName();

			byte[] directedBroadcast = localIP.GetAddressBytes();
			directedBroadcast[3] = 0xFF;

			IPEndPoint targetEndPoint = new IPEndPoint(new IPAddress(directedBroadcast), 8877);
			BroadcastUDPClient sendUdpClient = new BroadcastUDPClient();
			byte[] ipArray = localIP.GetAddressBytes();
			byte[] nameArray = Encoding.UTF8.GetBytes(name);
			byte[] data = new byte[ipArray.Length + nameArray.Length];

			ipArray.CopyTo(data, 0);
			nameArray.CopyTo(data, ipArray.Length - 1);

			//Console.WriteLine($"Sent advertisement from {targetEndPoint.Address}: {BitConverter.ToString(data)}");
			sendUdpClient.Send(data, data.Length, targetEndPoint);
		}

		private void UDPListener () {
			IPAddress anAddress = IPAddress.Any;
			UdpClient listener = new UdpClient();
			IPEndPoint endPoint = new IPEndPoint(anAddress, 8878);
			listener.Client.Bind(endPoint);

			var from = new IPEndPoint(0, 0);
			while (true) {
				byte[] buffer = listener.Receive(ref from);
				//Console.WriteLine(BitConverter.ToString(buffer));
				string name = Encoding.ASCII.GetString(buffer);

				oscDevice deviceToRemove = null;
				foreach (oscDevice device in devices) {
					if (device.deviceName == name) {
						deviceToRemove = device;
						break;
					}
				}
				if (deviceToRemove != null) {
					deviceToRemove.Close();
					devices.Remove(deviceToRemove);
					Dispatcher.Invoke(() => Console.WriteLine($"{name} just diconnected"));
				}
				foreach (Device device in uiDevices) {
					if (device.Name == name) {
						Dispatcher.Invoke(() => uiDevices.Remove(device));
						break;
					}
				}
			}
		}

		private void TCPListener () {
			IPAddress anAddress = IPAddress.Any;
			TcpListener listener = new TcpListener(anAddress, 8878);
			listener.Start();

			while (true) {
				TcpClient client = listener.AcceptTcpClient();

				NetworkStream networkStream = client.GetStream();
				byte[] buffer = new byte[client.ReceiveBufferSize];
				byte[] ipBuffer = new byte[4];

				int bytesRead = networkStream.Read(buffer, 0, client.ReceiveBufferSize);
				Array.Copy(buffer, ipBuffer, 4);

				string dataReceived = BitConverter.ToString(buffer, 0, bytesRead);
				//Console.WriteLine($"Data received: {dataReceived}");
				Console.WriteLine($"Device {Encoding.ASCII.GetString(buffer, 4, bytesRead - 4)} connected from {new IPAddress(ipBuffer)}");

				int ports = devices.Count + 1;

				AddOSCDevice(new IPAddress(ipBuffer), Encoding.ASCII.GetString(buffer, 4, bytesRead - 4), ports);


				byte oscSend = Convert.ToByte(ports); // offset from 9000
				byte oscReceive = Convert.ToByte(ports); // offset from 8000

				byte[] sendBuffer = new byte[] { oscSend, oscReceive, Convert.ToByte(config.NUM_CHANNELS), Convert.ToByte(config.NUM_MIXES) };

				networkStream.Write(sendBuffer, 0, sendBuffer.Length);
				client.Close();
			}
		}
		#endregion

		#region Device UI management
		void AddOSCDevice (IPAddress ipAddress, string name, int ports) {
			int sendPort = 9000 + ports;
			int listenPort = 8000 + ports;
			oscDevice device = new oscDevice(name, ipAddress, sendPort, listenPort);
			Dispatcher.Invoke(() => {
				devices.Add(device);
				uiDevices.Add(new Device() { Name = name });
			});
		}

		async Task RefreshOSCDevices () {
			List<Task> tasks = new List<Task>();
			foreach (oscDevice device in devices) {
				tasks.Add(Task.Run(() => {
					device.ResendMixFaders();
					Thread.Sleep(5);
					device.ResendAllNames(channelConfig.GetChannelNames());
				}));
			}
			await Task.WhenAll(tasks);
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

		int SendMixMeteringBroadcast (byte[] data) {
			IPEndPoint targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 8879);
			BroadcastUDPClient sendUdpClient = new BroadcastUDPClient();
			//Console.WriteLine($"Sent metering bytes: {BitConverter.ToString(data)}");
			return sendUdpClient.Send(data, data.Length, targetEndPoint);
		}
		#endregion

		#region MIDI management
		public async Task InitializeMIDI () {
			try {
				Dispatcher.Invoke(() => { Console_in = OutputDevice.GetByName(inputMIDIComboBox.SelectedItem.ToString()); });
				Dispatcher.Invoke(() => { Console_out = InputDevice.GetByName(outputMIDIComboBox.SelectedItem.ToString()); });
			} catch (ArgumentException ex) {
				MessageBox.Show($"Can't initialize {inputMIDIComboBox.SelectedItem} and {outputMIDIComboBox.SelectedItem} MIDI ports!\n{ex.Message}");
				Console.WriteLine(ex.Message);
				return;
			}
			Console_in.EventSent += Console_in_EventSent;
			Console_out.EventReceived += Console_out_EventReceived;
			try {
				Console_out.StartEventsListening();
			} catch (MidiDeviceException ex) {
				Console.WriteLine($"Couldn't start listening to {outputMIDIComboBox.SelectedItem}");
				Console.WriteLine(ex.Message);
				return;
			}
			if (Console_out.IsListeningForEvents) {
				Dispatcher.Invoke(() => {
					inputMIDIComboBox.IsEnabled = false;
					outputMIDIComboBox.IsEnabled = false;
					Title = "TouchFaders MIDI | MIDI started";
					Console.WriteLine("Started MIDI");
				});
				queueTimer = new Timer(sendQueueItem, null, 0, 8); // theoretical minimum of 7.2 (when sending 18-byte SysEx)
				await GetAllFaderValues();
				await GetChannelFaders();
				await GetAllChannelsLinkGroup();
				await RequestChannelsPatch();
				selectedChannelIndexToGet.Push(0);
				meteringTimer = new Timer(GetMixesMetering, null, 100, 2000);
				selectedChannelTimer = new Timer(GetSelectedChannelInfo, null, 1000, 500);
				SendAllAudioSessions();
				//await GetChannelNames();
			}
		}

		void sendQueueItem (object state) {
			if (midiQueue.Count > 0) {
				try {
					NormalSysExEvent sysExEvent = midiQueue.Dequeue();
					if (sysExEvent != null) {
						Console_in.SendEvent(sysExEvent);
						Dispatcher.Invoke(() => midiProgressBar.Value += 1);
					}
				} catch (MidiDeviceException ex) {
					Console.WriteLine($"Well shucks, {Console_in.Name} don't work no more...");
					Console.WriteLine(ex.Message);
					MessageBox.Show(ex.Message);
				} catch (ObjectDisposedException) {
					Console.WriteLine($"Tried to use {Console_in.Name} without initializing MIDI!");
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
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			for (int channel = 0; channel < config.NUM_CHANNELS; channel++) {
				NormalSysExEvent sysExEvent = new NormalSysExEvent();
				byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, mix, 0x00, Convert.ToByte(channel), 0xF7 };
				sysExEvent.Data = data;
				await SendSysEx(sysExEvent);
			}
		}

		void GetSelectedChannelInfo (object state) {
			bool canContinue = false;
			try {
				Dispatcher.Invoke(() => canContinue = midiProgressBar.Value >= midiProgressBar.Maximum);
			} catch (TaskCanceledException) { }
			if (!canContinue) return;
			if (selectedChannelIndexToGet.Count == 0) {
				return;
			} else {
				int channel = selectedChannelIndexToGet.Pop();
				Task.Run(async () => {
					await GetChannelName(channel);
					await GetChannelIcon(channel);
					await GetChannelColour(channel);
				});
				selectedChannelIndexToGet.Clear();
			}
		}

		async Task GetChannelFaders () {
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			for (int channel = 0; channel < config.NUM_CHANNELS; channel++) {
				NormalSysExEvent kFader = new NormalSysExEvent();
				byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x00, 0x33, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
				kFader.Data = data;
				await SendSysEx(kFader);
			}
		}

		async Task GetChannelNames () {
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			for (int channel = 0; channel < config.NUM_CHANNELS; channel++) {
				await GetChannelName(channel);
			}
		}

		async Task GetChannelName (int channel) {
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			NormalSysExEvent kNameShort1 = new NormalSysExEvent();
			byte[] data1 = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x01, 0x14, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
			kNameShort1.Data = data1;
			await SendSysEx(kNameShort1);
			NormalSysExEvent kNameShort2 = new NormalSysExEvent();
			byte[] data2 = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x01, 0x14, 0x00, 0x01, 0x00, Convert.ToByte(channel), 0xF7 };
			kNameShort2.Data = data2;
			await SendSysEx(kNameShort2);
		}

		async Task GetChannelIcon (int channel) {
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			NormalSysExEvent kIconID = new NormalSysExEvent();
			byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x01, 0x15, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
			kIconID.Data = data;
			await SendSysEx(kIconID);
		}

		async Task GetChannelColour (int channel) {
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			NormalSysExEvent kIconBgColour = new NormalSysExEvent();
			byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x01, 0x15, 0x00, 0x01, 0x00, Convert.ToByte(channel), 0xF7 };
			kIconBgColour.Data = data;
			await SendSysEx(kIconBgColour);
		}

		async Task RequestChannelsPatch () {
			for (int channel = 0; channel < config.NUM_CHANNELS; channel++) {
				await RequestChannelPatch(channel);
			}
		}

		async Task RequestChannelPatch (int channel) {
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			NormalSysExEvent kPatchInInput = new NormalSysExEvent();
			byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x01, 0x0B, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
			kPatchInInput.Data = data;
			await SendSysEx(kPatchInInput);
		}

		async Task GetAllChannelsLinkGroup () {
			for (int channel = 0; channel < config.NUM_CHANNELS; channel++) {
				await GetChannelLinkGroup(channel);
			}
		}

		async Task GetChannelLinkGroup (int channel) {
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			NormalSysExEvent kGroupID_Input = new NormalSysExEvent();
			byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x01, 0x06, 0x00, 0x00, 0x00, Convert.ToByte(channel), 0xF7 };
			kGroupID_Input.Data = data;
			await SendSysEx(kGroupID_Input);
		}

		async void GetMixesMetering (object state) {
			bool get = true;
			Dispatcher.Invoke(() => {
				get = midiProgressBar.Value >= midiProgressBar.Maximum;
			});
			if (!get) return;
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			NormalSysExEvent mixesMeteringPost = new NormalSysExEvent();
			byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x21, 0x01, 0x03, 0x7F, 0x00, 0x00, 0xF7 };
			mixesMeteringPost.Data = data;
			await SendSysEx(mixesMeteringPost);
		}

		async Task StopMetering () {
			byte device_byte = 0x30;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			NormalSysExEvent stopMeteringEvent = new NormalSysExEvent();
			byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x21, 0x7F, 0xF7 };
			stopMeteringEvent.Data = data;
			await SendSysEx(stopMeteringEvent);
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

			byte device_byte = 0x10;
			device_byte |= Convert.ToByte(config.device_ID - 1);

			if (manufacturerID == 0x43 &&       // YAMAHA
				deviceNumber == device_byte &&  // 1 = parameter send; 3 = parameter request, device ID 1
				groupID == 0x3E &&              // Digital mixer
				modelID == 0x12) {              // LS9
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
			/*int linkedIndex = linkedChannels.getIndex(channel - 1); // TODO: fix this
			if (linkedIndex != -1) {
				sendsToMix[mix - 1, linkedIndex] = value;
			}*/
			sendsToMix[mix - 1, channel - 1] = value;
			//Console.WriteLine($"Received level for mix {mix}, channel {channel}, value {value}");
			foreach (oscDevice device in devices) {
				/*if (linkedIndex != -1) { // TODO: fix this
					if (device.LegacyApp) {
						device.sendOSCMessage(mix, linkedIndex + 1, value / 1023f);
					} else {
						device.sendOSCMessage(mix, linkedIndex + 1, value);
					}
				}*/
				device.sendOSCMessage(mix, channel, value);
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
					channelConfig.channels[channel].name = ChannelConfig.Channel.kNameShortToString(data);
					if (channel == selectedChannel.channelIndex) { selectedChannel.kNameShort1 = data; }
					selectedChannelCache[channel].kNameShort1 = data;
					break;
				case 0x01: // kNameShort2
					channelConfig.channels[channel].name += ChannelConfig.Channel.kNameShortToString(data);
					foreach (oscDevice device in devices) {
						device.SendChannelName(channel + 1, channelConfig.channels[channel].name);
					}
					if (channel == selectedChannel.channelIndex) { selectedChannel.kNameShort2 = data; }
					selectedChannelCache[channel].kNameShort2 = data;
					break;
			}
		}

		void HandleChannelPatch (byte[] bytes) {
			byte channelMSB = bytes[9];    // channel number MSB
			byte channelLSB = bytes[10];    // channel number LSB
			ushort channel = (ushort)(channelMSB << 7);  // Convert MSB to int in the right place
			channel += channelLSB;          // Add LSB

			byte inputPatch = bytes[15];
			channelConfig.channels[channel].patch = inputPatch;
			foreach (oscDevice device in devices) {
				device.SendChannelPatch(channel + 1, inputPatch);
			}
		}

		void HandleChannelOn (byte[] bytes) {
			byte channelMSB = bytes[9];    // channel number MSB
			byte channelLSB = bytes[10];    // channel number LSB
			ushort channel = (ushort)(channelMSB << 7);  // Convert MSB to int in the right place
			channel += channelLSB;          // Add LSB

			byte on = bytes[15];

			if (channel >= config.mixer.channelCount - 8) {
				int index = config.mixer.channelCount - channel - 1;
				bool mute;
				if (on == 0) {
					mute = true;
				} else {
					mute = false;
				}
				audioMixerWindow.UpdateSession(index, mute);
			}
		}

		void HandleChannelLinkGroup (byte[] bytes) {
			byte channelMSB = bytes[9];    // channel number MSB
			byte channelLSB = bytes[10];    // channel number LSB
			ushort channel = (ushort)(channelMSB << 7);  // Convert MSB to int in the right place
			channel += channelLSB;          // Add LSB

			byte group = bytes[15];

			channelConfig.channels[channel].linkGroup = ChannelConfig.ChannelGroupChars[group];
		}

		void HandleChannelFader (byte[] bytes) {
			byte channelMSB = bytes[9];    // channel number MSB
			byte channelLSB = bytes[10];    // channel number LSB
			ushort channel = (ushort)(channelMSB << 7);  // Convert MSB to int in the right place
			channel += channelLSB;          // Add LSB

			byte data2 = bytes[14];
			byte data1 = bytes[15];

			ushort level = (ushort)(data2 << 7);
			level += data1;

			if (channel < config.mixer.channelCount - 8) { // TODO: make this proper and UI and stuff
				channelConfig.channels[channel].level = level;

				selectedChannelCache[channel].level = level;
				Dispatcher.Invoke(() => {
					if (midiProgressBar.Value >= midiProgressBar.Maximum) {
						if (channel == selectedChannel.channelIndex) {
							selectedChannel.level = level;
						} else {
							selectedChannel = selectedChannelCache[channel];
							selectedChannel.channelIndex = channel;
							selectedChannel.level = level;
							//_ = GetSelectedChannelInfo(); // if too many channels are moved in quick succession, overload occurs
							selectedChannelIndexToGet.Push(channel);
						}
					}
					UpdateSelectedChannel();
				});
			} else { // Now it's for the application audio mixer stuff
				bool canUpdate = false;
				Dispatcher.Invoke(() => canUpdate = midiProgressBar.Value >= midiProgressBar.Maximum);
				if (canUpdate) {
					int index = config.mixer.channelCount - channel - 1;
					float volume = level / 1023f;
					audioMixerWindow.UpdateSession(index, volume);
					//Console.WriteLine($"Updating audio session (index): {index}");
				}
			}
		}
		#endregion

		#region MIDI I/O
		void Console_out_EventReceived (object sender, MidiEventReceivedEventArgs e) {
			var console = (MidiDevice)sender;
			if (e.Event.EventType != MidiEventType.NormalSysEx)
				return;
			SysExEvent midiEvent = (SysExEvent)e.Event;
			byte[] bytes = midiEvent.Data;
			//string byte_string = BitConverter.ToString(bytes).Replace("-", ", ");
			//Console.WriteLine($"Event received from '{console.Name}' data: {byte_string}");
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
				} else if (dataCategory == 0x01 &&
						   elementMSB == 0x00 &&
						   elementLSB == 0x31) {
					HandleChannelOn(bytes);
				} else if (dataCategory == 0x01 &&  // kInput
							elementMSB == 0x00 &&    // kFader
							elementLSB == 0x33 &&    // kFader
							0 <= channel &&
							channel < config.NUM_CHANNELS) {
					HandleChannelFader(bytes);
				} else if (dataCategory == 0x01 &&
						   elementMSB == 0x01 &&
						   elementLSB == 0x06) { // kGroupID_Input
					HandleChannelLinkGroup(bytes);
				} else if (dataCategory == 0x01 &&
						   elementMSB == 0x01 &&
						   elementLSB == 0x0B) { // kPatchInInput
					HandleChannelPatch(bytes);
				} else if (dataCategory == 0x01 &&  // kIconInputChannel
						   elementMSB == 0x01 &&
						   elementLSB == 0x15 &&
						   indexMSB == 0x00 &&
						   indexLSB == 0x00) {    // kIconID
					if (channel == selectedChannel.channelIndex) {
						selectedChannel.iconID = bytes[15];
						UpdateSelectedChannel();
					}
					selectedChannelCache[channel].iconID = bytes[15];
				} else if (dataCategory == 0x01 &&  // kIconInputChannel
						   elementMSB == 0x01 &&
						   elementLSB == 0x15 &&
						   indexMSB == 0x00 &&
						   indexLSB == 0x01) {    // KIconBgColor
					if (channel == selectedChannel.channelIndex) {
						selectedChannel.bgColourID = bytes[15];
						UpdateSelectedChannel();
					}
					selectedChannelCache[channel].bgColourID = bytes[15];
				}
			} else {
				byte dataCategory = bytes[4];
				byte UL = bytes[5];
				byte LU = bytes[6];
				byte LL = bytes[7];
				if (dataCategory == 0x21 &&
					UL == 0x01 &&
					LU == 0x03 &&
					LL == 0x00) {
					byte[] meteringData = new byte[config.mixer.mixCount];
					for (int i = 0; i < config.mixer.mixCount; i++) {
						meteringData[i] = bytes[i + 8];
					}
					//Console.WriteLine("Got metering data!");
					SendMixMeteringBroadcast(meteringData);
				}
			}
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
			channel--; // Console channels are 0-indexed, OSC is 1-indexed
			ushort channel_int = Convert.ToUInt16(channel);
			byte channelLSB = (byte)(channel_int & 0x7Fu);
			ushort shiftedChannel = (ushort)(channel_int >> 7);
			byte channelMSB = (byte)(shiftedChannel & 0x7Fu);

			ushort value_int = Convert.ToUInt16(value); // There are 1023 fader levels as per the manual
			byte valueLSB = (byte)(value_int & 0x7Fu);
			ushort shiftedValue = (ushort)(value_int >> 7);
			byte valueMSB = (byte)(shiftedValue & 0x7Fu);

			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //		Mix					Ch							  db		dB
			byte device_byte = 0x10;
			device_byte |= Convert.ToByte(config.device_ID - 1);
			byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, mixLSB, channelMSB, channelLSB, 0x00, 0x00, 0x00, valueMSB, valueLSB, 0xF7 };
			sysExEvent.Data = data;
			bool enabled = false;
			Dispatcher.Invoke(() => { enabled = stopMIDIButton.IsEnabled; });
			if (enabled)
				_ = SendSysEx(sysExEvent);
		}

		public void SendChannelLinkGroup (int channel, char linkGroup) {
			byte device_byte = 0x10;
			device_byte |= Convert.ToByte(config.device_ID - 1);

			ushort channel_int = Convert.ToUInt16(channel);
			byte channelLSB = (byte)(channel_int & 0x7Fu);
			ushort shiftedChannel = (ushort)(channel_int >> 7);
			byte channelMSB = (byte)(shiftedChannel & 0x7Fu);

			byte group = Convert.ToByte(ChannelConfig.ChannelGroupChars.IndexOf(linkGroup));
			//Console.WriteLine($"Setting channel {channel + 1} link group to {linkGroup}");

			NormalSysExEvent kGroupID_Input = new NormalSysExEvent();
			byte[] data = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x01, 0x06, 0x00, 0x00, channelMSB, channelLSB, 0x00, 0x00, 0x00, 0x00, group, 0xF7 };
			kGroupID_Input.Data = data;
			bool enabled = false;
			Dispatcher.Invoke(() => { enabled = stopMIDIButton.IsEnabled; });
			if (enabled)
				_ = SendSysEx(kGroupID_Input);
		}

		public void SendAllAudioSessions () {
			for (int i = 0; i < audioMixerWindow.sessions.Count; i++) {
				SessionUI sessionUI = audioMixerWindow.sessions[i];
				//Console.WriteLine($"Sending {sessionUI.sessionLabel} to ch {config.mixer.channelCount - i}");
				SendAudioSession(i, sessionUI.session.SimpleAudioVolume.MasterVolume, sessionUI.session.SimpleAudioVolume.Mute, true);
			}
		}

		public void SendAudioSession (int index, float volume, bool mute, bool sendIt = false) {
			byte device_byte = 0x10;
			device_byte |= Convert.ToByte(config.device_ID - 1);

			int channel = config.mixer.channelCount - index - 1;
			ushort channel_int = Convert.ToUInt16(channel);
			byte channelLSB = (byte)(channel_int & 0x7Fu);
			ushort shiftedChannel = (ushort)(channel_int >> 7);
			byte channelMSB = (byte)(shiftedChannel & 0x7Fu);

			int value = Convert.ToInt32(volume * 1023);
			ushort value_int = Convert.ToUInt16(value); // There are 1023 fader levels as per the manual
			byte valueLSB = (byte)(value_int & 0x7Fu);
			ushort shiftedValue = (ushort)(value_int >> 7);
			byte valueMSB = (byte)(shiftedValue & 0x7Fu);

			NormalSysExEvent sessionVolume = new NormalSysExEvent();
			byte[] dataVolume = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x00, 0x33, 0x00, 0x00, channelMSB, channelLSB, 0x00, 0x00, 0x00, valueMSB, valueLSB, 0xF7 };
			sessionVolume.Data = dataVolume;

			byte on;
			if (mute) {
				on = 0x00;
			} else {
				on = 0x01;
			}
			NormalSysExEvent sessionOn = new NormalSysExEvent();
			byte[] dataOn = { 0x43, device_byte, 0x3E, 0x12, 0x01, 0x00, 0x31, 0x00, 0x00, channelMSB, channelLSB, 0x00, 0x00, 0x00, 0x00, on, 0xF7 };
			sessionOn.Data = dataOn;

			bool canContinue = false;
			Dispatcher.Invoke(() => canContinue = midiProgressBar.Value >= midiProgressBar.Maximum);
			if (canContinue || sendIt) {
				//Console.WriteLine(BitConverter.ToString(dataOn));
				_ = SendSysEx(sessionVolume);
				_ = SendSysEx(sessionOn);
			}
		}

		private void SendOSCValue (int mix, int channel, int value, oscDevice sender) {
			Task.Run(() => {
				foreach (oscDevice device in devices) {
					if (device != sender) { // Avoid feedback loop!
						device.sendOSCMessage(mix, channel, value);
					}
				}
			});
		}

		public async Task SendSysEx (NormalSysExEvent normalSysExEvent) {
			midiQueue.Enqueue(normalSysExEvent);
			await Task.Run(() => {
				Thread.Sleep(25);
			});
		}

		void Console_in_EventSent (object sender, MidiEventSentEventArgs e) {
			var console = (MidiDevice)sender;
			NormalSysExEvent sysExEvent = e.Event as NormalSysExEvent;
			//string byte_string = BitConverter.ToString(sysExEvent.Data).Replace("-", ", ");
			//Console.WriteLine($"{DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()} Event sent with data: {byte_string}");
		}
		#endregion

		#region UIEvents
		void startMIDIButton_Click (object sender, RoutedEventArgs e) {
			if (inputMIDIComboBox.SelectedItem != null && outputMIDIComboBox.SelectedItem != null) {
				CalculateSysExCommands();
				Dispatcher.Invoke(() => {
					startMIDIButton.IsEnabled = false;
					midiProgressBar.Value = 0;
				});
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

		void CalculateSysExCommands () {
			int total = config.NUM_CHANNELS * config.NUM_MIXES; // sends to mix levels
			total += config.NUM_CHANNELS; // channel levels
			total += config.NUM_CHANNELS; // channel link groups
			total += config.NUM_CHANNELS; // channel patch in (inputs)
										  //total += config.NUM_CHANNELS; // channel names?
			Dispatcher.Invoke(() => midiProgressBar.Maximum = total);
		}

		void stopMIDIButton_Click (object sender, RoutedEventArgs e) {
			(meteringTimer as IDisposable)?.Dispose();
			if (stopMIDIButton.IsEnabled) {
				_ = StopMetering();
				Thread.Sleep(1000);
			}
			(queueTimer as IDisposable)?.Dispose();
			Console.WriteLine("Stopped MIDI");
			Dispatcher.Invoke(() => {
				Title = "TouchFaders MIDI | MIDI not started";
				refreshMIDIButton.IsEnabled = false;
				startMIDIButton.IsEnabled = true;
				stopMIDIButton.IsEnabled = false;
				midiProgressBar.Value = 0;
			});
			(Console_in as IDisposable)?.Dispose();
			(Console_out as IDisposable)?.Dispose();
			displayMIDIDevices();
		}

		void refreshMIDIButton_Click (object sender, RoutedEventArgs e) {
			Dispatcher.Invoke(new Action(() => {
				refreshMIDIButton.IsEnabled = false;
				midiProgressBar.Value = 0;
			}));
			bool enabled = stopMIDIButton.IsEnabled;
			CalculateSysExCommands();
			Task.Run(async () => {
				if (enabled) {
					await GetAllFaderValues();
					await GetChannelFaders();
					await GetAllChannelsLinkGroup();
					selectedChannelIndexToGet.Push(0);
					//await GetChannelNames();
				}
				Dispatcher.Invoke(new Action(() => { refreshMIDIButton.IsEnabled = true; }));
			});

		}

		private void infoWindowButton_Click (object sender, RoutedEventArgs e) {
			if (infoWindow.Visibility == Visibility.Visible) {
				if (infoWindow.IsActive) {
					infoWindow.Hide();
				} else {
					infoWindow.Activate();
				}
				return;
			} else {
				if (WindowState == WindowState.Maximized) {
					infoWindow.WindowState = WindowState.Maximized;
				}
				infoWindow.Show();
			}
		}

		private void configWindowButton_Click (object sender, RoutedEventArgs e) {
			ConfigWindow configWindow = new ConfigWindow();
			configWindow.Owner = this;
			configWindow.DataContext = this.DataContext;
			configWindow.config = config;
			if (WindowState == WindowState.Maximized) {
				configWindow.WindowState = WindowState.Maximized;
			}
			configWindow.ShowDialog();
		}

		private void audioWindowButton_Click (object sender, RoutedEventArgs e) {
			if (audioMixerWindow.Visibility == Visibility.Visible) {
				if (audioMixerWindow.IsActive) {
					audioMixerWindow.Hide();
				} else {
					audioMixerWindow.Activate();
				}
			} else {
				audioMixerWindow.Show();
			}
		}

		private void quitButton_Click (object sender, RoutedEventArgs e) {
			this.Close();
        }

		private void UpdateSelectedChannel () {
			if (!Dispatcher.CheckAccess()) {
				Dispatcher.Invoke(() => UpdateSelectedChannel());
			} else {
				selectedChannelFader.Value = selectedChannel.level;
				selectedChannelName.Content = selectedChannel.name;
				selectedChannelImage.Source = new System.Windows.Media.Imaging.BitmapImage(ChannelConfig.SelectedChannel.iconURIs[selectedChannel.iconID]);
				selectedChannelColour.Fill = ChannelConfig.SelectedChannel.bgColours[selectedChannel.bgColourID];
			}
		}

		private void devicesListBox_MouseDown (object sender, System.Windows.Input.MouseButtonEventArgs e) {
			devicesListBox.UnselectAll();
		}

		private void MainWindow_KeyDown (object sender, System.Windows.Input.KeyEventArgs e) {
			if (menuBar.IsKeyboardFocusWithin) return;
			switch (e.Key) {
				case System.Windows.Input.Key.D:
					e.Handled = true;
					displayMIDIDevices();
					break;
				case System.Windows.Input.Key.R:
					e.Handled = true;
					if (refreshMIDIButton.IsEnabled)
						refreshMIDIButton_Click(this, new RoutedEventArgs());
					break;
				case System.Windows.Input.Key.O:
					e.Handled = true;
					Task.Run(async () => await RefreshOSCDevices());
					break;
				case System.Windows.Input.Key.S:
					e.Handled = true;
					if (startMIDIButton.IsEnabled)
						startMIDIButton_Click(this, new RoutedEventArgs());
					if (stopMIDIButton.IsEnabled)
						stopMIDIButton_Click(this, new RoutedEventArgs());
					break;
				case System.Windows.Input.Key.A:
					e.Handled = true;
					audioWindowButton_Click(this, new RoutedEventArgs());
					break;
				case System.Windows.Input.Key.I:
					e.Handled = true;
					infoWindowButton_Click(this, new RoutedEventArgs());
					break;
				case System.Windows.Input.Key.T:
					e.Handled = true;
					if (stopMIDIButton.IsEnabled) {
						if (sendsToMix[0, 0] != 0) {
							SendFaderValue(1, 1, 0, null);
							break;
						} else {
							SendFaderValue(1, 1, 823, null);
							break;
						}
					}
					break;
				case System.Windows.Input.Key.Q:
					e.Handled = true;
					quitButton_Click(this, new RoutedEventArgs());
					break;
			}
		}
		#endregion
	}
}