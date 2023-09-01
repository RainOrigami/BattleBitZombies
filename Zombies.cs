using BattleBitAPI;
using BattleBitAPI.Common;
using BattleBitAPI.Server;
using BBRAPIModules;
using Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Zombies
{
    [RequireModule(typeof(CommandHandler))]
    public class Zombies : BattleBitModule
    {
        private const Team HUMANS = Team.TeamA;
        private const Team ZOMBIES = Team.TeamB;

        private const SpawningRule ZOMBIES_SPAWN_RULE = SpawningRule.Flags | SpawningRule.SquadCaptain | SpawningRule.SquadMates;
        private const SpawningRule HUMANS_SPAWN_RULE = SpawningRule.Flags | SpawningRule.RallyPoints | SpawningRule.SquadCaptain | SpawningRule.SquadMates;

        private static readonly string[] HUMAN_UNIFORM = new[] { "ANY_NU_Uniform_Survivor_00", "ANY_NU_Uniform_Survivor_01", "ANY_NU_Uniform_Survivor_02", "ANY_NU_Uniform_Survivor_03", "ANY_NU_Uniform_Survivor_04" };
        private static readonly string[] HUMAN_HELMET = new[] { "ANV2_Survivor_All_Helmet_00_A_Z", "ANV2_Survivor_All_Helmet_00_B_Z", "ANV2_Survivor_All_Helmet_01_A_Z", "ANV2_Survivor_All_Helmet_02_A_Z", "ANV2_Survivor_All_Helmet_03_A_Z", "ANV2_Survivor_All_Helmet_04_A_Z", "ANV2_Survivor_All_Helmet_05_A_Z", "ANV2_Survivor_All_Helmet_05_B_Z" };
        private static readonly string[] HUMAN_BACKPACK = new[] { "ANV2_Survivor_All_Backpack_00_A_H", "ANV2_Survivor_All_Backpack_00_A_N", "ANV2_Survivor_All_Backpack_01_A_H", "ANV2_Survivor_All_Backpack_01_A_N", "ANV2_Survivor_All_Backpack_02_A_N" };
        private static readonly string[] HUMAN_ARMOR = new[] { "ANV2_Survivor_All_Armor_00_A_L", "ANV2_Survivor_All_Armor_00_A_N", "ANV2_Survivor_All_Armor_01_A_L", "ANV2_Survivor_All_Armor_02_A_L" };
        private static readonly string[] ZOMBIE_EYES = new[] { "Eye_Zombie_01" };
        private static readonly string[] ZOMBIE_FACE = new[] { "Face_Zombie_01" };
        private static readonly string[] ZOMBIE_HAIR = new[] { "Hair_Zombie_01" };
        private static readonly string[] ZOMBIE_BODY = new[] { "Zombie_01" };
        private static readonly string[] ZOMBIE_UNIFORM = new[] { "ANY_NU_Uniform_Zombie_01" };
        private static readonly string[] ZOMBIE_HELMET = new[] { "ANV2_Universal_Zombie_Helmet_00_A_Z" };
        private static readonly string[] ZOMBIE_ARMOR = new[] { "ANV2_Universal_All_Armor_Null" };
        private static readonly string[] ZOMBIE_BACKPACK = new[] { "ANV2_Universal_All_Backpack_Null" };
        private static readonly string[] ZOMBIE_BELT = new[] { "ANV2_Universal_All_Belt_Null" };

        private static readonly Attachment[] HUMAN_FLASHLIGHTS = new[] { Attachments.Flashlight, Attachments.Searchlight, Attachments.TacticalFlashlight };

        public ZombiesConfiguration Configuration { get; set; }
        public ZombiesState State { get; set; }

        [ModuleReference]
        public CommandHandler CommandHandler { get; set; }

        [ModuleReference]
        public dynamic? DiscordWebhooks { get; set; }

        private bool safetyEnding = false;
        private int amountOfHumansAnnounced = int.MaxValue;
        private Dictionary<ulong, ZombiesPlayer> players = new();

        public override void OnModulesLoaded()
        {
            this.CommandHandler.Register(this);
            this.DiscordWebhooks?.SendMessage("Zombies game mode loaded");
        }

        private ZombiesPlayer getPlayer(RunnerPlayer player)
        {
            if (!this.players.ContainsKey(player.SteamID))
            {
                Console.WriteLine($"Player {player.Name} ({player.SteamID} is not in the player list.");
            }

            return this.players[player.SteamID];
        }

        public override async Task OnConnected()
        {
            //this.Server.ServerSettings.CanVoteDay = true;
            this.Server.ServerSettings.CanVoteNight = true;
            this.Server.SetServerSizeForNextMatch(MapSize._64vs64);
            this.Server.GamemodeRotation.SetRotation("FRONTLINE");
            //this.Server.GamemodeRotation.SetRotation("ELI");

            this.Server.ServerSettings.UnlockAllAttachments = true;

            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                try
                {
                    //if (player.Squad.Name != Squads.Alpha)
                    //{
                    //    player.DisbandTheSquad();
                    //    player.JoinSquad(Squads.Alpha);
                    //}

                    player.Modifications.ReviveHP = 0;
                    if (!this.players.ContainsKey(player.SteamID))
                    {
                        this.players.Add(player.SteamID, new ZombiesPlayer(player));
                    }
                    player.Modifications.CanDeploy = true;
                    player.Modifications.Freeze = false;
                    if (this.getPlayer(player).IsZombie)
                    {
                        Console.WriteLine($"Player {player.Name} is a zombie and build phase is {this.State.BuildPhase}");
                        player.Modifications.CanDeploy = !this.State.BuildPhase;
                        player.Modifications.Freeze = this.State.BuildPhase;
                    }
                    else
                    {
                        player.Modifications.CanDeploy = true;
                        player.Modifications.Freeze = false;
                    }
                    player.Modifications.IsExposedOnMap = false;
                    this.getPlayer(player).IsZombie = player.Team == ZOMBIES;
                    player.Modifications.CaptureFlagSpeedMultiplier = 0.5f;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            this.Server.RoundSettings.PlayersToStart = this.Configuration.RequiredPlayersToStart;

#pragma warning disable CS4014
            Task.Run(async () =>
#pragma warning restore CS4014
            {
                while (this.IsLoaded && this.Server.IsConnected)
                {
                    await checkGameEnd();
                    await Task.Delay(1000);
                }
            });

            Task.Run(humanExposer);
            //Task.Run(squadPointProvider);
        }

        //private async Task squadPointProvider()
        //{
        //    while (this.IsLoaded && this.Server.IsConnected)
        //    {
        //        foreach (Squad<RunnerPlayer> squad in this.Server.AllSquads)
        //        {
        //            if (squad.Team == HUMANS)
        //            {
        //                squad.SquadPoints = 1000;
        //            }
        //            else
        //            {
        //                squad.SquadPoints = 0;
        //            }
        //        }

        //        await Task.Delay(1000);
        //    }
        //}

        //public override Task OnSquadPointsChanged(Squad<RunnerPlayer> squad, int newPoints)
        //{
        //    if (squad.Team == HUMANS)
        //    {
        //        squad.SquadPoints = 1000;
        //    }
        //    else
        //    {
        //        squad.SquadPoints = 0;
        //    }

        //    return Task.CompletedTask;
        //}

        private async Task humanExposer()
        {
            while (this.IsLoaded && this.Server.IsConnected)
            {

                foreach (RunnerPlayer player in this.Server.AllPlayers)
                {
                    player.Modifications.IsExposedOnMap = !this.getPlayer(player).IsZombie;
                }

                if (this.Server.AllPlayers.Count(p => !this.getPlayer(p).IsZombie) > 10)
                {

                    await Task.Delay(3000);

                    if (!this.IsLoaded || !this.Server.IsConnected)
                    {
                        return;
                    }

                    foreach (RunnerPlayer player in this.Server.AllPlayers.Where(p => !this.getPlayer(p).IsZombie))
                    {
                        player.Modifications.IsExposedOnMap = false;
                    }
                }
                await Task.Delay(10000);
            }
        }

        [CommandCallback("list", Description = "List all players and their status")]
        public void ListCommand(RunnerPlayer player)
        {
            StringBuilder sb = new();
            sb.AppendLine("<b>==ZOMBIES==</b>");
            sb.AppendLine(string.Join(" / ", this.Server.AllPlayers.Where(p => this.getPlayer(p).IsZombie).Select(p => $"{p.Name} is {(p.IsAlive ? "<color=\"green\">alive" : "<color=\"red\">dead")}<color=\"white\">")));
            sb.AppendLine("<b>==HUMANS==</b>");
            sb.AppendLine(string.Join(" / ", this.Server.AllPlayers.Where(p => !this.getPlayer(p).IsZombie).Select(p => $"{p.Name} is {(p.IsAlive ? "<color=\"green\">alive" : "<color=\"red\">dead")}<color=\"white\">")));
            player.Message(sb.ToString());
        }

        [CommandCallback("zombie", Description = "Check whether you're a zombie or not")]
        public void ZombieCommand(RunnerPlayer player)
        {
            this.Server.SayToAllChat($"{player.Name} is {(this.getPlayer(player).IsZombie ? "a" : $"not {(this.getPlayer(player).Turn ? "yet " : "")}a")} zombie");
        }

        public override Task OnGameStateChanged(GameState oldState, GameState newState)
        {
            if (oldState == newState)
            {
                return Task.CompletedTask;
            }

            switch (newState)
            {
                case GameState.WaitingForPlayers:
                    this.Server.RoundSettings.PlayersToStart = this.Configuration.RequiredPlayersToStart;
                    break;
                case GameState.CountingDown:
                    this.Server.RoundSettings.SecondsLeft = 30;

                    foreach (RunnerPlayer player in this.Server.AllPlayers)
                    {
                        player.Modifications.CanDeploy = false;
                        player.Modifications.CanSuicide = false;
                        player.Modifications.CanSpectate = false;
                    }

                    if (this.Configuration.BuildPhaseDuration > 0)
                    {
                        // start the build phase
                        this.State.BuildPhase = true;
                        this.State.EndOfBuildPhase = DateTime.Now.AddSeconds(this.Configuration.BuildPhaseDuration);
                        this.State.Save();

                        Task.Run(buildStateManager);
                    }

                    break;
                case GameState.Playing:
                    this.Server.RoundSettings.SecondsLeft = 900;
                    safetyEnding = false;
                    amountOfHumansAnnounced = int.MaxValue;
                    this.Server.ServerSettings.CanVoteDay = Random.Shared.Next(0, 3) == 0;
                    this.Server.ServerSettings.CanVoteNight = true;
                    foreach (Squad<RunnerPlayer> squad in this.Server.AllSquads.Where(s => s.Team == HUMANS))
                    {
                        squad.SquadPoints = squadPoint;
                    }
                    break;
                case GameState.EndingGame:
                    this.State.BuildPhase = false;
                    this.State.EndOfBuildPhase = DateTime.MinValue;

                    break;
                default:
                    break;
            }

            return Task.CompletedTask;
        }
        private int squadPoint = 400;
        public override Task OnSquadLeaderChanged(Squad<RunnerPlayer> squad, RunnerPlayer newLeader)
        {
            if (squad.Team == HUMANS)
            {
                if (squad.Members.Count() == 1 && squad.SquadPoints == 0)
                {
                    squad.SquadPoints = squadPoint;

                }
            }
            return Task.CompletedTask;
        }

        public override Task OnSquadPointsChanged(Squad<RunnerPlayer> squad, int newPoints)
        {
            if (squad.Team == ZOMBIES)
            {
                squad.SquadPoints = 0;
            }

            return Task.CompletedTask;
        }

        private async Task buildStateManager()
        {
            if (!this.State.BuildPhase)
            {
                return;
            }

            // Let humans deploy
            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                if (!this.getPlayer(player).IsZombie || player.Team == HUMANS)
                {
                    player.Modifications.CanDeploy = true;
                }
                else
                {
                    player.Modifications.CanDeploy = false;
                    player.Modifications.Freeze = true;
                }
            }

            // Announce build phase
            this.Server.AnnounceLong($"Human build phase has started! Zombies will arrive in {this.Configuration.BuildPhaseDuration} seconds!");

            while (this.IsLoaded && this.Server.IsConnected && this.State.BuildPhase)
            {
                try
                {
                    if (this.State.EndOfBuildPhase <= DateTime.Now)
                    {
                        this.Server.AnnounceShort("Build phase has ended! Zombies are now released!");
                        this.State.BuildPhase = false;
                        this.State.EndOfBuildPhase = DateTime.MinValue;
                        this.State.Save();

                        foreach (RunnerPlayer player in this.Server.AllPlayers)
                        {
                            player.Modifications.CanDeploy = true;
                            player.Modifications.Freeze = false;
                        }

                        break;
                    }

                    int secondsLeft = (int)(this.State.EndOfBuildPhase - DateTime.Now).TotalSeconds;

                    Console.WriteLine($"{secondsLeft} ({secondsLeft % 10})");

                    if ((secondsLeft % 10) == 0)
                    {
                        this.Server.SayToAllChat($"Zombies will arrive in {secondsLeft} seconds!");
                    }

                    if (this.State.EndOfBuildPhase.AddSeconds(-10) <= DateTime.Now)
                    {
                        this.Server.AnnounceShort($"Zombies will arrive in {this.State.EndOfBuildPhase.Subtract(DateTime.Now).TotalSeconds:0} seconds!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                await Task.Delay(1000);
            }
        }

        #region Team Handling
        private void forcePlayerToCorrectTeam(RunnerPlayer player)
        {
            player.Modifications.SpawningRule = this.getPlayer(player).IsZombie ? ZOMBIES_SPAWN_RULE : HUMANS_SPAWN_RULE;

            if (player.Team == (this.getPlayer(player).IsZombie ? ZOMBIES : HUMANS))
            {
                //if (player.Squad == null)
                //{
                //    player.JoinSquad(Squads.Alpha);
                //}
                return;
            }

            player.Kill();
            player.ChangeTeam();

            if (this.getPlayer(player).IsZombie)
            {
                player.Message("You have been infected and are now a zombie!", 10);
            }

            //player.JoinSquad(Squads.Alpha);
        }

        //public override Task OnPlayerJoinedSquad(RunnerPlayer player, Squad<RunnerPlayer> squad)
        //{
        //    if (squad.Name != Squads.Alpha)
        //    {
        //        player.DisbandTheSquad();
        //        player.JoinSquad(Squads.Alpha);
        //    }

        //    return Task.CompletedTask;
        //}

        //public override Task OnPlayerLeftSquad(RunnerPlayer player, Squad<RunnerPlayer> squad)
        //{
        //    player.JoinSquad(Squads.Alpha);

        //    return Task.CompletedTask;
        //}

        public override Task OnPlayerConnected(RunnerPlayer player)
        {
            player.Modifications.ReviveHP = 0;
            player.Modifications.CanDeploy = true;
            player.Modifications.AllowedVehicles = VehicleType.None;
            player.Modifications.SpawningRule = HUMANS_SPAWN_RULE;
            player.Modifications.CaptureFlagSpeedMultiplier = 0.5f;
            if (!this.players.ContainsKey(player.SteamID))
            {
                this.players.Add(player.SteamID, new ZombiesPlayer(player));
            }

            if (this.Server.RoundSettings.State == GameState.Playing)
            {
                this.getPlayer(player).IsZombie = !this.State.BuildPhase;
            }
            else
            {
                this.getPlayer(player).IsZombie = this.Server.AllPlayers.Count(p => this.getPlayer(p).IsZombie) < this.Configuration.InitialZombieCount && player.Team == ZOMBIES;
            }

            this.forcePlayerToCorrectTeam(player);

            if (this.State.BuildPhase)
            {
                player.Modifications.CanDeploy = !this.getPlayer(player).IsZombie;
                player.Modifications.Freeze = this.getPlayer(player).IsZombie;
            }
            else
            {
                player.Modifications.CanDeploy = true;
                player.Modifications.Freeze = false;
            }

            //this.Server.SayToAllChat($"/*Welcome*/ {player.Name} to the server!");

            return Task.CompletedTask;
        }

        public override Task OnPlayerDisconnected(RunnerPlayer player)
        {
            if (!this.Server.AllPlayers.Any(p => this.getPlayer(p).IsZombie) && this.Server.AllPlayers.Any())
            {
                RunnerPlayer newZombie = this.Server.AllPlayers.Skip(Random.Shared.Next(0, this.Server.AllPlayers.Count())).First();
                this.getPlayer(newZombie).IsZombie = true;
                this.forcePlayerToCorrectTeam(newZombie);
            }

            this.players.Remove(player.SteamID);

            return Task.CompletedTask;
        }

        public override Task<bool> OnPlayerRequestingToChangeTeam(RunnerPlayer player, Team requestedTeam)
        {
            return Task.FromResult(false);
        }

        public override Task OnPlayerChangeTeam(RunnerPlayer player, Team team)
        {
            this.forcePlayerToCorrectTeam(player);

            return Task.CompletedTask;
        }
        #endregion

        #region Loadout and rank
        public override Task OnPlayerJoiningToServer(ulong steamID, PlayerJoiningArguments args)
        {
            args.Stats.Progress.Rank = 200;
            args.Stats.Progress.Prestige = 10;
            return Task.CompletedTask;
        }

        public override Task<OnPlayerSpawnArguments?> OnPlayerSpawning(RunnerPlayer player, OnPlayerSpawnArguments request)
        {
            this.getPlayer(player).IsJuggernaut = false;

            if (player.Team != (this.getPlayer(player).IsZombie ? ZOMBIES : HUMANS))
            {
                this.forcePlayerToCorrectTeam(player);
                return Task.FromResult<OnPlayerSpawnArguments?>(null);
            }

            if (player.Team == HUMANS)
            {
#if FLARE_BLOCKED
                //if (request.Loadout.Throwable == Gadgets.Flare)
                //{
                //    request.Loadout.Throwable = Gadgets.Flashbang;
                //}
#endif

                if (request.Loadout.HeavyGadget == Gadgets.SuicideC4)
                {
                    request.Loadout.HeavyGadget = Gadgets.C4;
                    player.Message("Suicide C4 is not allowed and was replaced by C4.", 10);
                }

                if (request.Loadout.LightGadget == Gadgets.SuicideC4)
                {
                    request.Loadout.LightGadget = Gadgets.C4;
                    player.Message("Suicide C4 is not allowed and was replaced by C4.", 10);
                }

                //request.Loadout.FirstAid = null;

                // Humans can spawn as whatever they like

                // Human skins
                request.Wearings.Uniform = HUMAN_UNIFORM[Random.Shared.Next(0, HUMAN_UNIFORM.Length)];
                request.Wearings.Head = HUMAN_HELMET[Random.Shared.Next(0, HUMAN_HELMET.Length)];
                request.Wearings.Backbag = HUMAN_BACKPACK[Random.Shared.Next(0, HUMAN_BACKPACK.Length)];
                request.Wearings.Chest = HUMAN_ARMOR[Random.Shared.Next(0, HUMAN_ARMOR.Length)];

                if (this.Server.DayNight == MapDayNight.Night)
                {
                    // Force flashlight on primary and secondary
                    request.Loadout.PrimaryWeapon.SideRail = HUMAN_FLASHLIGHTS[Random.Shared.Next(0, HUMAN_FLASHLIGHTS.Length)];
                    request.Loadout.SecondaryWeapon.SideRail = HUMAN_FLASHLIGHTS[Random.Shared.Next(0, HUMAN_FLASHLIGHTS.Length)];
                }

                if (this.Server.AllPlayers.Count() < this.Configuration.PistolThreshold && this.Server.AllPlayers.Count(p => !this.getPlayer(p).IsZombie) >= this.Server.AllPlayers.Count(p => this.getPlayer(p).IsZombie))
                {
                    this.getPlayer(player).InitialLoadout = request.Loadout;

                    request.Loadout.Throwable = null;
                    request.Loadout.ThrowableExtra = 0;
                    request.Loadout.HeavyGadget = null;
                    request.Loadout.HeavyGadgetExtra = 0;
                    request.Loadout.LightGadget = null;
                    request.Loadout.LightGadgetExtra = 0;
                    request.Loadout.PrimaryWeapon.Tool = null;
                    request.Loadout.PrimaryExtraMagazines = 0;
                    request.Loadout.SecondaryExtraMagazines = 10;
                }

                return Task.FromResult(request as OnPlayerSpawnArguments?);
            }

            // Zombies can only spawn with melee weapons
            request.Loadout.FirstAid = default;
            request.Loadout.PrimaryWeapon = default;
            request.Loadout.SecondaryWeapon = default;
            request.Loadout.Throwable = Gadgets.SmokeGrenadeRed; // Red smoke makes the zombies menacing
            request.Loadout.ThrowableExtra = 20;
            request.Loadout.LightGadget = Gadgets.SledgeHammer;
            request.Loadout.HeavyGadget = Gadgets.Pickaxe;
            request.Loadout.FirstAid = default;
            request.Loadout.FirstAidExtra = 0;

            bool isClassZombie = false;

            if (Random.Shared.Next(0, 100) <= 5)
            {
                player.Message("You are a flashbang zombie, use it wisely!", 10);
                request.Loadout.ThrowableExtra = 4;
                request.Loadout.Throwable = Gadgets.Flashbang;
                isClassZombie = true;
            }

            if (Random.Shared.Next(0, 100) <= 10 && !isClassZombie)
            {
                player.Message("You are a riot shield zombie, use it wisely!", 10);
                request.Loadout.HeavyGadget = Gadgets.RiotShield;
                isClassZombie = true;
            }

            if (Random.Shared.Next(0, 100) <= 5 && !isClassZombie)
            {
                player.Message("You are a suicide C4 zombie, use it wisely!", 10);
                request.Loadout.HeavyGadget = Gadgets.SuicideC4;
                isClassZombie = true;
            }

            if ((Random.Shared.Next(0, 100) == 1 && !isClassZombie))
            {
                player.Message("<color=\"red\">YOU ARE JUGGERNAUT ZOMBIE, GO FUCK SHIT UP", 30);
                this.getPlayer(player).IsJuggernaut = true;
            }

            request.Wearings.Eye = ZOMBIE_EYES[Random.Shared.Next(0, ZOMBIE_EYES.Length)];
            request.Wearings.Face = ZOMBIE_FACE[Random.Shared.Next(0, ZOMBIE_FACE.Length)];
            request.Wearings.Hair = ZOMBIE_HAIR[Random.Shared.Next(0, ZOMBIE_HAIR.Length)];
            request.Wearings.Skin = ZOMBIE_BODY[Random.Shared.Next(0, ZOMBIE_BODY.Length)];
            request.Wearings.Uniform = ZOMBIE_UNIFORM[Random.Shared.Next(0, ZOMBIE_UNIFORM.Length)];
            request.Wearings.Head = ZOMBIE_HELMET[Random.Shared.Next(0, ZOMBIE_HELMET.Length)];
            request.Wearings.Chest = ZOMBIE_ARMOR[Random.Shared.Next(0, ZOMBIE_ARMOR.Length)];
            request.Wearings.Backbag = ZOMBIE_BACKPACK[Random.Shared.Next(0, ZOMBIE_BACKPACK.Length)];
            request.Wearings.Belt = ZOMBIE_BELT[Random.Shared.Next(0, ZOMBIE_BELT.Length)];

            return Task.FromResult(request as OnPlayerSpawnArguments?);
        }

        public override async Task OnPlayerSpawned(RunnerPlayer player)
        {
            if (player.Team == ZOMBIES)
            {
                // Zombies are faster, jump higher, have more health and one-hit
                player.Modifications.FallDamageMultiplier = this.Configuration.FallDamageMultiplier;
                player.Modifications.RunningSpeedMultiplier = this.Configuration.RunningSpeedMultiplier;
                player.Modifications.JumpHeightMultiplier = this.Configuration.JumpHeightMultiplier;
                player.Modifications.FriendlyHUDEnabled = true;
                player.Modifications.CanUseNightVision = true;

                var ratio = (float)this.Server.AllPlayers.Count(p => this.getPlayer(p).IsZombie) / ((float)this.Server.AllPlayers.Count() - 1);
                var multiplier = this.Configuration.ZombieMinDamageReceived + (this.Configuration.ZombieMaxDamageReceived - this.Configuration.ZombieMinDamageReceived) * ratio;

                if (this.getPlayer(player).IsJuggernaut)
                {
                    multiplier = 0.01f;
                }

                player.Modifications.ReceiveDamageMultiplier = multiplier;
                await Console.Out.WriteLineAsync($"Damage received multiplier of {player.Name} is set to {multiplier} ({ratio} zombie/total ratio)");

                return;
            }

            // Humans are normal
            player.Modifications.FallDamageMultiplier = 1f;
            player.Modifications.RunningSpeedMultiplier = 0.9f;
            player.Modifications.FriendlyHUDEnabled = false;
            player.Modifications.JumpHeightMultiplier = 1f;
            player.Modifications.ReceiveDamageMultiplier = 1f;

            // No night vision
            player.Modifications.CanUseNightVision = false;
        }
        #endregion

        #region Zombie game logic
        public override async Task OnAPlayerDownedAnotherPlayer(OnPlayerKillArguments<RunnerPlayer> playerKill)
        {
            if (playerKill.Victim.Team == ZOMBIES)
            {
                // Zombies will just respawn
                return;
            }

            if (playerKill.Killer.Team != ZOMBIES)
            {
                // Humans killing humans is fine (friendly fire)
                return;
            }

            //            if (playerKill.Killer.SteamID == playerKill.Victim.SteamID)
            //            {
            //                // Suicides have a chance to turn humans into zombies
            //                if (Random.Shared.NextDouble() > this.Configuration.SuicideZombieficationChance)
            //                {
            //                    return;
            //                }
            //                else
            //                {
            //#pragma warning disable CS4014
            //                    Task.Run(async () =>
            //#pragma warning restore CS4014
            //                    {
            //                        RunnerPlayer player = playerKill.Victim;
            //                        this.getPlayer(player).Turn = true;
            //                        int waitTime = Random.Shared.Next(this.Configuration.SuicideZombieficationMaxTime);
            //                        await Task.Delay(waitTime / 2);
            //                        if (player.Team == HUMANS)
            //                        {
            //                            this.Server.SayToAllChat($"<b>{player.Name}<b> has been bitten by a <color=\"red\">zombie<color=\"white\">. Be careful around them!");
            //                            player.Message("You have been bitten by a zombie! Any time now you will turn into one.", 10);
            //                        }
            //                        else
            //                        {
            //                            return;
            //                        }

            //                        await Task.Delay(waitTime / 2);

            //                        if (player.Team == HUMANS)
            //                        {
            //                            this.getPlayer(player).IsZombie = true;
            //                            player.ChangeTeam(ZOMBIES);
            //                            player.Modifications.SpawningRule = ZOMBIES_SPAWN_RULE;
            //                            player.Message("You have been infected and are now a zombie!", 10);
            //                            //this.Server.SayToAllChat($"<b>{player.Name}<b> is now a <color=\"red\">zombie<color=\"white\">!");
            //                            this.DiscordWebhooks?.SendMessage($"Player {playerKill.Victim.Name} succumbed to the bite and has become a zombie.");
            //                            await this.checkGameEnd();
            //                        }
            //                    });
            //                }
            //            }

            // Zombie killed human, turn human into zombie
            this.getPlayer(playerKill.Victim).Turn = true;
            await this.checkGameEnd();

            await Task.CompletedTask;
        }

        public override Task OnAPlayerRevivedAnotherPlayer(RunnerPlayer from, RunnerPlayer to)
        {
            this.getPlayer(to).Turn = false;

            return Task.CompletedTask;
        }

        public override async Task OnPlayerDied(RunnerPlayer player)
        {
            if (this.getPlayer(player).IsZombie)
            {
                return;
            }

            player.Modifications.SpawningRule = ZOMBIES_SPAWN_RULE;
            this.getPlayer(player).Turn = false;
            this.getPlayer(player).IsZombie = true;
            this.forcePlayerToCorrectTeam(player);
            //this.Server.SayToAllChat($"<b>{player.Name}<b> is now a <color=\"red\">zombie<color=\"white\">!");
            //this.DiscordWebhooks?.SendMessage($"Player {player.Name} died and has become a zombie.");

            await checkGameEnd();
        }

        public override Task OnSessionChanged(long oldSessionID, long newSessionID)
        {
            foreach (ZombiesPlayer player in this.players.Values)
            {
                player.Turn = false;
                player.IsZombie = false;
                player.ReceivedLoadout = false;
            }

            return Task.CompletedTask;
        }

        private async Task checkGameEnd()
        {
            Console.WriteLine($"There are {this.Server.AllPlayers.Count(p => this.getPlayer(p).IsZombie)} zombies and {this.Server.AllPlayers.Count(p => !this.getPlayer(p).IsZombie)} humans for a total of {this.Server.AllPlayers.Count()} players.");

            if ((this.Server.RoundSettings.State != GameState.Playing) || safetyEnding)
            {
                return;
            }

            if (this.Server.RoundSettings.SecondsLeft <= 2)
            {
                Console.WriteLine("HUMANS WIN ANNOUNCEMENT");
                this.Server.AnnounceLong("Humans win");
                await Task.Delay(200);
                this.Server.ForceEndGame(HUMANS);
                this.safetyEnding = true;
            }

            if (this.Server.AllPlayers.Count() < this.Configuration.PistolThreshold && this.Server.AllPlayers.Count(p => !this.getPlayer(p).IsZombie) < this.Server.AllPlayers.Count(p => this.getPlayer(p).IsZombie))
            {
                foreach (RunnerPlayer player in this.Server.AllPlayers.Where(p => !this.getPlayer(p).IsZombie && p.IsAlive && !p.IsDown && !this.getPlayer(p).ReceivedLoadout))
                {
                    this.getPlayer(player).ReceivedLoadout = true;

                    PlayerLoadout? targetLoadout = this.getPlayer(player).InitialLoadout;
                    if (targetLoadout == null)
                    {
                        player.SetThrowable(Gadgets.ImpactGrenade.Name, 6, false);
                        //player.SetFirstAidGadget(Gadgets.Bandage.Name, 6, false);
                        player.SetHeavyGadget(Gadgets.HeavyAmmoKit.Name, 2, false);
                        player.SetLightGadget(Gadgets.C4.Name, 4, false);
                        player.SetPrimaryWeapon(new WeaponItem()
                        {
                            Tool = Weapons.M4A1,
                            MainSight = Attachments.RedDot,
                            Barrel = Attachments.SuppressorLong,
                            SideRail = this.Server.DayNight == MapDayNight.Night ? HUMAN_FLASHLIGHTS[Random.Shared.Next(0, HUMAN_FLASHLIGHTS.Length)] : Attachments.Redlaser,
                            UnderRail = Attachments.VerticalGrip
                        }, 20, false);
                    }
                    else
                    {
                        player.SetThrowable(targetLoadout.Value.ThrowableName, targetLoadout.Value.ThrowableExtra, false);
                        player.SetFirstAidGadget(targetLoadout.Value.FirstAidName, targetLoadout.Value.FirstAidExtra, false);
                        player.SetHeavyGadget(targetLoadout.Value.HeavyGadgetName, targetLoadout.Value.HeavyGadgetExtra, false);
                        player.SetLightGadget(targetLoadout.Value.LightGadgetName, targetLoadout.Value.LightGadgetExtra, false);
                        player.SetPrimaryWeapon(targetLoadout.Value.PrimaryWeapon, targetLoadout.Value.PrimaryExtraMagazines, false);
                    }
                }
            }

            int humanCount = this.Server.AllPlayers.Count(player => !this.getPlayer(player).IsZombie && !player.IsDown);

            if (humanCount == 0)
            {
                safetyEnding = true;
                this.Server.AnnounceLong("ZOMBIES WIN!");
                this.DiscordWebhooks?.SendMessage($"== ZOMBIES WIN ==");
                await Task.Delay(1000);
                this.Server.ForceEndGame(ZOMBIES);
                return;
            }

            if (amountOfHumansAnnounced > humanCount)
            {
                if (humanCount <= this.Configuration.AnnounceLastHumansCount)
                {
                    if (humanCount == 1)
                    {
                        RunnerPlayer? lastHuman = this.Server.AllPlayers.FirstOrDefault(p => !this.getPlayer(p).IsZombie);
                        if (lastHuman != null)
                        {
                            this.Server.AnnounceShort($"<b>{lastHuman.Name}<b> is the LAST HUMAN, <color=\"red\">KILL IT!");
                            this.Server.SayToAllChat($"<b>{lastHuman.Name}<b> is the LAST HUMAN, <color=\"red\">KILL IT!");
                        }
                        else
                        {
                            this.Server.AnnounceShort($"LAST HUMAN, <color=\"red\">KILL IT!");
                            this.Server.SayToAllChat($"LAST HUMAN, <color=\"red\">KILL IT!");
                        }
                    }
                    else
                    {
                        this.Server.SayToAllChat($"{humanCount} HUMANS LEFT, <color=\"red\">KILL THEM!");
                    }
                }
            }

            amountOfHumansAnnounced = humanCount;
        }
        #endregion

        [CommandCallback("set", Description = "Set a specific balancing value.", AllowedRoles = Roles.Admin)]
        public void SetCommand(RunnerPlayer player, BalanceVariable name, float value)
        {
            switch (name)
            {
                case BalanceVariable.InitialZombieCount:
                    this.Configuration.InitialZombieCount = (int)value;
                    break;
                case BalanceVariable.AnnounceLastHumansCount:
                    this.Configuration.AnnounceLastHumansCount = (int)value;
                    break;
                case BalanceVariable.RequiredPlayersToStart:
                    this.Configuration.RequiredPlayersToStart = (int)value;
                    break;
                case BalanceVariable.ZombieMinDamageReceived:
                    this.Configuration.ZombieMinDamageReceived = value;
                    break;
                case BalanceVariable.ZombieMaxDamageReceived:
                    this.Configuration.ZombieMaxDamageReceived = value;
                    break;
                case BalanceVariable.SuicideZombieficationChance:
                    this.Configuration.SuicideZombieficationChance = value;
                    break;
                case BalanceVariable.SuicideZombieficationMaxTime:
                    this.Configuration.SuicideZombieficationMaxTime = (int)value;
                    break;
                case BalanceVariable.FallDamageMultiplier:
                    this.Configuration.FallDamageMultiplier = value;
                    break;
                case BalanceVariable.RunningSpeedMultiplier:
                    this.Configuration.RunningSpeedMultiplier = value;
                    break;
                case BalanceVariable.JumpHeightMultiplier:
                    this.Configuration.JumpHeightMultiplier = value;
                    break;
                case BalanceVariable.BuildPhaseDuration:
                    this.Configuration.BuildPhaseDuration = (int)value;
                    break;
                default:
                    break;
            }

            this.Configuration.Save();
        }

        [CommandCallback("switch", Description = "Switch a player to the other team.", AllowedRoles = Roles.Moderator)]
        public async void SwitchCommand(RunnerPlayer source, RunnerPlayer target)
        {
            target.Kill();
            this.getPlayer(target).IsZombie = !this.getPlayer(target).IsZombie;
            this.forcePlayerToCorrectTeam(target);
            if (this.getPlayer(target).IsZombie)
            {
                this.Server.SayToAllChat($"<b>{target.Name}<b> is now a <color=\"red\">zombie<color=\"white\">!");
            }

            await checkGameEnd();
        }

        [CommandCallback("afk", Description = "Make zombies win because humans camp or are AFK", AllowedRoles = Roles.Moderator)]
        public async void LastHumanAFKOrCamping(RunnerPlayer caller)
        {
            if (this.Server.AllPlayers.Count(p => !this.getPlayer(p).IsZombie) > 10)
            {
                caller.Message("There are too many humans to end the game.");
                return;
            }

            safetyEnding = true;
            this.Server.AnnounceLong("ZOMBIES WIN!");
            this.DiscordWebhooks?.SendMessage($"== ZOMBIES WIN ==");
            await Task.Delay(1000);
            this.Server.ForceEndGame(ZOMBIES);

            return;
        }
    }

    public enum BalanceVariable
    {
        InitialZombieCount,
        AnnounceLastHumansCount,
        RequiredPlayersToStart,
        ZombieMinDamageReceived,
        ZombieMaxDamageReceived,
        SuicideZombieficationChance,
        SuicideZombieficationMaxTime,
        FallDamageMultiplier,
        RunningSpeedMultiplier,
        JumpHeightMultiplier,
        BuildPhaseDuration
    }

    public class ZombiesPlayer
    {
        public RunnerPlayer Player { get; set; }

        public ZombiesPlayer(RunnerPlayer player)
        {
            this.Player = player;
        }

        public PlayerLoadout? InitialLoadout { get; set; } = null;

        public bool IsZombie { get; set; }
        public bool Turn { get; set; }
        public bool ReceivedLoadout { get; set; } = false;
        public bool IsJuggernaut { get; internal set; }
    }

    public class ZombiesConfiguration : ModuleConfiguration
    {
        public int InitialZombieCount { get; set; } = 6;
        public int AnnounceLastHumansCount { get; set; } = 10;
        public int RequiredPlayersToStart { get; set; } = 20;
        public float ZombieMinDamageReceived { get; set; } = 0.2f;
        public float ZombieMaxDamageReceived { get; set; } = 2f;
        public float SuicideZombieficationChance { get; set; } = 0.3141f;
        public int SuicideZombieficationMaxTime { get; set; } = 120000;
        public float FallDamageMultiplier { get; set; } = 1f;
        public float RunningSpeedMultiplier { get; set; } = 1f;
        public float JumpHeightMultiplier { get; set; } = 1f;
        public int BuildPhaseDuration { get; set; } = 0;
        public int PistolThreshold { get; set; } = 255;
    }

    public class ZombiesState : ModuleConfiguration
    {
        public bool BuildPhase { get; set; } = false;
        public DateTime EndOfBuildPhase { get; set; } = DateTime.MinValue;
    }
}