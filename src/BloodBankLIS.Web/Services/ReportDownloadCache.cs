using Microsoft.Extensions.Caching.Memory;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// Holds a just-exported report long enough for the browser to download it
/// without pushing the file through the Blazor circuit.
/// </summary>
public sealed class ReportDownloadCache
{
    private readonly IMemoryCache _cache;

    public ReportDownloadCache(IMemoryCache cache) => _cache = cache;

    public string Put(string fileName, string contentType, byte[] bytes)
    {
        var token = Guid.NewGuid().ToString("N");
        _cache.Set(token, new ReportDownload(fileName, contentType, bytes), TimeSpan.FromMinutes(2));
        return token;
    }

    public ReportDownload? Take(string token)
    {
        if (!_cache.TryGetValue(token, out ReportDownload? file) || file is null)
            return null;

        _cache.Remove(token);
        return file;
    }
}

public sealed record ReportDownload(string FileName, string ContentType, byte[] Bytes);
