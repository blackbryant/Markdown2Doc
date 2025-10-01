using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.Json;

namespace Markdown2Doc
{
    /// <summary>
    /// EnvUtils using Properties.Settings as primary store.
    /// - If a strongly-typed setting exists (Properties.Settings.Default has a property named key), uses it.
    /// - Else if there's a user-scoped "DynamicSettings" (string) it stores a JSON dictionary {key: value}.
    /// - Else falls back to environment variables and resx lookup.
    /// 
    /// Requirements:
    /// - Recommended: add a User-scoped string setting named "DynamicSettings" in Settings.settings.
    /// - Optionally add strongly-typed settings (e.g., downloadUrl) for keys you know ahead of time.
    /// </summary>
    public static class EnvUtils
    {
        public  const string ENV_OUTPUT = "output"; 


        private static readonly object _sync = new object();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = false };

        /// <summary>
        /// 讀取設定（優先：strongly-typed Setting -> DynamicSettings JSON -> Environment -> resx）
        /// </summary>
        public static string? GetString(string key, Assembly? assembly = null, string? baseName = null, CultureInfo? culture = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            // 1) 如果 Settings 有 strongly-typed property，直接取
            try
            {
                var settingsType = Properties.Settings.Default.GetType();
                var prop = settingsType.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    var raw = Properties.Settings.Default[key];
                    if (raw != null) return raw.ToString();
                }
            }
            catch
            {
                // ignore and continue
            }

            // 2) 嘗試從 DynamicSettings (JSON string) 讀取
            try
            {
                var dyn = ReadDynamicSettingsDictionary();
                if (dyn != null && dyn.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                    return v;
            }
            catch
            {
                // ignore
            }

            // 3) 再去看 environment (process/user/machine)
            try
            {
                var v = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process)
                        ?? Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User)
                        ?? Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrEmpty(v)) return v;
            }
            catch { }

            // 4) 最後 fallback 回原本的 resx 機制（保留相容）
            return ReadFromResxFallback(key, assembly, baseName, culture);
        }

        /// <summary>
        /// 設定值（優先：若存在 strongly-typed setting 則寫入該 setting；否則寫入 DynamicSettings JSON（若存在）；否則寫入 Environment (User)）。
        /// 備註：若你希望所有鍵都寫入 DynamicSettings，請先在 Settings.settings 建立 User-scoped string "DynamicSettings"。
        /// </summary>
        public static void SetString(string key, string? value, EnvironmentVariableTarget envTarget = EnvironmentVariableTarget.User)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

            // 1) 如果 Settings 有 strongly-typed property，直接寫並 Save()
            try
            {
                var settingsType = Properties.Settings.Default.GetType();
                var prop = settingsType.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    // 強型別 property 存取
                    Properties.Settings.Default[key] = value ?? string.Empty;
                    Properties.Settings.Default.Save();
                    return;
                }
            }
            catch
            {
                // ignore and continue
            }

            // 2) 嘗試把它寫入 DynamicSettings JSON（如果該設定存在）
            try
            {
                if (Properties.Settings.Default.Properties.Cast<System.Configuration.SettingsProperty>().Any(p => p.Name == "DynamicSettings"))
                {
                    lock (_sync)
                    {
                        var dict = ReadDynamicSettingsDictionary() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (value == null)
                        {
                            if (dict.Remove(key))
                            {
                                WriteDynamicSettingsDictionary(dict);
                            }
                        }
                        else
                        {
                            dict[key] = value;
                            WriteDynamicSettingsDictionary(dict);
                        }
                    }
                    return;
                }
            }
            catch
            {
                // ignore and fallback to env var
            }

            // 3) Fallback - 存到 Environment variable (User scope)
            try
            {
                Environment.SetEnvironmentVariable(key, value, envTarget);
                return;
            }
            catch
            {
                // ignore
            }

            // 4) 當所有方式都失敗時，拋例外或 silently return；這邊選擇拋例外讓呼叫方知道
            throw new InvalidOperationException("Cannot persist setting. Ensure you have either a strongly-typed setting or a 'DynamicSettings' user-scoped string setting, or proper environment variable permissions.");
        }

        /// <summary>
        /// 刪除指定 key（會嘗試從 strong setting / dynamic json / process env 順序刪除）。
        /// </summary>
        public static void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            try
            {
                var settingsType = Properties.Settings.Default.GetType();
                var prop = settingsType.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    // 若是 strongly-typed setting，RESET 為 default
                    Properties.Settings.Default[key] = null;
                    Properties.Settings.Default.Save();
                    return;
                }
            }
            catch { }

            try
            {
                if (Properties.Settings.Default.Properties.Cast<System.Configuration.SettingsProperty>().Any(p => p.Name == "DynamicSettings"))
                {
                    lock (_sync)
                    {
                        var dict = ReadDynamicSettingsDictionary() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (dict.Remove(key))
                            WriteDynamicSettingsDictionary(dict);
                    }
                    return;
                }
            }
            catch { }

            try { Environment.SetEnvironmentVariable(key, null, EnvironmentVariableTarget.Process); } catch { }
        }

        /// <summary>
        /// 列出 DynamicSettings 的 keys（若沒有 DynamicSettings 則回傳空陣列）。
        /// </summary>
        public static string[] ListDynamicKeys()
        {
            try
            {
                var dict = ReadDynamicSettingsDictionary();
                if (dict != null) return dict.Keys.ToArray();
            }
            catch { }
            return Array.Empty<string>();
        }

        #region Helpers for DynamicSettings JSON & Resx fallback

        private static Dictionary<string, string>? ReadDynamicSettingsDictionary()
        {
            // 如果 DynamicSettings setting 存在，讀出 JSON 反序列化為 Dictionary
            try
            {
                if (!Properties.Settings.Default.Properties.Cast<System.Configuration.SettingsProperty>().Any(p => p.Name == "DynamicSettings"))
                    return null;

                var raw = Properties.Settings.Default["DynamicSettings"] as string;
                if (string.IsNullOrWhiteSpace(raw)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(raw, _jsonOptions);
                return dict ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void WriteDynamicSettingsDictionary(Dictionary<string, string> dict)
        {
            lock (_sync)
            {
                var json = JsonSerializer.Serialize(dict, _jsonOptions);
                Properties.Settings.Default["DynamicSettings"] = json;
                Properties.Settings.Default.Save();
            }
        }

        private static string? ReadFromResxFallback(string key, Assembly? assembly, string? baseName, CultureInfo? culture)
        {
            // 使用你原本的 resx 搜尋邏輯
            try
            {
                culture ??= CultureInfo.CurrentUICulture;
                assembly ??= Assembly.GetCallingAssembly() ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                if (!string.IsNullOrWhiteSpace(baseName))
                {
                    try
                    {
                        var rm = new ResourceManager(baseName, assembly);
                        var val = rm.GetString(key, culture);
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                    catch { }
                }

                var candidates = new[]
                {
                    $"{assembly.GetName().Name}.Properties.Resources",
                    "Properties.Resources",
                    $"{assembly.GetName().Name}.Resources",
                    "Resources"
                };

                foreach (var candidate in candidates.Distinct())
                {
                    try
                    {
                        var rm = new ResourceManager(candidate, assembly);
                        var val = rm.GetString(key, culture);
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                    catch { }
                }

                var resourceType = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name.EndsWith("Resources", StringComparison.OrdinalIgnoreCase)
                                         && t.GetProperty("ResourceManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) != null);

                if (resourceType != null)
                {
                    var prop = resourceType.GetProperty("ResourceManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (prop != null)
                    {
                        var rm = prop.GetValue(null) as ResourceManager;
                        var val = rm?.GetString(key, culture);
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                }
            }
            catch { }

            return null;
        }

        #endregion
    }
}
