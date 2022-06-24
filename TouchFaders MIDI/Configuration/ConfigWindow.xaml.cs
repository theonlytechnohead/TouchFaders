using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using TouchFaders_MIDI.Configuration;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for ConfigWindow.xaml
	/// </summary>
	public partial class ConfigWindow : Window {

		public AppConfiguration.Config config;

		ObservableCollection<Mixer> mixers = new ObservableCollection<Mixer>();

		public ConfigWindow () {
			InitializeComponent();
			this.Loaded += ConfigWindow_Loaded;
			Foreground = MainWindow.instance.Foreground;
			Background = MainWindow.instance.Background;
		}

		private void ConfigWindow_Loaded (object sender, RoutedEventArgs e) {
			if (config == null) return;

			deviceGroupBox.Header = $"Device ID: {config.DEVICE_ID}";
			deviceIDSlider.Value = config.DEVICE_ID;

			Mixer dataObject = new Mixer();
			PropertyInfo[] properties = typeof(Mixer).GetProperties(BindingFlags.Static | BindingFlags.Public);
			foreach (PropertyInfo info in properties) {
				mixers.Add(info.GetValue(dataObject) as Mixer);
			}

			mixerComboBox.ItemsSource = mixers;
			mixerComboBox.SelectedItem = config.MIXER;

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
			double yScale = ActualHeight / 300f; // must be set to initial window sizing for proper scaling!!!
			double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
			ScaleValue = (double)OnCoerceScaleValue(configWindow, value); // Update the actual scale for the main window
		}

		#endregion

		private void deviceIDSlider_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
			Slider slider = sender as Slider;
			if (config == null) return;
			config.DEVICE_ID = (int)slider.Value;
			deviceGroupBox.Header = $"Device ID: {config.DEVICE_ID}";
		}

		private void mixerComboBox_SelectionChanged (object sender, SelectionChangedEventArgs e) {
			config.MIXER = mixerComboBox.SelectedItem as Mixer;

			if (channelSlider.Value >= config.MIXER.channelCount) channelSlider.Value = config.MIXER.channelCount;
			channelSlider.Maximum = config.MIXER.channelCount;

			if (mixSlider.Value >= config.MIXER.mixCount) mixSlider.Value = config.MIXER.mixCount;
			mixSlider.Maximum = config.MIXER.mixCount;
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
}
