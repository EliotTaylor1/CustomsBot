using CustomsBot.Domain;

namespace CustomsBot.Riot;

/// <summary>Regional routing values used by account/match endpoints (as opposed to platform routes).</summary>
public enum RegionalRoute
{
    Americas,
    Asia,
    Europe
}

public static class RegionRouting
{
    /// <summary>Maps a platform <see cref="Region"/> to its account/match regional route.</summary>
    public static RegionalRoute ToRegionalRoute(this Region region) => region switch
    {
        Region.Na1 or Region.Br1 or Region.La1 or Region.La2 or Region.Oc1 => RegionalRoute.Americas,
        Region.Kr or Region.Jp1 => RegionalRoute.Asia,
        Region.Euw1 or Region.Eun1 or Region.Tr1 or Region.Ru => RegionalRoute.Europe,
        _ => RegionalRoute.Americas
    };

    public static string ToHost(this RegionalRoute route) =>
        $"https://{route.ToString().ToLowerInvariant()}.api.riotgames.com";

    /// <summary>Platform code used by tournament provider registration, e.g. <c>NA1</c>, <c>EUW1</c>.</summary>
    public static string ToPlatformCode(this Region region) => region.ToString().ToUpperInvariant();

    /// <summary>Data Dragon / tournament map type string.</summary>
    public static string ToMapType(this GameMap map) => map switch
    {
        GameMap.SummonersRift => "SUMMONERS_RIFT",
        GameMap.HowlingAbyss => "HOWLING_ABYSS",
        _ => "SUMMONERS_RIFT"
    };
}
