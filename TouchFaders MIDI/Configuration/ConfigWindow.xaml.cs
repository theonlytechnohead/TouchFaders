using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TouchFaders_MIDI.Configuration;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for ConfigWindow.xaml
	/// </summary>
	public partial class ConfigWindow : Window {

		public AppConfiguration.Config config;

		public Mixer.Type mixerType {
			get {
				return MainWindow.instance.config.MIXER.type;
			}
			set {
				MainWindow.instance.config.MIXER.type = value;
				UpdateModels();
				UpdateCounts();
			}
		}

		public Mixer.Model mixerModel {
			get {
				return MainWindow.instance.config.MIXER.model;
			}
			set {
				MainWindow.instance.config.MIXER.model = value;
				UpdateCounts();
			}
		}

		void UpdateModels () {
			smallModel.Content = $"{mixerType}";
			mediumModel.Content = $"{mixerType}";
			largeModel.Content = $"{mixerType}";
			smallModel.Visibility = Visibility.Visible;
			mediumModel.Visibility = Visibility.Visible;
			largeModel.Visibility = Visibility.Visible;
			// TODO: hardcoded!
			switch (mixerType) {
				case Mixer.Type.LS9:
					smallModel.Content += "-16";
					mediumModel.Visibility = Visibility.Collapsed;
					largeModel.Content += "-32";
					if (mediumModel.IsChecked.Value) {
						largeModel.IsChecked = true;
					}
					break;
				case Mixer.Type.QL:
					smallModel.Content += "1";
					mediumModel.Visibility = Visibility.Collapsed;
					largeModel.Content += "5";
					if (mediumModel.IsChecked.Value) {
						largeModel.IsChecked = true;
					}
					break;
				case Mixer.Type.CL:
					smallModel.Content += "1";
					mediumModel.Content += "3";
					largeModel.Content += "5";
					break;
			}
		}

		void UpdateCounts () {
			// TODO: hardcoded!
			int channels = 0;
			int mixes = 0;
			switch (mixerType) {
				case Mixer.Type.LS9:
					switch (mixerModel) {
						case Mixer.Model._1:
							channels = 32;
							mixes = 16;
							break;
						case Mixer.Model._3:
							channels = 64;
							mixes = 16;
							break;
						case Mixer.Model._5:
							channels = 64;
							mixes = 16;
							break;
					}
					break;
				case Mixer.Type.QL:
					switch (mixerModel) {
						case Mixer.Model._1:
							channels = 32;
							mixes = 16;
							break;
						case Mixer.Model._3:
							channels = 64;
							mixes = 16;
							break;
						case Mixer.Model._5:
							channels = 64;
							mixes = 16;
							break;
					}
					break;
				case Mixer.Type.CL:
					switch (mixerModel) {
						case Mixer.Model._1:
							channels = 48;
							mixes = 24;
							break;
						case Mixer.Model._3:
							channels = 64;
							mixes = 24;
							break;
						case Mixer.Model._5:
							channels = 72;
							mixes = 24;
							break;
					}
					break;
			}
			channelSlider.Maximum = channels;
			channelSlider.Value = channels;
			mixSlider.Maximum = mixes;
			mixSlider.Value = mixes;
		}

		public Mixer.Connection mixerConnection {
			get {
				return MainWindow.instance.config.MIXER.connection;
			}
			set {
				MainWindow.instance.config.MIXER.connection = value;
				switch (value) {
					case Mixer.Connection.MIDI:
						deviceIDSlider.IsEnabled = true;
						break;
					case Mixer.Connection.TCP:
						deviceIDSlider.IsEnabled = false;
						deviceIDSlider.Value = 1;
						break;
				}
			}
		}

		ObservableCollection<Mixer> mixers = new ObservableCollection<Mixer>();

		public ConfigWindow () {
			InitializeComponent();
			this.Loaded += ConfigWindow_Loaded;
			Foreground = MainWindow.instance.Foreground;
			Background = MainWindow.instance.Background;
		}

		private void ConfigWindow_Loaded (object sender, RoutedEventArgs e) {
			if (config == null) return;

			UpdateModels();

			deviceIDLabel.Content = $"ID: {config.DEVICE_ID}";
			deviceIDSlider.Value = config.DEVICE_ID;

			channelGroupBox.Header = $"Channels: {config.NUM_CHANNELS}";
			channelSlider.Value = config.NUM_CHANNELS;

			mixGroupBox.Header = $"Mixes: {config.NUM_MIXES}";
			mixSlider.Value = config.NUM_MIXES;

		}

		protected override void OnClosed (EventArgs e) {
			MainWindow.instance.config = config;
			base.OnClosed(e);
		}

		#region Scaling
		// This section smoothly scales everything within the mainGrid
		public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
			typeof(double),
			typeof(ConfigWindow),
			new UIPropertyMetadata(1.0,
				new PropertyChangedCallback(OnScaleValueChanged),
				new CoerceValueCallback(OnCoerceScaleValue)));

		private static object OnCoerceScaleValue (DependencyObject o, object value) {
			ConfigWindow configWindow = o as ConfigWindow;
			if (configWindow != null)
				return configWindow.OnCoerceScaleValue((double)value);
			else
				return value;
		}

		private static void OnScaleValueChanged (DependencyObject o, DependencyPropertyChangedEventArgs e) {
			ConfigWindow configWindow = o as ConfigWindow;
			if (configWindow != null)
				configWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
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

		private void configGrid_SizeChanged (object sender, SizeChangedEventArgs e) {
			CalculateScale();
		}

		private void CalculateScale () {
			double xScale = ActualWidth / 500f; // must be set to initial window sizing for proper scaling!!!
			double yScale = ActualHeight / 400f; // must be set to initial window sizing for proper scaling!!!
			double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
			ScaleValue = (double)OnCoerceScaleValue(configWindow, value); // Update the actual scale for the main window
		}

		#endregion

		private void deviceIDSlider_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
			Slider slider = sender as Slider;
			if (config == null) return;
			config.DEVICE_ID = (int)slider.Value;
			deviceIDLabel.Content = $"ID: {config.DEVICE_ID}";
		}

		private void channelSlider_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
			Slider slider = sender as Slider;
			if (config == null) return;
			config.NUM_CHANNELS = (int)slider.Value;
			channelGroupBox.Header = $"Channels: {config.NUM_CHANNELS}";
		}

		private void editChannelsButton_Click (object sender, RoutedEventArgs e) {
			ChannelConfigWindow channelConfigWindow = new ChannelConfigWindow();
			channelConfigWindow.Owner = this;
			channelConfigWindow.DataContext = this.DataContext;
			if (WindowState == WindowState.Maximized) {
				channelConfigWindow.WindowState = WindowState.Maximized;
			}
			channelConfigWindow.ShowDialog();
			//Close();
		}

		private void mixSlider_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
			Slider slider = sender as Slider;
			if (config == null) return;
			config.NUM_MIXES = (int)slider.Value;
			mixGroupBox.Header = $"Mixes: {config.NUM_MIXES}";
		}

		private void editMixesButton_Click (object sender, RoutedEventArgs e) {
			MixConfigWindow mixConfigWindow = new MixConfigWindow();
			mixConfigWindow.Owner = this;
			mixConfigWindow.DataContext = this.DataContext;
			if (WindowState == WindowState.Maximized) {
				mixConfigWindow.WindowState = WindowState.Maximized;
			}
			mixConfigWindow.ShowDialog();
		}

	}

	public class ComparisonConverter : IValueConverter {
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			return value?.Equals(parameter);
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			return value?.Equals(true) == true ? parameter : Binding.DoNothing;
		}
	}
}
