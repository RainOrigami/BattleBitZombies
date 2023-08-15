# BattleBitZombies
28 days later inspired zombies pvp game mode module for battle bit modular api

Clone into modules directory of your https://github.com/RainOrigami/BattleBitAPIRunner instance.

Configuration is in the ZombiesConfiguration.json in the bin/release/publish folder (create it manually).

- InitialZombieCount: how many zombies will spawn at the start of the game
- AnnounceLastHumansCount: how many humans left to start announcing remaining humans
- RequiredPlayersToStart: how many players are required to start the game
- ZombieMinDamageReceived: damage scaling minimum factor for zombies
- ZombieMaxDamageReceived: damage scaling maximum factor for zombies
- SuicideZombieficationChance: chance of a player turning into a zombie when suiciding
- SuicideZombieficationMaxTime: maximum time in seconds for a player to turn into a zombie when suiciding