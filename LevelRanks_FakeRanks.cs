using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using LevelsRanks.API;
using MySqlConnector;
using Dapper;


namespace LevelRanks_FakeRanks;

public class Config
{
    public int Type { get; set; } = 2;
}

public class LevelRanks_FakeRanks : BasePlugin
{
    public override string ModuleName => "[LR] Fake Ranks";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Ravid";
    public override string ModuleDescription => "Fake Ranks in scoreboard for LevelRanks plugin.";

    private readonly PluginCapability<IPointsManager> _pointsManagerCapability = new("levelsranks");
    private IPointsManager? _pointsManager;

    private bool AllowUpdate = false;

    private Config config = new();

    public override void Load(bool hotReload)
    { 
        base.Load(hotReload);

        _pointsManager = _pointsManagerCapability.Get();
        
        if (_pointsManager == null)
        {
            Server.PrintToConsole("Points management system is currently unavailable.");
            return;
        }

        config = LoadConfig();

        RegisterEventHandler((EventRoundPrestart @event, GameEventInfo info) => {  AllowUpdate = true; return HookResult.Continue; });
        RegisterEventHandler((EventCsWinPanelMatch @event, GameEventInfo info) => { AllowUpdate = false; return HookResult.Continue; });

        AddCommand("css_fakeranks_reload", "Reloads the plugin configuration.", OnReloadCommand);

        RegisterListener<Listeners.OnTick>(() =>
        {
            if (config.Type == 0 || !AllowUpdate)
            {
                return;
            }

            Utilities.GetPlayers().ForEach(p =>
            {
                if (p.IsValid && !p.IsBot)
                {
                    if(config.Type == 1)
                    {
                        GetDataFromDB(p);
                    } else{
                        var rankid = _pointsManager.GetCurrentRank(p.SteamID.ToString());

                        p.CompetitiveWins = 10;
                        p.CompetitiveRankType = 12; 
                        p.CompetitiveRanking = rankid!.Id;
                        Utilities.SetStateChanged(p, "CCSPlayerController", "m_iCompetitiveRankType");
                    }
                }
            });
        });
    }

    private void GetDataFromDB(CCSPlayerController player)
    {
        var connectionString = _pointsManager!.GetConnectionString();
        var databaseConfig = _pointsManager.GetDatabaseConfig();

        using var connection = new MySqlConnection(connectionString);
        connection.Open();

        var pointsQuery = $"SELECT `value` FROM {databaseConfig.Name} WHERE steam = @SteamID";
        var points = connection.QueryFirstOrDefault<int>(pointsQuery, new { SteamID = ConvertSteamID64ToSteamID(player.SteamID.ToString()) });

        if(points < 0)
        {
            points = 0;
        }

        player.CompetitiveWins = 10;
        player.CompetitiveRankType = 11;
        player.CompetitiveRanking = points;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRankType");
    }

    //Reload command
    // need @css/root permission
    [RequiresPermissions(new string[] { "@css/root" })]
    public void OnReloadCommand(CCSPlayerController? player, CommandInfo info)
    {
        if(player == null)
        {
            info.ReplyToCommand(" \x04[FakeRanks]\x01 You must be a player to use this command.");
            return;
        }

        if(!player.IsValid || player.IsBot)
        {
            info.ReplyToCommand(" \x04[FakeRanks]\x01 You must be a player to use this command.");
            return;
        }

        config = LoadConfig();
        info.ReplyToCommand(" \x04[FakeRanks]\x01 Configuration reloaded.");
    }

    public Config LoadConfig()
    {
         var configPath = Path.Combine(ModuleDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            return CreateConfig(configPath);
        }

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
    }

    private static Config CreateConfig(string configPath)
    {
        var config = new Config();

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        return config;
    }

    public static string ConvertSteamID64ToSteamID(string steamId64)
    {
        if (ulong.TryParse(steamId64, out var communityId) && communityId > 76561197960265728)
        {
            var authServer = (communityId - 76561197960265728) % 2;
            var authId = (communityId - 76561197960265728 - authServer) / 2;
            return $"STEAM_1:{authServer}:{authId}";
        }
        return string.Empty; 
    }   
}
