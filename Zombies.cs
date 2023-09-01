using BattleBitAPI.Common;
using BattleBitAPI.Server;
using BattleBitBaseModules;
using BBRAPIModules;
using Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zombies
{
    [RequireModule(typeof(CommandHandler))]
    public class Zombies : BattleBitModule
    {
        #region STATIC SETTINGS
        internal const Team HUMANS = Team.TeamA;
        internal const Team ZOMBIES = Team.TeamB;

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

        private static readonly string[] HUMAN_FLASHLIGHTS = new[] { Attachments.Flashlight.Name, Attachments.Searchlight.Name, Attachments.TacticalFlashlight.Name };

        private static readonly ZombieClass[] zombieClasses = new[]
        {
            new ZombieClass("Tank", 1, p =>
            {
                p.Modifications.ReceiveDamageMultiplier = 0.01f;
            }),
            new ZombieClass("Boomer", 1, p =>
            {
                p.SetLightGadget(Gadgets.SuicideC4.Name, 1);
            }),
            new ZombieClass("Flasher", 2, p =>
            {
                p.SetThrowable(Gadgets.Flashbang.Name, 2);
            }),
            new ZombieClass("Hunter", 2, p =>
            {
                p.Modifications.RunningSpeedMultiplier = 3f;
            }),
            new ZombieClass("Jumper", 2, p =>
            {
                p.Modifications.JumpHeightMultiplier = 3f;
            }),
            new ZombieClass("Climber", 2, p =>
            {
                p.SetLightGadget(Gadgets.GrapplingHook.Name, 1);
            }),
            new ZombieClass("Shielded", 3, p =>
            {
                p.SetHeavyGadget(Gadgets.RiotShield.Name, 1);
            }),
        };

        private static readonly string[] allowedZombieMeleeGadgets = new[]
        {
            Gadgets.SledgeHammer.Name,
            Gadgets.SledgeHammerSkinA.Name,
            Gadgets.SledgeHammerSkinB.Name,
            Gadgets.SledgeHammerSkinC.Name,
            Gadgets.Pickaxe.Name,
            Gadgets.PickaxeIronPickaxe.Name
        };

        private static readonly string[] allowedZombieThrowables = new[]
        {
            Gadgets.SmokeGrenadeBlue.Name,
            Gadgets.SmokeGrenadeGreen.Name,
            Gadgets.SmokeGrenadeRed.Name,
            Gadgets.SmokeGrenadeWhite.Name
        };

        private static readonly WeaponItem emptyWeapon = new()
        {
            Barrel = null,
            BoltAction = null,
            CantedSight = null,
            MainSight = null,
            SideRail = null,
            Tool = null,
            TopSight = null,
            UnderRail = null
        };
        #endregion

        #region CONFIGURATION
        public ZombiesConfiguration Configuration { get; set; }
        public ZombiesState State { get; set; }
        #endregion

        #region MODULES
        [ModuleReference]
        public CommandHandler CommandHandler { get; set; }

        [ModuleReference]
        public dynamic? DiscordWebhooks { get; set; }

        [ModuleReference]
        public RichText RichText { get; set; }
        #endregion

        #region ZOMBIE PLAYERS
        private Dictionary<ulong, ZombiesPlayer> players = new();

        private ZombiesPlayer getPlayer(RunnerPlayer player)
        {
            if (!this.players.ContainsKey(player.SteamID))
            {
                this.players.Add(player.SteamID, new ZombiesPlayer(player));
            }

            return this.players[player.SteamID];
        }
        #endregion

        #region GAME STATE MANAGEMENT
        private async Task gameStateManagerWorker()
        {
            Stopwatch stopwatch = new Stopwatch();
            while (this.IsLoaded && this.Server.IsConnected)
            {
                stopwatch.Restart();
                await manageGameState();
                this.State.Save();
                stopwatch.Stop();
                int timeToWait = this.Configuration.GameStateUpdateTimer - (int)stopwatch.ElapsedMilliseconds;
                if (timeToWait > 0)
                {
                    await Task.Delay(timeToWait);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    await Console.Out.WriteLineAsync($"GameStateManager is running behind by {timeToWait}ms");
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// Handles changing of the game state
        /// </summary>
        /// <returns></returns>
        private async Task manageGameState()
        {
            ZombiesGameState oldState = this.State.GameState;

            // Transition to waiting for players, can transition from any state

            if (this.Server.RoundSettings.State == GameState.WaitingForPlayers && this.State.GameState != ZombiesGameState.WaitingForPlayers)
            {
                this.State.GameState = ZombiesGameState.WaitingForPlayers;
                await this.zombieGameStateChanged(oldState);
                return;
            }

            // Transition to countdown, can transition from any state

            if (this.Server.RoundSettings.State == GameState.CountingDown && this.State.GameState != ZombiesGameState.Countdown)
            {
                this.State.GameState = ZombiesGameState.Countdown;
                await this.zombieGameStateChanged(oldState);
                return;
            }

            // Transition to build phase, can only transition from countdown

            if (this.Server.RoundSettings.State == GameState.Playing && this.State.GameState == ZombiesGameState.Countdown)
            {
                this.State.GameState = ZombiesGameState.BuildPhase;
                await this.zombieGameStateChanged(oldState);
                return;
            }

            // Build phase management
            this.buildPhaseManagement();

            // Transition to playing, can transition from any state

            if (this.Server.RoundSettings.State == GameState.Playing && this.State.GameState != ZombiesGameState.GamePhase && !this.State.BuildPhase)
            {
                this.State.GameState = ZombiesGameState.GamePhase;
                await this.zombieGameStateChanged(oldState);
                return;
            }

            // Transition to zombie win

            if (this.Server.RoundSettings.State == GameState.Playing && this.State.GameState == ZombiesGameState.GamePhase && isZombieWin())
            {
                this.State.GameState = ZombiesGameState.ZombieWin;
                await this.zombieGameStateChanged(oldState);
                return;
            }

            // Transition to human win

            if (this.Server.RoundSettings.State == GameState.Playing && this.State.GameState == ZombiesGameState.GamePhase && isHumanWin())
            {
                this.State.GameState = ZombiesGameState.HumanWin;
                await this.zombieGameStateChanged(oldState);
                return;
            }

            // No transition, tick the current state
            this.zombieGameStateTick();
        }

        private void zombieGameStateTick()
        {
            switch (this.State.GameState)
            {
                case ZombiesGameState.WaitingForPlayers:
                    this.waitingForPlayersGameStateTick();
                    break;
                case ZombiesGameState.Countdown:
                    this.countdownGameStateTick();
                    break;
                case ZombiesGameState.BuildPhase:
                    this.buildPhaseGameStateTick();
                    break;
                case ZombiesGameState.GamePhase:
                    this.gamePhaseGameStateTick();
                    break;
                case ZombiesGameState.ZombieWin:
                    this.zombieWinGameStateTick();
                    break;
                case ZombiesGameState.HumanWin:
                    this.humanWinGameStateTick();
                    break;
                case ZombiesGameState.Ended:
                    this.endedGameStateTick();
                    break;
                default:
                    break;
            }
        }

        private async Task zombieGameStateChanged(ZombiesGameState oldState)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"Game state changed from {oldState} to {this.State.GameState}");
            Console.ResetColor();

            switch (this.State.GameState)
            {
                case ZombiesGameState.WaitingForPlayers:
                    await waitingForPlayersGameState();
                    break;
                case ZombiesGameState.Countdown:
                    await countdownGameState();
                    break;
                case ZombiesGameState.BuildPhase:
                    await buildPhaseGameState();
                    break;
                case ZombiesGameState.GamePhase:
                    await gamePhaseGameState();
                    break;
                case ZombiesGameState.ZombieWin:
                    await zombieWinGameState();
                    break;
                case ZombiesGameState.HumanWin:
                    await humanWinGameState();
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unhandled game state!");
                    Console.ResetColor();
                    break;
            }
        }
        #endregion

        #region GAME STATE HANDLERS
        #region GAME STATE CHANGED HANDLERS
        #region WAITING FOR PLAYERS
        private async Task waitingForPlayersGameState()
        {
            this.Server.RoundSettings.PlayersToStart = this.Configuration.RequiredPlayersToStart;

            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                await this.applyWaitingForPlayersRuleSetToPlayer(player);
            }
        }
        #endregion

        #region COUNTDOWN
        private async Task countdownGameState()
        {
            //RunnerPlayer[] initialZombies = await initialZombiePopulation();
            //foreach (RunnerPlayer player in initialZombies)
            //{
            //    await this.makePlayerZombie(player);
            //}

            this.Server.RoundSettings.SecondsLeft = this.Configuration.CountdownPhaseDuration;

            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                applyCountdownRuleSetToPlayer(player);
            }

            await Task.CompletedTask;
        }
        #endregion

        #region BUILD PHASE
        private async Task buildPhaseGameState()
        {
            // Ruleset for build phase:
            // - All human squads receive BuildPhaseSquadPoints build points
            // - All squads can not make squad points by capturing/killing
            // - All zombie squads have 0 build points

            this.State.BuildPhase = true;
            this.State.EndOfBuildPhase = DateTime.Now.AddSeconds(this.Configuration.BuildPhaseDuration);

            this.Server.AnnounceLong($"Human build phase has started! Zombies will arrive in {(int)(this.State.EndOfBuildPhase - DateTime.Now).TotalSeconds} seconds!");

            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                await this.applyBuildPhaseRuleSetToPlayer(player);
            }

            foreach (Squad<RunnerPlayer> squad in this.Server.AllSquads)
            {
                if (squad.Team == HUMANS)
                {
                    squad.SquadPoints = this.Configuration.BuildPhaseSquadPoints;
                    this.setLastHumanSquadPoints(squad.Name, squad.SquadPoints);
                }
                else
                {
                    squad.SquadPoints = 0;
                }
            }
        }

        private void buildPhaseManagement()
        {
            if (this.State.GameState != ZombiesGameState.BuildPhase && this.State.BuildPhase)
            {
                this.State.BuildPhase = false;
                return;
            }

            if (this.State.EndOfBuildPhase <= DateTime.Now)
            {
                this.State.BuildPhase = false;
                this.State.EndOfBuildPhase = DateTime.MinValue;
                return;
            }

            int secondsLeft = (int)(this.State.EndOfBuildPhase - DateTime.Now).TotalSeconds;

            Console.WriteLine($"{secondsLeft} ({secondsLeft % 10})");

            if ((secondsLeft % 10) == 0)
            {
                this.Server.SayToAllChat($"Zombies will arrive in {secondsLeft} seconds!");
            }

            if (this.State.EndOfBuildPhase.AddSeconds(-10) < DateTime.Now)
            {
                this.Server.AnnounceShort($"Zombies will arrive in {this.State.EndOfBuildPhase.Subtract(DateTime.Now).TotalSeconds:0} seconds!");
            }
        }
        #endregion

        #region GAME PHASE
        private async Task gamePhaseGameState()
        {
            // Ruleset for game phase:
            // - All human squads will have their squad points set to GamePhaseSquadPoints
            // - Squads can not make squad points by capturing/killing
            // - Human squad points are set to GamePhaseSquadPoints
            // - Zombies can deploy
            // - Zombies are unfrozen
            // - A random population of humans will turn into zombies in the midst of the humans at a random time interval between 0 and ZombieMaxInfectionTime ms

            this.Server.RoundSettings.SecondsLeft = this.Configuration.GamePhaseDuration;

            foreach (Squad<RunnerPlayer> squad in this.Server.AllSquads.Where(s => s.Team == HUMANS))
            {
                squad.SquadPoints = this.Configuration.GamePhaseSquadPoints;
                this.setLastHumanSquadPoints(squad.Name, squad.SquadPoints);
            }

            this.Server.AnnounceShort($"The infection is starting!");

            RunnerPlayer[] initialZombies = await initialZombiePopulation();
            foreach (RunnerPlayer player in initialZombies)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(Random.Shared.Next(this.Configuration.ZombieMaxInfectionTime));
                    await this.makePlayerZombie(player);
                    this.Server.UILogOnServer($"{player.Name} has been turned into a zombie!", 10);
                });
            }
        }
        #endregion

        #region END OF GAME
        private async Task zombieWinGameState()
        {
            this.Server.AnnounceShort($"{RichText?.FromColorName("red")}ZOMBIES WIN");
            this.Server.ForceEndGame(ZOMBIES);
            this.State.GameState = ZombiesGameState.Ended;

            await Task.CompletedTask;
        }

        private async Task humanWinGameState()
        {
            this.Server.AnnounceShort($"{RichText?.FromColorName("blue")}HUMANS WIN");
            this.Server.ForceEndGame(HUMANS);
            this.State.GameState = ZombiesGameState.Ended;

            await Task.CompletedTask;
        }

        private bool isZombieWin()
        {
            if (this.actualHumanCount == 0)
            {
                return true;
            }

            return false;
        }

        private bool isHumanWin()
        {
            if (this.Server.RoundSettings.SecondsLeft <= 2)
            {
                return true;
            }

            return false;
        }
        #endregion

        #endregion

        #region GAME STATE TICK HANDLERS
        private void endedGameStateTick()
        {
        }

        private void humanWinGameStateTick()
        {
        }

        private void zombieWinGameStateTick()
        {
        }

        private void gamePhaseGameStateTick()
        {
            this.exposeHumansOnMap();
            this.announceHumanCount();

            this.humanLoadoutHandler();
            this.zombieLoadoutHandler();
        }


        private void buildPhaseGameStateTick()
        {
            this.humanLoadoutHandler();
            this.zombieLoadoutHandler();
        }

        private void countdownGameStateTick()
        {
        }

        private void waitingForPlayersGameStateTick()
        {
        }
        #endregion
        #endregion

        #region SERVER CALLBACKS

        public override void OnModulesLoaded()
        {
            this.CommandHandler.Register(this);
        }
        public override async Task OnConnected()
        {
            _ = Task.Run(gameStateManagerWorker);

            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                await OnPlayerConnected(player);
            }

            await Task.CompletedTask;
        }

        public override async Task OnPlayerConnected(RunnerPlayer player)
        {
            // Ruleset:
            // - Enforce team based on game state
            // - Enforce ruleset based on game state

            if (this.State.GameState == ZombiesGameState.BuildPhase ||
                this.State.GameState == ZombiesGameState.Countdown ||
                this.State.GameState == ZombiesGameState.WaitingForPlayers)
            {
                if (player.Team != HUMANS)
                {
                    player.ChangeTeam(HUMANS);
                }

                await this.applyHumanRuleSetToPlayer(player);
                await this.applyBuildPhaseRuleSetToPlayer(player);

                return;
            }

            if (this.State.GameState == ZombiesGameState.GamePhase)
            {
                if (player.Team == HUMANS)
                {
                    player.ChangeTeam(ZOMBIES);
                }

                await this.applyZombieRuleSetToPlayer(player);

                return;
            }
        }

        public override Task<bool> OnPlayerRequestingToChangeTeam(RunnerPlayer player, Team requestedTeam)
        {
            // Ruleset:
            // - Do not allow players to change teams

            return Task.FromResult(false);
        }

        public override Task OnPlayerJoiningToServer(ulong steamID, PlayerJoiningArguments args)
        {
            // Ruleset:
            // - Set player rank to 200
            // - Set player prestige to 10

            args.Stats.Progress.Rank = 200;
            args.Stats.Progress.Prestige = 10;
            return Task.CompletedTask;
        }

        public override async Task<OnPlayerSpawnArguments?> OnPlayerSpawning(RunnerPlayer player, OnPlayerSpawnArguments request)
        {
            if (player.Team == HUMANS)
            {
                // Ruleset for humans:
                // - Set randomized human survivor skin
                // - If night, force flashlight on primary and secondary but keep existing flashlight
                // - Apply human ruleset based on game state

                // Human skins
                request.Wearings.Uniform = HUMAN_UNIFORM[Random.Shared.Next(HUMAN_UNIFORM.Length)];
                request.Wearings.Head = HUMAN_HELMET[Random.Shared.Next(HUMAN_HELMET.Length)];
                request.Wearings.Backbag = HUMAN_BACKPACK[Random.Shared.Next(HUMAN_BACKPACK.Length)];
                request.Wearings.Chest = HUMAN_ARMOR[Random.Shared.Next(HUMAN_ARMOR.Length)];

                if (this.Server.DayNight == MapDayNight.Night)
                {
                    if (!HUMAN_FLASHLIGHTS.Contains(request.Loadout.PrimaryWeapon.SideRail.Name))
                    {
                        request.Loadout.PrimaryWeapon.SideRail = new Attachment(HUMAN_FLASHLIGHTS[Random.Shared.Next(HUMAN_FLASHLIGHTS.Length)], AttachmentType.SideRail);
                    }

                    if (!HUMAN_FLASHLIGHTS.Contains(request.Loadout.SecondaryWeapon.SideRail.Name))
                    {
                        request.Loadout.SecondaryWeapon.SideRail = new Attachment(HUMAN_FLASHLIGHTS[Random.Shared.Next(HUMAN_FLASHLIGHTS.Length)], AttachmentType.SideRail);
                    }
                }

                switch (this.State.GameState)
                {
                    case ZombiesGameState.GamePhase:
                    case ZombiesGameState.Countdown:
                        await this.applyHumanRuleSetToPlayer(player);
                        break;
                    case ZombiesGameState.BuildPhase:
                        await this.applyBuildPhaseRuleSetToPlayer(player);
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Human spawned during game state {this.State.GameState} which is ignored.");
                        Console.ResetColor();
                        break;
                }

                if (this.State.GameState == ZombiesGameState.Countdown ||
                    this.State.GameState == ZombiesGameState.BuildPhase ||
                    this.State.GameState == ZombiesGameState.GamePhase)
                {
                    // Only allow for humans to spawn with the loadout they are supposed to have
                    double humanRatio = this.actualHumanCount / this.Server.AllPlayers.Count();

                    ZombiesPlayer human = this.getPlayer(player);

                    if (humanRatio <= this.Configuration.HumanRatioThrowable)
                    {
                        human.ReceivedThrowable = true;
                    }
                    else
                    {
                        request.Loadout.Throwable = null;
                    }

                    if (humanRatio <= this.Configuration.HumanRatioPrimary)
                    {
                        human.ReceivedPrimary = true;
                    }
                    else
                    {
                        request.Loadout.PrimaryWeapon.Tool = null;
                    }

                    if (humanRatio <= this.Configuration.HumanRatioLightGadget)
                    {
                        human.ReceivedLightGadget = true;
                    }
                    else
                    {
                        request.Loadout.LightGadget = null;
                    }

                    if (humanRatio <= this.Configuration.HumanRatioHeavyGadget)
                    {
                        human.ReceivedHeavyGadget = true;
                    }
                    else
                    {
                        request.Loadout.HeavyGadget = null;
                    }
                }

                return request;
            }

            // Ruleset for zombies:
            // - Set zombie skin
            // - Apply loadout for zombie
            // - Apply zombie ruleset based on game state

            request.Wearings.Eye = ZOMBIE_EYES[Random.Shared.Next(ZOMBIE_EYES.Length)];
            request.Wearings.Face = ZOMBIE_FACE[Random.Shared.Next(ZOMBIE_FACE.Length)];
            request.Wearings.Hair = ZOMBIE_HAIR[Random.Shared.Next(ZOMBIE_HAIR.Length)];
            request.Wearings.Skin = ZOMBIE_BODY[Random.Shared.Next(ZOMBIE_BODY.Length)];
            request.Wearings.Uniform = ZOMBIE_UNIFORM[Random.Shared.Next(ZOMBIE_UNIFORM.Length)];
            request.Wearings.Head = ZOMBIE_HELMET[Random.Shared.Next(ZOMBIE_HELMET.Length)];
            request.Wearings.Chest = ZOMBIE_ARMOR[Random.Shared.Next(ZOMBIE_ARMOR.Length)];
            request.Wearings.Backbag = ZOMBIE_BACKPACK[Random.Shared.Next(ZOMBIE_BACKPACK.Length)];
            request.Wearings.Belt = ZOMBIE_BELT[Random.Shared.Next(ZOMBIE_BELT.Length)];

            if (request.Loadout.PrimaryWeapon.Tool is not null)
            {
                request.Loadout.PrimaryWeapon.Tool = null;
                request.Loadout.PrimaryExtraMagazines = 0;
            }

            if (request.Loadout.SecondaryWeapon.Tool is not null)
            {
                request.Loadout.SecondaryWeapon.Tool = null;
                request.Loadout.SecondaryExtraMagazines = 0;
            }

            if (!allowedZombieMeleeGadgets.Contains(request.Loadout.HeavyGadgetName))
            {
                request.Loadout.HeavyGadget = Gadgets.SledgeHammer;
                request.Loadout.HeavyGadgetExtra = 0;
            }

            if (!allowedZombieMeleeGadgets.Contains(request.Loadout.LightGadgetName))
            {
                request.Loadout.LightGadget = Gadgets.Pickaxe;
                request.Loadout.LightGadgetExtra = 0;
            }

            if (!allowedZombieThrowables.Contains(request.Loadout.ThrowableName))
            {
                request.Loadout.Throwable = Gadgets.SmokeGrenadeRed;
                request.Loadout.ThrowableExtra = 10;
            }

            request.Loadout.FirstAid = Gadgets.Bandage;
            request.Loadout.FirstAidExtra = 0;

            switch (this.State.GameState)
            {
                case ZombiesGameState.Countdown:
                case ZombiesGameState.BuildPhase:
                case ZombiesGameState.GamePhase:
                    await this.applyZombieRuleSetToPlayer(player);
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Zombie spawned during game state {this.State.GameState} which is ignored.");
                    Console.ResetColor();
                    break;
            }

            return request;
        }

        public override async Task OnPlayerSpawned(RunnerPlayer player)
        {
            if (player.Team == HUMANS)
            {
                await this.applyHumanRuleSetToPlayer(player);

                return;
            }

            await this.applyZombieRuleSetToPlayer(player);
        }

        public override Task OnPlayerDisconnected(RunnerPlayer player)
        {
            if (this.players.ContainsKey(player.SteamID))
            {
                this.players.Remove(player.SteamID);
            }

            return Task.CompletedTask;
        }

        public override async Task OnPlayerDied(RunnerPlayer player)
        {
            if (player.Team == ZOMBIES)
            {
                return;
            }

            if (this.State.GameState != ZombiesGameState.GamePhase)
            {
                return;
            }

            await this.makePlayerZombie(player);

            await Task.CompletedTask;
        }

        public override Task OnSessionChanged(long oldSessionID, long newSessionID)
        {
            this.State.Reset();
            this.players.Clear();

            return Task.CompletedTask;
        }
        public override async Task OnSquadPointsChanged(Squad<RunnerPlayer> squad, int newPoints)
        {
            // Ruleset for squad point changes:
            // - Zombies can never have squad points
            // - Humans can not make squad points by capturing/killing

            if (squad.Team == ZOMBIES)
            {
                squad.SquadPoints = 0;
                return;
            }

            if (this.getLastHumanSquadPoints(squad.Name) < newPoints)
            {
                squad.SquadPoints = this.getLastHumanSquadPoints(squad.Name);
                return;
            }

            this.setLastHumanSquadPoints(squad.Name, newPoints);

            await Task.CompletedTask;
        }
        #endregion

        #region HELPER METHODS
        private int actualHumanCount => this.Server.AllPlayers.Count(player => player.Team == HUMANS && !player.IsDown && player.IsAlive);

        private Task<RunnerPlayer[]> initialZombiePopulation()
        {
            List<RunnerPlayer> zombies = new();

            // Initial zombies selection
            List<RunnerPlayer> players = this.Server.AllPlayers.Where(p => p.Team == HUMANS).ToList();
            int initialZombieCount = this.Configuration.InitialZombieCount;

            // If initial zombie count is greater than initial zombie maximum percentage, set it to the maximum percentage
            int maxAmountOfZombies = (int)(players.Count * (this.Configuration.InitialZombieMaxPercentage / 100.0));
            if (initialZombieCount > maxAmountOfZombies)
            {
                initialZombieCount = maxAmountOfZombies;
            }

            for (int i = 0; i < initialZombieCount; i++)
            {
                // TODO: maybe add ability for players to pick a preference of being a zombie or human
                int randomIndex = Random.Shared.Next(players.Count);
                RunnerPlayer player = players[randomIndex];
                players.RemoveAt(randomIndex);
                zombies.Add(player);
            }

            return Task.FromResult(zombies.ToArray());
        }

        private int getLastHumanSquadPoints(Squads humanSquad)
        {
            if (!this.State.LastSquadPoints.ContainsKey(humanSquad))
            {
                this.State.LastSquadPoints.Add(humanSquad, 0);
            }

            return this.State.LastSquadPoints[humanSquad];
        }

        private void setLastHumanSquadPoints(Squads humanSquad, int points)
        {
            if (!this.State.LastSquadPoints.ContainsKey(humanSquad))
            {
                this.State.LastSquadPoints.Add(humanSquad, 0);
            }

            this.State.LastSquadPoints[humanSquad] = points;
        }

        private Task<Squad<RunnerPlayer>> findFirstNonEmptySquad(Team team)
        {
            foreach (Squad<RunnerPlayer> squad in this.Server.AllSquads.Where(s => s.Team == team))
            {
                if (squad.NumberOfMembers < 8)
                {
                    return Task.FromResult(squad);
                }
            }

            // No free squads available, this is impossible (8 players * 64 squads = 512 players which is more than the max player count of 254)
            throw new Exception("No free squads available");
        }

        private async Task makePlayerZombie(RunnerPlayer player)
        {
            if (player.Team == ZOMBIES)
            {
                return;
            }

            player.ChangeTeam(ZOMBIES);
            await this.applyZombieRuleSetToPlayer(player);
            player.Message($"You have been turned into a {RichText?.FromColorName("red")}ZOMBIE{RichText?.FromColorName("white")}.", 10);

            await Task.CompletedTask;
        }
        #endregion

        #region RULE SETS
        #region GAME STATE RULE SETS
        private async Task applyWaitingForPlayersRuleSetToPlayer(RunnerPlayer player)
        {
            // Ruleset for waiting for players:
            // - All players are human
            // - All players can not deploy
            // - All players are frozen

            player.ChangeTeam(HUMANS);
            player.Modifications.CanDeploy = false;
            player.Modifications.Freeze = true;

            await Task.CompletedTask;
        }

        private void applyCountdownRuleSetToPlayer(RunnerPlayer player)
        {
            // Ruleset for countdown:
            // - Humans can deploy
            // - Humans are unfrozen

            if (player.Team == HUMANS)
            {
                player.Modifications.CanDeploy = true;
                player.Modifications.Freeze = false;
            }
        }

        private async Task applyBuildPhaseRuleSetToPlayer(RunnerPlayer player)
        {
            // Ruleset for build phase:
            // - Humans can deploy
            // - Humans are unfrozen
            // - Zombies can not deploy
            // - Zombies are frozen
            // - All humans must be in a squad

            if (player.Team == HUMANS)
            {
                await this.applyHumanRuleSetToPlayer(player);

                player.Modifications.CanDeploy = true;
                player.Modifications.Freeze = false;

                if (player.Squad.Name == Squads.NoSquad)
                {
                    Squad<RunnerPlayer> targetSquad = await this.findFirstNonEmptySquad(player.Team);
                    player.JoinSquad(targetSquad.Name);
                }
            }
            else if (player.Team == ZOMBIES)
            {
                await this.applyZombieRuleSetToPlayer(player);

                player.Modifications.CanDeploy = false;
                player.Modifications.Freeze = true;
            }
        }
        #endregion

        #region PLAYER RULE SETS
        private async Task applyHumanRuleSetToPlayer(RunnerPlayer player)
        {
            // Ruleset for humans:
            // - Humans are all default
            // - Humans can not suicide
            // - Humans can not use vehicles
            // - Humans can not use NVGs
            // - Humans do not see friendly HUD indicators
            // - Humans do not have hitmarkers
            // - Humans can not heal using bandages
            // - Humans can not revive
            // - Humans can spawn anywhere except vehicles

            player.Modifications.AirStrafe = true;
            player.Modifications.AllowedVehicles = VehicleType.None;
            player.Modifications.CanUseNightVision = false;
            player.Modifications.FallDamageMultiplier = 1f;
            player.Modifications.FriendlyHUDEnabled = false;
            player.Modifications.GiveDamageMultiplier = 1f;
            player.Modifications.HitMarkersEnabled = false;
            player.Modifications.HpPerBandage = 0f;
            player.Modifications.JumpHeightMultiplier = 1f;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Initial MinimumDamageToStartBleeding is {player.Modifications.MinimumDamageToStartBleeding}");
            Console.ResetColor();
            player.Modifications.MinimumDamageToStartBleeding = 30f; // TODO: get initial value
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Initial MinimumHpToStartBleeding is {player.Modifications.MinimumHpToStartBleeding}");
            Console.ResetColor();
            player.Modifications.MinimumHpToStartBleeding = 30f; // TODO: get initial value
            player.Modifications.ReceiveDamageMultiplier = 1f;
            player.Modifications.ReloadSpeedMultiplier = 1f;
            player.Modifications.ReviveHP = 0;
            player.Modifications.RunningSpeedMultiplier = 1f;
            player.Modifications.SpawningRule = HUMANS_SPAWN_RULE;

            player.Modifications.CanDeploy = true;
            player.Modifications.Freeze = false;

            await Task.CompletedTask;
        }

        private async Task applyZombieRuleSetToPlayer(RunnerPlayer player)
        {
            // Ruleset for zombies:
            // - Zombies can not have a primary weapon
            // - Zombies can not have a secondary weapon
            // - Zombies can not have a gadget
            // - Zombies must have a melee weapon
            // - Zombies can only have smoke grenades
            // - Zombies can not have bandages
            // - Zombies can not revive or heal
            // - Zombies do not bleed
            // - Zombies are faster
            // - Zombies jump higher
            // - Zombies can suicide
            // - Zombies can not use vehicles
            // - Zombies can use NVGs
            // - Zombies have adjusted incoming damage
            // - Zombies can see friendly HUD indicators
            // - Zombies have hitmarkers
            // - Zombies may have classes that change these rules
            // - Zombies can only spawn on points and squad mates

            if (player.CurrentLoadout.PrimaryWeapon.Tool is not null)
            {
                player.SetPrimaryWeapon(emptyWeapon, 0);
            }

            if (player.CurrentLoadout.SecondaryWeapon.Tool is not null)
            {
                player.SetSecondaryWeapon(emptyWeapon, 0);
            }

            if (!allowedZombieMeleeGadgets.Contains(player.CurrentLoadout.HeavyGadgetName))
            {
                player.SetHeavyGadget(Gadgets.SledgeHammer.Name, 0);
            }

            if (!allowedZombieMeleeGadgets.Contains(player.CurrentLoadout.LightGadgetName))
            {
                player.SetLightGadget(Gadgets.Pickaxe.Name, 0);
            }

            if (!allowedZombieThrowables.Contains(player.CurrentLoadout.ThrowableName))
            {
                player.SetThrowable(Gadgets.SmokeGrenadeBlue.Name, 10);
            }

            player.SetFirstAidGadget(Gadgets.Bandage.Name, 0);

            player.Modifications.AirStrafe = true;
            player.Modifications.AllowedVehicles = VehicleType.None;
            player.Modifications.CanUseNightVision = true;
            player.Modifications.FallDamageMultiplier = 0f;
            player.Modifications.FriendlyHUDEnabled = true;
            player.Modifications.GiveDamageMultiplier = 1f;
            player.Modifications.HitMarkersEnabled = true;
            player.Modifications.HpPerBandage = 0f;
            player.Modifications.JumpHeightMultiplier = this.Configuration.ZombieJumpHeightMultiplier;
            player.Modifications.MinimumDamageToStartBleeding = 100f;
            player.Modifications.MinimumHpToStartBleeding = 0;
            player.Modifications.ReceiveDamageMultiplier = 1f;
            player.Modifications.ReloadSpeedMultiplier = 1f;
            player.Modifications.ReviveHP = 0;
            player.Modifications.RunningSpeedMultiplier = this.Configuration.ZombieRunningSpeedMultiplier;
            player.Modifications.SpawningRule = ZOMBIES_SPAWN_RULE;

            if (this.State.GameState == ZombiesGameState.GamePhase)
            {
                player.Modifications.CanDeploy = true;
                player.Modifications.Freeze = false;
            }
            else
            {
                player.Modifications.CanDeploy = false;
                player.Modifications.Freeze = true;
            }

            await Task.CompletedTask;
        }
        #endregion
        #endregion

        #region LOADOUT HANDLERS
        private void zombieLoadoutHandler()
        {
            // Ruleset:
            // - Maximum of ZombieClassRatio % of zombies can have a class
            // - If there are more zombies than the ratio, the rest will be normal zombies
            // - Classes are limited by amount of zombies with that class

            ZombiesPlayer[] classedZombies = this.Server.AllPlayers.Where(p => p.Team == ZOMBIES && !p.IsDown && p.IsAlive).Select(p => this.getPlayer(p)).Where(p => p.ZombieClass is not null).ToArray();
            ZombieClass[] availableClasses = zombieClasses.Where(c => classedZombies.Count(z => z.ZombieClass == c) < c.RequestedAmount).ToArray();

            if (availableClasses.Length == 0)
            {
                return;
            }

            double classedZombiesRatio = (double)classedZombies.Length / this.Server.AllPlayers.Count(player => player.Team == ZOMBIES);
            if (classedZombiesRatio >= this.Configuration.ZombieClassRatio)
            {
                return;
            }

            ZombiesPlayer[] classCandidates = this.Server.AllPlayers.Where(p => p.Team == ZOMBIES && !p.IsDown && p.IsAlive).Select(p => this.getPlayer(p)).Where(p => p.ZombieClass is null).ToArray();
            if (classCandidates.Length == 0)
            {
                return;
            }

            ZombiesPlayer candidate = classCandidates[Random.Shared.Next(classCandidates.Length)];
            ZombieClass zombieClass = availableClasses[Random.Shared.Next(availableClasses.Length)];

            zombieClass.ApplyToPlayer(candidate.Player);
            candidate.Player.Message($"{this.RichText?.Size(120)}{this.RichText?.FromColorName("yellow")}YOU HAVE MUTATED!{this.RichText?.NewLine() ?? " "}{this.RichText?.Size(100)}{this.RichText?.Color()}You are a {this.RichText?.FromColorName("red")}{zombieClass.Name} {this.RichText?.Color()}zombie now! Go fuck shit up!", 15);
        }

        private void humanLoadoutHandler()
        {
            foreach (RunnerPlayer player in this.Server.AllPlayers.Where(p => p.Team == HUMANS))
            {
                ZombiesPlayer human = this.getPlayer(player);
                if (!human.ReceivedPrimary || !human.ReceivedHeavyGadget || !human.ReceivedLightGadget || !human.ReceivedThrowable)
                {
                    this.applyHumanLoadoutToPlayer(player);
                }
            }
        }

        private void applyHumanLoadoutToPlayer(RunnerPlayer player)
        {
            double humanRatio = this.actualHumanCount / this.Server.AllPlayers.Count();
            ZombiesPlayer human = this.getPlayer(player);
            if (human.InitialLoadout == null)
            {
                human.InitialLoadout = new()
                {
                    HeavyGadget = Gadgets.GrapplingHook,
                    HeavyGadgetExtra = 1,
                    LightGadget = Gadgets.SmallAmmoKit,
                    LightGadgetExtra = 1,
                    PrimaryWeapon = new WeaponItem()
                    {
                        Barrel = Attachments.Compensator,
                        BoltAction = null,
                        CantedSight = null,
                        MainSight = Attachments.RedDot,
                        SideRail = this.Server.DayNight == MapDayNight.Night ? new Attachment(HUMAN_FLASHLIGHTS[Random.Shared.Next(HUMAN_FLASHLIGHTS.Length)], AttachmentType.SideRail) : Attachments.Redlaser,
                        Tool = Weapons.M4A1,
                        TopSight = null,
                        UnderRail = Attachments.VerticalGrip
                    },
                    PrimaryExtraMagazines = 4,
                    Throwable = Gadgets.ImpactGrenade
                };
            }

            if (humanRatio <= this.Configuration.HumanRatioThrowable && !human.ReceivedThrowable)
            {
                player.SayToChat($"{this.RichText?.FromColorName("green")}You received your throwable!");
                player.SetThrowable(human.InitialLoadout.Value.ThrowableName, human.InitialLoadout.Value.ThrowableExtra);
                human.ReceivedThrowable = true;
            }

            if (humanRatio <= this.Configuration.HumanRatioLightGadget && !human.ReceivedLightGadget)
            {
                player.SayToChat($"{this.RichText?.FromColorName("green")}You received your light gadget!");
                player.SetLightGadget(human.InitialLoadout.Value.LightGadgetName, human.InitialLoadout.Value.LightGadgetExtra);
                human.ReceivedLightGadget = true;
            }

            if (humanRatio <= this.Configuration.HumanRatioHeavyGadget && !human.ReceivedHeavyGadget)
            {
                player.SayToChat($"{this.RichText?.FromColorName("green")}You received your heavy gadget!");
                player.SetHeavyGadget(human.InitialLoadout.Value.HeavyGadgetName, human.InitialLoadout.Value.HeavyGadgetExtra);
                human.ReceivedHeavyGadget = true;
            }

            if (humanRatio <= this.Configuration.HumanRatioPrimary && !human.ReceivedPrimary)
            {
                player.SayToChat($"{this.RichText?.FromColorName("green")}You received your primary weapon!");
                player.SetPrimaryWeapon(human.InitialLoadout.Value.PrimaryWeapon, human.InitialLoadout.Value.PrimaryExtraMagazines);
                human.ReceivedPrimary = true;
            }
        }
        #endregion

        #region ACTIONS
        private void announceHumanCount()
        {
            int humanCount = this.actualHumanCount;

            if (this.State.LastHumansAnnounced > humanCount)
            {
                if (humanCount <= this.Configuration.AnnounceLastHumansCount)
                {
                    if (humanCount == 1)
                    {
                        RunnerPlayer? lastHuman = this.Server.AllPlayers.FirstOrDefault(p => p.Team == HUMANS && !p.IsDown && p.IsAlive);
                        if (lastHuman != null)
                        {
                            this.Server.AnnounceShort($"<b>{lastHuman.Name}<b> is the LAST HUMAN, {this.RichText?.FromColorName("red")}KILL IT!");
                            this.Server.SayToAllChat($"<b>{lastHuman.Name}<b> is the LAST HUMAN, {this.RichText?.FromColorName("red")}KILL IT!");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("No last human found");
                            Console.ResetColor();

                            this.Server.AnnounceShort($"LAST HUMAN, {this.RichText?.FromColorName("red")}KILL IT!");
                            this.Server.SayToAllChat($"{this.RichText?.Size(110)}LAST HUMAN, {this.RichText?.FromColorName("red")}KILL IT!");
                        }
                    }
                    else
                    {
                        this.Server.SayToAllChat($"{humanCount} HUMANS LEFT, {this.RichText?.FromColorName("red")}KILL THEM!");
                    }
                }
            }

            this.Configuration.AnnounceLastHumansCount = humanCount;
        }

        private void exposeHumansOnMap()
        {
            if (DateTime.Now >= this.State.NextHumanExposeSwitch)
            {
                if (this.State.ExposeOnMap)
                {
                    this.State.NextHumanExposeSwitch = DateTime.Now.AddSeconds(this.Configuration.HumanExposeOffTime);
                    this.State.ExposeOnMap = false;
                }
                else
                {
                    this.State.NextHumanExposeSwitch = DateTime.Now.AddSeconds(this.Configuration.HumanExposeOnTime);
                    this.State.ExposeOnMap = true;
                }

                foreach (RunnerPlayer player in this.Server.AllPlayers)
                {
                    if (player.Team == ZOMBIES)
                    {
                        player.Modifications.IsExposedOnMap = false;
                        continue;
                    }

                    player.Modifications.IsExposedOnMap = this.State.ExposeOnMap;
                }
            }
        }


        private async Task applyServerSettings()
        {
            this.Server.SetServerSizeForNextMatch(MapSize._64vs64);
            this.Server.GamemodeRotation.SetRotation("FRONTLINE");
            this.Server.ServerSettings.UnlockAllAttachments = true;

            await Task.CompletedTask;
        }
        #endregion

        #region COMMANDS
        // Player commands

        [CommandCallback("list", Description = "List all players and their status")]
        public void ListCommand(RunnerPlayer player)
        {
            StringBuilder sb = new();
            sb.AppendLine("<b>==ZOMBIES==</b>");
            sb.AppendLine(string.Join(" / ", this.Server.AllPlayers.Where(p => p.Team == ZOMBIES).Select(p => $"{p.Name} is {(p.IsAlive ? "<color=\"green\">alive" : "<color=\"red\">dead")}<color=\"white\">")));
            sb.AppendLine("<b>==HUMANS==</b>");
            sb.AppendLine(string.Join(" / ", this.Server.AllPlayers.Where(p => p.Team == HUMANS).Select(p => $"{p.Name} is {(p.IsAlive ? "<color=\"green\">alive" : "<color=\"red\">dead")}<color=\"white\">")));
            player.Message(sb.ToString());
        }

        [CommandCallback("zombie", Description = "Check whether you're a zombie or not")]
        public void ZombieCommand(RunnerPlayer player)
        {
            this.Server.SayToAllChat($"{player.Name} is {(player.Team == ZOMBIES ? "a" : "not a")} zombie");
        }

        // Moderator/admin commands

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
                    this.Configuration.ZombieRunningSpeedMultiplier = value;
                    break;
                case BalanceVariable.JumpHeightMultiplier:
                    this.Configuration.ZombieJumpHeightMultiplier = value;
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
            Team newTeam = target.Team == ZOMBIES ? HUMANS : ZOMBIES;
            target.Kill();
            target.ChangeTeam(newTeam);

            switch (this.State.GameState)
            {
                case ZombiesGameState.Countdown:
                case ZombiesGameState.BuildPhase:
                case ZombiesGameState.GamePhase:
                    if (newTeam == ZOMBIES)
                    {
                        await this.applyZombieRuleSetToPlayer(target);
                    }
                    else
                    {
                        await this.applyHumanRuleSetToPlayer(target);
                    }
                    break;
            }
        }

        [CommandCallback("afk", Description = "Make zombies win because humans camp or are AFK", AllowedRoles = Roles.Moderator)]
        public async void LastHumanAFKOrCamping(RunnerPlayer caller)
        {
            if (this.Server.AllPlayers.Count(p => p.Team == HUMANS) > 10)
            {
                caller.Message("There are too many humans to end the game.");
                return;
            }

            this.Server.AnnounceLong("ZOMBIES WIN!");
            this.Server.ForceEndGame(ZOMBIES);
            this.DiscordWebhooks?.SendMessage($"== ZOMBIES WIN ==");

            return;
        }

        [CommandCallback("resetbuild", Description = "Reset the build phase.", AllowedRoles = Roles.Moderator)]
        public void ResetBuildCommand(RunnerPlayer caller)
        {
            this.State.GameState = ZombiesGameState.BuildPhase;
            this.State.BuildPhase = false;
            this.State.EndOfBuildPhase = DateTime.MinValue;

            caller.Message("Build phase reset.", 10);
            this.Server.SayToAllChat($"{this.RichText?.Size(125)}{this.RichText?.FromColorName("yellow")}Build phase aborted.");
        }
        #endregion
    }

    #region CLASSES AND ENUMS
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

        public bool ReceivedThrowable { get; set; } = false;
        public bool ReceivedLightGadget { get; set; } = false;
        public bool ReceivedHeavyGadget { get; set; } = false;
        public bool ReceivedPrimary { get; set; } = false;
        public ZombieClass? ZombieClass { get; set; } = null;
    }

    public class ZombieClass
    {
        public string Name { get; }
        public int RequestedAmount { get; }

        public Action<RunnerPlayer> ApplyToPlayer { get; }

        public ZombieClass(string name, int requestedAmount, Action<RunnerPlayer> applyToPlayer)
        {
            this.Name = name;
            this.RequestedAmount = requestedAmount;
            this.ApplyToPlayer = applyToPlayer;
        }
    }

    public class ZombiesConfiguration : ModuleConfiguration
    {
        public int InitialZombieCount { get; set; } = 6;
        public int InitialZombieMaxPercentage { get; set; } = 15;
        public int AnnounceLastHumansCount { get; set; } = 10;
        public int RequiredPlayersToStart { get; set; } = 20;
        public float ZombieMinDamageReceived { get; set; } = 0.2f;
        public float ZombieMaxDamageReceived { get; set; } = 2f;
        public float SuicideZombieficationChance { get; set; } = 0.3141f;
        public int SuicideZombieficationMaxTime { get; set; } = 120000;
        public float FallDamageMultiplier { get; set; } = 1f;
        public float ZombieRunningSpeedMultiplier { get; set; } = 1f;
        public float ZombieJumpHeightMultiplier { get; set; } = 1f;
        public int BuildPhaseDuration { get; set; } = 0;
        public int PistolThreshold { get; set; } = 255;
        public int GameStateUpdateTimer { get; set; } = 250;
        public int BuildPhaseSquadPoints { get; set; } = 500;
        public int GamePhaseSquadPoints { get; set; } = 0;
        public int ZombieMaxInfectionTime { get; set; } = 10000;
        public int GamePhaseDuration { get; set; } = 900;
        public int CountdownPhaseDuration { get; set; } = 10;
        public double HumanExposeOffTime { get; set; } = 6;
        public double HumanExposeOnTime { get; set; } = 2;
        public double HumanRatioThrowable { get; set; } = 0.65;
        public double HumanRatioLightGadget { get; set; } = 0.5;
        public double HumanRatioHeavyGadget { get; set; } = 0.35;
        public double HumanRatioPrimary { get; set; } = 0.2;
        public double ZombieClassRatio { get; set; } = 0.1;
    }

    public class ZombiesState : ModuleConfiguration
    {
        public ZombiesGameState GameState { get; set; } = ZombiesGameState.WaitingForPlayers;
        public Dictionary<Squads, int> LastSquadPoints { get; set; } = new();
        public int LastHumansAnnounced { get; set; } = int.MaxValue;
        public bool BuildPhase { get; set; } = false;
        public DateTime EndOfBuildPhase { get; set; } = DateTime.MinValue;
        public DateTime NextHumanExposeSwitch { get; set; } = DateTime.MinValue;
        public bool ExposeOnMap { get; set; } = false;

        public void Reset()
        {
            this.GameState = ZombiesGameState.WaitingForPlayers;
            this.LastSquadPoints = new();
            this.LastHumansAnnounced = int.MaxValue;
            this.BuildPhase = false;
            this.EndOfBuildPhase = DateTime.MinValue;
            this.NextHumanExposeSwitch = DateTime.MinValue;
            this.ExposeOnMap = false;

            this.Save();
        }
    }

    public enum ZombiesGameState
    {
        WaitingForPlayers,
        Countdown,
        BuildPhase,
        GamePhase,
        ZombieWin,
        HumanWin,
        Ended
    }
    #endregion
}