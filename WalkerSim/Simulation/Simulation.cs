using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System.IO;

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
        static int DayTimeMin = GamePrefs.GetInt(EnumGamePrefs.DayNightLength);
        static int MaxAliveZombies = GamePrefs.GetInt(EnumGamePrefs.MaxSpawnedZombies);

        static string ConfigFile = string.Format("{0}/WalkerSim.xml", API.ModPath);

        static float DayTimeScale = (24.0f * 60.0f) / DayTimeMin;
        static string SimulationFile = string.Format("{0}/WalkerSim.bin", GameUtils.GetSaveGameDir());

        System.Object _lock = new System.Object();
        static ViewServer _server = new ViewServer();

        Config _config = new Config();

        WorldState _worldState = new WorldState();

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
        float _timeScale = 1.0f;
        double _accumulator = 0.0;

        DateTime _nextSave = DateTime.Now;

        BackgroundWorker _worker = new BackgroundWorker();
        bool _running = false;

        public Simulation()
        {
            _config.Load(ConfigFile);

            var world = GameManager.Instance.World;
            world.GetWorldExtent(out _worldMins, out _worldMaxs);

            float lenX = _worldMins.x < 0 ? _worldMaxs.x + Math.Abs(_worldMins.x) : _worldMaxs.x - Math.Abs(_worldMins.x);
            float lenY = _worldMins.z < 0 ? _worldMaxs.z + Math.Abs(_worldMins.z) : _worldMaxs.x - Math.Abs(_worldMins.z);

            float squareKm = (lenX / 1000.0f) * (lenY / 1000.0f);
            float populationSize = squareKm * _config.PopulationDensity;
            _maxZombies = (int)Math.Floor(populationSize);

            Log.Out("Simulation File: {0}", SimulationFile);
            Log.Out("World X: {0}, World Y: {1}, {2}, {3}", lenX, lenY, _worldMins, _worldMaxs);
            Log.Out("Day Time: {0}", DayTimeMin);
            Log.Out("Day Time Scale: {0}", DayTimeScale);
            Log.Out("Max Zombies: {0}", _maxZombies);

            if (_config.EnableViewServer)
            {
                Log.Out("Starting server...");
                if (_server.Start(_config.ViewServerPort))
                {
                    Log.Out("ViewServer running at port {0}", _config.ViewServerPort);
                }
            }

            _biomeData.Init();
            _pois.BuildCache();
            _worldZones.BuildZones(_worldMins, _worldMaxs, _config);

            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += BackgroundUpdate;

            Log.Out("[WalkerSim] Initialized");
        }

        public void SetTimeScale(float scale)
        {
            _timeScale = Mathf.Clamp(scale, 0.01f, 100.0f);
            _accumulator = 0;
        }

        public void Start()
        {
            if (_running)
            {
                Log.Error("Simulation is already running");
                return;
            }

            Log.Out("[WalkerSim] Starting worker..");

            if (!_config.Persistent || !Load())
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
            if (!_config.Persistent)
                return;

            try
            {
                using (Stream stream = File.Open(SimulationFile, FileMode.Create))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    lock (_lock)
                    {
                        formatter.Serialize(stream, _config);

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
                    if (!config.Equals(_config))
                    {
                        Log.Out("[WalkerSim] Configuration changed, not loading save.");
                        return false;
                    }

                    lock (_lock)
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

            lock (_lock)
            {
                // Cleanup all zombies.
                var ents = new List<Entity>(world.Entities.list);
                foreach (var ent in ents)
                {
                    if (ent.entityType == EntityType.Zombie)
                    {
                        world.RemoveEntity(ent.entityId, EnumRemoveEntityReason.Despawned);
                    }
                }

                _activeZombies.Clear();
                _inactiveZombies.Clear();

                // Populate
                CreateInactiveRoaming();
            }

            _nextSave = DateTime.Now.AddMinutes(5);
        }

        private void CreateInactiveRoaming()
        {
            int maxZombies = _maxZombies;
            int numCreated = 0;

            while (_inactiveZombies.Count < maxZombies)
            {
                CreateInactiveZombie(true);
                numCreated++;
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
                    res.x = _prng.Get(_worldMins.x, _worldMaxs.x);
                    res.z = _worldMins.z + 1;
                    break;
                case 1:
                    // Bottom
                    res.x = _prng.Get(_worldMins.x, _worldMaxs.x);
                    res.z = _worldMaxs.z - 1;
                    break;
                case 2:
                    // Left
                    res.x = _worldMins.x + 1;
                    res.z = _prng.Get(_worldMins.z, _worldMaxs.z);
                    break;
                case 3:
                    // Right
                    res.x = _worldMaxs.x - 1;
                    res.z = _prng.Get(_worldMins.z, _worldMaxs.z);
                    break;
            }
            return res;
        }

        private Vector3 GetRandomDir()
        {
            Vector3 res = new Vector3()
            {
                x = _prng.Get(-1.0f, 1.0f),
                y = 0,
                z = _prng.Get(-1.0f, 1.0f),
            };
            return res;
        }

        private ZombieAgent CreateInactiveZombie(bool initial)
        {
            ZombieAgent zombie = new ZombieAgent();
            zombie.id = _nextZombieId++;

            if (initial)
            {
                // The initial population is placed nearby pois more frequently.
                var poiChance = _config.POITravellerChance;

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

            zombie.target = GetNextTarget(zombie);
            zombie.targetPos = GetTargetPos(zombie.target);

            _inactiveZombies.Add(zombie);

            return zombie;
        }

        private void RespawnInactiveZombie(ZombieAgent zombie)
        {
            lock (_inactiveQueue)
            {
                zombie.pos = GetRandomBorderPoint();
                zombie.target = GetNextTarget(zombie);
                zombie.targetPos = GetTargetPos(zombie.target);
                _inactiveQueue.Enqueue(zombie);
            }
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

        private bool CreateZombie(ZombieAgent zombie, PlayerZone zone)
        {
            var world = GameManager.Instance.World;

            if (!CanSpawnActiveZombie())
            {
                return false;
            }

            Vector3 spawnPos = Vector3.zero;
            Chunk chunk = null;

            {
                chunk = (Chunk)world.GetChunkSync(World.toChunkXZ(Mathf.FloorToInt(zombie.pos.x)), 0, World.toChunkXZ(Mathf.FloorToInt(zombie.pos.z)));
                if (chunk == null)
                {
#if DEBUG
                    Log.Out("[WalkerSim] Chunk not loaded at {0} {1}", zombie.pos, zombie.pos.z);
#endif
                    return false;
                }

                int height = world.GetTerrainHeight(Mathf.FloorToInt(zombie.pos.x), Mathf.FloorToInt(zombie.pos.z));

                spawnPos = new Vector3(zombie.pos.x, height + 1.0f, zombie.pos.z);
                if (!world.CanMobsSpawnAtPos(spawnPos))
                {
#if DEBUG
                    Log.Out("[WalkerSim] Unable to spawn zombie at {0}, CanMobsSpawnAtPos failed", spawnPos);
#endif
                    return false;
                }
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
                    zombieEnt.SetInvestigatePosition(zone.center, 6000);
                else
                    zombieEnt.SetInvestigatePosition(targetPos, 6000);
            }

            // If the zombie was previously damaged take health to this one.
            if (zombie.health != -1)
                zombieEnt.Health = zombie.health;
            else
                zombie.health = zombieEnt.Health;

            zombieEnt.IsHordeZombie = true;
            zombieEnt.IsBloodMoon = _worldState.IsBloodMoon();

            zombieEnt.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);

            world.SpawnEntityInWorld(zombieEnt);

            zombie.entityId = zombieEnt.entityId;
            zombie.currentZone = zone;

            zone.numZombies++;

#if DEBUG
            Log.Out("[WalkerSim] Spawned zombie {0} at {1}", zombieEnt, spawnPos);
#endif
            lock (_lock)
            {
                _activeZombies.Add(zombie);
            }

            return true;
        }

        private void RequestActiveZombie(ZombieAgent zombie, PlayerZone zone)
        {
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
            for (; ; )
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
            var closest = _pois.GetRandomClosest(zombie.pos, _prng, 5000, 3);
            if (closest == null)
                return _pois.GetRandom(_prng);
            return closest;
        }

        private Zone GetNextTarget(ZombieAgent zombie)
        {
            if (_prng.Chance(_config.POITravellerChance))
            {
                return GetNextPOI(zombie);
            }
            return _worldZones.GetRandom(_prng);
        }

        private Vector3 GetTargetPos(Zone target)
        {
            return target.GetRandomPos(_prng);
        }

        private void UpdateActiveZombies()
        {
            var world = GameManager.Instance.World;
            int maxPerZone = MaxZombiesPerZone();
            int deactivatedZombies = 0;

            for (int i = 0; i < _activeZombies.Count; i++)
            {
                bool removeZombie = false;

                var zombie = _activeZombies[i];
                var currentZone = zombie.currentZone as PlayerZone;
                if (currentZone != null)
                {
                    currentZone.numZombies--;
                    if (currentZone.numZombies < 0)
                        currentZone.numZombies = 0;
                }

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
                        if (zones.Count == 0)
                        {
#if DEBUG
                            Log.Out("[WalkerSim] Zombie {0} out of range, turning inactive", ent);
#endif
                            deactivatedZombies++;
                            removeZombie = true;

                            world.RemoveEntity(zombie.entityId, EnumRemoveEntityReason.Despawned);

                            lock (_lock)
                            {
                                zombie.entityId = -1;
                                zombie.currentZone = null;
                                _inactiveZombies.Add(zombie);
                            }
                        }
                        else
                        {
                            zombie.currentZone = null;
                            foreach (var zone in zones)
                            {
                                if (zone.numZombies + 1 < maxPerZone)
                                {
                                    zone.numZombies++;
                                    zombie.currentZone = zone;
                                    break;
                                }
                            }
                            if (zombie.currentZone == null)
                            {
#if DEBUG
                                Log.Out("Unable to assign zone for Zombie {0}, all zones full", ent);
#endif
                            }
                        }
                    }
                }

                if (removeZombie)
                {
                    lock (_lock)
                    {
                        _activeZombies.RemoveAt(i);
                        if (_activeZombies.Count == 0)
                            break;
                    }
                    i--;
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

        private void UpdateTarget(ZombieAgent zombie, WorldEvent ev)
        {
            if (ev != null)
            {
                var dist = Vector3.Distance(zombie.pos, ev.Pos);
                if (dist <= ev.Radius)
                {
                    Vector3 soundDir = new Vector3();
                    soundDir.x = _prng.Get(-1.0f, 1.0f);
                    soundDir.z = _prng.Get(-1.0f, 1.0f);

                    soundDir.Normalize();
                    soundDir *= (dist * 0.75f);

                    zombie.targetPos = ev.Pos + soundDir;

#if DEBUG
                    Log.Out("Reacting to sound at {0}", ev.Pos);
#endif
                    return;
                }
            }

            // If we have an activate target wait for arrival.
            if (!zombie.ReachedTarget())
                return;

            if (_worldState.IsBloodMoon())
            {
                zombie.target = _playerZones.GetRandomClosest(zombie.pos, _prng, 200.0f);
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
        }

        private void UpdateInactiveZombie(ZombieAgent zombie, float dt, WorldEvent ev)
        {
            UpdateTarget(zombie, ev);

            zombie.dir = zombie.targetPos - zombie.pos;
            zombie.dir.Normalize();

            float speed = _worldState.GetZombieSpeed() * dt;
            zombie.pos = Vector3.MoveTowards(zombie.pos, zombie.targetPos, speed);
        }

        private bool CanSpawnActiveZombie()
        {
            lock (_spawnQueue)
            {
                int alive = GameStats.GetInt(EnumGameStats.EnemyCount);
                if (alive >= MaxAliveZombies)
                    return false;
                if (_activeZombies.Count + _spawnQueue.Count < MaxAliveZombies)
                    return true;
            }
            return false;
        }

        int MaxZombiesPerZone()
        {
            return MaxAliveZombies / Math.Max(1, ConnectionManager.Instance.Clients.Count);
        }
        private void UpdateInactiveZombies(float dt)
        {
            //Log.Out("[WalkerSim] UpdateInactiveZombies");

            // Repopulate
            lock (_inactiveZombies)
            {
                while (_inactiveQueue.Count > 0)
                {
                    var zombie = _inactiveQueue.Dequeue();
                    _inactiveZombies.Add(zombie);
                }
            }

            // Simulate
            int activatedZombies = 0;
            int maxUpdates = _maxZombies;
            int maxPerZone = MaxZombiesPerZone();
            int numInactive = _inactiveZombies.Count;

            WorldEvent ev = null;
            lock (_worldEvents)
            {
                if (_worldEvents.Count > 0)
                {
                    ev = _worldEvents.Dequeue();
                }
            }

            for (int i = 0; i < numInactive; i++)
            {
                lock (_lock)
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
                _worldState.Update();
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
            float updateRate = 1.0f / (float)_config.UpdateInterval;

            BackgroundWorker worker = sender as BackgroundWorker;
            while (worker.CancellationPending == false)
            {
                bool isPaused = !(_playerZones.HasPlayers() || !_config.PauseWithoutPlayers);

                double dt = updateWatch.ElapsedMicroseconds / 1000000.0;
                updateWatch.ResetAndRestart();

                totalElapsed += dt;

                if (!isPaused)
                {
                    dtAverage += dt;
                    dtAverage *= 0.5;

                    double dtScaled = dt;
                    dtScaled *= _timeScale;
                    _accumulator += dtScaled;
                }
                else
                {
                    dtAverage = 0.0;
                }

                _server.Update();

                if (_accumulator < updateRate)
                {
                    if (isPaused)
                        System.Threading.Thread.Sleep(1000);
                    else
                        System.Threading.Thread.Sleep(1);
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

                BroadcastMapData();

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

        public void BroadcastMapData()
        {
            if (!_server.HasClients())
                return;

            var now = DateTime.Now;
            if (now < _nextBroadcast)
                return;

            // Broadcast only with 20hz.
            _nextBroadcast = now.AddMilliseconds(1.0f / 20.0f);

            try
            {
                Viewer.MapData data = new Viewer.MapData();
                data.w = 512;
                data.h = 512;
                data.mapW = Utils.Distance(_worldMins.x, _worldMaxs.x);
                data.mapH = Utils.Distance(_worldMins.z, _worldMaxs.z);
                data.density = _config.PopulationDensity;
                data.zombieSpeed = _worldState.GetZombieSpeed();
                data.timescale = _timeScale;

                lock (_lock)
                {
                    data.inactive = new List<Viewer.DataZombie>();

                    var inactive = _inactiveZombies;
                    for (int i = 0; i < inactive.Count; i++)
                    {
                        var zombie = inactive[i];
                        Vector2i p = WorldToBitmap(zombie.pos);
                        data.inactive.Add(new Viewer.DataZombie
                        {
                            id = zombie.id,
                            x = p.x,
                            y = p.y,
                        });
                    }

                    data.active = new List<Viewer.DataZombie>();

                    var active = _activeZombies;
                    for (int i = 0; i < active.Count; i++)
                    {
                        var zombie = active[i];
                        Vector2i p = WorldToBitmap(zombie.pos);
                        data.active.Add(new Viewer.DataZombie
                        {
                            id = zombie.id,
                            x = p.x,
                            y = p.y,
                        });
                    }

                    data.playerZones = _playerZones.GetSerializable(this);
                    data.poiZones = _pois.GetSerializable(this);
                    data.worldZones = _worldZones.GetSerializable(this);
                }

                _server.Broadcast(Viewer.DataType.MapData, data);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
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
        }
    }
}
