using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace EndRoundSounds;

[MinimumApiVersion(80)]
public class EndRoundSoundsPlugin : BasePlugin, IPluginConfig<EndRoundSoundsConfig>
{
    public override string ModuleName => "End Round Sounds Plugin";
    public override string ModuleVersion => "1.1.1";
    public override string ModuleAuthor => "GianniKoch";

    public override string ModuleDescription =>
        "A basic plugin that plays configurable sounds at the end of each round.";

    public required EndRoundSoundsConfig Config { get; set; }

    private void LogInformation(string message) => Logger.LogInformation("[EndRoundSounds] {Message}", message);

    private void LogWarning(string message) => Logger.LogWarning("[EndRoundSounds] {Message}", message);

    public override void Load(bool hotReload)
    {
        LogInformation($"Load invoked. hotReload={hotReload}");
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        LogInformation("Registered EventRoundEnd handler.");
        LogInformation("Loaded End Round Sounds Plugin!");
    }

    public void OnConfigParsed(EndRoundSoundsConfig config)
    {
        Config = config;

        LogInformation("OnConfigParsed invoked.");
        LogInformation(
            $"Configuration mode: {nameof(Config.WinnerLoserDifferentiation)}={Config.WinnerLoserDifferentiation}, Sounds={Config.Sounds.Count}, SoundsWin={Config.SoundsWin.Count}, SoundsLose={Config.SoundsLose.Count}");

        if (Config.WinnerLoserDifferentiation)
        {
            LogInformation(
                $"Using {nameof(Config.WinnerLoserDifferentiation)}, found {Config.SoundsWin.Count} win sounds and {Config.SoundsLose.Count} lose sounds!");
        }
        else
        {
            LogInformation($"Found {Config.Sounds.Count} sounds!");
        }

        if (Config.WinnerLoserDifferentiation && (Config.SoundsWin.Count == 0 || Config.SoundsLose.Count == 0))
        {
            LogWarning(
                $"{nameof(Config.WinnerLoserDifferentiation)} is enabled but one side has no sounds configured. Sounds may fail for some players.");
        }

        if (!Config.WinnerLoserDifferentiation && Config.Sounds.Count == 0)
        {
            LogWarning("No sounds configured in shared mode.");
        }
    }

    public override void Unload(bool hotReload)
    {
        DeregisterEventHandler<EventRoundEnd>(OnRoundEnd);
        LogInformation($"Unload invoked. hotReload={hotReload}");
        LogInformation("Unloaded End Round Sounds Plugin!");
    }

    /// <summary>
    /// Handle the round end event.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        LogInformation($"OnRoundEnd triggered. winner={(CsTeam)@event.Winner}");

        if (ShouldSkipEndRoundSounds(out var skipReason))
        {
            LogInformation($"Skipping end-round sound playback. Reason: {skipReason}");
            return HookResult.Continue;
        }

        var players = Utilities.GetPlayers();
        LogInformation($"Found {players.Count} players to process.");

        var winningTeam = (CsTeam)@event.Winner;
        foreach (var player in players)
        {
            var sound = GetSoundPathForPlayer(player, winningTeam);

            if (string.IsNullOrWhiteSpace(sound))
            {
                LogWarning($"Selected an empty sound path for playerTeam={player.Team}. Skipping playback for this player.");
                continue;
            }

            LogInformation($"PlayerTeam={player.Team}, WinningTeam={winningTeam}, SelectedSound='{sound}'");
            PlaySoundForPlayer(player, sound);
        }

        LogInformation("Finished processing OnRoundEnd playback.");

        return HookResult.Continue;
    }

    /// <summary>
    /// Config is empty, or no sounds are set, skip playing the end sounds.
    /// </summary>
    /// <returns>Config is invalid</returns>
    private bool ShouldSkipEndRoundSounds(out string reason)
    {
        if (Config.WinnerLoserDifferentiation && Config.SoundsWin.Count == 0 && Config.SoundsLose.Count == 0)
        {
            reason = "Winner/loser differentiation mode is enabled and both SoundsWin and SoundsLose are empty.";
            return true;
        }

        if (!Config.WinnerLoserDifferentiation && Config.Sounds.Count == 0)
        {
            reason = "Shared sound mode is enabled and Sounds is empty.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Get a random sound path from the config.
    /// </summary>
    /// <param name="player">Player that sounds needs to play for</param>
    /// <param name="winningTeam">Winning team of the round</param>
    private string GetSoundPathForPlayer(CCSPlayerController player, CsTeam winningTeam)
    {
        if (Config.WinnerLoserDifferentiation)
        {
            if (player.Team == winningTeam)
            {
                var index = Random.Shared.NextDistinct(Config.SoundsWin.Count);
                LogInformation($"Selecting win sound index={index} from poolSize={Config.SoundsWin.Count}");
                return Config.SoundsWin[index];
            }
            else
            {
                var index = Random.Shared.NextDistinct(Config.SoundsLose.Count);
                LogInformation($"Selecting lose sound index={index} from poolSize={Config.SoundsLose.Count}");
                return Config.SoundsLose[index];
            }
        }
        else
        {
            var index = Random.Shared.NextDistinct(Config.Sounds.Count);
            LogInformation($"Selecting shared sound index={index} from poolSize={Config.Sounds.Count}");
            return Config.Sounds[index];
        }
    }

    /// <summary>
    /// Send a client command to play a sound from a workshop item for the given player.
    /// Check the README.md of this project for more info about creating the necessary workshop item.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="path">Path to sound file in workshop items</param>
    private void PlaySoundForPlayer(CCSPlayerController player, string path)
    {
        var command = $"play \"{path}\"";
        LogInformation($"Executing client command for playerTeam={player.Team}: {command}");
        player.ExecuteClientCommand(command);
    }
}

public class EndRoundSoundsConfig : BasePluginConfig
{
    public bool WinnerLoserDifferentiation { get; set; } = true;
    public List<string> SoundsWin { get; set; } = [];
    public List<string> SoundsLose { get; set; } = [];
    public List<string> Sounds { get; set; } = [];
}
