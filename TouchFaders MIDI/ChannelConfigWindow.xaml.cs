using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
	/// Interaction logic for ChannelConfigWindow.xaml
	/// </summary>
	public partial class ChannelConfigWindow : Window {

		public AppConfiguration.appconfig config;

		public ObservableCollection<ChannelConfigUI> channelConfig;

		public class ChannelConfigUI {
			public string ChannelName { get; set; }
			public char ChannelGroup { get; set; }
			public ObservableCollection<char> ChannelGroups { get; set; }

			public ChannelConfigUI (string name, char group) {
				ChannelName = name;
				ChannelGroup = group;
				ChannelGroups = ChannelConfig.chGroupsChars;
			}
		}

		public ChannelConfigWindow () {
			InitializeComponent();
			channelConfig = new ObservableCollection<ChannelConfigUI>();
		}

		private void channelConfigWindow_Loaded (object sender, RoutedEventArgs e) {
			channelDataGrid.ItemsSource = channelConfig;
			for (int i = 1; i <= 64; i++) {
				channelConfig.Add(new ChannelConfigUI($"Ch {i}", ChannelConfig.chGroupsChars[1]));
			}
		}
	}
}
