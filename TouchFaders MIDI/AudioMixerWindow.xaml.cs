using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using CoreAudio;

namespace TouchFaders_MIDI {
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
				Name = $"{device.Properties[PKEY.PKEY_Device_DeviceDesc].Value} ({device.FriendlyName})";
			}

			public override string ToString () {
				return Name;
			}
		}

		ObservableCollection<RenderDevice> devices = new ObservableCollection<RenderDevice>();
		MMDevice selectedDevice;
		AudioSessionManager2 audioSessionManager2;

		public AudioMixerWindow () {
			InitializeComponent();

			instance = this;

			deviceComboBox.DataContext = this;
			deviceComboBox.ItemsSource = devices;
			deviceComboBox.SelectionChanged += (_, __) => EnumerateSessions();

			ListDevices();
		}

		private void ListDevices () {
			MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
			MMDevice defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
			audioSessionManager2 = defaultDevice.AudioSessionManager2;
			MMDeviceCollection devCol = deviceEnumerator.EnumerateAudioEndPoints(EDataFlow.eRender, DEVICE_STATE.DEVICE_STATE_ACTIVE);
			for (int i = 0; i < devCol.Count; i++) {
				devices.Add(new RenderDevice(devCol[i]));
				if (devCol[i].FriendlyName == defaultDevice.FriendlyName) {
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
					sessionUI.session.OnSimpleVolumeChanged += Session_OnSimpleVolumeChanged;
					sessionStackPanel.Children.Add(sessionUI);
				}
			}
		}

		private void OnSessionCreated (object sender, CoreAudio.Interfaces.IAudioSessionControl2 newSession) {
			Dispatcher.Invoke(() => {
				SessionUI sessionUI = new SessionUI();
				ConstructorInfo contructor = typeof(AudioSessionControl2).GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)[0];
				AudioSessionControl2 session = contructor.Invoke(new object[] { newSession }) as AudioSessionControl2;
				sessionUI.SetSession(session);
				sessionUI.session.OnSimpleVolumeChanged += Session_OnSimpleVolumeChanged;
				sessionStackPanel.Children.Add(sessionUI);
			});
		}

		private void Session_OnSimpleVolumeChanged (object sender, float newVolume, bool newMute) {
			// send MIDI messages
		}

		public void UpdateSession (int sessionIndex, float newVolume) {
			SessionUI sessionUI = sessionStackPanel.Children[sessionIndex] as SessionUI;
			UpdateSession(sessionIndex, newVolume, sessionUI.session.SimpleAudioVolume.Mute);
		}

		public void UpdateSession (int sessionIndex, bool newMute) {
			SessionUI sessionUI = sessionStackPanel.Children[sessionIndex] as SessionUI;
			UpdateSession(sessionIndex, sessionUI.session.SimpleAudioVolume.MasterVolume, newMute);
		}

		public void UpdateSession (int sessionIndex, float newVolume, bool newMute) {
			SessionUI sessionUI = sessionStackPanel.Children[sessionIndex] as SessionUI;
			sessionUI.UpdateSession(this, newVolume, newMute);
		}

	}
}
