using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TouchFaders_MIDI.Configuration {
    /// <summary>
    /// Interaction logic for MixConfigWindow.xaml
    /// </summary>
    public partial class MixConfigWindow : Window {
        
		public ObservableCollection<MixConfigUI> mixConfigUI;

		public class MixConfigUI {

			public class NameArgs : EventArgs {
				public int mix;
				public string name;
			}

			public int mix;
			private string name;
			public string MixName { get => name; set { name = value; PropertyChanged?.Invoke(this, new NameArgs() { mix = mix, name = MixName }); } }
			private string mixColour;
			public Dictionary<string, SolidColorBrush> Colours {
				get {
					return DataStructures.bgColourMap;
				}
			}
			public string MixColour {
				get {
					return mixColour;
				}
				set {
					mixColour = value;
					PropertyChanged?.Invoke(this, new EventArgs());
				}
			}

			public EventHandler PropertyChanged;

			public MixConfigUI(Data.Mix mix) {
				this.mix = mix.mix;
				MixName = mix.name;
				MixColour = DataStructures.bgColourNames[mix.bgColourId];
            }
        }

        public MixConfigWindow () {
            InitializeComponent();
			mixConfigUI = new ObservableCollection<MixConfigUI>();
			Foreground = MainWindow.instance.Foreground;
			Background = MainWindow.instance.Background;
			mixDataGrid.Foreground = MainWindow.instance.Foreground;
			mixDataGrid.Background = MainWindow.instance.Background;
		}

		private void mixConfigWindow_Loaded (object sender, RoutedEventArgs e) {
			mixDataGrid.DataContext = this;
			mixDataGrid.ItemsSource = mixConfigUI;
            mixConfigUI.CollectionChanged += MixConfigUI_CollectionChanged;
			foreach (var mix in MainWindow.instance.data.mixes) {
				mixConfigUI.Add(new MixConfigUI(mix));
            }
		}

        private void MixConfigUI_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e) {
			if (e.Action == NotifyCollectionChangedAction.Remove) {
				foreach (MixConfigUI item in e.OldItems) {
					//Removed items
					item.PropertyChanged -= MixConfigUIPropertyChanged;
				}
			} else if (e.Action == NotifyCollectionChangedAction.Add) {
				foreach (MixConfigUI item in e.NewItems) {
					//Added items
					item.PropertyChanged += MixConfigUIPropertyChanged;
				}
			}
		}

        private void MixConfigUIPropertyChanged (object sender, EventArgs e) {
            if (e is MixConfigUI.NameArgs) {
				MixConfigUI.NameArgs args = e as MixConfigUI.NameArgs;
				MainWindow.instance.data.mixes[args.mix - 1].name = args.name;
            }
        }

        protected override void OnClosed (EventArgs e) {
			foreach (var mixConfig in mixConfigUI) {
				MainWindow.instance.data.mixes[mixConfig.mix - 1].name = mixConfig.MixName;
				MainWindow.instance.data.mixes[mixConfig.mix - 1].bgColourId = DataStructures.bgColourNames.IndexOf(mixConfig.MixColour);
            }
			base.OnClosed(e);
        }

        private void mixDataGrid_LoadingRow (object sender, DataGridRowEventArgs e) {
			e.Row.Header = (e.Row.GetIndex() + 1).ToString();
			e.Row.DataContext = this;
		}

		private void mixDataGrid_MouseDown (object sender, MouseButtonEventArgs e) {
			mixDataGrid.SelectedCells.Clear();
		}

		#region Scaling
		// This section smoothly scales everything within the mainGrid
		public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
			typeof(double),
			typeof(MixConfigWindow),
			new UIPropertyMetadata(1.0,
				new PropertyChangedCallback(OnScaleValueChanged),
				new CoerceValueCallback(OnCoerceScaleValue)));

		private static object OnCoerceScaleValue (DependencyObject o, object value) {
			MixConfigWindow mixConfigWindow = o as MixConfigWindow;
			if (mixConfigWindow != null)
				return mixConfigWindow.OnCoerceScaleValue((double)value);
			else
				return value;
		}

		private static void OnScaleValueChanged (DependencyObject o, DependencyPropertyChangedEventArgs e) {
			MixConfigWindow mixConfigWindow = o as MixConfigWindow;
			if (mixConfigWindow != null)
				mixConfigWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
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

		private void mixConfigWindowGrid_SizeChanged (object sender, SizeChangedEventArgs e) {
			CalculateScale();
		}

		private void CalculateScale () {
			double xScale = ActualWidth / 800f; // must be set to initial window sizing for proper scaling!!!
			double yScale = ActualHeight / 450f; // must be set to initial window sizing for proper scaling!!!
			double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
			ScaleValue = (double)OnCoerceScaleValue(mixConfigWindow, value); // Update the actual scale for the main window
		}


        #endregion

    }
}
