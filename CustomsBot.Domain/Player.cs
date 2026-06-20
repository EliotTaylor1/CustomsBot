namespace CustomsBot.Domain;

public class Player
{
    public Guid Id { get; set; }

    public ulong DiscordId { get; set; }
    public string DiscordUsername { get; set; } = null!;
    public string? DiscordAvatar { get; set; }

    /// <summary>Riot ID (gameName#tagLine), set during account linking.</summary>
    public string? RiotId { get; set; }

    /// <summary>Riot PUUID; required before a player can be issued a tournament code.</summary>
    public string? Puuid { get; set; }

    public Region? Region { get; set; }
}
