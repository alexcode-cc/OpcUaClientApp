using System;
using System.IO;
using System.Text.Json;

namespace OpcUaClientApp
{
    public class AppSettings
    {
        private static readonly string SettingsFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpcUaClientApp");
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

        // 端點連線設定
        public string? LastEndpointUrl { get; set; }
        public string? LastSecurityPolicy { get; set; }
        public string? LastMessageSecurityMode { get; set; }

        // 最後查看的節點資訊
        public string? LastViewedNodeId { get; set; }
        public string? LastViewedNodeDisplayName { get; set; }
        public string? LastViewedNodeClass { get; set; }
        public string? LastViewedNodePath { get; set; }  // 節點路徑（用於樹狀結構展開）
        public bool LastViewedNodeIsSubscribed { get; set; }
        public int? LastViewedSubscriptionInterval { get; set; }

        /// <summary>
        /// 載入應用程式設定
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                // 如果設定檔存在，載入設定
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                // 如果載入失敗，返回預設設定
            }

            return new AppSettings();
        }

        /// <summary>
        /// 儲存應用程式設定
        /// </summary>
        public void Save()
        {
            try
            {
                // 確保設定資料夾存在
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                // 序列化並儲存設定
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception)
            {
                // 忽略儲存錯誤
            }
        }
    }
}
