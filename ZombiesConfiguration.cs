using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zombies
{
    public class ZombiesConfiguration
    {
        public int InitialZombieCount { get; set; } = 6;
        public int AnnounceLastHumansCount { get; set; } = 10;
        public int RequiredPlayersToStart { get; set; } = 20;
        public float ZombieMinDamageReceived { get; set; } = 0.2f;
        public float ZombieMaxDamageReceived { get; set; } = 2f;
        public float SuicideZombieficationChance { get; set; } = 0.3141f;
        public int SuicideZombieficationMaxTime { get; set; } = 120000;
    }
}
