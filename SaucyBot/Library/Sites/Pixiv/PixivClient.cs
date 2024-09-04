﻿using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using SaucyBot.Services;

namespace SaucyBot.Library.Sites.Pixiv;

public sealed class PixivClient : IPixivClient
{
    private const string BaseUrl = "https://www.pixiv.net";
    private const string LoginPageUrl = "https://accounts.pixiv.net/login";
    private const string LoginApiUrl = "https://accounts.pixiv.net/api/login";
    private const string WebApiUrl = "https://www.pixiv.net/ajax";

    private readonly Uri _referrer = new(BaseUrl);

    private readonly ILogger<PixivClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly ICacheManager _cache;

    private readonly CookieContainer _cookieContainer = new();
    private readonly HttpClient _client;

    private bool _isLoggedIn;

    public PixivClient(
        ILogger<PixivClient> logger,
        IConfiguration configuration,
        ICacheManager cacheManager
    ) {
        _logger = logger;
        _configuration = configuration;
        _cache = cacheManager;
        
        _cookieContainer.Add(new Cookie
        {
            Name = "PHPSESSID",
            Value = _configuration.GetSection("Sites:Pixiv:SessionCookie").Get<string>(),
            Domain = ".pixiv.net",
            Path = "/",
            HttpOnly = true,
            Secure = true,
        });

        var httpClientHandler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };

        _client = new HttpClient(httpClientHandler);

        _client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36"
        );

        _client.DefaultRequestHeaders.Referrer = _referrer;
        
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
    }

    public async Task<bool> Login()
    {
        if (_isLoggedIn)
        {
            return true;
        }
        
        return _isLoggedIn = await CookieLogin();
    }

    private async Task<bool> CookieLogin()
    {
        try
        {
            var response = await _client.GetStringAsync(BaseUrl);

            return response.Contains("logout.php") ||
                   response.Contains("pixiv.user.loggedIn = true") ||
                   response.Contains("_gaq.push(['_setCustomVar', 1, 'login', 'yes'") ||
                   response.Contains("var dataLayer = [{ login: 'yes',");
        }
        catch (Exception e)
        {
            _logger.LogDebug("Failed logging into Pixiv with error: {Exception}", e.Message);
            
            return false;
        }
    }

    public async Task<IllustrationDetailsResponse?> IllustrationDetails(string id)
    {
        var response = await _cache.Remember($"pixiv.illustration_details_{id}", async () => await _client.GetStringAsync($"{WebApiUrl}/illust/{id}"));

        return response is null ? null : JsonSerializer.Deserialize<IllustrationDetailsResponse>(response);
    }

    public async Task<IllustrationPagesResponse?> IllustrationPages(string id)
    {
        var response = await _cache.Remember($"pixiv.illustration_pages_{id}", async () => await _client.GetStringAsync($"{WebApiUrl}/illust/{id}/pages")); 
        
        return response is null ? null : JsonSerializer.Deserialize<IllustrationPagesResponse>(response);
    }

    public async Task<UgoiraMetadataResponse?> UgoiraMetadata(string id)
    {
        var response = await _cache.Remember($"pixiv.ugoira_metadata_{id}", async () => await _client.GetStringAsync($"{WebApiUrl}/illust/{id}/ugoira_meta"));
        
        return response is null ? null : JsonSerializer.Deserialize<UgoiraMetadataResponse>(response);
    }

    public async Task<HttpResponseMessage> PokeFile(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        
        return await _client.SendAsync(request);
    }

    public async Task<MemoryStream> GetFile(string url)
    {
        var response = await _client.GetStreamAsync(url);

        var stream = new MemoryStream();
        
        await response.CopyToAsync(stream);

        return stream;
    }
}

#region Response Types
public sealed record IllustrationDetailsResponse(
    [property: JsonPropertyName("error")]
    bool Error,
    [property: JsonPropertyName("message")]
    string Message,
    [property: JsonPropertyName("body")]
    IllustrationDetails IllustrationDetails
);

public enum IllustrationType
{
    Illustration = 0,
    // Illustration Type 1 seems to be the same as Type 0
    // These might be from pixiv Sketch potentially?
    Unknown = 1,
    Ugoira = 2,
}

public sealed record IllustrationDetails(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("description")]
    string Description,
    [property: JsonPropertyName("illustType")]
    IllustrationType Type,
    [property: JsonPropertyName("urls")]
    IllustrationDetailsUrls IllustrationDetailsUrls,
    [property: JsonPropertyName("pageCount")]
    int PageCount
);

public sealed record IllustrationDetailsUrls(
    [property: JsonPropertyName("mini")]
    string Mini,
    [property: JsonPropertyName("thumb")]
    string Thumbnail,
    [property: JsonPropertyName("small")]
    string Small,
    [property: JsonPropertyName("regular")]
    string Regular,
    [property: JsonPropertyName("original")]
    string Original
)
{
    public IEnumerable<string> All => new[] { Original, Regular, Small, Thumbnail, Mini };
};

public record IllustrationPagesResponse(
    [property: JsonPropertyName("error")]
    bool Error,
    [property: JsonPropertyName("message")]
    string Message,
    [property: JsonPropertyName("body")]
    List<IllustrationPages> IllustrationPages
);

public sealed record IllustrationPages(
    [property: JsonPropertyName("urls")]
    IllustrationPagesUrls IllustrationPagesUrls,
    [property: JsonPropertyName("width")]
    int Width,
    [property: JsonPropertyName("height")]
    int Height
);

public sealed record IllustrationPagesUrls(
    [property: JsonPropertyName("thumb_mini")]
    string Thumbnail,
    [property: JsonPropertyName("small")]
    string Small,
    [property: JsonPropertyName("regular")]
    string Regular,
    [property: JsonPropertyName("original")]
    string Original
)
{
    public IEnumerable<string> All => new[] { Original, Regular, Small, Thumbnail };
};

public sealed record UgoiraMetadataResponse(
    [property: JsonPropertyName("error")]
    bool Error,
    [property: JsonPropertyName("message")]
    string Message,
    [property: JsonPropertyName("body")]
    UgoiraMetadata UgoiraMetadata
);

public sealed record UgoiraMetadata(
    [property: JsonPropertyName("frames")]
    List<UgoiraFrame> Frames,
    [property: JsonPropertyName("mime_type")]
    string MimeType,
    [property: JsonPropertyName("originalSrc")]
    string OriginalSource,
    [property: JsonPropertyName("src")]
    string Source
);

public sealed record UgoiraFrame(
    [property: JsonPropertyName("file")]
    string File,
    [property: JsonPropertyName("delay")]
    int Delay
);

#endregion
