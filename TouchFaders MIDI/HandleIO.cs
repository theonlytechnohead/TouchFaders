using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TouchFaders_MIDI {
	class HandleIO {

		public static Data LoadAll () {
			Data data = new Data ();
			try {
				string dataFile = File.ReadAllText($"{AppConfiguration.CONFIG_DIR}/{AppConfiguration.DATA_FILE}.json");
				var values = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(dataFile);
			} catch (FileNotFoundException) {
				_ = SaveAll(data);
			} catch (Exception ex) {
				Dispatcher.CurrentDispatcher.Invoke(() => System.Windows.MessageBox.Show(ex.StackTrace, ex.Message));
			}
			return data;
		}

		public static async Task SaveAll (Data data) {
			JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true };
			_ = Directory.CreateDirectory(AppConfiguration.CONFIG_DIR);
			if (data != null) {
                using FileStream fs = File.Create($"{AppConfiguration.CONFIG_DIR}/{AppConfiguration.DATA_FILE}.json");
                await JsonSerializer.SerializeAsync(fs, data, serializerOptions);
            }
        }
	}
}