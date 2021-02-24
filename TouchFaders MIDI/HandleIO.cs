using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TouchFaders_MIDI {
	class HandleIO {

		public class FileData {
			public ObservableCollection<oscDevice> oscDevices = new ObservableCollection<oscDevice>();
			public SendsToMix sendsToMix = new SendsToMix();

			//public ChannelNames channelNames = new ChannelNames();
			public ChannelConfig channelConfig = new ChannelConfig();

			public ChannelFaders channelFaders = new ChannelFaders();

			public MixNames mixNames = new MixNames();
			public MixFaders mixFaders = new MixFaders();
		}

		public static FileData LoadAll () {
			FileData data = new FileData();
			try {
				JsonSerializerOptions jsonDeserializerOptions = new JsonSerializerOptions { IgnoreNullValues = true, };

				/*
				string oscDevicesFile = File.ReadAllText("config/oscDevices.txt");
				if (MainWindow.instance.config.oscDevices_version >= 1) {
					data.oscDevices = JsonSerializer.Deserialize<ObservableCollection<oscDevice>>(oscDevicesFile, jsonDeserializerOptions);
				}
				*/

				string sendsToMixFile = File.ReadAllText("config/sendsToMix.txt");
				if (MainWindow.instance.config.sendsToMix_version >= 1) {
					data.sendsToMix.sendLevel = JsonSerializer.Deserialize<List<List<int>>>(sendsToMixFile, jsonDeserializerOptions);
				}

				if (MainWindow.instance.config.channelConfig_version >= 2) {
					string channelConfigFile = File.ReadAllText("config/channelConfig.txt");
					data.channelConfig = JsonSerializer.Deserialize<ChannelConfig>(channelConfigFile, jsonDeserializerOptions);
				}
				if (MainWindow.instance.config.channelNames_version == 1) {
					string channelNamesFile = File.ReadAllText("config/channelNames.txt");
					string channelFadersFile = File.ReadAllText("config/channelFaders.txt");

					ChannelNames channelNames = new ChannelNames();
					channelNames.names = JsonSerializer.Deserialize<List<string>>(channelNamesFile, jsonDeserializerOptions);

					ChannelFaders channelFaders = new ChannelFaders();
					channelFaders.faders = JsonSerializer.Deserialize<List<int>>(channelFadersFile, jsonDeserializerOptions);

					ChannelConfig channelConfig = new ChannelConfig();
					for (int i = 0; i < channelNames.names.Count && i < channelFaders.faders.Count; i++) {
						ChannelConfig.Channel channel = new ChannelConfig.Channel() {
							name = channelNames.names[i],
							level = channelFaders.faders[i],
							linkGroup = ChannelConfig.ChannelGroupChars[0]
						};
						channelConfig.channels.Add(channel);
					}

					data.channelConfig = channelConfig;
				}
				if (MainWindow.instance.config.channelConfig_version == 1) {
					MainWindow.instance.config.channelConfig_version = AppConfiguration.appconfig.defaultValues().channelConfig_version;
					MainWindow.instance.config.channelNames_version = null;
					MainWindow.instance.config.channelFaders_version = null;
					File.Delete("config/channelNames.txt");
					File.Delete("config/channelFaders.txt");
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
				Dispatcher.CurrentDispatcher.Invoke(() => System.Windows.MessageBox.Show(ex.StackTrace, ex.Message));
			}
			return data;
		}

		public static async Task SaveAll (FileData data) {
			JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true, };
			_ = Directory.CreateDirectory("config");
			/*
			if (data.oscDevices != null) {
				using (FileStream fs = File.Create("config/oscDevices.txt")) {
					await JsonSerializer.SerializeAsync(fs, data.oscDevices, serializerOptions);
				}
			}
			*/
			if (data.sendsToMix != null) {
				using (FileStream fs = File.Create("config/sendsToMix.txt")) {
					await JsonSerializer.SerializeAsync(fs, data.sendsToMix.sendLevel, serializerOptions);
				}
			}
			if (data.channelConfig != null) {
				using (FileStream fs = File.Create("config/channelConfig.txt")) {
					await JsonSerializer.SerializeAsync(fs, data.channelConfig, serializerOptions);
				}
			}
		}
	}
}