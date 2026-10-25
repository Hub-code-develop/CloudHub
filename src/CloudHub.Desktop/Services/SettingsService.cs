using System.Text.Json;

namespace CloudHub.Desktop.Services;

/// <summary>设置的读写与变更广播。</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsService(string? path = null)
    {
        _path = path ?? CloudHub.Core.CloudHubPaths.SettingsFile;
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? Changed;

    public string Path => _path;

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.ClientId))
                    {
                        loaded.ClientId = CloudHub.Core.Auth.WellKnownClients.AzureCliClientId;
                    }

                    if (string.IsNullOrWhiteSpace(loaded.TenantId))
                    {
                        loaded.TenantId = "common";
                    }

                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // 配置坏了就用默认值，不要因此起不来。
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
        }
        catch (IOException)
        {
        }

        Changed?.Invoke(this, Current);
    }

    /// <summary>用一次编辑替换整个设置对象并落盘。</summary>
    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        Save();
    }
}
