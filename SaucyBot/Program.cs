using SaucyBot;
using SaucyBot.Database;
using SaucyBot.Library.Sites.ArtStation;
using SaucyBot.Library.Sites.DeviantArt;
using SaucyBot.Library.Sites.E621;
using SaucyBot.Library.Sites.ExHentai;
using SaucyBot.Library.Sites.FurAffinity;
using SaucyBot.Library.Sites.HentaiFoundry;
using SaucyBot.Library.Sites.Misskey;
using SaucyBot.Library.Sites.Newgrounds;
using SaucyBot.Library.Sites.Pixiv;
using SaucyBot.Library.Sites.Twitter;
using SaucyBot.Services;
using SaucyBot.Services.Cache;
using SaucyBot.Site;
using Serilog;

await Host.CreateDefaultBuilder(args)
    .UseSerilog((context, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        
        services.AddDbContext<DatabaseContext>(ServiceLifetime.Transient);
        services.AddDbContextFactory<DatabaseContext>(lifetime: ServiceLifetime.Transient);

        services.AddMemoryCache();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetSection("Cache:Redis:ConnectionString").Get<string>();
        });
        
        services.AddSingleton<SiteManager>();
        services.AddSingleton<MessageManager>();
        services.AddSingleton<DatabaseManager>();
        services.AddSingleton<ICacheManager, CacheManager>();

        services.AddSingleton<MemoryCacheDriver>();
        services.AddSingleton<RedisCacheDriver>();

        services.AddSingleton<IGuildConfigurationManager, GuildConfigurationManager>();
        
        services.AddSingleton<IFurAffinityClient, FaExportClient>();
        services.AddSingleton<IPixivClient, PixivClient>();
        services.AddSingleton<IArtStationClient, ArtStationClient>();
        services.AddSingleton<IHentaiFoundryClient, HentaiFoundryClient>();
        services.AddSingleton<INewgroundsClient, NewgroundsClient>();
        services.AddSingleton<IExHentaiClient, ExHentaiClient>();
        services.AddSingleton<IDeviantArtOpenEmbedClient, DeviantArtOpenEmbedClient>();
        services.AddSingleton<IDeviantArtClient, DeviantArtClient>();
        services.AddSingleton<IE621Client, E621Client>();
        services.AddSingleton<IFxTwitterClient, FxTwitterClient>();
        services.AddSingleton<ITwitterImageSyndicationClient, TwitterImageSyndicationClient>();
        services.AddSingleton<IMisskeyClient, MisskeyClient>();

        services.AddSingleton<FurAffinity>();
        services.AddSingleton<Pixiv>();
        services.AddSingleton<ArtStation>();
        services.AddSingleton<HentaiFoundry>();
        services.AddSingleton<Twitter>();
        services.AddSingleton<FxTwitter>();
        services.AddSingleton<DeviantArt>();
        services.AddSingleton<E621>();
        services.AddSingleton<ExHentai>();
        services.AddSingleton<Newgrounds>();
        services.AddSingleton<Reddit>();
        services.AddSingleton<Misskey>();

        services.AddHostedService<Worker>();
    })
    .UseConsoleLifetime()
    .Build()
    .RunAsync();
