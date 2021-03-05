using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for ChannelConfigWindow.xaml
	/// </summary>
	public partial class ChannelConfigWindow : Window {

		public ChannelConfig channelConfig;

		public ObservableCollection<ChannelConfigUI> channelConfigUI;

		public class ChannelConfigUI {
			public string ChannelName { get; set; }
			int ChannelLevel { get; set; }
			private char group;
			public char ChannelGroup {
				get {
					return group;
				}
				set {
					group = value;
					PropertyChanged?.Invoke(this, new EventArgs());
				}
			}
			public ObservableCollection<char> ChannelGroups { get; set; }

			public EventHandler PropertyChanged;

			public ChannelConfigUI (ChannelConfig.Channel channel) {
				ChannelName = channel.name;
				ChannelLevel = channel.level;
				ChannelGroup = channel.linkGroup;
				ChannelGroups = ChannelConfig.ChannelGroupChars;
			}

			public ChannelConfig.Channel AsChannel () {
				return new ChannelConfig.Channel() {
					name = ChannelName,
					level = ChannelLevel,
					linkGroup = ChannelGroup
				};
			}
		}

		public ChannelConfigWindow () {
			InitializeComponent();
			channelConfigUI = new ObservableCollection<ChannelConfigUI>();
			Foreground = MainWindow.instance.Foreground;
			Background = MainWindow.instance.Background;
			channelDataGrid.Foreground = MainWindow.instance.Foreground;
			channelDataGrid.Background = MainWindow.instance.Background;
		}

		private void channelConfigWindow_Loaded (object sender, RoutedEventArgs e) {
			channelDataGrid.DataContext = this;
			channelDataGrid.ItemsSource = channelConfigUI;
			channelConfigUI.CollectionChanged += ChannelConfigUI_CollectionChanged;
			for (int i = 1; i <= 64; i++) {
				ChannelConfigUI channel = new ChannelConfigUI(channelConfig.channels[i - 1]);
				channelConfigUI.Add(channel);
			}
		}

		private void ChannelConfigUI_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e) {
			if (e.Action == NotifyCollectionChangedAction.Remove) {
				foreach (ChannelConfigUI item in e.OldItems) {
					//Removed items
					item.PropertyChanged -= ChannelConfigUIPropertyChanged;
				}
			} else if (e.Action == NotifyCollectionChangedAction.Add) {
				foreach (ChannelConfigUI item in e.NewItems) {
					//Added items
					item.PropertyChanged += ChannelConfigUIPropertyChanged;
				}
			}
		}

		private void ChannelConfigUIPropertyChanged (object sender, EventArgs e) {
			ChannelConfigUI configUI = sender as ChannelConfigUI;
			int index = channelConfigUI.IndexOf(configUI);
			char group = configUI.ChannelGroup;
			//Console.WriteLine($"{channelConfigUI.IndexOf(configUI)}:{configUI.ChannelGroup}");
			MainWindow.instance.SendChannelLinkGroup(index, group);
		}

		protected override void OnClosed (EventArgs e) {
			for (int i = 0; i < channelConfig.channels.Count; i++) {
				channelConfig.channels[i] = channelConfigUI[i].AsChannel();
			}
			MainWindow.instance.channelConfig = channelConfig;
			base.OnClosed(e);
		}

		private void channelDataGrid_LoadingRow (object sender, DataGridRowEventArgs e) {
			e.Row.Header = (e.Row.GetIndex() + 1).ToString();
			e.Row.DataContext = this;
		}

		private void channelDataGrid_MouseDown (object sender, MouseButtonEventArgs e) {
			channelDataGrid.SelectedCells.Clear();
		}

		#region Scaling
		// This section smoothly scales everything within the mainGrid
		public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
			typeof(double),
			typeof(ChannelConfigWindow),
			new UIPropertyMetadata(1.0,
				new PropertyChangedCallback(OnScaleValueChanged),
				new CoerceValueCallback(OnCoerceScaleValue)));

		private static object OnCoerceScaleValue (DependencyObject o, object value) {
			ChannelConfigWindow channelConfigWindow = o as ChannelConfigWindow;
			if (channelConfigWindow != null)
				return channelConfigWindow.OnCoerceScaleValue((double)value);
			else
				return value;
		}

		private static void OnScaleValueChanged (DependencyObject o, DependencyPropertyChangedEventArgs e) {
			ChannelConfigWindow channelConfigWindow = o as ChannelConfigWindow;
			if (channelConfigWindow != null)
				channelConfigWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
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

		private void channelConfigWindowGrid_SizeChanged (object sender, RoutedEventArgs e) {
			CalculateScale();
		}

		private void CalculateScale () {
			double xScale = ActualWidth / 800f; // must be set to initial window sizing for proper scaling!!!
			double yScale = ActualHeight / 450f; // must be set to initial window sizing for proper scaling!!!
			double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
			ScaleValue = (double)OnCoerceScaleValue(channelConfigWindow, value); // Update the actual scale for the main window
		}

		#endregion

	}
}
