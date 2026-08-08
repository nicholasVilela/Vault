namespace Vault.Core.IGDB;

public static class IgdbRoutes {
    private static string BaseApi => "https://api.igdb.com";
    private static string Version => "v4";

    public static string Token(string clientId, string clientSecret) => 
        $"https://id.twitch.tv/oauth2/token?client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials";

    public static string Platforms => $"{BaseApi}/{Version}/platforms";
    public static string Games => $"{BaseApi}/{Version}/games";
    public static string Multiquery => $"{BaseApi}/{Version}/multiquery";
}
