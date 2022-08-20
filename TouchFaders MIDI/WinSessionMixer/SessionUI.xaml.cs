using CoreAudio;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for SessionUI.xaml
	/// </summary>
	public partial class SessionUI : UserControl {
		public AudioSessionControl2 session;
		private ConcurrentQueue<float> volPeakHistory = new ConcurrentQueue<float>();
		private int historySize = 8;
		private bool allowUpdateUI = true;
		private bool isClosing = false;

		private Task getPeaksTask;
		private Task setMeterTask;

		public SessionUI () {
			InitializeComponent();

			Foreground = MainWindow.instance.Foreground;
			Background = MainWindow.instance.Background;

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
					sessionLabel.Content = session.IsSystemSoundsSession ? "System" : session.DisplayName;
					if (sessionLabel.Content.ToString() == "") sessionLabel.Content = p.ProcessName;
				});
			} else {
				sessionLabel.Content = session.IsSystemSoundsSession ? "System" : session.DisplayName;
				if (sessionLabel.Content.ToString() == "") sessionLabel.Content = p.ProcessName;
			}

			sessionTextBox.Text = ParseLabel(sessionLabel.Content.ToString());

			session.OnSimpleVolumeChanged += SessionVolumeChanged;
			sessionSlider.ValueChanged += SessionSlider_ValueChanged;
			sessionCheckBox.Checked += (_, __) => {
				session.SimpleAudioVolume.Mute = true;
				UpdateMuted();
			};
			sessionCheckBox.Unchecked += (_, __) => {
				session.SimpleAudioVolume.Mute = false;
				UpdateMuted();
			};

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
							try {
								Dispatcher.Invoke(() => {
									sessionProgressBar.Value = newValue;
									sessionProgressBarGrey.Value = newValue;
									lastValue = newValue;
								});
							} catch (TaskCanceledException) {
								// Eh, who cares?
							}
						}
						Thread.Sleep(16);
					}
				});
			};

			UpdateUI(session, session.SimpleAudioVolume.MasterVolume, session.SimpleAudioVolume.Mute);
			Dispatcher.Invoke(() => {
				var margin = sessionProgressBar.Margin;
				margin.Right = (1 - session.SimpleAudioVolume.MasterVolume) * sessionProgressBar.ActualWidth;
				sessionProgressBar.Margin = margin;
			});
		}

		private void SessionVolumeChanged (object sender, float newVolume, bool newMute) {
			UpdateUI(sender, newVolume, newMute);
			AudioMixerWindow.instance.SessionVolumeChanged(sender, newVolume, newMute);
		}

		private void SessionSlider_ValueChanged (object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e) {
			session.OnSimpleVolumeChanged -= SessionVolumeChanged;
			session.SimpleAudioVolume.MasterVolume = (float)sessionSlider.Value;
			AudioMixerWindow.instance.SessionVolumeChanged(session, session.SimpleAudioVolume.MasterVolume, session.SimpleAudioVolume.Mute);
			session.OnSimpleVolumeChanged += SessionVolumeChanged;
		}

		private void SessionStateChanged (object sender, AudioSessionState newState) {
			if (newState == AudioSessionState.AudioSessionStateExpired) {
				session.Dispose();
				AudioMixerWindow.instance.SessionVolumeChanged(session, 0f, true);
				Dispatcher.Invoke(() => { AudioMixerWindow.instance.sessionStackPanel.Children.Remove(this); });
			}
		}

		private void UpdateMuted () {
			if (sessionCheckBox.IsChecked.Value) {
				SolidColorBrush backgroundBrush = new SolidColorBrush();
				backgroundBrush.Color = System.Windows.Media.Color.FromArgb(100, 240, 240, 240);
				Background = backgroundBrush;
				SolidColorBrush borderBrush = new SolidColorBrush();
				borderBrush.Color = System.Windows.Media.Color.FromArgb(100, 200, 200, 200);
				BorderBrush = borderBrush;
				sessionProgressBar.Foreground = new SolidColorBrush() { Color = Color.FromRgb(176, 176, 176) };
				Color greyColour = ((SolidColorBrush)sessionProgressBarGrey.Foreground).Color;
				sessionProgressBarGrey.Foreground = new SolidColorBrush() { Color = Color.FromArgb(0, greyColour.R, greyColour.G, greyColour.B) };
			} else {
				SolidColorBrush backgroundBrush = new SolidColorBrush();
				Color bgColour = System.Windows.Media.Color.FromArgb(100, 255, 255, 255);
				bgColour = Color.Multiply(bgColour, ((SolidColorBrush)MainWindow.instance.Background).Color.R);
				backgroundBrush.Color = bgColour;
				Background = backgroundBrush;
				SolidColorBrush borderBrush = new SolidColorBrush();
				borderBrush.Color = bgColour;
				BorderBrush = borderBrush;
				sessionProgressBar.Foreground = new SolidColorBrush() { Color = Color.FromRgb(6, 176, 37) };
				Color greyColour = ((SolidColorBrush)sessionProgressBarGrey.Foreground).Color;
				sessionProgressBarGrey.Foreground = new SolidColorBrush() { Color = Color.FromArgb(255, greyColour.R, greyColour.G, greyColour.B) };
			}
		}


		public void UpdateUI (object sender) {
			UpdateUI(sender, session.SimpleAudioVolume.MasterVolume, session.SimpleAudioVolume.Mute);
		}

		private void UpdateUI (object sender, float newVolume, bool newMute) {
			if (!allowUpdateUI) return;
			if (!Dispatcher.CheckAccess()) {
				Dispatcher.Invoke(() => UpdateUI(sender, newVolume, newMute));
			} else {
				sessionSlider.ValueChanged -= SessionSlider_ValueChanged;
				sessionSlider.Value = newVolume;
				sessionCheckBox.IsChecked = newMute;
				var margin = sessionProgressBar.Margin;
				margin.Right = (1 - newVolume) * sessionProgressBarGrey.ActualWidth;
				sessionProgressBar.Margin = margin;
				sessionSlider.ValueChanged += SessionSlider_ValueChanged;
			}
		}

		public void UpdateSession (object sender, float newVolume, bool newMute) {
			session.OnSimpleVolumeChanged -= SessionVolumeChanged;
			UpdateUI(sender, newVolume, newMute);
			session.SimpleAudioVolume.MasterVolume = newVolume;
			session.SimpleAudioVolume.Mute = newMute;
			Dispatcher.Invoke(() => {
				UpdateMuted();
			});
			session.OnSimpleVolumeChanged += SessionVolumeChanged;
		}

		private string ParseLabel (string text) {
			if (text.Length <= 6) {
				string firstLetter = text.Substring(0, 1).ToUpper();
				return firstLetter + text.Substring(1);
			}
			if (text.Split()[0].Length <= 6) {
				string firstLetter = text.Substring(0, 1).ToUpper();
				return firstLetter + text.Split()[0].Substring(1);
			}
			int iterations = 0;
			string output = "";
			while (iterations < text.Length - 6) {
				output = RemoveLastVowel(text);
				if (output.Length == 6) {
					break;
				}
				iterations++;
			}

			if (output.Length > 6) {
				output = output.Substring(0, 6);
			}
			string firstLetterUppercase = output.Substring(0, 1).ToUpper();
			return firstLetterUppercase + output.Substring(1);
		}

		private string RemoveLastVowel (string text) {
			List<string> vowels = new List<string>() { "a", "e", "i", "o", "u" };
			for (int i = text.Length - 1; 0 <= i; i--) {
				string character = text.Substring(i, 1);
				if (vowels.Contains(character.ToLower())) {
					return text.Substring(0, i) + text.Substring(i + 1);
				}
			}
			return text;
		}
	}
}
