using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace FuturaPlanConfigWarden {
    internal class DbCache {
        private string m_cacheLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FuturaPlan");
        private string m_cacheName = "dbcache.json";

        public DbCache() {
            if (!Directory.Exists(m_cacheLocation)) {
                Directory.CreateDirectory(m_cacheLocation);
            }
        }

        public void Update(ICollection<string> databaseList, string active) {
            JObject json = new() {
                ["active"] = JToken.FromObject(active),
                ["dblist"] = JToken.FromObject(databaseList)
            };

            using (StreamWriter writer = new(File.Create(Path.Combine(m_cacheLocation, m_cacheName)))) {
                writer.Write(json.ToString());
            }
        }

        public bool TryGetCacheData(out ObservableCollection<string> data, out string active) {

            string file = Path.Combine(m_cacheLocation, m_cacheName);

            JObject json = new();
            data = new ObservableCollection<string>();
            active = "";

            if (!File.Exists(file)) {
                return false;
            }

            using (StreamReader reader = new(File.OpenRead(file))) {
                json = JObject.Parse(reader.ReadToEnd());
            }

            string activedb = json["active"].ToString();

            active = json["dblist"].FirstOrDefault(active => active.Value<string>() == activedb)?.ToString();

            if (string.IsNullOrEmpty(active)) {
                return false;
            }

            foreach (string db in json["dblist"] as JArray) {
                data.Add(db);
            }

            return true;
        }
    }
}
