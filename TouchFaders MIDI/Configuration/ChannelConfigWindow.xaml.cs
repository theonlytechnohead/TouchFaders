using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TouchFaders_MIDI {
    /// <summary>
    /// Interaction logic for ChannelConfigWindow.xaml
    /// </summary>
    public partial class ChannelConfigWindow : Window {

		public ObservableCollection<ChannelConfigUI> channelConfigUI;

		public class ChannelConfigUI {

			public class NameArgs : EventArgs {
				public int channel;
				public string name;
			}

			public class PatchArgs : EventArgs {
				public int channel;
				public int patch;
			}

			public class ColourArgs : EventArgs {
				public int channel;
				public string colourName;
            }

			public int channel;
			private string name;
            public string ChannelName { get => name; set { name = value; PropertyChanged?.Invoke(this, new NameArgs() { channel = channel, name = ChannelName }); } }
			private string channelColour;
			public Dictionary<string, SolidColorBrush> Colours {
				get {
					return DataStructures.bgColourMap;
				}
            }
			public string ChannelColour {
				get { return channelColour; }
				set {
					channelColour = value;
					PropertyChanged?.Invoke(this, new ColourArgs() { channel = channel, colourName = ChannelColour});
				}
			}
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
			private int patch;
			public int ChannelPatch { get => patch; set { patch = value; PropertyChanged?.Invoke(this, new PatchArgs() { channel = channel, patch = ChannelPatch }); } }
			public List<int> ChannelPatches { get; set; }

			public EventHandler PropertyChanged;

			public ChannelConfigUI (Data.Channel channel) {
				this.channel = channel.channel;
				ChannelName = channel.name;
				ChannelColour = DataStructures.bgColourNames[channel.bgColourId];
				ChannelGroup = channel.linkGroup;
				ChannelGroups = DataStructures.ChannelGroupChars;
				ChannelPatch = channel.patch;
				ChannelPatches = (from port in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select port).ToList();
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
			foreach (var channel in MainWindow.instance.data.channels) {
				channelConfigUI.Add(new ChannelConfigUI(channel));
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
			ChannelConfigUI channelConfig = sender as ChannelConfigUI;
			if (e is ChannelConfigUI.NameArgs) {
				ChannelConfigUI.NameArgs args = e as ChannelConfigUI.NameArgs;
				MainWindow.instance.data.channels[args.channel - 1].name = args.name;
			} else if (e is ChannelConfigUI.PatchArgs) {
				ChannelConfigUI.PatchArgs args = e as ChannelConfigUI.PatchArgs;
				MainWindow.instance.data.channels[args.channel - 1].patch = args.patch;
			} else if (e is ChannelConfigUI.ColourArgs) {
				ChannelConfigUI.ColourArgs args = e as ChannelConfigUI.ColourArgs;
				MainWindow.instance.data.channels[args.channel - 1].bgColourId = DataStructures.bgColourNames.IndexOf(args.colourName);
			} else {
				int index = channelConfigUI.IndexOf(channelConfig);
				char group = channelConfig.ChannelGroup;
				//Console.WriteLine($"{channelConfigUI.IndexOf(configUI)}:{configUI.ChannelGroup}");
				MainWindow.instance.SendChannelLinkGroup(index, group);
			}
		}

		protected override void OnClosed (EventArgs e) {
			foreach (var channelConfig in channelConfigUI) {
				MainWindow.instance.data.channels[channelConfig.channel - 1].name = channelConfig.ChannelName;
				MainWindow.instance.data.channels[channelConfig.channel - 1].bgColourId = DataStructures.bgColourNames.IndexOf(channelConfig.ChannelColour);
				MainWindow.instance.data.channels[channelConfig.channel - 1].linkGroup = channelConfig.ChannelGroup;
				MainWindow.instance.data.channels[channelConfig.channel - 1].patch = channelConfig.ChannelPatch;
			}
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
