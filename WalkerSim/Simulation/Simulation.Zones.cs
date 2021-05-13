using UnityEngine;

namespace WalkerSim
{
    partial class Simulation
    {
        PlayerZoneManager _playerZones = new PlayerZoneManager();
        POIZoneManager _pois = new POIZoneManager();
        WorldZoneManager _worldZones = new WorldZoneManager();

        Vector3 GetRandomZonePos(PlayerZone zone)
        {
            var world = GameManager.Instance.World;

            Vector3 pos = new Vector3();
            Vector3 spawnPos = new Vector3();
            for (int i = 0; i < 10; i++)
            {
                pos.x = _prng.Get(zone.minsSpawnBlock.x, zone.maxsSpawnBlock.x);
                pos.z = _prng.Get(zone.minsSpawnBlock.z, zone.maxsSpawnBlock.z);

                int height = world.GetTerrainHeight((int)pos.x, (int)pos.z);

                spawnPos.x = pos.x;
                spawnPos.y = height + 1.0f;
                spawnPos.z = pos.z;
                if (world.CanMobsSpawnAtPos(spawnPos))
                {
                    return spawnPos;
                }
            }

            return Vector3.zero;
        }

        void UpdatePlayerZones()
        {
            _playerZones.Update();
        }

        POIZone GetNextPOI(ZombieAgent zombie)
        {
            var closest = _pois.GetRandomClosest(zombie.pos, _prng, 500, zombie.visitedZones);
            if (closest == null)
                return _pois.GetRandom(_prng);

            return closest;
        }
    }
}
