using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace YAMAHA_MIDI {
	/// <summary>
	/// Interaction logic for InfoWindow.xaml
	/// </summary>
	public partial class InfoWindow : Window {
		readonly List<Label> labels;
		readonly List<ProgressBar> faderBars;

		public InfoWindow () {
			InitializeComponent();
			labels = new List<Label> {
				labelChannel1,
				labelChannel2,
				labelChannel3,
				labelChannel4,
				labelChannel5,
				labelChannel6,
				labelChannel7,
				labelChannel8,
				labelChannel9,
				labelChannel10,
				labelChannel11,
				labelChannel12,
				labelChannel13,
				labelChannel14,
				labelChannel15,
				labelChannel16
			};
			faderBars = new List<ProgressBar>() {
				faderChannel1,
				faderChannel2,
				faderChannel3,
				faderChannel4,
				faderChannel5,
				faderChannel6,
				faderChannel7,
				faderChannel8,
				faderChannel9,
				faderChannel10,
				faderChannel11,
				faderChannel12,
				faderChannel13,
				faderChannel14,
				faderChannel15,
				faderChannel16
			};
			MainWindow.instance.channelNames.channelNamesChanged += channelNamesChanged;
			MainWindow.instance.channelFaders.channelFadersChanged += channelFadersChanged;
			SetLabelsText(MainWindow.instance.channelNames.names);
			SetFadersValue(MainWindow.instance.channelFaders.faders);
		}

		private void channelNamesChanged (object sender, EventArgs e) {
			SetLabelsText(MainWindow.instance.channelNames.names);
		}

		private void channelFadersChanged (object sender, EventArgs e) {
			SetFadersValue(MainWindow.instance.channelFaders.faders);
		}

		void SetLabelsText (List<string> channelNames) {
			Dispatcher.Invoke(() => {
				for (int i = 0; i <= 15; i++) {
					labels[i].Content = channelNames[i];
				}
			});
		}

		void SetFadersValue (List<int> channelFaders) {
			Dispatcher.Invoke(() => {
				for (int i = 0; i <= 15; i++) {
					faderBars[i].Value = channelFaders[i];
				}
			});
		}

		#region Scaling
		// This section smoothly scales everything within the mainGrid
		public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
			typeof(double),
			typeof(InfoWindow),
			new UIPropertyMetadata(1.0,
				new PropertyChangedCallback(OnScaleValueChanged),
				new CoerceValueCallback(OnCoerceScaleValue)));

		private static object OnCoerceScaleValue (DependencyObject o, object value) {
			InfoWindow infoWindow = o as InfoWindow;
			if (infoWindow != null)
				return infoWindow.OnCoerceScaleValue((double)value);
			else
				return value;
		}

		private static void OnScaleValueChanged (DependencyObject o, DependencyPropertyChangedEventArgs e) {
			InfoWindow infoWindow = o as InfoWindow;
			if (infoWindow != null)
				infoWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
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

		private void infoGrid_SizeChanged (object sender, EventArgs e) {
			CalculateScale();
		}

		private void CalculateScale () {
			double xScale = ActualWidth / 800f; // must be set to initial window sizing for proper scaling!!!
			double yScale = ActualHeight / 450f; // must be set to initial window sizing for proper scaling!!!
			double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
			ScaleValue = (double)OnCoerceScaleValue(infoWindow, value); // Update the actual scale for the main window
		}
		#endregion
	}
}
