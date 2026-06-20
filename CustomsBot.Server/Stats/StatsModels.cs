namespace CustomsBot.Server.Stats;

/// <summary>Subset of the Tournament-V5 game-completion callback we need to locate the match.</summary>
public record TournamentCallback(string ShortCode, long GameId, string Region);

/// <summary>Dev / pull-by-code fallback: submit a known Match-V5 id for a game.</summary>
public record SubmitResultRequest(string MatchId);
