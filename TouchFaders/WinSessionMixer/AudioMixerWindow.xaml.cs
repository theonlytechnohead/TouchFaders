using CoreAudio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TouchFaders {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class AudioMixerWindow : Window {

        public static AudioMixerWindow instance;

        private class RenderDevice {
            public readonly string Name;
            public readonly MMDevice Device;

            public RenderDevice (MMDevice device) {
                Device = device;
                Name = $"{device.Properties[PKey.DeviceDescription].Value} - {device.DeviceInterfaceFriendlyName}";
            }

            public override string ToString () {
                return Name;
            }
        }

        ObservableCollection<RenderDevice> devices = new ObservableCollection<RenderDevice>();
        MMDevice selectedDevice;
        AudioSessionManager2 audioSessionManager2;

        public List<SessionUI> sessions = new List<SessionUI>();

        #region Hide close button
        // from https://stackoverflow.com/questions/743906/how-to-hide-close-button-in-wpf-window
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong (IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong (IntPtr hWnd, int nIndex, int dwNewLong);
        #endregion

        public AudioMixerWindow () {
            InitializeComponent();

            instance = this;

            Loaded += delegate {
                // hide close button
                var hwnd = new WindowInteropHelper(this).Handle;
                SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
            };

            Foreground = MainWindow.instance.Foreground;
            Background = MainWindow.instance.Background;

            deviceComboBox.DataContext = this;
            deviceComboBox.ItemsSource = devices;
            deviceComboBox.SelectionChanged += (_, __) => EnumerateSessions();

            ListDevices();

            Activated += (_, __) => {
                foreach (SessionUI sessionUI in sessions) {
                    sessionUI.UpdateUI(this);
                }
            };

            //Console.WriteLine((sessionStackPanel.Children[0] as SessionUI).sessionLabel);
        }

        protected override void OnClosing (System.ComponentModel.CancelEventArgs e) {
            if (Visibility == Visibility.Visible) {
                e.Cancel = true;
            } else {
                base.OnClosing(e);
            }
        }

        private void ListDevices () {
            MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator(new Guid());
            MMDevice defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            audioSessionManager2 = defaultDevice.AudioSessionManager2;
            MMDeviceCollection devCol = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (int i = 0; i < devCol.Count; i++) {
                devices.Add(new RenderDevice(devCol[i]));
                if (devCol[i].DeviceFriendlyName == defaultDevice.DeviceFriendlyName) {
                    deviceComboBox.SelectedIndex = i;
                }
            }
        }

        private void EnumerateSessions () {
            selectedDevice = ((RenderDevice)deviceComboBox.SelectedItem).Device;
            audioSessionManager2.OnSessionCreated -= OnSessionCreated;
            audioSessionManager2 = selectedDevice.AudioSessionManager2;
            audioSessionManager2.OnSessionCreated += OnSessionCreated;
            audioSessionManager2.RefreshSessions();
            SessionCollection sessions = audioSessionManager2.Sessions;

            sessionStackPanel.Children.Clear();

            foreach (AudioSessionControl2 session in sessions) {
                if (session.State != AudioSessionState.AudioSessionStateExpired) {
                    SessionUI sessionUI = new SessionUI();
                    sessionUI.SetSession(session);
                    //sessionUI.session.OnSimpleVolumeChanged += Session_OnSimpleVolumeChanged;
                    sessionStackPanel.Children.Add(sessionUI);
                    this.sessions.Add(sessionUI);
                }
            }
        }

        private void OnSessionCreated (object sender, CoreAudio.Interfaces.IAudioSessionControl2 newSession) {
            Dispatcher.Invoke(() => {
                SessionUI sessionUI = new SessionUI();
                ConstructorInfo contructor = typeof(AudioSessionControl2).GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)[0];
                AudioSessionControl2 session = contructor.Invoke(new object[] { newSession }) as AudioSessionControl2;
                sessionUI.SetSession(session);
                sessionStackPanel.Children.Add(sessionUI);
                this.sessions.Add(sessionUI);
                MainWindow.instance.SendAudioSession(sessions.IndexOf(sessionUI), session.SimpleAudioVolume.MasterVolume, session.SimpleAudioVolume.Mute, true);
                //Session_OnSimpleVolumeChanged(session, session.SimpleAudioVolume.MasterVolume, session.SimpleAudioVolume.Mute);
                SessionVolumeChanged(session, session.SimpleAudioVolume.MasterVolume, session.SimpleAudioVolume.Mute);
                //sessionUI.session.OnSimpleVolumeChanged += Session_OnSimpleVolumeChanged;
            });
        }

        public void SessionVolumeChanged (object sender, float newVolume, bool newMute) {
            AudioSessionControl2 target = sender as AudioSessionControl2;

            int index = 0;
            foreach (SessionUI sessionUI in sessions) {
                if (sessionUI.session == target) {
                    index = sessions.IndexOf(sessionUI);
                    break;
                }
            }
            //Console.WriteLine($"Session {sessions[index].sessionLabel} vol: {newVolume} mute: {newMute}");
            MainWindow.instance.SendAudioSession(index, newVolume, newMute);
        }

        public void UpdateSession (int sessionIndex, float newVolume) {
            if (sessionIndex >= sessions.Count) return;
            SessionUI sessionUI = sessions[sessionIndex];
            if (sessionUI == null) return;
            UpdateSession(sessionIndex, newVolume, sessionUI.session.SimpleAudioVolume.Mute);
        }

        public void UpdateSession (int sessionIndex, bool newMute) {
            if (sessionIndex >= sessions.Count) return;
            SessionUI sessionUI = sessions[sessionIndex];
            if (sessionUI == null) return;
            UpdateSession(sessionIndex, sessionUI.session.SimpleAudioVolume.MasterVolume, newMute);
        }

        public void UpdateSession (int sessionIndex, float newVolume, bool newMute) {
            if (sessionIndex >= sessions.Count) return;
            SessionUI sessionUI = sessions[sessionIndex];
            if (sessionUI == null) return;
            //sessionUI.session.OnSimpleVolumeChanged -= Session_OnSimpleVolumeChanged;
            sessionUI.UpdateSession(this, newVolume, newMute);
            //sessionUI.session.OnSimpleVolumeChanged += Session_OnSimpleVolumeChanged;
        }

    }
}
