namespace SaucyBot.Library.Sites.BlueSky;

public interface IVixBlueskyClient
{
    public Task<VixBlueskyResponse?> GetPost(string name, string identifier);
}
