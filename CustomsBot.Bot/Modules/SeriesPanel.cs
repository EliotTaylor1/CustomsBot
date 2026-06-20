using CustomsBot.Domain;
using NetCord;
using NetCord.Rest;

namespace CustomsBot.Bot.Modules;

/// <summary>
/// Builds the series setup panel: a summary embed plus a user select menu for adding
/// players to the pool. Shared by the create-series command and the panel's handler.
/// </summary>
public static class SeriesPanel
{
    /// <summary>Custom-id prefix; the series id is appended after the ':' separator.</summary>
    public const string AddPlayersId = "series-add";

    public static EmbedProperties BuildEmbed(Series series, IReadOnlyList<string> participantNames)
    {
        return new EmbedProperties()
            .WithTitle($"Series: {series.Name}")
            .WithColor(new Color(0x5865F2))
            .AddFields(
                new EmbedFieldProperties().WithName("Status").WithValue("Not started").WithInline(true),
                new EmbedFieldProperties().WithName("Best of").WithValue(series.BestOf.ToString()).WithInline(true),
                new EmbedFieldProperties().WithName("Fearless").WithValue(series.Fearless ? "On" : "Off").WithInline(true),
                new EmbedFieldProperties().WithName("Map").WithValue(series.Map.ToString()).WithInline(true),
                new EmbedFieldProperties().WithName("Region").WithValue(series.Region.ToString()).WithInline(true),
                new EmbedFieldProperties().WithName("Draft").WithValue(series.DraftType.ToString()).WithInline(true),
                new EmbedFieldProperties()
                    .WithName($"Players ({participantNames.Count})")
                    .WithValue(participantNames.Count == 0 ? "_None yet_" : string.Join("\n", participantNames)));
    }

    public static UserMenuProperties BuildMenu(Guid seriesId)
    {
        return new UserMenuProperties($"{AddPlayersId}:{seriesId}")
        {
            Placeholder = "Add players to the pool",
            MinValues = 1,
            MaxValues = 25,
        };
    }

    public static InteractionMessageProperties Build(Series series, IReadOnlyList<string> participantNames)
    {
        return new InteractionMessageProperties
        {
            Embeds = [BuildEmbed(series, participantNames)],
            Components = [BuildMenu(series.Id)],
        };
    }
}
