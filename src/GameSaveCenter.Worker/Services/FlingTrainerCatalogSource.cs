using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

public interface ITrainerCatalogSource
{
    Task<TrainerCatalogSyncResultDto> SyncCatalogAsync(CancellationToken token);
    Task<List<TrainerCatalogItemDto>> SearchAsync(string query, int limit, CancellationToken token);
    Task<List<TrainerReleaseDto>> GetReleasesAsync(string catalogId, CancellationToken token);
    Task DownloadAsync(string releaseId, string targetPath, IProgress<(long Received,long? Total)>? progress, CancellationToken token);
}

/// <summary>Isolates FLiNG's public HTML so page changes cannot affect local tool management.</summary>
public sealed class FlingTrainerCatalogSource : ITrainerCatalogSource
{
    private const long MaxDownloadBytes=2L*1024*1024*1024;
    private static readonly Uri CatalogUri = new("https://flingtrainer.com/all-trainers/");
    private static readonly Regex TrainerLink = new(
        "<a[^>]+href=[\"'](?<url>https://flingtrainer\\.com/trainer/[^\"'#?]+/?)[\"'][^>]*>(?<title>.*?)</a>",
        RegexOptions.IgnoreCase|RegexOptions.Singleline|RegexOptions.Compiled);
    private static readonly Regex DownloadLink = new(
        "<a[^>]+href=[\"'](?<url>https://flingtrainer\\.com/downloads/[^\"']+)[\"'][^>]*>(?<name>.*?)</a>",
        RegexOptions.IgnoreCase|RegexOptions.Singleline|RegexOptions.Compiled);
    private static readonly Regex Tags = new("<[^>]+>",RegexOptions.Compiled);
    private readonly HttpClient _http;
    private readonly SqliteStateStore _store;
    private readonly ILogger<FlingTrainerCatalogSource> _logger;

    public FlingTrainerCatalogSource(SqliteStateStore store,ILogger<FlingTrainerCatalogSource> logger)
    {
        _store=store;_logger=logger;
        _http=new HttpClient(new HttpClientHandler{AutomaticDecompression=DecompressionMethods.All})
        {Timeout=TimeSpan.FromSeconds(45)};
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("GameSaveCenter/0.5 (+https://github.com/)");
    }

    public async Task<TrainerCatalogSyncResultDto> SyncCatalogAsync(CancellationToken token)
    {
        var html=await GetHtmlAsync(CatalogUri.ToString(),token).ConfigureAwait(false);
        var now=DateTime.UtcNow;
        var items=TrainerLink.Matches(html).Cast<Match>()
            .Select(match =>
            {
                var url=WebUtility.HtmlDecode(match.Groups["url"].Value);
                var title=Clean(match.Groups["title"].Value);
                return new TrainerCatalogItemDto
                {
                    CatalogId=StableId(url),Title=title,NormalizedTitle=Normalize(title),
                    PageUrl=url,LastSyncedUtc=now
                };
            })
            .Where(x=>!string.IsNullOrWhiteSpace(x.Title))
            .GroupBy(x=>x.PageUrl,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();
        if(items.Count<100)throw new WorkerOperationException("FLING_CATALOG_PARSE_FAILED","FLiNG 目录结构可能已经变化，未覆盖本地缓存。",$"Parsed only {items.Count} trainer links.");
        await _store.ReplaceTrainerCatalogAsync(items,token).ConfigureAwait(false);
        _logger.LogInformation("Synchronized {Count} FLiNG catalog entries",items.Count);
        return new TrainerCatalogSyncResultDto{ItemCount=items.Count,SyncedUtc=now,Message=$"已同步 {items.Count} 个 FLiNG 修改器"};
    }

    public Task<List<TrainerCatalogItemDto>> SearchAsync(string query,int limit,CancellationToken token)
        =>_store.SearchTrainerCatalogAsync(query,limit,token);

    public async Task<List<TrainerReleaseDto>> GetReleasesAsync(string catalogId,CancellationToken token)
    {
        var item=await _store.GetTrainerCatalogItemAsync(catalogId,token).ConfigureAwait(false)
                 ?? throw new KeyNotFoundException("FLiNG 目录项不存在，请刷新目录。");
        EnsureFlingUri(item.PageUrl);
        var html=await GetHtmlAsync(item.PageUrl,token).ConfigureAwait(false);
        var releases=DownloadLink.Matches(html).Cast<Match>().Select(match =>
        {
            var url=WebUtility.HtmlDecode(match.Groups["url"].Value);
            var name=Clean(match.Groups["name"].Value);
            return new TrainerReleaseDto
            {
                ReleaseId=StableId(url),CatalogId=catalogId,DisplayName=string.IsNullOrWhiteSpace(name)?"FLiNG Trainer":name,
                DownloadUrl=url
            };
        }).GroupBy(x=>x.DownloadUrl,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();
        if(releases.Count==0)throw new WorkerOperationException("FLING_RELEASE_PARSE_FAILED","没有从详情页识别到可下载版本；FLiNG 页面可能已变化。",item.PageUrl);
        await _store.ReplaceTrainerReleasesAsync(catalogId,releases,token).ConfigureAwait(false);
        return releases;
    }

    public async Task DownloadAsync(string releaseId,string targetPath,IProgress<(long Received,long? Total)>? progress,CancellationToken token)
    {
        var release=await _store.GetTrainerReleaseAsync(releaseId,token).ConfigureAwait(false)
                    ?? throw new KeyNotFoundException("下载版本不存在，请重新展开版本列表。");
        EnsureFlingUri(release.DownloadUrl);
        using var response=await _http.GetAsync(release.DownloadUrl,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
        EnsureFlingUri(response.RequestMessage?.RequestUri?.ToString()??string.Empty);
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength is long declaredLength&&declaredLength>MaxDownloadBytes)
            throw new WorkerOperationException("FLING_DOWNLOAD_TOO_LARGE","修改器下载文件超过安全大小上限，已拒绝下载。",$"{declaredLength} bytes");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await using var source=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var target=new FileStream(targetPath,FileMode.Create,FileAccess.Write,FileShare.None,81920,true);
        var buffer=new byte[81920];long received=0;var total=response.Content.Headers.ContentLength;
        while(true)
        {
            var count=await source.ReadAsync(buffer,token).ConfigureAwait(false);if(count==0)break;
            await target.WriteAsync(buffer.AsMemory(0,count),token).ConfigureAwait(false);received+=count;
            if(received>MaxDownloadBytes)
                throw new WorkerOperationException("FLING_DOWNLOAD_TOO_LARGE","修改器下载文件超过安全大小上限，已中止下载。",$"{received} bytes");
            progress?.Report((received,total));
        }
        if(received==0)throw new WorkerOperationException("FLING_DOWNLOAD_EMPTY","下载内容为空。",release.DownloadUrl);
    }

    private async Task<string> GetHtmlAsync(string url,CancellationToken token)
    {
        EnsureFlingUri(url);
        using var response=await _http.GetAsync(url,HttpCompletionOption.ResponseContentRead,token).ConfigureAwait(false);
        EnsureFlingUri(response.RequestMessage?.RequestUri?.ToString()??string.Empty);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
    }

    private static void EnsureFlingUri(string value)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps||
           !(uri.Host.Equals("flingtrainer.com",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".flingtrainer.com",StringComparison.OrdinalIgnoreCase)))
            throw new WorkerOperationException("FLING_URL_REJECTED","拒绝访问非 FLiNG HTTPS 地址。",value);
    }

    private static string Clean(string value)=>WebUtility.HtmlDecode(Tags.Replace(value,string.Empty)).Trim();
    private static string Normalize(string value)=>new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    private static string StableId(string value)
    {
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..24];
    }
}
