using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;

namespace TouchFaders.Configuration {
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

        public async static Task Store (object data, string path = "") {
            path = process(path, data.GetType().Name);
            using StreamWriter writer = writeFile(path);
            if (writer == null) { return; }
            foreach (var item in data.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) {
                await writer.WriteAsync($"{item.Name}: ");
                if (item.PropertyType == typeof(string)) {
                    await writer.WriteLineAsync($"\"{item.GetValue(data)}\"");
                } else if (item.PropertyType == typeof(int)) {
                    await writer.WriteLineAsync(item.GetValue(data).ToString());
                } else if (item.PropertyType == typeof(bool)) {
                    await writer.WriteLineAsync(item.GetValue(data).ToString());
                } else if (typeof(IList).IsAssignableFrom(item.PropertyType)) {
                    await writer.WriteLineAsync($"List<{(item.GetValue(data) as IEnumerable).GetType().GetGenericArguments()[0].Name}>");
                    // iterate and store recursive
                    int i = 0;
                    foreach (var listItem in item.GetValue(data) as IEnumerable) {
                        await Store(listItem, Path.Combine(path, listItem.GetType().Name + i));
                        i++;
                    }
                } else if (item.PropertyType.IsClass) {
                    await writer.WriteLineAsync(item.GetValue(data).ToString());
                    // store recursive
                    await Store(item.GetValue(data), Path.Combine(path, item.Name));
                } else {
                    writer.WriteLine(item.GetValue(data));
                }
            }
        }

        public static object Load (object data, string path = "", Type typeOverride = null) {
            Type type = typeOverride ?? data.GetType();
            path = processPath(path, type.Name);
            using (StreamReader reader = readFile(path)) {
                foreach (var item in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) {
                    string[] line = reader.ReadLine().Split(':');
                    string name = line[0];
                    string value = line[1].TrimStart();
                    if (item.Name != name || !item.CanWrite) {
                        continue;
                    }
                    if (item.PropertyType == typeof(string)) {
                        string property = value.Trim('"');
                        item.SetValue(data, property);
                    } else if (item.PropertyType == typeof(int)) {
                        int property = int.Parse(value);
                        item.SetValue(data, property);
                    } else if (item.PropertyType == typeof(bool)) {
                        bool property = bool.Parse(value);
                        item.SetValue(data, property);
                    } else if (item.PropertyType.IsEnum) {
                        var property = Enum.Parse(item.PropertyType, value);
                        item.SetValue(data, property);
                    } else if (typeof(IList).IsAssignableFrom(item.PropertyType)) {
                        // iterate and load recursive
                        // https://stackoverflow.com/questions/46495831/how-can-i-cast-listobject-to-listfoo-when-foo-is-a-type-variable-and-i-dont
                        // this is cool and perhaps useful to know, but not actually necessary in this case
                        // https://stackoverflow.com/questions/4612618/how-to-get-the-count-property-using-reflection-for-generic-types
                        // I'm using IList because it actually works for what I need
                        var list = item.GetValue(data) as IList;

                        for (int i = 0; i < list.Count; i++) {
                            var collectionItem = list[i];
                            string subDirectory = Path.Combine(path, list.GetType().GetGenericArguments()[0].Name + i);
                            if (Directory.Exists(subDirectory)) {
                                object loadedValue = Load(collectionItem, subDirectory);
                                list[i] = loadedValue;
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
