using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using CoreAudio;
using static CoreAudio.AudioSessionControl2;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for SessionUI.xaml
	/// </summary>
	public partial class SessionUI : UserControl {
		private AudioSessionControl2 session;
		private ConcurrentQueue<float> volPeakHistory = new ConcurrentQueue<float>();
		private int historySize = 8;
		private bool allowUpdateUI = true;
		private bool isClosing = false;

		private Task getPeaksTask;
		private Task setMeterTask;

		public SessionUI () {
			InitializeComponent();
			for (int i = 0; i < historySize; i++) {
				volPeakHistory.Enqueue(0);
			}
		}

		~SessionUI () {
			isClosing = true;
			Task.Run(() => {
				while (!getPeaksTask.IsCompleted) { }
				getPeaksTask.Dispose();
			});
			Task.Run(() => {
				while (!setMeterTask.IsCompleted) { }
				setMeterTask.Dispose();
			});
		}

		public void SetSession (AudioSessionControl2 session) {
			this.session = session;
			session.OnStateChanged += SessionStateChanged;
			Process p = Process.GetProcessById((int)session.GetProcessID);

			if (!Dispatcher.CheckAccess()) {
				Dispatcher.Invoke(() => {
					sessionLabel.Content = session.IsSystemSoundsSession ? "System sounds" : session.DisplayName;
					if (sessionLabel.Content.ToString() == "") sessionLabel.Content = p.ProcessName;
				});
			} else {
				sessionLabel.Content = session.IsSystemSoundsSession ? "System sounds" : session.DisplayName;
				if (sessionLabel.Content.ToString() == "") sessionLabel.Content = p.ProcessName;
			}

			sessionLabel.Content = ParseLabel(sessionLabel.Content.ToString());

			session.OnSimpleVolumeChanged += new SimpleVolumeChangedDelegate(UpdateUI);
			sessionSlider.ValueChanged += (_, __) => UpdateVolume();
			sessionCheckBox.Checked += (_, __) => UpdateMuted();
			sessionCheckBox.Unchecked += (_, __) => UpdateMuted();

			Loaded += (_, __) => {
				float newValue = 0;
				float lastValue = -1;

				getPeaksTask = Task.Run(() => {
					while (!isClosing) {
						volPeakHistory.Enqueue(session.AudioMeterInformation.MasterPeakValue);
						if (volPeakHistory.Count > historySize) volPeakHistory.TryDequeue(out float _);

						Thread.Sleep(5);
					}
				});

				setMeterTask = Task.Run(() => {
					while (!isClosing) {
						newValue = volPeakHistory.Average();

						if (newValue != lastValue) {
							Dispatcher.Invoke(() => {
								sessionProgressBar.Value = newValue;
								lastValue = newValue;
							});
						}
						Thread.Sleep(16);
					}
				});
			};

			UpdateUI(null, session.SimpleAudioVolume.MasterVolume, session.SimpleAudioVolume.Mute);
		}

		private void SessionStateChanged (object sender, AudioSessionState newState) {
			if (newState == AudioSessionState.AudioSessionStateExpired) {
				session.Dispose();
				Dispatcher.Invoke(() => { AudioMixerWindow.instance.sessionStackPanel.Children.Remove(this); });
			}
		}

		private void UpdateVolume () {
			session.SimpleAudioVolume.MasterVolume = (float)(sessionSlider.Value);
		}

		private void UpdateMuted () {
			session.SimpleAudioVolume.Mute = sessionCheckBox.IsChecked.Value;
			if (session.SimpleAudioVolume.Mute) {
				SolidColorBrush backgroundBrush = new SolidColorBrush();
				backgroundBrush.Color = System.Windows.Media.Color.FromRgb(240, 240, 240);
				Background = backgroundBrush;
				SolidColorBrush borderBrush = new SolidColorBrush();
				borderBrush.Color = System.Windows.Media.Color.FromRgb(200, 200, 200);
				BorderBrush = borderBrush;
			} else {
				SolidColorBrush backgroundBrush = new SolidColorBrush();
				backgroundBrush.Color = System.Windows.Media.Color.FromRgb(255, 255, 255);
				Background = backgroundBrush;
				SolidColorBrush borderBrush = new SolidColorBrush();
				borderBrush.Color = System.Windows.Media.Color.FromRgb(255, 255, 255);
				BorderBrush = borderBrush;
			}
		}

		private void UpdateUI (object sender, float newVolume, bool newMute) {
			if (!allowUpdateUI) return;
			if (!Dispatcher.CheckAccess()) {
				Dispatcher.Invoke(new SimpleVolumeChangedDelegate(UpdateUI), new object[] { sender, newVolume, newMute });
			} else {
				sessionSlider.Value = newVolume;
				sessionCheckBox.IsChecked = newMute;
			}
		}

		private string ParseLabel (string text) {
			if (text.Length <= 6) {
				string firstLetter = text.Substring(0, 1).ToUpper();
				return firstLetter + text.Substring(1);
			}
			List<string> vowels = new List<string>() { "a", "e", "i", "o", "u" };
			string output = "";
			for (int i = 0; i < text.Length; i++) {
				string character = text.Substring(i, 1);
				if (i == 0) {
					output += character.ToUpper();
					continue;
				}
				if (vowels.Contains(character.ToLower())) {
					continue;
				}
				output += character;
			}

			if (output.Length > 6) {
				output = output.Substring(0, 6);
			}
			return output;
		}
	}
}
