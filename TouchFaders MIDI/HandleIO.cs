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
				string dataFile = File.ReadAllText("config/data.json");
				var values = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(dataFile);
			} catch (FileNotFoundException ex) {
				_ = SaveAll(data);
			} catch (Exception ex) {
				Dispatcher.CurrentDispatcher.Invoke(() => System.Windows.MessageBox.Show(ex.StackTrace, ex.Message));
			}
			return data;
		}

		public static async Task SaveAll (Data data) {
			JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true, IgnoreNullValues = true };
			_ = Directory.CreateDirectory("config");
			if (data != null) {
                using (FileStream fs = File.Create("config/data.json")) {
                await JsonSerializer.SerializeAsync(fs, data, serializerOptions);
                }
            }
        }
	}
}