using System.Collections.Generic;

namespace WalkerSim
{
    class BiomeData
    {
        private DictionarySave<System.String, BiomeSpawnEntityGroupList> list = new DictionarySave<string, BiomeSpawnEntityGroupList>();

        public void Init(bool clearOriginal = false)
        {
            var world = GameManager.Instance.World;

            foreach (var pair in world.Biomes.GetBiomeMap())
            {
                var biome = pair.Value;

                BiomeSpawnEntityGroupList biomeSpawnEntityGroupList = BiomeSpawningClass.list[biome.m_sBiomeName];
                if (biomeSpawnEntityGroupList == null)
                {
                    continue;
                }

                // Clearing the biome data prevents random zombie spawns
                // We make a copy to keep for ourselves for the simulation.
                var copy = new BiomeSpawnEntityGroupList();
                copy.list = new List<BiomeSpawnEntityGroupData>(biomeSpawnEntityGroupList.list);

                if (clearOriginal)
                {
                    biomeSpawnEntityGroupList.list.Clear();
                }

                list.Add(biome.m_sBiomeName, copy);
            }
        }

        public int GetZombieClass(World world, Chunk chunk, int x, int y, PRNG prng)
        {
            ChunkAreaBiomeSpawnData spawnData = chunk.GetChunkBiomeSpawnData();
            if (spawnData == null)
            {
#if DEBUG
				Log.Out("No biome spawn data present");
#endif
                return -1;
            }

            var biomeData = world.Biomes.GetBiome(spawnData.biomeId);
            if (biomeData == null)
            {
#if DEBUG
				Log.Out("No biome data for biome id {0}", spawnData.biomeId);
#endif
                return -1;
            }

            BiomeSpawnEntityGroupList biomeSpawnEntityGroupList = list[biomeData.m_sBiomeName];
            if (biomeSpawnEntityGroupList == null)
            {
#if DEBUG
				Log.Out("No biome spawn group specified for {0}", biomeData.m_sBiomeName);
#endif
                return -1;
            }

            var numGroups = biomeSpawnEntityGroupList.list.Count;
            if (numGroups == 0)
            {
#if DEBUG
				Log.Out("No biome spawn group is empty for {0}", biomeData.m_sBiomeName);
#endif
                return -1;
            }

            var dayTime = world.IsDaytime() ? EDaytime.Day : EDaytime.Night;
            for (int i = 0; i < 5; i++)
            {
                int pickIndex = prng.Get(0, numGroups);

                var pick = biomeSpawnEntityGroupList.list[pickIndex];
                if (pick.daytime == EDaytime.Any || pick.daytime == dayTime)
                {
                    int lastClassId = -1;
                    return EntityGroups.GetRandomFromGroup(pick.entityGroupRefName, ref lastClassId);
                }
            }

            return -1;
        }
    }
}
