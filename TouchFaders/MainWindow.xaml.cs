using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TouchFaders.Configuration;
using Windows.UI.ViewManagement;

namespace TouchFaders {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged (string name = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public class Device {
            public string Name { get; set; }

            public override string ToString () {
                return Name;
            }
        }

        public static MainWindow instance;
        private AppConfiguration.Config c;
        public AppConfiguration.Config config {
            get => c;
            set {
                OnPropertyChanged();
                c = value;
            }
        }
        public Data data;

        // OSC
        readonly List<oscDevice> devices = new List<oscDevice>();
        readonly ObservableCollection<Device> uiDevices = new ObservableCollection<Device>();
        Timer advertisingTimer;

        // Metering
        Timer meteringTimer;

        // RCP
        readonly AudioConsole audioConsole = new AudioConsole();

        // selected channel
        public Data.SelectedChannel selectedChannel;
        List<Data.SelectedChannel> selectedChannelCache = new List<Data.SelectedChannel>();
        Stack<int> selectedChannelIndexToGet = new Stack<int>();
        Timer selectedChannelTimer;

        // windows
        public InfoWindow infoWindow;
        public AudioMixerWindow audioMixerWindow;


        #region WindowEvents
        public MainWindow () {
            InitializeComponent();

            instance = this;
            config = (AppConfiguration.Config)Parser.Load(AppConfiguration.Config.defaultValues());
            Title = "TouchFaders | disconnected";

            Task.Run(() => { DataLoaded((Data)Parser.Load(new Data())); });

            UISettings settings = new UISettings();
            Windows.UI.Color foreground = settings.GetColorValue(UIColorType.Foreground);
            Windows.UI.Color background = settings.GetColorValue(UIColorType.Background);
            Foreground = new SolidColorBrush(Color.FromArgb(foreground.A, foreground.R, foreground.G, foreground.B));
            Background = new SolidColorBrush(Color.FromArgb(background.A, background.R, background.G, background.B));

            if (Application.Current.Resources.Contains("textColour")) {
                Application.Current.Resources["textColour"] = new SolidColorBrush(Color.FromArgb(foreground.A, foreground.R, foreground.G, foreground.B));
            }
            if (Application.Current.Resources.Contains("textBackground")) {
                Application.Current.Resources["textBackground"] = new SolidColorBrush(Color.FromArgb(background.A, background.R, background.G, background.B));
            }

            this.KeyDown += MainWindow_KeyDown;

        }

        private void mainWindow_Loaded (object sender, RoutedEventArgs e) {
            config = config;  // fix wonky property notification

            selectedChannel = new Data.SelectedChannel();
            UpdateSelectedChannel();
            devicesListBox.DataContext = this;
            devicesListBox.ItemsSource = uiDevices;
        }

        protected async override void OnClosed (EventArgs e) {
            Console.WriteLine("Closing...");
            base.OnClosed(e);

            foreach (oscDevice device in devices) {
                device.SendDisconnect();
                device.Close();
            }

            if (infoWindow != null) {
                infoWindow.Visibility = Visibility.Hidden;
                infoWindow.Close();
            }
            if (audioMixerWindow != null) {
                audioMixerWindow.Visibility = Visibility.Hidden;
                audioMixerWindow.Close();
            }
            stopConnectionButton_Click(null, null);
            advertisingTimer?.Dispose();
            await Parser.Store(config);
            await Parser.Store(data);
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
            double yScale = ActualHeight / 375f; // must be set to initial window sizing for proper scaling!!!
            double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
            ScaleValue = (double)OnCoerceScaleValue(mainWindow, value); // Update the actual scale for the main window
        }

        #endregion

        #region File & network I/O (and setup)
        void DataLoaded (Data data) {
            Dispatcher.Invoke(() => {
                this.data = data;
                configWindowButton.IsEnabled = true;
            });

            Data.channelNameChanged += Data_channelNameChanged;
            Data.channelMuteChanged += Data_channelMuteChanged;
            Data.channelPatchChanged += Data_channelPatchChanged;
            Data.channelColourChanged += Data_channelColourChanged;
            Data.mixNameChanged += Data_mixNameChanged;
            for (int i = 0; i < config.MIXER.channelCount; i++) {
                selectedChannelCache.Add(new Data.SelectedChannel() { name = $"ch {i + 1}", channelIndex = i });
            }

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

            Dispatcher.Invoke(() => {
                MenuItem ipMenu = (MenuItem)menuBar.Items[menuBar.Items.Count - 1];
                ipMenu.Header = $"{name} ({localIP})";
            });


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
                        device.Close();
                        break;
                    }
                }
                if (deviceToRemove != null) {
                    devices.Remove(deviceToRemove);
                    try {
                        Dispatcher.Invoke(() => Console.WriteLine($"{name} just diconnected"));
                    } catch (Exception) { }
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


                byte oscSend = Convert.ToByte(ports); // offset from 9000 in client app
                byte oscReceive = Convert.ToByte(ports); // offset from 8000 in client app

                List<byte> sendArray = new List<byte>();
                sendArray.Add(oscSend);
                sendArray.Add(oscReceive);
                sendArray.Add(Convert.ToByte(config.NUM_CHANNELS));
                foreach (var channel in data.channels) {
                    sendArray.Add(Convert.ToByte(channel.bgColourId));
                }
                sendArray.Add(Convert.ToByte(config.NUM_MIXES));
                foreach (var mix in data.mixes) {
                    sendArray.Add(Convert.ToByte(mix.bgColourId));
                }
                foreach (var mix in data.mixes) {
                    for (int i = 0; i < 6; i++) {
                        if (i < mix.name.Length) {
                            sendArray.Add(Convert.ToByte(mix.name[i]));
                        } else {
                            sendArray.Add(0);
                        }
                    }
                }

                byte[] sendBuffer = sendArray.ToArray();

                //int b = 0;
                //sendArray.ForEach(send => { Console.WriteLine($"{b}: {send.ToString()}"); b++; });

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
                tasks.Add(Task.Run(() => device.Refresh()));
            }
            await Task.WhenAll(tasks);
        }

        int SendMixMeteringBroadcast (byte[] data) {
            IPEndPoint targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 8879);
            BroadcastUDPClient sendUdpClient = new BroadcastUDPClient();
            //Console.WriteLine($"Sent metering bytes: {BitConverter.ToString(data)}");
            return sendUdpClient.Send(data, data.Length, targetEndPoint);
        }
        #endregion

        #region Callbacks
        private void Data_channelNameChanged (object sender, EventArgs e) {
            var args = e as Data.Channel.NameArgs;
            foreach (var device in devices) {
                device.SendChannelName(args.channel, args.name);
            }
        }

        private void Data_channelMuteChanged (object sender, EventArgs e) {
            var args = e as Data.Channel.MuteArgs;
            foreach (var device in devices) {
                device.SendChannelMute(args.channel, args.muted);
            }
        }

        private void Data_channelPatchChanged (object sender, EventArgs e) {
            var args = e as Data.Channel.PatchArgs;
            foreach (var device in devices) {
                device.SendChannelPatch(args.channel, args.patch);
            }
        }

        private void Data_channelColourChanged (object sender, EventArgs e) {
            var args = e as Data.Channel.ColourArgs;
            foreach (var device in devices) {
                device.SendChannelColour(args.channel, args.bgColourId);
            }
        }

        private void Data_mixNameChanged (object sender, EventArgs e) {
            var args = e as Data.Mix.NameArgs;
            foreach (var device in devices) {
                // TODO: this is sent over TCP initially
                // might as well relay to the console anyway
                //device.SendMixName(args.mix, args.name);
            }
        }
        #endregion

        #region MIDI I/O
        public void SendFaderValue (int mix, int channel, int value, oscDevice sender) {
            this.data.channels[channel - 1].sends[mix - 1].level = value;
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

            SysExCommand kInputToMix = config.MIXER.commands[SysExCommand.CommandType.kInputToMix];
        }

        public void SendChannelMute (int mix, int channel, bool muted, oscDevice sender) {
            data.channels[channel - 1].sends[mix - 1].muted = muted;
            SendOSCValue(mix, channel, muted, sender);
            // TODO: encode for MIDI
            // TODO: send to console
        }

        public void SendChannelLinkGroup (int channel, char linkGroup) {
            ushort channel_int = Convert.ToUInt16(channel);
            byte channelLSB = (byte)(channel_int & 0x7Fu);
            ushort shiftedChannel = (ushort)(channel_int >> 7);
            byte channelMSB = (byte)(shiftedChannel & 0x7Fu);

            byte group = Convert.ToByte(DataStructures.ChannelGroupChars.IndexOf(linkGroup));
            //Console.WriteLine($"Setting channel {channel + 1} link group to {linkGroup}");

            SysExCommand kGroupdId = config.MIXER.commands[SysExCommand.CommandType.kGroupID_Input];
        }

        public void SendAllAudioSessions () {
            for (int i = 0; i < audioMixerWindow.sessions.Count; i++) {
                SessionUI sessionUI = audioMixerWindow.sessions[i];
                //Console.WriteLine($"Sending {sessionUI.sessionLabel} to ch {config.mixer.channelCount - i}");
                SendAudioSession(i, sessionUI.session.SimpleAudioVolume.MasterVolume, sessionUI.session.SimpleAudioVolume.Mute, true);
            }
        }

        public void SendAudioSession (int index, float volume, bool mute, bool sendIt = false) {
            int channel = config.MIXER.channelCount - index - 1;
            ushort channel_int = Convert.ToUInt16(channel);
            byte channelLSB = (byte)(channel_int & 0x7Fu);
            ushort shiftedChannel = (ushort)(channel_int >> 7);
            byte channelMSB = (byte)(shiftedChannel & 0x7Fu);

            int value = Convert.ToInt32(volume * 1023);
            ushort value_int = Convert.ToUInt16(value); // There are 1023 fader levels as per the manual
            byte valueLSB = (byte)(value_int & 0x7Fu);
            ushort shiftedValue = (ushort)(value_int >> 7);
            byte valueMSB = (byte)(shiftedValue & 0x7Fu);

            SysExCommand kInputFader = config.MIXER.commands[SysExCommand.CommandType.kInputFader];
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

        private void SendOSCValue (int mix, int channel, bool muted, oscDevice sender) {
            Task.Run(() => {
                foreach (oscDevice device in devices) {
                    if (device != sender) { // Avoid feedback loop!
                        device.SendSendMute(mix, channel, muted);
                    }
                }
            });
        }
        #endregion

        #region Console I/O API
        void TryStart () {
            audioConsole.Connect(addressTextBox.Text);
        }
        #endregion

        #region UIEvents
        void startConnectionButton_Click (object sender, RoutedEventArgs e) {
            TryStart();
        }

        void CalculateSysExCommands () {
            int total = config.NUM_CHANNELS * config.NUM_MIXES; // sends to mix levels
            total += config.NUM_CHANNELS; // channel levels
            total += config.NUM_CHANNELS; // channel link groups
                                          //total += config.NUM_CHANNELS; // channel patch in (inputs)
                                          //total += config.NUM_CHANNELS; // channel names?
            Dispatcher.Invoke(() => syncProgressBar.Maximum = total);
        }

        void stopConnectionButton_Click (object sender, RoutedEventArgs e) {
            (meteringTimer as IDisposable)?.Dispose();
            if (stopConnectionButton.IsEnabled) {
                Thread.Sleep(1000);
            }
            Dispatcher.Invoke(() => {
                Title = "TouchFaders | disconnected";
                refreshConnectionButton.IsEnabled = false;
                startConnectionButton.IsEnabled = true;
                stopConnectionButton.IsEnabled = false;
                syncProgressBar.IsIndeterminate = false;
                syncProgressBar.Value = 0;
                configWindowButton.IsEnabled = true;
            });
        }

        void refreshConnectionButton_Click (object sender, RoutedEventArgs e) {
            if (refreshConnectionButton.IsEnabled) {
                Dispatcher.Invoke(new Action(() => {
                    refreshConnectionButton.IsEnabled = false;
                    syncProgressBar.IsIndeterminate = false;
                    syncProgressBar.Value = 0;
                }));
                bool enabled = stopConnectionButton.IsEnabled;
                CalculateSysExCommands();
                Task.Run(() => {
                    if (enabled) {
                        selectedChannelIndexToGet.Push(0);
                        //await GetChannelNames();
                    }
                    Dispatcher.Invoke(new Action(() => {
                        refreshConnectionButton.IsEnabled = true;
                        syncProgressBar.IsIndeterminate = true;
                    }));
                });
            }
        }

        void testMIDIButton_Click (object sender, RoutedEventArgs e) {
            if (stopConnectionButton.IsEnabled) {
                if (data.channels[0].sends[0].level != 0) {
                    SendFaderValue(1, 1, 0, null);
                } else {
                    SendFaderValue(1, 1, 823, null);
                }
            }
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
                selectedChannelImage.Source = new System.Windows.Media.Imaging.BitmapImage(Data.SelectedChannel.iconURIs[selectedChannel.iconID]);
                selectedChannelColour.Fill = DataStructures.bgColours[selectedChannel.bgColourID];
            }
        }

        private void devicesListBox_MouseDown (object sender, System.Windows.Input.MouseButtonEventArgs e) {
            devicesListBox.UnselectAll();
        }

        private void MainWindow_KeyDown (object sender, System.Windows.Input.KeyEventArgs e) {
            if (menuBar.IsMouseCaptured || menuBar.IsKeyboardFocusWithin) return;
            if (addressTextBox.IsMouseCaptured || addressTextBox.IsKeyboardFocusWithin) {
                if (e.Key == System.Windows.Input.Key.Escape) {
                    e.Handled = true;
                    System.Windows.Input.FocusManager.SetFocusedElement(this, null);
                    System.Windows.Input.Keyboard.ClearFocus();
                    instance.Focus();
                }
                return;
            }
            switch (e.Key) {
                case System.Windows.Input.Key.R:
                    e.Handled = true;
                    if (refreshConnectionButton.IsEnabled)
                        refreshConnectionButton_Click(this, new RoutedEventArgs());
                    break;
                case System.Windows.Input.Key.O:
                    e.Handled = true;
                    Task.Run(async () => await RefreshOSCDevices());
                    break;
                case System.Windows.Input.Key.S:
                    e.Handled = true;
                    if (startConnectionButton.IsEnabled)
                        startConnectionButton_Click(this, new RoutedEventArgs());
                    if (stopConnectionButton.IsEnabled)
                        stopConnectionButton_Click(this, new RoutedEventArgs());
                    break;
                case System.Windows.Input.Key.A:
                    e.Handled = true;
                    audioWindowButton_Click(this, new RoutedEventArgs());
                    break;
                case System.Windows.Input.Key.I:
                    e.Handled = true;
                    infoWindowButton_Click(this, new RoutedEventArgs());
                    break;
                case System.Windows.Input.Key.C:
                    e.Handled = true;
                    if (startConnectionButton.IsEnabled)
                        configWindowButton_Click(this, new RoutedEventArgs());
                    break;
                case System.Windows.Input.Key.T:
                    e.Handled = true;
                    testMIDIButton_Click(this, new RoutedEventArgs());
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