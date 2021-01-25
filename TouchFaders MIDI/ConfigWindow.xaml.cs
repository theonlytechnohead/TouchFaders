using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for ConfigWindow.xaml
	/// </summary>
	public partial class ConfigWindow : Window {

		public AppConfiguration.appconfig config;

		public ConfigWindow () {
			InitializeComponent();
			this.Loaded += ConfigWindow_Loaded;
		}

		private void ConfigWindow_Loaded (object sender, RoutedEventArgs e) {
			if (config == null) return;
			channelSlider.Value = config.NUM_CHANNELS;
			mixSlider.Value = config.NUM_MIXES;
		}

		protected override void OnClosed (EventArgs e) {
			MainWindow.instance.config = config;
			base.OnClosed(e);
		}

		private void channelSlider_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
			Slider channelSlider = sender as Slider;
			if (config == null) return;
			config.NUM_CHANNELS = (int)channelSlider.Value;
		}

		private void editChannelNamesButton_Click (object sender, RoutedEventArgs e) {
			Process fileopener = new Process();
			fileopener.StartInfo.FileName = "explorer";
			fileopener.StartInfo.Arguments = "\"config\\channelNames.txt\"";
			fileopener.Start();
		}

		private void mixSlider_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
			Slider mixSlider = sender as Slider;
			if (config == null) return;
			config.NUM_MIXES = (int)mixSlider.Value;
		}

		private void editMixNamesButton_Click (object sender, RoutedEventArgs e) {
			Process fileopener = new Process();
			fileopener.StartInfo.FileName = "explorer";
			fileopener.StartInfo.Arguments = "\"config\\mixNames.txt\"";
			fileopener.Start();
		}

	}
}
