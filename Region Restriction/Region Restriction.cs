using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace RegionRestriction
{
    [ApiVersion(2, 1)]
    public class RegionRestrictionPlugin : TerrariaPlugin
    {
        private const string FilePath = "regions.json";
        private Dictionary<string, string> _regions = new Dictionary<string, string>();

        public override string Name => "Region Restriction";
        public override string Author => "Verza";
        public override string Description => "Teleports players to spawn if they enter restricted regions without the required group.";
        public override Version Version => new Version(1, 0, 0);

        public RegionRestrictionPlugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnGameInitialize);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnPlayerJoin);
            ServerApi.Hooks.NetGetData.Register(this, OnPlayerMove);
            GeneralHooks.ReloadEvent += OnReload;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnGameInitialize);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnPlayerJoin);
                ServerApi.Hooks.NetGetData.Deregister(this, OnPlayerMove);
                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnGameInitialize(EventArgs args)
        {
            LoadRegions();
            Commands.ChatCommands.Add(new Command("regionrestriction.manage", AddRegion, "regionadd")
            {
                HelpText = "Adds a region with a required group. Usage: /regionadd <region_name> <required_group>"
            });
            Commands.ChatCommands.Add(new Command("regionrestriction.manage", ListRegions, "regionlist")
            {
                HelpText = "Lists all regions with required groups."
            });
        }

        private void OnReload(ReloadEventArgs args)
        {
            LoadRegions();
            args.Player.SendSuccessMessage("Regions reloaded from file.");
        }

        private void OnPlayerJoin(GreetPlayerEventArgs args)
        {
            TSPlayer player = TShock.Players[args.Who];
            if (player == null) return;

            foreach (var region in _regions)
            {
                var tsRegion = TShock.Regions.GetRegionByName(region.Key);
                if (tsRegion != null && tsRegion.InArea((int)(player.TileX), (int)(player.TileY)) && !player.Group.HasPermission(region.Value))
                {
                    TeleportToSpawn(player);
                    player.SendInfoMessage($"You are not allowed to enter {region.Key}.");
                }
            }
        }

        private void OnPlayerMove(GetDataEventArgs args)
        {
            if (args.MsgID != PacketTypes.PlayerUpdate)
                return;

            TSPlayer player = TShock.Players[args.Msg.whoAmI];
            if (player == null) return;

            foreach (var region in _regions)
            {
                var tsRegion = TShock.Regions.GetRegionByName(region.Key);
                if (tsRegion != null && tsRegion.InArea((int)(player.TileX), (int)(player.TileY)) && !player.Group.HasPermission(region.Value))
                {
                    TeleportToSpawn(player);
                    player.SendInfoMessage($"You are not allowed to enter {region.Key}.");
                }
            }
        }

        private void AddRegion(CommandArgs args)
        {
            if (args.Parameters.Count != 2)
            {
                args.Player.SendErrorMessage("Usage: /regionadd <region name> <required group>");
                return;
            }

            string regionName = args.Parameters[0];
            string requiredGroup = args.Parameters[1];

            if (TShock.Regions.GetRegionByName(regionName) == null)
            {
                args.Player.SendErrorMessage("Region not found.");
                return;
            }

            _regions[regionName] = requiredGroup;
            SaveRegions();
            args.Player.SendSuccessMessage($"Region {regionName} added with required group {requiredGroup}.");
        }

        private void ListRegions(CommandArgs args)
        {
            if (_regions.Count == 0)
            {
                args.Player.SendInfoMessage("No regions have been added.");
                return;
            }

            foreach (var region in _regions)
            {
                args.Player.SendInfoMessage($"Region: {region.Key}, Required Group: {region.Value}");
            }
        }

        private void TeleportToSpawn(TSPlayer player)
        {
            player.Teleport(Main.spawnTileX * 16, Main.spawnTileY * 16);
        }

        private void SaveRegions()
        {
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_regions, Formatting.Indented));
        }

        private void LoadRegions()
        {
            if (File.Exists(FilePath))
            {
                _regions = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(FilePath));
            }
            else
            {
                _regions = new Dictionary<string, string>();
                SaveRegions();  // Create the file if it doesn't exist
            }
        }
    }
}
