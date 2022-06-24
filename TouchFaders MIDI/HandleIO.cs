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

		public class FileData {
			public Data data;
        }

		public static FileData LoadAll () {
			FileData data = new FileData();
			try {
				if (File.Exists("config/data.json")) {
					string dataFile = File.ReadAllText("config/data.json");
					var values = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(dataFile);
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
			JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true };
			_ = Directory.CreateDirectory("config");
			if (data.data != null) {
                using (FileStream fs = File.Create("config/data.json")) {
                await JsonSerializer.SerializeAsync(fs, data.data, serializerOptions);
                }
            }
        }
	}
}