using BattleBitAPI;
using BattleBitAPI.Common;
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

        private static readonly string[] HUMAN_UNIFORM = new[] { "USA_NU_Uniform_Uni_02", "USA_NU_Uniform_Uni_03_Patreon", "RUS_NU_Uniform_Uni_02", "RUS_NU_Uniform_Uni_02_Patreon" };
        private static readonly string[] HUMAN_HELMET = new[] { "USV2_Universal_DON_Helmet_00_A_Z", "RUV2_Universal_DON_Helmet_00_A_Z", "USV2_Universal_All_Helmet_01_A_Z", "RUV2_Universal_All_Helmet_01_A_Z", "USV2_Universal_DON_Helmet_01_A_Z", "RUV2_Universal_DON_Helmet_01_A_Z", "RUV2_Universal_All_Helmet_00_A_Z", "RUV2_Universal_All_Helmet_00_A_Z", "USV2_Assault_All_Helmet_00_D_N", "RUV2_Assault_All_Helmet_00_D_N", "USV2_Medic_All_Helmet_00_A_Z", "RUV2_Medic_All_Helmet_00_A_Z", "USV2_Medic_All_Helmet_01_A_Z", "RUV2_Medic_All_Helmet_01_A_Z", "USV2_Engineer_All_Helmet_00_A_Z", "RUV2_Engineer_All_Helmet_00_A_Z", "RUV2_Engineer_All_Helmet_00_B_Z", "USV2_Engineer_VIP_Helmet_00_A_Z", "RUV2_Engineer_VIP_Helmet_00_A_Z", "USV2_Engineer_DON_Helmet_00_A_Z", "RUV2_Engineer_DON_Helmet_00_A_Z", "USV2_Engineer_All_Helmet_01_C_N", "RUV2_Engineer_All_Helmet_01_C_N", "USV2_Sniper_All_Helmet_00_A_Z", "RUV2_Sniper_All_Helmet_00_A_Z", "USV2_Sniper_All_Helmet_01_A_Z", "RUV2_Sniper_All_Helmet_01_A_Z", "USV2_Leader_All_Helmet_00_A_Z", "RUV2_Leader_All_Helmet_00_A_Z" };
        private static readonly string[] ZOMBIE_EYES = new[] { "Eye_Zombie_01" };
        private static readonly string[] ZOMBIE_FACE = new[] { "Face_Zombie_01" };
        private static readonly string[] ZOMBIE_HAIR = new[] { "Hair_Zombie_01" };
        private static readonly string[] ZOMBIE_BODY = new[] { "Zombie_01" };
        private static readonly string[] ZOMBIE_UNIFORM = new[] { "ANY_NU_Uniform_Zombie_01" };
        private static readonly string[] ZOMBIE_HELMET = new[] { "ANV2_Universal_Zombie_Helmet_00_A_Z" };
        private static readonly string[] ZOMBIE_ARMOR = new[] { "ANV2_Universal_All_Armor_Null" };
        private static readonly string[] ZOMBIE_BACKPACK = new[] { "ANV2_Universal_All_Backpack_Null" };
        private static readonly string[] ZOMBIE_BELT = new[] { "ANV2_Universal_All_Belt_Null" };

        public ZombiesConfiguration Configuration { get; set; }

        [ModuleReference]
        public CommandHandler CommandHandler { get; set; }

        [ModuleReference]
        public BattleBitModule? DiscordWebhooks { get; set; }

        private bool safetyEnding = false;
        private int amountOfHumansAnnounced = int.MaxValue;
        private List<ulong> zombies = new();

        public override void OnModulesLoaded()
        {
            this.CommandHandler.Register(this);
            this.DiscordWebhooks?.Call("SendMessage", "Zombies game mode loaded");
        }

        private bool isZombie(RunnerPlayer player)
        {
            return this.zombies.Contains(player.SteamID);
        }

        private void setZombie(RunnerPlayer player, bool zombie)
        {
            if (zombie && !this.zombies.Contains(player.SteamID))
            {
                this.zombies.Add(player.SteamID);
            }
            else if (!zombie && this.zombies.Contains(player.SteamID))
            {
                this.zombies.Remove(player.SteamID);
            }
        }

        public override async Task OnConnected()
        {
            await base.OnConnected();

            this.Server.GamemodeRotation.AddToRotation("FRONTLINE");

            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                this.setZombie(player, player.Team == ZOMBIES);
            }

            this.Server.RoundSettings.PlayersToStart = this.Configuration.RequiredPlayersToStart;

#pragma warning disable CS4014
            Task.Run(async () =>
#pragma warning restore CS4014
            {
                while (this.Server.IsConnected)
                {
                    await checkGameEnd();
                    await Task.Delay(10000);
                }
            });

        }

        [CommandCallback("list", Description = "List all players and their status")]
        public void ListCommand(RunnerPlayer player)
        {
            StringBuilder sb = new();
            sb.AppendLine("<b>==ZOMBIES==</b>");
            sb.AppendLine(string.Join(" / ", this.Server.AllPlayers.Where(p => this.isZombie(p)).Select(p => $"{p.Name} is {(p.IsAlive ? "<color=\"green\">alive" : "<color=\"red\">dead")}<color=\"white\">")));
            sb.AppendLine("<b>==HUMANS==</b>");
            sb.AppendLine(string.Join(" / ", this.Server.AllPlayers.Where(p => !this.isZombie(p)).Select(p => $"{p.Name} is {(p.IsAlive ? "<color=\"green\">alive" : "<color=\"red\">dead")}<color=\"white\">")));
            player.Message(sb.ToString());
        }

        [CommandCallback("zombie", Description = "Check whether you're a zombie or not")]
        public void ZombieCommand(RunnerPlayer player)
        {
            this.Server.SayToChat($"{player.Name} is {(this.isZombie(player) ? "a" : $"not {(this.turnPlayer.Contains(player.SteamID) ? "yet " : "")}a")} zombie");
        }

        public override async Task OnGameStateChanged(GameState oldState, GameState newState)
        {
            await base.OnGameStateChanged(oldState, newState);

            if (oldState == newState)
            {
                return;
            }

            switch (newState)
            {
                case GameState.WaitingForPlayers:
                    this.Server.RoundSettings.PlayersToStart = this.Configuration.RequiredPlayersToStart;
                    break;
                case GameState.CountingDown:
                    this.Server.RoundSettings.SecondsLeft = 10;
                    break;
                case GameState.Playing:
                    safetyEnding = false;
                    amountOfHumansAnnounced = int.MaxValue;
                    break;
                case GameState.EndingGame:
                    break;
                default:
                    break;
            }
        }

        #region Team Handling
        private async Task forcePlayerToCorrectTeam(RunnerPlayer player)
        {
            if (player.Team == (this.isZombie(player) ? ZOMBIES : HUMANS))
            {
                return;
            }

            player.Kill();
            player.ChangeTeam();

            if (this.isZombie(player))
            {
                player.Message("You have been infected and are now a zombie!", 10);
            }

            await Task.CompletedTask;
        }

        public override async Task OnPlayerConnected(RunnerPlayer player)
        {
            await base.OnPlayerConnected(player);

            if (this.Server.RoundSettings.State == GameState.Playing)
            {
                this.setZombie(player, true);
            }
            else
            {
                this.setZombie(player, this.Server.AllPlayers.Count(p => this.isZombie(p)) < this.Configuration.InitialZombieCount && player.Team == ZOMBIES);
            }
            await this.forcePlayerToCorrectTeam(player);
            this.Server.SayToChat($"Welcome {player.Name} to the server!");
        }

        public override async Task OnPlayerDisconnected(RunnerPlayer player)
        {
            await base.OnPlayerDisconnected(player);

            if (!this.Server.AllPlayers.Any(p => this.isZombie(p)) && this.Server.AllPlayers.Any())
            {
                RunnerPlayer newZombie = this.Server.AllPlayers.Skip(Random.Shared.Next(0, this.Server.AllPlayers.Count())).First();
                this.setZombie(newZombie, true);
                await this.forcePlayerToCorrectTeam(newZombie);
            }

        }

        public override async Task OnPlayerChangeTeam(RunnerPlayer player, Team team)
        {
            await base.OnPlayerChangeTeam(player, team);

            await this.forcePlayerToCorrectTeam(player);
        }
        #endregion

        #region Loadout and rank
        public override Task OnPlayerJoiningToServer(ulong steamID, PlayerJoiningArguments args)
        {
            args.Stats.Progress.Rank = 200;
            args.Stats.Progress.Prestige = 10;
            return Task.CompletedTask;
        }

        private List<ulong> allowedToSpawn = new();

        public override Task<OnPlayerSpawnArguments> OnPlayerSpawning(RunnerPlayer player, OnPlayerSpawnArguments request)
        {
            this.allowedToSpawn.Add(player.SteamID);

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

                // Humans can spawn as whatever they like

                // Human skins
                request.Wearings.Uniform = HUMAN_UNIFORM[Random.Shared.Next(0, HUMAN_UNIFORM.Length)];
                request.Wearings.Head = HUMAN_HELMET[Random.Shared.Next(0, HUMAN_HELMET.Length)];

                return Task.FromResult(request);
            }

            if (request.RequestedPoint != PlayerSpawningPosition.SpawnAtPoint)
            {
                this.allowedToSpawn.Remove(player.SteamID);
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

            request.Wearings.Eye = ZOMBIE_EYES[Random.Shared.Next(0, ZOMBIE_EYES.Length)];
            request.Wearings.Face = ZOMBIE_FACE[Random.Shared.Next(0, ZOMBIE_FACE.Length)];
            request.Wearings.Hair = ZOMBIE_HAIR[Random.Shared.Next(0, ZOMBIE_HAIR.Length)];
            request.Wearings.Skin = ZOMBIE_BODY[Random.Shared.Next(0, ZOMBIE_BODY.Length)];
            request.Wearings.Uniform = ZOMBIE_UNIFORM[Random.Shared.Next(0, ZOMBIE_UNIFORM.Length)];
            request.Wearings.Head = ZOMBIE_HELMET[Random.Shared.Next(0, ZOMBIE_HELMET.Length)];
            request.Wearings.Chest = ZOMBIE_ARMOR[Random.Shared.Next(0, ZOMBIE_ARMOR.Length)];
            request.Wearings.Backbag = ZOMBIE_BACKPACK[Random.Shared.Next(0, ZOMBIE_BACKPACK.Length)];
            request.Wearings.Belt = ZOMBIE_BELT[Random.Shared.Next(0, ZOMBIE_BELT.Length)];

            return Task.FromResult(request);
        }

        public override async Task OnPlayerSpawned(RunnerPlayer player)
        {
            await base.OnPlayerSpawned(player);

            if (player.Team != (this.isZombie(player) ? ZOMBIES : HUMANS))
            {
                player.Kill();
                await this.forcePlayerToCorrectTeam(player);
            }

            if (player.Team == ZOMBIES)
            {
                // Zombies are faster, jump higher, have more health and one-hit
                player.Modifications.FallDamageMultiplier = 0.0f;
                player.Modifications.RunningSpeedMultiplier = 1.2f; // TODO: anti cheat kicks because of this
                player.Modifications.JumpHeightMultiplier = 2.5f;

                var ratio = (float)this.Server.AllPlayers.Count(p => this.isZombie(p)) / ((float)this.Server.AllPlayers.Count() - 1);
                var multiplier = this.Configuration.ZombieMinDamageReceived + (this.Configuration.ZombieMaxDamageReceived - this.Configuration.ZombieMinDamageReceived) * ratio;
                player.Modifications.ReceiveDamageMultiplier = multiplier;
                await Console.Out.WriteLineAsync($"Damage received multiplier of {player.Name} is set to " + multiplier);

                player.Modifications.GiveDamageMultiplier = 10f; // TODO: does not count for gadgets

                if (!this.allowedToSpawn.Contains(player.SteamID))
                {
                    player.Kill();
                    player.Message("<color=\"red\">You are only allowed to spawn on points.", 10);
                }

                return;
            }

            // Humans are normal
            player.Modifications.FallDamageMultiplier = 1f;
            player.Modifications.RunningSpeedMultiplier = 1f;
            player.Modifications.JumpHeightMultiplier = 1f;
            player.Modifications.ReceiveDamageMultiplier = 1f;
            player.Modifications.GiveDamageMultiplier = 1f;

            // No night vision
            player.Modifications.CanUseNightVision = false;
        }
        #endregion

        #region Zombie game logic
        List<ulong> turnPlayer = new();
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

            if (playerKill.Killer.SteamID == playerKill.Victim.SteamID)
            {
                // Suicides have a chance to turn humans into zombies
                if (Random.Shared.NextDouble() > this.Configuration.SuicideZombieficationChance)
                {
                    return;
                }
                else
                {
#pragma warning disable CS4014
                    Task.Run(async () =>
#pragma warning restore CS4014
                    {
                        RunnerPlayer player = playerKill.Victim;
                        this.turnPlayer.Add(player.SteamID);
                        int waitTime = Random.Shared.Next(this.Configuration.SuicideZombieficationMaxTime);
                        await Task.Delay(waitTime / 2);
                        if (player.Team == HUMANS)
                        {
                            this.Server.SayToChat($"<b>{player.Name}<b> has been bitten by a <color=\"red\">zombie<color=\"white\">. Be careful around them!");
                            player.Message("You have been bitten by a zombie! Any time now you will turn into one.", 10);
                        }
                        else
                        {
                            return;
                        }

                        await Task.Delay(waitTime / 2);

                        if (player.Team == HUMANS)
                        {
                            this.setZombie(player, true);
                            player.ChangeTeam(ZOMBIES);
                            player.Message("You have been infected and are now a zombie!", 10);
                            this.Server.SayToChat($"<b>{player.Name}<b> is now a <color=\"red\">zombie<color=\"white\">!");
                            this.DiscordWebhooks?.Call("SendMessage", $"Player {playerKill.Victim.Name} succumbed to the bite and has become a zombie.");
                            await this.checkGameEnd();
                        }
                    });
                }
            }

            this.DiscordWebhooks?.Call("SendMessage", $"Player {playerKill.Victim.Name} died and has become a zombie.");
            // Zombie killed human, turn human into zombie
            this.turnPlayer.Add(playerKill.Victim.SteamID);

            await Task.CompletedTask;
        }

        public override async Task OnPlayerDied(RunnerPlayer player)
        {
            if (!this.turnPlayer.Contains(player.SteamID))
            {
                return;
            }

            this.turnPlayer.Remove(player.SteamID);
            this.setZombie(player, true);
            await this.forcePlayerToCorrectTeam(player);
            this.Server.SayToChat($"<b>{player.Name}<b> is now a <color=\"red\">zombie<color=\"white\">!");

            await checkGameEnd();
        }

        private async Task checkGameEnd()
        {
            if ((this.Server.RoundSettings.State != GameState.Playing) || safetyEnding)
            {
                return;
            }

            int humanCount = this.Server.AllPlayers.Count(player => !this.isZombie(player));

            if (humanCount == 0)
            {
                safetyEnding = true;
                this.Server.AnnounceLong("ZOMBIES WIN!");
                this.DiscordWebhooks?.Call("SendMessage", $"== ZOMBIES WIN ==");
                await Task.Delay(2000);
                this.Server.ForceEndGame();
                return;
            }

            if (amountOfHumansAnnounced > humanCount)
            {
                if (humanCount <= this.Configuration.AnnounceLastHumansCount)
                {
                    if (humanCount == 1)
                    {
                        RunnerPlayer? lastHuman = this.Server.AllPlayers.FirstOrDefault(p => !this.isZombie(p));
                        if (lastHuman != null)
                        {
                            this.Server.AnnounceShort($"<b>{lastHuman.Name}<b> is the LAST HUMAN, <color=\"red\">KILL IT!");
                            this.Server.SayToChat($"<b>{lastHuman.Name}<b> is the LAST HUMAN, <color=\"red\">KILL IT!");
                        }
                        else
                        {
                            this.Server.AnnounceShort($"LAST HUMAN, <color=\"red\">KILL IT!");
                            this.Server.SayToChat($"LAST HUMAN, <color=\"red\">KILL IT!");
                        }
                    }
                    else
                    {
                        this.Server.SayToChat($"{humanCount} HUMANS LEFT, <color=\"red\">KILL THEM!");
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
                default:
                    break;
            }

            this.Configuration.Save();
        }

        [CommandCallback("switch", Description = "Switch a player to the other team.", AllowedRoles = Roles.Admin)]
        public async void SwitchCommand(RunnerPlayer source, RunnerPlayer target)
        {
            target.Kill();
            this.setZombie(target, !this.isZombie(target));
            await this.forcePlayerToCorrectTeam(target);
            if (this.isZombie(target))
            {
                this.Server.SayToChat($"<b>{target.Name}<b> is now a <color=\"red\">zombie<color=\"white\">!");
            }

            await checkGameEnd();
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
        JumpHeightMultiplier
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
    }
}