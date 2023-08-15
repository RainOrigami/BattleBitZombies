using BattleBitAPI;
using BattleBitAPI.Common;
using BBRAPIModules;
using Newtonsoft.Json;

namespace Zombies
{
    public class Zombies : BattleBitModule
    {
        public Zombies(RunnerServer server) : base(server)
        {
            this.configuration = JsonConvert.DeserializeObject<ZombiesConfiguration>(File.ReadAllText("ZombiesConfiguration.json")) ?? new();
        }

        private const Team HUMANS = Team.TeamA;
        private const Team ZOMBIES = Team.TeamB;
        private const int WAVE_TIME = 30;
        private const int WAVE_WATCHDOG_TIMER = 1000;

        private static readonly string[] HUMAN_UNIFORM = new[] { "USA_NU_Uniform_Uni_02", "USA_NU_Uniform_Uni_03_Patreon", "RUS_NU_Uniform_Uni_02", "RUS_NU_Uniform_Uni_02_Patreon" };
        private static readonly string[] HUMAN_HELMET = new[] { "USV2_Universal_DON_Helmet_00_A_Z", "RUV2_Universal_DON_Helmet_00_A_Z", "USV2_Universal_All_Helmet_01_A_Z", "RUV2_Universal_All_Helmet_01_A_Z", "USV2_Universal_DON_Helmet_01_A_Z", "RUV2_Universal_DON_Helmet_01_A_Z", "RUV2_Universal_All_Helmet_00_A_Z", "RUV2_Universal_All_Helmet_00_A_Z", "USV2_Assault_All_Helmet_00_D_N", "RUV2_Assault_All_Helmet_00_D_N", "USV2_Medic_All_Helmet_00_A_Z", "RUV2_Medic_All_Helmet_00_A_Z", "USV2_Medic_All_Helmet_01_A_Z", "RUV2_Medic_All_Helmet_01_A_Z", "USV2_Engineer_All_Helmet_00_A_Z", "RUV2_Engineer_All_Helmet_00_A_Z", "RUV2_Engineer_All_Helmet_00_B_Z", "USV2_Engineer_VIP_Helmet_00_A_Z", "RUV2_Engineer_VIP_Helmet_00_A_Z", "USV2_Engineer_DON_Helmet_00_A_Z", "RUV2_Engineer_DON_Helmet_00_A_Z", "USV2_Engineer_All_Helmet_01_C_N", "RUV2_Engineer_All_Helmet_01_C_N", "USV2_Sniper_All_Helmet_00_A_Z", "RUV2_Sniper_All_Helmet_00_A_Z", "USV2_Sniper_All_Helmet_01_A_Z", "RUV2_Sniper_All_Helmet_01_A_Z", "USV2_Leader_All_Helmet_00_A_Z", "RUV2_Leader_All_Helmet_00_A_Z" };
        private static readonly string[] ZOMBIE_EYES = new[] { "Eye_Zombie_01" };
        private static readonly string[] ZOMBIE_FACE = new[] { "Face_Zombie_01" };
        private static readonly string[] ZOMBIE_HAIR = new[] { "Hair_Zombie_01" };
        private static readonly string[] ZOMBIE_BODY = new[] { "Zombie_01" };
        private static readonly string[] ZOMBIE_UNIFORM = new[] { "ANY_NU_Uniform_Zombie_01" };
        private static readonly string[] ZOMBIE_HELMET = new[] { "ANV2_Universal_Zombie_Helmet_00_A_Z" };

        private ZombiesConfiguration configuration;
        private bool safetyEnding = false;
        private int amountOfHumansAnnounced = int.MaxValue;
        private DateTime lastWave = DateTime.Now.Subtract(new TimeSpan(0, 0, WAVE_TIME));

        private List<ulong> zombies = new();

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

            this.Server.RoundSettings.PlayersToStart = this.configuration.RequiredPlayersToStart;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed);
            {
                while (this.Server.IsConnected)
                {
                    await checkGameEnd();
                    await Task.Delay(10000);
                }
            });

#if WAVE_HANDLER
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(waveHandler);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed);
#endif
        }


        #region Wave handling
#if WAVE_HANDLER
        private async Task waveHandler()
        {
            while (true)
            {
                await Task.Delay(WAVE_WATCHDOG_TIMER);
                if (!this.Server.IsConnected || this.Server.RoundSettings.State != GameState.Playing)
                {
                    continue;
                }

                // Force wave
                foreach (RunnerPlayer player in this.Server.AllPlayers.Where(p => !p.IsAlive))
                {
                    // TODO: required information to spawn a player
                }
            }

            await Task.CompletedTask;
        }
#endif
        #endregion

        public override async Task<bool> OnPlayerTypedMessage(RunnerPlayer player, ChatChannel channel, string msg)
        {
            bool result = await base.OnPlayerTypedMessage(player, channel, msg);

            if (!result)
            {
                return result;
            }

            if (msg == "!zombie")
            {
                Task.Run(async () =>
                {
                    await Task.Delay(50);
                    this.Server.SayToChat($"{player.Name} is {(this.isZombie(player) ? "a" : "not a")} zombie");
                });
                //player.Message($"You are {(player.IsZombie ? "a " : "not a")} zombie");
            }

            if (msg == "!switch")
            {
                //if (player.IsZombie)
                //{
                //    await Task.Delay(50);
                //    this.SayToChat("You can not switch to the zombie side. THIS IS A DEBUG COMMAND USE WITH CAUTION AND EXPECT BUGS");
                //}
                this.setZombie(player, !this.isZombie(player));
                await forcePlayerToCorrectTeam(player);
                return false;
            }

            //if (msg == "!test")
            //{
            //    this.SayToChat("<b>test</b>");
            //    this.AnnounceLong("<b>test<b>");
            //    foreach (var item in this.AllPlayers)
            //    {
            //        item.Message("<b>test</b>");
            //    }
            //}

            //if (msg == "!checkend")
            //{
            //    await checkGameEnd(null);
            //}

            if (msg == "!list")
            {
                await Task.Delay(50);
                this.Server.SayToChat("<b>==ZOMBIES==");
                this.Server.SayToChat(string.Join(" / ", this.Server.AllPlayers.Where(p => this.isZombie(p)).Select(p => $"{p.Name} is {(p.IsAlive ? "<color=\"green\">alive" : "<color=\"red\">dead")}<color=\"white\">")));
                this.Server.SayToChat("<b>==HUMANS==");
                this.Server.SayToChat(string.Join(" / ", this.Server.AllPlayers.Where(p => !this.isZombie(p)).Select(p => $"{p.Name} is {(p.IsAlive ? "<color=\"green\">alive" : "<color=\"red\">dead")}<color=\"white\">")));
                return false;
            }

            if (msg == "!fix")
            {
                this.Server.RoundSettings.PlayersToStart = this.configuration.RequiredPlayersToStart;
            }

            return true;
        }

        public override async Task OnGameStateChanged(GameState oldState, GameState newState)
        {
            await base.OnGameStateChanged(oldState, newState);
            Console.WriteLine($"Changed state to {newState}");

            if (oldState == newState)
            {
                return;
            }

            switch (newState)
            {
                case GameState.WaitingForPlayers:
                    this.Server.RoundSettings.PlayersToStart = this.configuration.RequiredPlayersToStart;
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
                player.Message("You have been infected and are now a zombie!");
            }

            await Task.CompletedTask;
        }

        public override async Task OnPlayerConnected(RunnerPlayer player)
        {
            await base.OnPlayerConnected(player);

            Console.WriteLine("Debug: OnPlayerConnected");
            this.setZombie(player, this.Server.AllPlayers.Count(p => this.isZombie(p)) < this.configuration.InitialZombieCount && player.Team == ZOMBIES);
            await this.forcePlayerToCorrectTeam(player);
            this.Server.SayToChat($"Welcome {player.Name} to the server!");
        }

        public override async Task OnPlayerDisconnected(RunnerPlayer player)
        {
            await base.OnPlayerDisconnected(player);

            Console.WriteLine("Debug: OnPlayerDisconnected");

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

            Console.WriteLine("Debug: OnPlayerChangeTeam");
            await this.forcePlayerToCorrectTeam(player);
        }
        #endregion

        #region Loadout and rank
        public override Task OnPlayerJoiningToServer(ulong steamID, PlayerJoiningArguments args)
        {
            args.Stats.Progress.Rank = 200;
            args.Stats.Progress.Prestige = 10;
            args.Stats.Roles = Roles.Vip;
            args.Stats.IsBanned = false;
            return Task.CompletedTask;
        }

        private List<ulong> allowedToSpawn = new();

        public override Task<OnPlayerSpawnArguments> OnPlayerSpawning(RunnerPlayer player, OnPlayerSpawnArguments request)
        {
            Console.WriteLine("Debug: OnPlayerSpawning, coordinates " + request.SpawnPosition.ToString());

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
                    player.Message("Suicide C4 is not allowed and was replaced by C4.");
                }

                if (request.Loadout.LightGadget == Gadgets.SuicideC4)
                {
                    request.Loadout.LightGadget = Gadgets.C4;
                    player.Message("Suicide C4 is not allowed and was replaced by C4.");
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

            //request.Wearings.Eye = ZOMBIE_EYES[Random.Shared.Next(0, ZOMBIE_EYES.Length)];
            //request.Wearings.Face = ZOMBIE_FACE[Random.Shared.Next(0, ZOMBIE_FACE.Length)];
            //request.Wearings.Hair = ZOMBIE_HAIR[Random.Shared.Next(0, ZOMBIE_HAIR.Length)];
            //request.Wearings.Skin = ZOMBIE_BODY[Random.Shared.Next(0, ZOMBIE_BODY.Length)];
            //request.Wearings.Uniform = ZOMBIE_UNIFORM[Random.Shared.Next(0, ZOMBIE_UNIFORM.Length)];
            //request.Wearings.Head = ZOMBIE_HELMET[Random.Shared.Next(0, ZOMBIE_HELMET.Length)];

            return Task.FromResult(request);
        }

        public override async Task OnPlayerSpawned(RunnerPlayer player)
        {
            await base.OnPlayerSpawned(player);

            Console.WriteLine("Debug: OnPlayerSpawned");
            if (player.Team != (this.isZombie(player) ? ZOMBIES : HUMANS))
            {
                player.Kill();
                await this.forcePlayerToCorrectTeam(player);
            }

            if (player.Team == ZOMBIES)
            {
                // Zombies are faster, jump higher, have more health and one-hit
                player.SetFallDamageMultiplier(0.0f);
                player.SetRunningSpeedMultiplier(1.2f); // TODO: anti cheat kicks because of this
                player.SetJumpMultiplier(2.5f);

                var ratio = (float)this.Server.AllPlayers.Count(p => this.isZombie(p)) / ((float)this.Server.AllPlayers.Count() - 1);
                var multiplier = this.configuration.ZombieMinDamageReceived + (this.configuration.ZombieMaxDamageReceived - this.configuration.ZombieMinDamageReceived) * ratio;
                player.SetReceiveDamageMultiplier(multiplier);
                await Console.Out.WriteLineAsync($"Damage received multiplier is set to " + multiplier);

                player.SetGiveDamageMultiplier(10f); // TODO: does not count for gadgets

                if (!this.allowedToSpawn.Contains(player.SteamID))
                {
                    player.Kill();
                    player.Message("<color=\"red\">You are only allowed to spawn on points.");
                }

                return;
            }

            // Humans are normal
            player.SetFallDamageMultiplier(1f);
            player.SetRunningSpeedMultiplier(1f);
            player.SetJumpMultiplier(1f);
            player.SetReceiveDamageMultiplier(1f);
            player.SetGiveDamageMultiplier(1f);
        }
        #endregion

        #region Zombie game logic
        List<ulong> turnPlayer = new();
        public override async Task OnAPlayerDownedAnotherPlayer(OnPlayerKillArguments<RunnerPlayer> playerKill)
        {
            Console.WriteLine("Debug: OnAPlayerDownedAnotherPlayer");

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
                if (Random.Shared.NextDouble() > this.configuration.SuicideZombieficationChance)
                {
                    return;
                }
                else
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(async () =>
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    {
                        RunnerPlayer player = playerKill.Victim;
                        int waitTime = Random.Shared.Next(this.configuration.SuicideZombieficationMaxTime);
                        await Task.Delay(waitTime / 2);
                        if (player.Team == HUMANS)
                        {
                            this.Server.SayToChat($"<b>{player.Name}<b> has been bitten by a <color=\"red\">zombie<color=\"white\">. Be careful around them!");
                            player.Message("You have been bitten by a zombie! Soon you will turn into one.");
                        }
                        else
                        {
                            return;
                        }

                        await Task.Delay(waitTime / 2);

                        if (player.Team == HUMANS)
                        {
                            this.turnPlayer.Add(player.SteamID);
                            this.setZombie(player, true);
                            player.ChangeTeam(ZOMBIES);
                            player.Message("You have been infected and are now a zombie!");
                            this.Server.SayToChat($"<b>{player.Name}<b> is now a <color=\"red\">zombie<color=\"white\">!");
                            await this.checkGameEnd();
                        }
                    });
                }
            }

            //if (this.discordWebhooks != null)
            //{
            //    this.discordWebhooks.SendMessage($"Player {playerKill.Victim.Name} died and has become a zombie.");
            //}
            // TODO: discord webhooks

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
            Console.WriteLine("Debug: checkGameEnd");

            if ((this.Server.RoundSettings.State != GameState.Playing) || safetyEnding)
            {
                Console.WriteLine($"Not ending because {this.Server.RoundSettings.State} / {safetyEnding}");
                return;
            }

            int humanCount = this.Server.AllPlayers.Count(player => !this.isZombie(player));

            await Console.Out.WriteLineAsync($"Human count is {humanCount}");

            if (humanCount == 0)
            {
                safetyEnding = true;
                this.Server.AnnounceLong("ZOMBIES WIN!");
                //if (this.discordWebhooks != null)
                //{
                //    this.discordWebhooks.SendMessage("== ZOMBIES WIN ==");
                //}
                // TODO: discord
                await Task.Delay(2000);
                this.Server.ForceEndGame();
                return;
            }

            if (amountOfHumansAnnounced > humanCount)
            {
                if (humanCount <= this.configuration.AnnounceLastHumansCount)
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

    }
}