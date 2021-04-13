using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

        public bool TryGetCacheData(out ICollection<string> data, out string active) {

            JObject json = new();
            data = new List<string>();

            using (StreamReader reader = new(File.OpenRead(Path.Combine(m_cacheLocation, m_cacheName)))) {
                json = JObject.Parse(reader.ReadToEnd());
            }

            active = json["dblist"].FirstOrDefault(active => active == json["active"])?.ToString();

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
