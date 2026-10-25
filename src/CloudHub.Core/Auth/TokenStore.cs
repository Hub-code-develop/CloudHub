using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudHub.Core.Auth;

/// <summary>令牌落盘。文件写在用户配置目录，权限 0600。</summary>
public sealed class TokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;

    public TokenStore(string? path = null) => _path = path ?? CloudHubPaths.TokenCacheFile;

    public string Path => _path;

    public async Task<TokenSet?> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<TokenSet>(stream, JsonOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // 缓存损坏就当没登录过，不要因为一个坏文件让应用起不来。
            return null;
        }
    }

    public async Task SaveAsync(TokenSet tokens, CancellationToken ct = default)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";

        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, tokens, JsonOptions, ct).ConfigureAwait(false);
        }

        File.Move(temp, _path, overwrite: true);
        CloudHubPaths.RestrictToCurrentUser(_path);
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (IOException)
        {
        }
    }
}
