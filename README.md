# BattleBitZombies
28 days later inspired zombies pvp game mode module for battle bit modular api

## Dependencies
- [CommandHandler](https://github.com/RainOrigami/BattleBitBaseModules/blob/main/CommandHandler.cs)
- [Newtonsoft JSON](https://github.com/JamesNK/Newtonsoft.Json/releases) - `Bin\net6.0\Newtonsoft.Json.dll`
- (Optional) [DiscordWebhooks](https://github.com/RainOrigami/BattleBitBaseModules/blob/main/DiscordWebhooks.cs)

## Configuration

Configuration is in the ZombiesConfiguration.json (create it manually, otherwise default values are used).

- InitialZombieCount: how many zombies will spawn at the start of the game
- AnnounceLastHumansCount: how many humans left to start announcing remaining humans
- RequiredPlayersToStart: how many players are required to start the game
- ZombieMinDamageReceived: damage scaling minimum factor for zombies
- ZombieMaxDamageReceived: damage scaling maximum factor for zombies
- SuicideZombieficationChance: chance of a player turning into a zombie when suiciding
- SuicideZombieficationMaxTime: maximum time in seconds for a player to turn into a zombie when suiciding