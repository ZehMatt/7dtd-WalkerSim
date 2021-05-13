using System;
using System.Collections.Generic;
using UnityEngine;

namespace WalkerSim
{
    partial class Simulation
    {
        List<ZombieAgent> _activeZombies = new List<ZombieAgent>();
        Queue<ZombieSpawnRequest> _spawnQueue = new Queue<ZombieSpawnRequest>();

        bool IsSpawnProtected(Vector3 pos)
        {
            var world = GameManager.Instance.World;
            var players = world.Players.list;

            foreach (var ply in players)
            {
                for (int i = 0; i < ply.SpawnPoints.Count; ++i)
                {
                    var spawnPos = ply.SpawnPoints[i].ToVector3();
                    var dist = Vector3.Distance(pos, spawnPos);
                    if (dist <= 50)
                        return true;
                }
            }
            return false;
        }

        bool CanZombieSpawnAt(Vector3 pos)
        {
            var world = GameManager.Instance.World;

            if (!world.CanMobsSpawnAtPos(pos))
                return false;

            if (IsSpawnProtected(pos))
                return false;

            return true;
        }

        bool CanSpawnActiveZombie()
        {
            int alive = GameStats.GetInt(EnumGameStats.EnemyCount);
            if (alive + 1 >= MaxSpawnedZombies)
                return false;
            return true;
        }

        bool CreateZombie(ZombieAgent zombie, PlayerZone zone)
        {
            if (!CanSpawnActiveZombie())
            {
                return false;
            }

            var world = GameManager.Instance.World;
            Chunk chunk = (Chunk)world.GetChunkSync(World.toChunkXZ(Mathf.FloorToInt(zombie.pos.x)), 0, World.toChunkXZ(Mathf.FloorToInt(zombie.pos.z)));
            if (chunk == null)
            {
#if DEBUG
                Log.Out("[WalkerSim] Chunk not loaded at {0} {1}", zombie.pos, zombie.pos.z);
#endif
                return false;
            }

            int height = world.GetTerrainHeight(Mathf.FloorToInt(zombie.pos.x), Mathf.FloorToInt(zombie.pos.z));

            Vector3 spawnPos = new Vector3(zombie.pos.x, height + 1.0f, zombie.pos.z);
            if (!CanZombieSpawnAt(spawnPos))
            {
#if DEBUG
                Log.Out("[WalkerSim] Unable to spawn zombie at {0}, CanMobsSpawnAtPos failed", spawnPos);
#endif
                return false;
            }

            if (zombie.classId == -1)
            {
                zombie.classId = _biomeData.GetZombieClass(world, chunk, (int)spawnPos.x, (int)spawnPos.z, _prng);
                if (zombie.classId == -1)
                {
                    int lastClassId = -1;
                    zombie.classId = EntityGroups.GetRandomFromGroup("ZombiesAll", ref lastClassId);
#if DEBUG
                    Log.Out("Used fallback for zombie class!");
#endif
                }
            }

            EntityZombie zombieEnt = EntityFactory.CreateEntity(zombie.classId, spawnPos) as EntityZombie;
            if (zombieEnt == null)
            {
#if DEBUG
                Log.Error("[WalkerSim] Unable to create zombie entity!, Entity Id: {0}, Pos: {1}", zombie.classId, spawnPos);
#endif
                return false;
            }

            zombieEnt.bIsChunkObserver = true;

            // TODO: Figure out a better way to make them walk towards something.
            if (true)
            {
                // Send zombie towards a random position in the zone.
                Vector3 targetPos = GetRandomZonePos(zone);
                if (targetPos == Vector3.zero)
                    zombieEnt.SetInvestigatePosition(zone.center, 6000, false);
                else
                    zombieEnt.SetInvestigatePosition(targetPos, 6000, false);
            }

            // If the zombie was previously damaged take health to this one.
            if (zombie.health != -1)
                zombieEnt.Health = zombie.health;
            else
                zombie.health = zombieEnt.Health;

            zombieEnt.IsHordeZombie = true;
            zombieEnt.IsBloodMoon = _state.IsBloodMoon;

            zombieEnt.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);

            world.SpawnEntityInWorld(zombieEnt);

            zombie.entityId = zombieEnt.entityId;
            zombie.currentZone = zone;
            zombie.lifeTime = world.GetWorldTime();

            zone.numZombies++;

#if DEBUG
            Log.Out("[WalkerSim] Spawned zombie {0} at {1}", zombieEnt, spawnPos);
#endif
            lock (_activeZombies)
            {
                _activeZombies.Add(zombie);
            }

            return true;
        }

        void RequestActiveZombie(ZombieAgent zombie, PlayerZone zone)
        {
            zombie.state = ZombieAgent.State.Active;

            ZombieSpawnRequest spawn = new ZombieSpawnRequest();
            spawn.zombie = zombie;
            spawn.zone = zone;
            lock (_spawnQueue)
            {
                _spawnQueue.Enqueue(spawn);
            }
        }

        void ProcessSpawnQueue()
        {
            for (int i = 0; i < MaxZombieSpawnsPerTick; i++)
            {
                ZombieSpawnRequest zombieSpawn = null;
                lock (_spawnQueue)
                {
                    if (_spawnQueue.Count == 0)
                        break;
                    zombieSpawn = _spawnQueue.Dequeue();
                }
                if (!CreateZombie(zombieSpawn.zombie, zombieSpawn.zone))
                {
                    // Failed to spawn zombie, keep population size.
                    RespawnInactiveZombie(zombieSpawn.zombie);
                }
            }
        }

        int MaxZombiesPerZone()
        {
            return MaxSpawnedZombies / Math.Max(1, ConnectionManager.Instance.Clients.Count);
        }

        bool UpdateActiveZombie(ZombieAgent zombie)
        {
            var world = GameManager.Instance.World;
            int maxPerZone = MaxZombiesPerZone();

            bool removeZombie = false;

            var worldTime = world.GetWorldTime();
            var timeAlive = worldTime - zombie.lifeTime;

            var currentZone = zombie.currentZone as PlayerZone;
            if (currentZone != null)
            {
                currentZone.numZombies--;
                if (currentZone.numZombies < 0)
                    currentZone.numZombies = 0;
            }
            zombie.currentZone = null;

            Vector3 oldPos = new Vector3 { x = zombie.pos.x, y = zombie.pos.y, z = zombie.pos.z };
            EntityZombie ent = world.GetEntity(zombie.entityId) as EntityZombie;
            if (ent == null)
            {
#if DEBUG
                Log.Out("[WalkerSim] Failed to get zombie with entity id {0}", zombie.entityId);
#endif
                removeZombie = true;
                RespawnInactiveZombie(zombie);
            }
            else
            {
                zombie.pos = ent.GetPosition();
                zombie.health = ((EntityZombie)ent).Health;
                zombie.dir = -ent.rotation;

                if (ent.IsDead())
                {
                    removeZombie = true;
                    RespawnInactiveZombie(zombie);
                }
                else
                {
                    List<PlayerZone> zones = _playerZones.FindAllByPos2D(ent.GetPosition());
                    if (zones.Count == 0 && timeAlive >= MinZombieLifeTime)
                    {
#if DEBUG
                        Log.Out("[WalkerSim] Zombie {0} out of range, turning inactive", ent);
#endif
                        removeZombie = true;

                        world.RemoveEntity(zombie.entityId, EnumRemoveEntityReason.Despawned);

                        zombie.entityId = -1;
                        zombie.currentZone = null;

                        TurnZombieInactive(zombie);
                    }
                    else
                    {
                        foreach (var zone in zones)
                        {
                            if (zone.numZombies + 1 < maxPerZone)
                            {
                                zone.numZombies++;
                                zombie.currentZone = zone;
                                // If the zombie is inside a player zone make sure we renew the life time.
                                zombie.lifeTime = worldTime;
                                break;
                            }
                        }
                    }
                }
            }

            return removeZombie;
        }

        // This function is called only from the main thread.
        // This functions checks about every active zombie if they are too far
        // away from the player if that is the case they will be despawned and
        // put back into the simulation at the current coordinates.
        // NOTE: A call must only be made from the main thread.
        void UpdateActiveZombies()
        {
            lock (_activeZombies)
            {
                for (int i = 0; i < _activeZombies.Count; i++)
                {
                    var zombie = _activeZombies[i];

                    var removeZombie = UpdateActiveZombie(zombie);

                    if (removeZombie)
                    {
                        _activeZombies.RemoveAt(i);
                        if (_activeZombies.Count == 0)
                            break;

                        i--;
                    }
                }
            }
        }
    }
}
