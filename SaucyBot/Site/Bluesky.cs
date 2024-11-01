using System.Net;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using SaucyBot.Extensions;
using SaucyBot.Library;
using SaucyBot.Library.Sites.BlueSky;
using SaucyBot.Site.Response;

namespace SaucyBot.Site;

public sealed class Bluesky : BaseSite
{
    public override string Identifier => "Bluesky";

    protected override string Pattern =>
        @"https?:\/\/(www\.)?bsky\.app\/profile\/(?<user>.*)\/post\/(?<id>.*)\/?";

    protected override Color Color => new(0x1083FE);

    private readonly ILogger<FxTwitter> _logger;
    private readonly IConfiguration _configuration;
    private readonly IVixBlueskyClient _client;

    public Bluesky(ILogger<FxTwitter> logger, IConfiguration configuration, IVixBlueskyClient client)
    {
        _logger = logger;
        _configuration = configuration;
        
        _client = client;
    }

    public override async Task<ProcessResponse?> Process(Match match, SocketUserMessage? message = null)
    {
        var response = await _client.GetPost(
            match.Groups["user"].Value,
            match.Groups["id"].Value
        );
        
        var url = $"https://bsky.app/profile/{match.Groups["user"].Value}/post/{match.Groups["id"].Value}";

        var post = response?.Posts.FirstOrDefault();

        if (post is null)
        {
            return null;
        }
        
        var photoMedia = await FindAllPhotoElements(post);
        
        var videoMedia = await FindAllVideoElements(post);

        var hasPhoto = photoMedia.NotEmpty();

        var hasVideo = videoMedia.NotEmpty();

        if (hasVideo)
        {
            return HandleVideoLazy(match);
        }

        if (hasPhoto)
        {
            return HandlePhoto(url, post, photoMedia);
        }

        return HandleRegular(url, post);
    }

    private Task<List<VixBlueskyEmbedImage>> FindAllPhotoElements(VixBlueskyPost post)
    {
        return Task.FromResult(post.Embed?.Images ?? []);
    }

    private Task<List<string>> FindAllVideoElements(VixBlueskyPost post)
    {
        var output = new List<string>();

        if (post.Embed?.Playlist is not null)
        {
            output.Add(post.Embed.Playlist);
        }
        
        return Task.FromResult(output);
    }
    

    private ProcessResponse HandlePhoto(string url, VixBlueskyPost post, IEnumerable<VixBlueskyEmbedImage> results)
    {
        _logger.LogDebug("Processing as photo embed");
        
        var response = new ProcessResponse();
        
        foreach (var image in results)
        {
            var embed = new EmbedBuilder
            {
                Url = url,
                Timestamp = DateTimeOffset.Parse(post.Record.CreatedAt),
                Color = this.Color,
                Description = post.Record.Text,
                Author = new EmbedAuthorBuilder
                {
                    Name = $"{post.Author.DisplayName} (@{post.Author.Handle})",
                    IconUrl = post.Author.AvatarUrl,
                    Url = $"https://bsky.app/profile/{post.Author.Handle}",
                },
                Fields = new List<EmbedFieldBuilder>
                {
                    new ()
                    {
                        Name = "Replies",
                        Value = post.Replies,
                        IsInline = true
                    },
                    new () {
                        Name = "Reposts",
                        Value = post.Reposts,
                        IsInline = true
                    },
                    new ()
                    {
                        Name = "Quotes",
                        Value = post.Quotes,
                        IsInline = true
                    },
                    new ()
                    {
                        Name = "Likes",
                        Value = post.Likes,
                        IsInline = true
                    },
                },
                ImageUrl = image.Url,
                Footer = new EmbedFooterBuilder { IconUrl = Constants.BlueskyIconUrl, Text = "Bluesky" },
            };
            
            response.Embeds.Add(embed.Build());
        }

        return response;
    }
    

    private ProcessResponse HandleVideo(string url, Match match, VixBlueskyPost post)
    {
        _logger.LogDebug("Processing as video embed");
        
        var videoUrl = $"https://r.bskyx.app/profile/{match.Groups["user"].Value}/post/{match.Groups["id"].Value}";
        
        var response = new ProcessResponse();
        
        var embed = new EmbedBuilder
        {
            Url = url,
            Timestamp = DateTimeOffset.Parse(post.Record.CreatedAt),
            Color = this.Color,
            Description = post.Record.Text,
            Author = new EmbedAuthorBuilder
            {
                Name = $"{post.Author.DisplayName} (@{post.Author.Handle})",
                IconUrl = post.Author.AvatarUrl,
                Url = $"https://bsky.app/profile/{post.Author.Handle}",
            },
            Fields = new List<EmbedFieldBuilder>
            {
                new ()
                {
                    Name = "Replies",
                    Value = post.Replies,
                    IsInline = true
                },
                new () {
                    Name = "Reposts",
                    Value = post.Reposts,
                    IsInline = true
                },
                new ()
                {
                    Name = "Quotes",
                    Value = post.Quotes,
                    IsInline = true
                },
                new ()
                {
                    Name = "Likes",
                    Value = post.Likes,
                    IsInline = true
                },
            },
            Footer = new EmbedFooterBuilder { IconUrl = Constants.BlueskyIconUrl, Text = "Bluesky" },
        };
        
        response.Embeds.Add(embed.Build());

        response.Text = videoUrl;
        
        return response;
    }

    private ProcessResponse HandleVideoLazy(Match match)
    {
        var response = new ProcessResponse();
        
        response.Text = $"https://bskyx.app/profile/{match.Groups["user"].Value}/post/{match.Groups["id"].Value}";
        
        return response;
    }
    
    
    private ProcessResponse HandleRegular(string url, VixBlueskyPost post)
    {
        var response = new ProcessResponse();
        
        var embed = new EmbedBuilder
        {
            Url = url,
            Timestamp = DateTimeOffset.Parse(post.Record.CreatedAt),
            Color = this.Color,
            Description = post.Record.Text,
            Author = new EmbedAuthorBuilder
            {
                Name = $"{post.Author.DisplayName} (@{post.Author.Handle})",
                IconUrl = post.Author.AvatarUrl,
                Url = $"https://bsky.app/profile/{post.Author.Handle}",
            },
            Fields = new List<EmbedFieldBuilder>
            {
                new ()
                {
                    Name = "Replies",
                    Value = post.Replies,
                    IsInline = true
                },
                new () {
                    Name = "Reposts",
                    Value = post.Reposts,
                    IsInline = true
                },
                new ()
                {
                    Name = "Quotes",
                    Value = post.Quotes,
                    IsInline = true
                },
                new ()
                {
                    Name = "Likes",
                    Value = post.Likes,
                    IsInline = true
                },
            },
            Footer = new EmbedFooterBuilder { IconUrl = Constants.BlueskyIconUrl, Text = "Bluesky" },
        };
        
        response.Embeds.Add(embed.Build());
            
        return response;
    }
}
