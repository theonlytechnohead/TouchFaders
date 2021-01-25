using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TouchFaders_MIDI {
	class HandleIO {

		public class FileData {
			public ObservableCollection<oscDevice> oscDevices = new ObservableCollection<oscDevice>();
			public SendsToMix sendsToMix = new SendsToMix();
			public ChannelNames channelNames = new ChannelNames();
			public ChannelFaders channelFaders = new ChannelFaders();
			public MixNames mixNames = new MixNames();
			public MixFaders mixFaders = new MixFaders();
		}

		public static FileData LoadAll () {
			FileData data = new FileData();
			try {
				JsonSerializerOptions jsonDeserializerOptions = new JsonSerializerOptions { IgnoreNullValues = true, };

				string oscDevicesFile = File.ReadAllText("config/oscDevices.txt");
				if (MainWindow.instance.config.oscDevices_version >= 1) {
					data.oscDevices = JsonSerializer.Deserialize<ObservableCollection<oscDevice>>(oscDevicesFile, jsonDeserializerOptions);
				}

				string sendsToMixFile = File.ReadAllText("config/sendsToMix.txt");
				if (MainWindow.instance.config.sendsToMix_version >= 1) {
					data.sendsToMix.sendLevel = JsonSerializer.Deserialize<List<List<int>>>(sendsToMixFile, jsonDeserializerOptions);
				}

				string channelNamesFile = File.ReadAllText("config/channelNames.txt");
				if (MainWindow.instance.config.channelNames_version >= 1) {
					data.channelNames.names = JsonSerializer.Deserialize<List<string>>(channelNamesFile, jsonDeserializerOptions);
				}

				string channelFadersFile = File.ReadAllText("config/channelFaders.txt");
				if (MainWindow.instance.config.channelFaders_version >= 1) {
					data.channelFaders.faders = JsonSerializer.Deserialize<List<int>>(channelFadersFile, jsonDeserializerOptions);
				}

				if (MainWindow.instance.config.mixNames_version >= 1) {
					string mixNamesFile = File.ReadAllText("config/mixNames.txt");
					data.mixNames.names = JsonSerializer.Deserialize<List<string>>(mixNamesFile, jsonDeserializerOptions);
				}

				if (MainWindow.instance.config.mixFaders_version >= 1) {
					string mixFadersFile = File.ReadAllText("config/mixFaders.txt");
					data.mixFaders.faders = JsonSerializer.Deserialize<List<int>>(mixFadersFile, jsonDeserializerOptions);
				}
			} catch (FileNotFoundException ex) {
				//await SaveAll(data);
				Dispatcher.CurrentDispatcher.Invoke(() => System.Windows.MessageBox.Show(ex.Message));
			} catch (Exception ex) {
				Dispatcher.CurrentDispatcher.Invoke(() => System.Windows.MessageBox.Show(ex.Message));
			}
			return data;
		}

		public static async Task SaveAll (FileData data) {
			_ = Directory.CreateDirectory("config");
			if (data.oscDevices != null) {
				using (FileStream fs = File.Create("config/oscDevices.txt")) {
					await JsonSerializer.SerializeAsync(fs, data.oscDevices, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, });
				}
			}
			if (data.sendsToMix != null) {
				using (FileStream fs = File.Create("config/sendsToMix.txt")) {
					await JsonSerializer.SerializeAsync(fs, data.sendsToMix.sendLevel, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, });
				}
			}
			if (data.channelNames != null) {
				using (FileStream fs = File.Create("config/channelNames.txt")) {
					await JsonSerializer.SerializeAsync(fs, data.channelNames.names, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true });
				}
			}
			if (data.channelFaders != null) {
				using (FileStream fs = File.Create("config/channelFaders.txt")) {
					await JsonSerializer.SerializeAsync(fs, data.channelFaders.faders, new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true });
				}
			}
		}
	}
}