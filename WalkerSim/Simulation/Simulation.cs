using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System.IO;
using System.Globalization;

namespace WalkerSim
{
    enum WorldEventType
    {
        Sound,
    }
    class WorldEvent
    {
        public WorldEventType Type;
        public Vector3 Pos;
        public float Radius;
    }

    class ZombieSpawnRequest
    {
        public ZombieAgent zombie;
        public PlayerZone zone;
    }

    public class Simulation
    {
        const int MaxZombieSpawnsPerTick = 2;
        const ulong MinZombieLifeTime = 60; // 1 in-game minutes.

        static int DayTimeMin = GamePrefs.GetInt(EnumGamePrefs.DayNightLength);
        static int MaxAliveZombies = GamePrefs.GetInt(EnumGamePrefs.MaxSpawnedZombies);
        static int MaxSpawnedZombies = MaxAliveZombies;

        static string ConfigFile = string.Format("{0}/WalkerSim.xml", API.ModPath);
        static string SimulationFile = string.Format("{0}/WalkerSim.bin", GameUtils.GetSaveGameDir());

        static ViewServer _server = new ViewServer();

        State _state = new State();

        PlayerZoneManager _playerZones = new PlayerZoneManager();
        POIZoneManager _pois = new POIZoneManager();
        WorldZoneManager _worldZones = new WorldZoneManager();

        List<ZombieAgent> _inactiveZombies = new List<ZombieAgent>();
        List<ZombieAgent> _activeZombies = new List<ZombieAgent>();
        Dictionary<Vector2i, int> _zoneCounter = new Dictionary<Vector2i, int>();

        Queue<ZombieSpawnRequest> _spawnQueue = new Queue<ZombieSpawnRequest>();
        Queue<ZombieAgent> _inactiveQueue = new Queue<ZombieAgent>();

        Queue<WorldEvent> _worldEvents = new Queue<WorldEvent>();

        Vector3i _worldMins = new Vector3i();
        Vector3i _worldMaxs = new Vector3i();

        DateTime _nextBroadcast = DateTime.Now;
        BiomeData _biomeData = new BiomeData();

        PRNG _prng = new PRNG(0);

        int _nextZombieId = 0;
        int _maxZombies = 0;
        double _accumulator = 0.0;

        DateTime _nextSave = DateTime.Now;

        BackgroundWorker _worker = new BackgroundWorker();
        bool _running = false;

        public Simulation()
        {
            Config.Instance.Load(ConfigFile);

            var world = GameManager.Instance.World;
            world.GetWorldExtent(out _worldMins, out _worldMaxs);

            float lenX = _worldMins.x < 0 ? _worldMaxs.x + Math.Abs(_worldMins.x) : _worldMaxs.x - Math.Abs(_worldMins.x);
            float lenY = _worldMins.z < 0 ? _worldMaxs.z + Math.Abs(_worldMins.z) : _worldMaxs.x - Math.Abs(_worldMins.z);

            float squareKm = (lenX / 1000.0f) * (lenY / 1000.0f);
            float populationSize = squareKm * Config.Instance.PopulationDensity;
            _maxZombies = (int)Math.Floor(populationSize);
            _state.WalkSpeedScale = Config.Instance.WalkSpeedScale;

            MaxSpawnedZombies = MaxAliveZombies - Mathf.RoundToInt(MaxAliveZombies * Config.Instance.ReservedSpawns);

            Log.Out("Simulation File: {0}", SimulationFile);
            Log.Out("World X: {0}, World Y: {1} -- {2}, {3}", lenX, lenY, _worldMins, _worldMaxs);
            Log.Out("Day Time: {0}", DayTimeMin);
            Log.Out("Max Offline Zombies: {0}", _maxZombies);
            Log.Out("Max Spawned Zombies: {0}", MaxSpawnedZombies);

#if !DEBUG
            if (Config.Instance.EnableViewServer)
#endif
            {
                Log.Out("Starting server...");

                _server.OnClientConnected += new ViewServer.OnClientConnectedDelegate(OnClientConnected);
                _state.OnChange += new State.OnChangeDelegate(OnStateChanged);

                if (_server.Start(Config.Instance.ViewServerPort))
                {
                    Log.Out("ViewServer running at port {0}", Config.Instance.ViewServerPort);
                }
            }

            _biomeData.Init();
            _pois.BuildCache();
            _worldZones.BuildZones(_worldMins, _worldMaxs);

            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += BackgroundUpdate;

            Log.Out("[WalkerSim] Initialized");
        }

        void OnClientConnected(ViewServer sender, ViewServer.Client cl)
        {
            SendStaticState(sender, cl);
        }

        void OnStateChanged()
        {
            SendState(_server, null);
        }

        public void SetTimeScale(float scale)
        {
            _state.Timescale = Mathf.Clamp(scale, 0.01f, 100.0f);
            _accumulator = 0;
        }

        public void SetWalkSpeedScale(float scale)
        {
            _state.WalkSpeedScale = Mathf.Clamp(scale, 0.01f, 100.0f);
        }

        public void Start()
        {
            if (_running)
            {
                Log.Error("Simulation is already running");
                return;
            }

            Log.Out("[WalkerSim] Starting worker..");

#if DEBUG
            if (!Config.Instance.Persistent || !Load())
#endif
            {
                Reset();
            }

            _running = true;
            _worker.RunWorkerAsync();
        }

        public void Stop()
        {
            if (!_running)
                return;

            Log.Out("[WalkerSim] Stopping worker..");

            _worker.CancelAsync();
            _running = false;
        }

        public void AddPlayer(int entityId)
        {
            _playerZones.AddPlayer(entityId);
        }

        public void RemovePlayer(int entityId)
        {
            _playerZones.RemovePlayer(entityId);
        }

        public void Save()
        {
            if (!Config.Instance.Persistent)
                return;

            try
            {
                using (Stream stream = File.Open(SimulationFile, FileMode.Create))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    lock (_inactiveZombies)
                    {
                        formatter.Serialize(stream, Config.Instance);

                        List<ZombieData> data = new List<ZombieData>();
                        foreach (var zombie in _inactiveZombies)
                        {
                            data.Add(new ZombieData
                            {
                                health = zombie.health,
                                x = zombie.pos.x,
                                y = zombie.pos.y,
                                z = zombie.pos.z,
                                targetX = zombie.targetPos.x,
                                targetY = zombie.targetPos.y,
                                targetZ = zombie.targetPos.z,
                                dirX = zombie.dir.x,
                                dirY = zombie.dir.z,
                                target = zombie.target is POIZone,
                            });
                        }
                        formatter.Serialize(stream, data);
                    }
                    Log.Out("[WalkerSim] Saved simulation");
                }
            }
            catch (Exception ex)
            {
                Log.Out("Unable to save simulation");
                Log.Exception(ex);
            }
        }

        public void CheckAutoSave()
        {
            //Log.Out("[WalkerSim] CheckAutoSave");

            DateTime now = DateTime.Now;
            if (now < _nextSave)
                return;

            Save();
            _nextSave = now.AddMinutes(5);
        }

        public bool Load()
        {
            try
            {
                using (Stream stream = File.Open(SimulationFile, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    Config config = formatter.Deserialize(stream) as Config;
                    if (!config.Equals(Config.Instance))
                    {
                        Log.Out("[WalkerSim] Configuration changed, not loading save.");
                        return false;
                    }

                    lock (_inactiveZombies)
                    {
                        List<ZombieData> data = formatter.Deserialize(stream) as List<ZombieData>;
                        if (data.Count > 0)
                        {
                            _inactiveZombies.Clear();
                            foreach (var zombie in data)
                            {
                                var inactiveZombie = new ZombieAgent
                                {
                                    health = zombie.health,
                                    pos = new Vector3(zombie.x, zombie.y, zombie.z),
                                    dir = new Vector3(zombie.dirX, 0.0f, zombie.dirY),
                                    target = zombie.target ? _pois.GetRandom(_prng) : null,
                                    targetPos = new Vector3(zombie.targetX, zombie.targetY, zombie.targetZ),
                                };
                                _inactiveZombies.Add(inactiveZombie);
                            }
                            Log.Out("[WalkerSim] Loaded {0} inactive zombies", _inactiveZombies.Count);
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public void Reset()
        {
            var world = GameManager.Instance.World;

            lock (_spawnQueue)
            {
                _spawnQueue.Clear();
            }

            lock (_inactiveZombies)
            {
                _inactiveZombies.Clear();
            }

            lock (_activeZombies)
            {
                _activeZombies.Clear();
            }

            // Cleanup all zombies.
            var ents = new List<Entity>(world.Entities.list);
            foreach (var ent in ents)
            {
                if (ent.entityType == EntityType.Zombie)
                {
                    world.RemoveEntity(ent.entityId, EnumRemoveEntityReason.Despawned);
                }
            }

            // Populate
            CreateInactiveRoaming();

            _nextSave = DateTime.Now.AddMinutes(5);
        }

        void CreateInactiveRoaming()
        {
            int numCreated = 0;
            int maxZombies = _maxZombies;

            lock (_inactiveZombies)
            {
                while (_inactiveZombies.Count < maxZombies)
                {
                    CreateInactiveZombie(true);
                    numCreated++;
                }
            }

            if (numCreated > 0)
            {
                Log.Out("[WalkerSim] Created {0} inactive roaming", numCreated);
            }
        }

        private Vector3 GetRandomPos()
        {
            var res = new Vector3();
            res.x = _prng.Get(_worldMins.x, _worldMaxs.x);
            res.y = 0.0f;
            res.z = _prng.Get(_worldMins.z, _worldMaxs.z);
            return res;
        }

        private Vector3 GetRandomBorderPoint()
        {
            Vector3 res = new Vector3();
            res.y = 0;
            switch (_prng.Get(0, 4))
            {
                case 0:
                    // Top
                    res.x = _prng.Get(_worldMins.x + 1, _worldMaxs.x - 1);
                    res.z = _worldMins.z + 1;
                    break;
                case 1:
                    // Bottom
                    res.x = _prng.Get(_worldMins.x + 1, _worldMaxs.x - 1);
                    res.z = _worldMaxs.z - 1;
                    break;
                case 2:
                    // Left
                    res.x = _worldMins.x + 1;
                    res.z = _prng.Get(_worldMins.z + 1, _worldMaxs.z - 1);
                    break;
                case 3:
                    // Right
                    res.x = _worldMaxs.x - 1;
                    res.z = _prng.Get(_worldMins.z + 1, _worldMaxs.z - 1);
                    break;
            }
            return res;
        }

        ZombieAgent CreateInactiveZombie(bool initial)
        {
            ZombieAgent zombie = new ZombieAgent();
            zombie.id = _nextZombieId++;

            if (initial)
            {
                var poiChance = Config.Instance.POITravellerChance;

                var poi = _prng.Chance(poiChance) ? _pois.GetRandom(_prng) : null;
                if (poi != null)
                {
                    // To be not literally inside the POI we add a random radius.
                    var spawnRadius = 256.0f;
                    var randOffset = new Vector3(
                        _prng.Get(-spawnRadius, spawnRadius),
                        0.0f,
                        _prng.Get(-spawnRadius, spawnRadius));
                    zombie.pos = poi.GetRandomPos(_prng) + randOffset;
                    zombie.pos = WrapPos(zombie.pos);
                }
                else
                {
                    // Use a random world zone for the rest.
                    var zone = _worldZones.GetRandom(_prng);
                    if (zone != null)
                    {
                        zombie.pos = zone.GetRandomPos(_prng);
                    }
                    else
                    {
                        zombie.pos = GetRandomPos();
                    }
                }
            }
            else
            {
                // New zombies start at the border.
                zombie.pos = GetRandomBorderPoint();
            }

            zombie.state = ZombieAgent.State.Idle;

            _inactiveZombies.Add(zombie);

            return zombie;
        }

        void TurnZombieInactive(ZombieAgent zombie)
        {
            zombie.state = ZombieAgent.State.Idle;
            lock (_inactiveQueue)
            {
                _inactiveQueue.Enqueue(zombie);
            }
        }

        private void RespawnInactiveZombie(ZombieAgent zombie)
        {
            zombie.pos = GetRandomBorderPoint();
            TurnZombieInactive(zombie);
        }

        private Vector3 GetRandomZonePos(PlayerZone zone)
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

        private bool CreateZombie(ZombieAgent zombie, PlayerZone zone)
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

        private void RequestActiveZombie(ZombieAgent zombie, PlayerZone zone)
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

        private void ProcessSpawnQueue()
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

        private void UpdatePlayerZones()
        {
            _playerZones.Update();
        }

        private POIZone GetNextPOI(ZombieAgent zombie)
        {
            var closest = _pois.GetRandomClosest(zombie.pos, _prng, 500, zombie.visitedZones);
            if (closest == null)
                return _pois.GetRandom(_prng);

            return closest;
        }

        private Zone GetNextTarget(ZombieAgent zombie)
        {
            if (_prng.Chance(Config.Instance.POITravellerChance))
            {
                return GetNextPOI(zombie);
            }
            return _worldZones.GetRandom(_prng);
        }

        private Vector3 GetTargetPos(Zone target)
        {
            return target.GetRandomPos(_prng);
        }

        // This function is called only from the main thread.
        // This functions checks about every active zombie if they are too far
        // away from the player if that is the case they will be despawned and
        // put back into the simulation at the current coordinates.
        // NOTE: A call must only be made from the main thread.
        private void UpdateActiveZombies()
        {
            var world = GameManager.Instance.World;
            int maxPerZone = MaxZombiesPerZone();
            int deactivatedZombies = 0;

            lock (_activeZombies)
            {
                for (int i = 0; i < _activeZombies.Count; i++)
                {
                    bool removeZombie = false;

                    var zombie = _activeZombies[i];
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
                            deactivatedZombies++;
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
                                deactivatedZombies++;
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

        private Vector3 ClampPos(Vector3 pos)
        {
            pos.x = UnityEngine.Mathf.Clamp(pos.x, _worldMins.x, _worldMaxs.x);
            pos.y = UnityEngine.Mathf.Clamp(pos.y, _worldMins.y, _worldMaxs.y);
            pos.z = UnityEngine.Mathf.Clamp(pos.z, _worldMins.z, _worldMaxs.z);
            return pos;
        }

        private Vector3 WrapPos(Vector3 pos)
        {
            pos.x = ((pos.x - _worldMins.x) % (_worldMaxs.x - _worldMins.x)) + _worldMins.x;
            pos.y = ((pos.y - _worldMins.y) % (_worldMaxs.y - _worldMins.y)) + _worldMins.y;
            pos.z = ((pos.z - _worldMins.z) % (_worldMaxs.z - _worldMins.z)) + _worldMins.z;
            return pos;
        }

        private void ProcessWorldEvent(ZombieAgent zombie, WorldEvent ev)
        {
            if (ev == null)
                return;

            var dist = Vector3.Distance(zombie.pos, ev.Pos);
            if (dist <= ev.Radius)
            {
                Vector3 soundDir = new Vector3();
                soundDir.x = _prng.Get(-1.0f, 1.0f);
                soundDir.z = _prng.Get(-1.0f, 1.0f);

                // Pick a random position within 75% of the radius.
                soundDir.Normalize();
                soundDir *= (dist * 0.75f);

                zombie.targetPos = ev.Pos + soundDir;
                zombie.target = _worldZones.FindByPos2D(zombie.targetPos);

                zombie.state = ZombieAgent.State.Investigating;
            }
        }

        private void UpdateTarget(ZombieAgent zombie)
        {
            if (zombie.state != ZombieAgent.State.Idle)
            {
                // If we have an activate target wait for arrival.
                if (!zombie.ReachedTarget())
                    return;

                zombie.AddVisitedZone(zombie.target);
            }

            if (_state.IsBloodMoon)
            {
                zombie.target = _playerZones.GetRandomClosest(zombie.pos, _prng, 200.0f, null);
                if (zombie.target == null)
                {
                    zombie.target = GetNextTarget(zombie);
                }
            }
            else
            {
                zombie.target = GetNextTarget(zombie);
            }

            zombie.targetPos = GetTargetPos(zombie.target);
            zombie.state = ZombieAgent.State.Wandering;
        }

        void UpdateWalking(ZombieAgent zombie, float dt)
        {
#if false
            // Test investigation.
            if (zombie.state != ZombieAgent.State.Investigating)
                return;
#endif
            float speed = _state.ScaledZombieSpeed;
            speed *= dt;

            // Calculate direction towards target position.
            zombie.dir = zombie.targetPos - zombie.pos;
            zombie.dir.Normalize();

            var distance = Vector3.Distance(zombie.pos, zombie.targetPos) * 0.75f;

            var t = (zombie.simulationTime + zombie.id) * 0.2f;
            var offset = new Vector3(Mathf.Cos(t), 0.0f, Mathf.Sin(t));
            offset *= distance;

            // Move towards target.
            zombie.pos = Vector3.MoveTowards(zombie.pos, zombie.targetPos + offset, speed);
        }

        private void UpdateInactiveZombie(ZombieAgent zombie, float dt, WorldEvent ev)
        {
            zombie.simulationTime += dt;

            ProcessWorldEvent(zombie, ev);
            UpdateTarget(zombie);
            UpdateWalking(zombie, dt);
        }

        private bool CanSpawnActiveZombie()
        {
            int alive = GameStats.GetInt(EnumGameStats.EnemyCount);
            if (alive + 1 >= MaxSpawnedZombies)
                return false;
            return true;
        }

        int MaxZombiesPerZone()
        {
            return MaxSpawnedZombies / Math.Max(1, ConnectionManager.Instance.Clients.Count);
        }
        private void UpdateInactiveZombies(float dt)
        {
            // Repopulate
            lock (_inactiveZombies)
            {
                lock (_inactiveQueue)
                {
                    while (_inactiveQueue.Count > 0)
                    {
                        var zombie = _inactiveQueue.Dequeue();
                        _inactiveZombies.Add(zombie);
                    }
                }
            }

            // Simulate
            int activatedZombies = 0;
            int maxUpdates = _maxZombies;
            int maxPerZone = MaxZombiesPerZone();

            WorldEvent ev = null;
            lock (_worldEvents)
            {
                if (_worldEvents.Count > 0)
                {
                    ev = _worldEvents.Dequeue();
                }
            }

            for (int i = 0; ; i++)
            {
                lock (_inactiveZombies)
                {
                    if (i >= _inactiveZombies.Count)
                        break;

                    var world = GameManager.Instance.World;
                    if (world == null)
                    {
                        Log.Out("[WalkerSim] World no longer exists, bailing");
                        break;
                    }

                    bool removeZombie = false;
                    bool activatedZombie = false;

                    ZombieAgent zombie = _inactiveZombies[i];

                    UpdateInactiveZombie(zombie, dt, ev);

                    //Log.Out("New Zombie Position: {0}, Target: {1}", zombie.pos, zombie.targetPos);

                    if (!CanSpawnActiveZombie())
                        continue;

                    List<PlayerZone> zones = _playerZones.FindAllByPos2D(zombie.pos);
                    if (zones.Count <= 0)
                        continue;

                    foreach (var zone in zones)
                    {
                        var player = world.GetEntity(zone.entityId) as EntityPlayer;

                        // Use players spawn border.
                        if (zone.IsInside2D(zombie.pos))
                        {
                            if (!zone.InsideSpawnArea2D(zombie.pos))
                            {
                                removeZombie = true;
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }

                        if (zone.numZombies >= maxPerZone)
                        {
#if DEBUG
                            Log.Out("[WalkerSim] Zone {0} is full: {1} / {2}", zombie.pos, zone.numZombies, maxPerZone);
#endif
                            continue;
                        }

                        RequestActiveZombie(zombie, zone);
                        activatedZombie = true;
                        activatedZombies++;
                        break;
                    }

                    // Zombie inside one or more zones will be always removed.
                    if (activatedZombie)
                        removeZombie = true;

                    if (removeZombie)
                    {
                        _inactiveZombies.RemoveAt(i);
                        i--;

                        // If the zombie was not activated begin a new cycle.
                        if (!activatedZombie)
                        {
                            RespawnInactiveZombie(zombie);
                        }

                        // NOTE: This should never happen.
                        if (_inactiveZombies.Count == 0)
                        {
                            Log.Error("Population is empty, this should not happen.");
                            break;
                        }
                    }
                }
            }

#if DEBUG
            if (activatedZombies > 0)
            {
                Log.Out("[WalkerSim] Activated {0} zombies", activatedZombies);
            }
#endif
        }

        public void Update()
        {
            if (!_running)
                return;

            try
            {
                _state.Update();
                UpdatePlayerZones();
                UpdateActiveZombies();
                ProcessSpawnQueue();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        public void BackgroundUpdate(object sender, DoWorkEventArgs e)
        {
            Log.Out("[WalkerSim] Worker Start");

            MicroStopwatch updateWatch = new MicroStopwatch();
            updateWatch.Start();

            MicroStopwatch frameWatch = new MicroStopwatch();

            double totalElapsed = 0.0;
            double dtAverage = 0.0;
            double nextReport = 10.0;
            float updateRate = 1.0f / (float)Config.Instance.UpdateInterval;

            BackgroundWorker worker = sender as BackgroundWorker;
            while (worker.CancellationPending == false)
            {
#if DEBUG
                bool isPaused = false;
#else
                bool isPaused = !(_playerZones.HasPlayers() || !Config.Instance.PauseWithoutPlayers);
#endif
                if (Config.Instance.PauseDuringBloodmon && _state.IsBloodMoon)
                    isPaused = true;

                double dt = updateWatch.ElapsedMicroseconds / 1000000.0;
                updateWatch.ResetAndRestart();

                totalElapsed += dt;

                if (!isPaused)
                {
                    dtAverage += dt;
                    dtAverage *= 0.5;

                    double dtScaled = dt;
                    dtScaled *= _state.Timescale;
                    _accumulator += dtScaled;
                }
                else
                {
                    dtAverage = 0.0;
                    lock (_worldEvents)
                    {
                        // Don't accumulate world events while paused.
                        _worldEvents.Clear();
                    }
                }

                _server.Update();

                if (_accumulator < updateRate)
                {
                    System.Threading.Thread.Sleep(isPaused ? 100 : 1);
                }
                else
                {
                    frameWatch.ResetAndRestart();

                    try
                    {
                        while (_accumulator >= updateRate)
                        {
                            var world = GameManager.Instance.World;
                            if (world == null)
                            {
                                // Work-around for client only support, some events are skipped like for when the player exits.
                                Log.Out("[WalkerSim] World no longer exists, stopping simulation");
                                _worker.CancelAsync();
                                break;
                            }

                            _accumulator -= updateRate;

                            // Prevent long updates in case the timescale is cranked up.
                            if (frameWatch.ElapsedMilliseconds >= 66)
                                break;

                            UpdateInactiveZombies(updateRate);
                        }
                    }
                    catch (Exception ex)
                    {
                        //Log.Out("Exception in worker: {0}", ex.Message);
                        Log.Error("[WalkerSim] Exception in worker");
                        Log.Exception(ex);
                    }
                }

                lock (_server)
                {
                    SendPlayerZones(_server, null);
                    SendInactiveZombieList(_server, null);
                    SendActiveZombieList(_server, null);
                }

                if (totalElapsed >= nextReport && !isPaused)
                {
                    double avgFps = 1 / dtAverage;
                    if (avgFps < (1.0f / updateRate))
                    {
                        Log.Warning("[WalkerSim] Detected bad performance, FPS Average: {0}", avgFps);
                    }
                    nextReport = totalElapsed + 60.0;
                }
            }

            Log.Out("[WalkerSim] Worker Finished");
            _running = false;
        }

        public Vector2i WorldToBitmap(Vector3 pos)
        {
            Vector2i res = new Vector2i();
            res.x = (int)Utils.Remap(pos.x, _worldMins.x, _worldMaxs.x, 0, 512);
            res.y = (int)Utils.Remap(pos.z, _worldMins.z, _worldMaxs.z, 0, 512);
            return res;
        }

        void SendState(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            var data = new Viewer.State();
            data.w = 512;
            data.h = 512;
            data.mapW = Utils.Distance(_worldMins.x, _worldMaxs.x);
            data.mapH = Utils.Distance(_worldMins.z, _worldMaxs.z);
            data.density = Config.Instance.PopulationDensity;
            data.zombieSpeed = _state.ZombieSpeed;
            data.timescale = _state.Timescale;

            sender.SendData(cl, Viewer.DataType.Info, data);
        }

        void SendPOIZones(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            var zones = _pois.GetSerializable(this);
            if (zones.Count == 0)
                return;

            var data = new Viewer.POIZones();
            data.zones = zones;
            sender.SendData(cl, Viewer.DataType.POIZones, data);
        }
        void SendWorldZones(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            var data = new Viewer.WorldZones();
            data.zones = _worldZones.GetSerializable(this);
            sender.SendData(cl, Viewer.DataType.WorldZones, data);
        }
        void SendPlayerZones(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            var data = new Viewer.PlayerZones();
            data.zones = _playerZones.GetSerializable(this);
            sender.SendData(cl, Viewer.DataType.PlayerZones, data);
        }

        void SendInactiveZombieList(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            lock (_inactiveZombies)
            {
                if (_inactiveZombies.Count == 0)
                    return;

                var list = new List<Viewer.DataZombie>();
                foreach (var zombie in _inactiveZombies)
                {
                    Vector2i p = WorldToBitmap(zombie.pos);
                    list.Add(new Viewer.DataZombie
                    {
                        id = zombie.id,
                        x = p.x,
                        y = p.y,
                    });
                }

                var data = new Viewer.ZombieList();
                data.list = list;

                sender.SendData(cl, Viewer.DataType.InactiveZombies, data);
            }
        }

        void SendActiveZombieList(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            lock (_activeZombies)
            {
                var list = new List<Viewer.DataZombie>();
                foreach (var zombie in _activeZombies)
                {
                    Vector2i p = WorldToBitmap(zombie.pos);
                    list.Add(new Viewer.DataZombie
                    {
                        id = zombie.id,
                        x = p.x,
                        y = p.y,
                    });
                }

                var data = new Viewer.ZombieList();
                data.list = list;

                sender.SendData(cl, Viewer.DataType.ActiveZombies, data);
            }
        }

        void SendSoundEvent(ViewServer sender, Vector3 pos, float radius)
        {
            if (sender == null)
                return;

            var p = WorldToBitmap(pos);
            var data = new Viewer.WorldEventSound();
            data.x = p.x;
            data.y = p.y;
            // FIXME: This is only remapped in one direction.

            var worldSize = Utils.Distance(_worldMins.x, _worldMaxs.x);
            var rescaled = (radius / worldSize) * 512.0f;
            data.distance = (int)rescaled;

            Log.Out("Distance {0}, Scaled: {1}", radius, data.distance);

            sender.Broadcast(Viewer.DataType.WorldEventSound, data);
        }

        void SendStaticState(ViewServer sender, ViewServer.Client cl)
        {
            lock (sender)
            {
                SendState(sender, cl);
                SendWorldZones(sender, cl);
                SendPOIZones(sender, cl);
            }
        }

        public void AddSoundEvent(Vector3 pos, float radius)
        {
            lock (_worldEvents)
            {
                _worldEvents.Enqueue(new WorldEvent()
                {
                    Type = WorldEventType.Sound,
                    Pos = pos,
                    Radius = radius,
                });
            }
            SendSoundEvent(_server, pos, radius);
        }
    }
}
