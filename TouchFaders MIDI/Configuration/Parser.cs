using System.Collections;
using System.IO;

namespace TouchFaders_MIDI.Configuration {
    internal class Parser {

        const string basePath = "config";
        const string baseName = "data";
        const string extension = "txt";

        public Parser () {

        }

        private static string processPath (string path, string name) {
            path = path != "" ? path : name;
            if (!path.Contains(basePath)) {
                path = Path.Combine(basePath, path);
            }
            return path;
        }

        private static bool setupDirectory (string path) {
            if (Directory.Exists(path)) {
                return true;
            }
            try {
                DirectoryInfo info = Directory.CreateDirectory(path);
                if (info.Exists) {
                    return true;
                }
                return false;
            } catch { return false; }
        }

        private static bool setupFile (string path) {
            string fullPath = Path.Combine(path, $"{baseName}.{extension}");
            if (File.Exists(fullPath)) {
                return true;
            }
            try {
                FileStream stream = File.Create(fullPath);
                stream.Close();
                return true;
            } catch { return false; }
        }

        private static StreamWriter writeFile (string path) {
            string fullPath = Path.Combine(path, $"{baseName}.{extension}");
            if (File.Exists(fullPath)) {
                return new StreamWriter(fullPath, append: false);
            }
            return null;
        }

        private static StreamReader readFile (string path) {
            string fullPath = Path.Combine(path, $"{baseName}.{extension}");
            if (File.Exists(fullPath)) {
                return new StreamReader(fullPath);
            }
            return null;
        }

        private static string process (string path, string name) {
            path = processPath(path, name);
            setupDirectory(path);
            setupFile(path);
            return path;
        }

        public static void Store (object data, string path = "") {
            path = process(path, data.GetType().Name);
            using (StreamWriter writer = writeFile(path)) {
                if (writer == null) { return; }
                foreach (var item in data.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) {
                    writer.Write($"{item.Name}: ");
                    if (item.PropertyType == typeof(string)) {
                        writer.WriteLine($"\"{item.GetValue(data)}\"");
                    } else if (typeof(IList).IsAssignableFrom(item.PropertyType)) {
                        writer.WriteLine($"List<{(item.GetValue(data) as IEnumerable).GetType().GetGenericArguments()[0].Name}>");
                        // iterate and store recursive
                        int i = 0;
                        foreach (var listItem in item.GetValue(data) as IEnumerable) {
                            Store(listItem, Path.Combine(path, listItem.GetType().Name + i));
                            i++;
                        }
                    } else if (item.PropertyType.IsClass) {
                        writer.WriteLine(item.GetValue(data).ToString());
                        // store recursive
                        Store(item.GetValue(data), Path.Combine(path, item.Name));
                    } else {
                        writer.WriteLine(item.GetValue(data));
                    }
                }
            }
        }

        public static object Load (object data, string path = "") {
            path = processPath(path, data.GetType().Name);
            using (StreamReader reader = readFile(path)) {
                //Console.WriteLine($"Reading: {path}");
                foreach (var item in data.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) {
                    string[] line = reader.ReadLine().Split(':');
                    string name = line[0];
                    string value = line[1].TrimStart();
                    if (item.Name != name || !item.CanWrite) {
                        continue;
                    }
                    if (item.PropertyType == typeof(string)) {
                        string property = value;
                        item.SetValue(data, property);
                    } else if (item.PropertyType == typeof(int)) {
                        int property = int.Parse(value);
                        item.SetValue(data, property);
                    } else if (typeof(IList).IsAssignableFrom(item.PropertyType)) {
                        // iterate and load recursive
                        string itemName = (item.GetValue(data) as IEnumerable).GetType().GetGenericArguments()[0].Name;
                        int i = 0;
                        while (true) {
                            string subDirectory = Path.Combine(path, itemName + i);
                            if (Directory.Exists(subDirectory)) {
                                System.Console.WriteLine($"Loading: {subDirectory}");
                                object listItem = Load(item.GetValue(data), subDirectory);
                                item.SetValue(data, listItem);
                                i++;
                            } else {
                                break;
                            }
                        }
                    } else if (item.PropertyType.IsClass) {
                        // load recursive
                        object subObject = Load(item.GetValue(data), Path.Combine(path, item.Name));
                        item.SetValue(data, subObject);
                    }
                }
            }
            return data;
        }

    }
}
