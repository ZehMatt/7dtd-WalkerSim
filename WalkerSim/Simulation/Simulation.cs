using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System.IO;

namespace WalkerSim
{
	class ZombieSpawnRequest
	{
		public ZombieAgent zombie;
		public PlayerZone zone;
	}

	public class Simulation
	{
		static int DayTimeMin = GamePrefs.GetInt(EnumGamePrefs.DayNightLength);
		static int MaxAliveZombies = 64; // GamePrefs.GetInt(EnumGamePrefs.MaxSpawnedZombies);

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
		Sleepers _sleepers = new Sleepers();

		List<ZombieAgent> _inactiveZombies = new List<ZombieAgent>();
		List<ZombieAgent> _activeZombies = new List<ZombieAgent>();
		Dictionary<Vector2i, int> _zoneCounter = new Dictionary<Vector2i, int>();

		Queue<ZombieSpawnRequest> _spawnQueue = new Queue<ZombieSpawnRequest>();
		System.Object _spawnQueueLock = new System.Object();

		Queue<ZombieAgent> _inactiveQueue = new Queue<ZombieAgent>();
		System.Object _inactiveQueueLock = new System.Object();

		Vector3i _worldMins = new Vector3i();
		Vector3i _worldMaxs = new Vector3i();

		PRNG _prng = new PRNG(0);

		int _nextZombieId = 0;
		int _maxZombies = 0;
		int _updateOffset = 0;
		float _timeScale = 1.0f;
		double _accumulator = 0.0;
		int _spinupTicks = 0;

		DateTime _nextSave = DateTime.Now;

		BackgroundWorker _worker = new BackgroundWorker();
		bool _running = false;

		public Simulation()
		{
			_config.Load(ConfigFile);

			GameManager.Instance.World.GetWorldExtent(out _worldMins, out _worldMaxs);

			_worker.WorkerSupportsCancellation = true;
			_worker.DoWork += BackgroundUpdate;

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

			_pois.BuildCache();
			_worldZones.BuildZones(_worldMins, _worldMaxs, _config);

			if (_config.IncludeSleepers)
			{
				_sleepers.BuildCache();
			}

			if (!_config.Persistent || !Load())
			{
				Reset();
			}

			_nextSave = DateTime.Now.AddMinutes(5);

			Log.Out("[WalkerSim] Initialized");
		}

		public void SetTimeScale(float scale)
		{
			_timeScale = Mathf.Clamp(scale, 0.01f, 100.0f);
		}

		public void Start()
		{
			Log.Out("[WalkerSim] Starting worker..");

			_running = true;
			_worker.RunWorkerAsync();
		}

		public void Stop()
		{
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
			try
			{
				using (Stream stream = File.Open(SimulationFile, FileMode.Create))
				{
					BinaryFormatter formatter = new BinaryFormatter();
					lock (_lock)
					{
						List<ZombieData> data = new List<ZombieData>();
						foreach (var zombie in _inactiveZombies)
						{
							data.Add(new ZombieData
							{
								health = zombie.health,
								x = zombie.pos.x,
								y = zombie.pos.z,
								dirX = zombie.dir.x,
								dirY = zombie.dir.z,
								target = zombie.target is POIZone,
								sleeperIndex = zombie.sleeperSpawn != null ? zombie.sleeperSpawn.index : -1,
								sleeping = zombie.sleeping,
							});
						}
						formatter.Serialize(stream, data);
					}
					Log.Out("Saved simulation");
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
				var sleeperSpawns = _sleepers.GetSpawns();

				using (Stream stream = File.Open(SimulationFile, FileMode.Open))
				{
					BinaryFormatter formatter = new BinaryFormatter();
					lock (_lock)
					{
						List<ZombieData> data = formatter.Deserialize(stream) as List<ZombieData>;
						if (data.Count > 0)
						{
							_inactiveZombies.Clear();
							foreach (var zombie in data)
							{
								SleeperSpawn sleeperSpawn = null;
								if (zombie.sleeperIndex != -1 && zombie.sleeperIndex < sleeperSpawns.Count)
								{
									sleeperSpawn = sleeperSpawns[zombie.sleeperIndex];
								}
								_inactiveZombies.Add(new ZombieAgent
								{
									health = zombie.health,
									pos = new Vector3(zombie.x, 0.0f, zombie.y),
									dir = new Vector3(zombie.dirX, 0.0f, zombie.dirY),
									target = zombie.target ? _pois.GetRandom(_prng) : null,
									sleeperSpawn = sleeperSpawn,
									sleeping = zombie.sleeping,
								});
							}
							Log.Out("Loaded {0} inactive zombies", _inactiveZombies.Count);
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
				var sleepers = _sleepers.GetSpawns();

				int numCreated = 0;
				int maxSleepers = _maxZombies * 10 / 100;
				int numSleepers = Math.Min(maxSleepers, sleepers.Count);
				int maxZombies = _maxZombies - numSleepers;

				while (_inactiveZombies.Count < maxZombies)
				{
					CreateInactiveZombie(true);
					numCreated++;
				}

				List<SleeperSpawn> sleeperList = new List<SleeperSpawn>(sleepers);
				for (int i = 0; i < numSleepers; i++)
				{
					var pick = _prng.Get(0, sleeperList.Count);
					var sleeperSpawn = sleeperList[pick];

					var targetPos = sleeperSpawn.pos;

					ZombieAgent zombie = CreateInactiveZombie(true);
					zombie.targetPos.x = targetPos.x;
					zombie.targetPos.y = targetPos.y;
					zombie.targetPos.z = targetPos.z;
					zombie.sleeperSpawn = sleeperSpawn;

					numCreated++;
					sleeperList.RemoveAt(pick);
				}

				if (numCreated > 0)
				{
					Log.Out("Created {0} inactive zombies, {1} sleepers", numCreated, numSleepers);
				}

				if (_config.SpinupTicks > 0)
				{
					Log.Out("Reset simulation, spin-up ticks: {0}", _config.SpinupTicks);

					float updateRate = 1.0f / (float)_config.UpdateInterval;
					_accumulator += (updateRate * _config.SpinupTicks);
					_spinupTicks = _config.SpinupTicks;
				}
			}
		}

		private Vector2i GetRandomPos()
		{
			Vector2i res = new Vector2i();
			res.x = _prng.Get(_worldMins.x, _worldMaxs.x);
			res.y = _prng.Get(_worldMins.z, _worldMaxs.z);
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
			zombie.pos = GetRandomBorderPoint();
			zombie.target = GetNextTarget(zombie);
			zombie.targetPos = GetTargetPos(zombie.target);

			_inactiveZombies.Add(zombie);

			return zombie;
		}

		private void RespawnInactiveZombie(ZombieAgent zombie)
		{
			lock (_inactiveQueueLock)
			{
				zombie.pos = GetRandomBorderPoint();
				if (zombie.sleeperSpawn == null)
				{
					zombie.target = GetNextTarget(zombie);
					zombie.targetPos = GetTargetPos(zombie.target);
				}
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
				RespawnInactiveZombie(zombie);
				return false;
			}

			Vector3 spawnPos = Vector3.zero;
			if (zombie.sleeperSpawn != null)
			{
				spawnPos = zombie.sleeperSpawn.pos.ToVector3();
				spawnPos.x += 0.5f;
				spawnPos.z += 0.5f;

				Chunk chunk = (Chunk)world.GetChunkSync(World.toChunkXZ(Mathf.FloorToInt(spawnPos.x)), 0, World.toChunkXZ(Mathf.FloorToInt(spawnPos.z)));
				if (chunk == null)
				{
#if DEBUG
					Log.Out("Chunk not loaded at {0} {1}", zombie.pos, zombie.pos.z);
#endif
					RespawnInactiveZombie(zombie);
					return false;
				}
			}
			else
			{
				int height = world.GetTerrainHeight(Mathf.FloorToInt(zombie.pos.x), Mathf.FloorToInt(zombie.pos.z));

				spawnPos = new Vector3(zombie.pos.x, height + 1.0f, zombie.pos.z);
				if (!world.CanMobsSpawnAtPos(spawnPos))
				{
#if DEBUG
					Log.Out("Unable to spawn zombie at {0}, CanMobsSpawnAtPos failed", spawnPos);
#endif
					RespawnInactiveZombie(zombie);
					return false;
				}
			}

			if (zombie.classId == -1)
			{
				int lastClassId = -1;
				zombie.classId = EntityGroups.GetRandomFromGroup(_config.ZombieGroup, ref lastClassId);
			}

			EntityZombie zombieEnt = EntityFactory.CreateEntity(zombie.classId, spawnPos) as EntityZombie;
			if (zombieEnt == null)
			{
#if DEBUG
				Log.Error("Unable to create zombie entity!, Entity Id: {0}, Pos: {1}", zombie.classId, spawnPos);
#endif
				RespawnInactiveZombie(zombie);
				return false;
			}

			zombieEnt.bIsChunkObserver = true;

			if (zombie.sleeperSpawn != null)
			{
				zombieEnt.IsSleeperPassive = false;
				zombieEnt.IsSleeperDecoy = false;
				zombieEnt.SleeperSpawnPosition = spawnPos;
				zombieEnt.SleeperSpawnLookDir = zombie.sleeperSpawn.look;

				TileEntitySleeper tileEntitySleeper = world.GetTileEntity(0, zombie.sleeperSpawn.pos) as TileEntitySleeper;
				if (tileEntitySleeper != null)
				{
					zombieEnt.SetSleeperSight(tileEntitySleeper.GetSightAngle(), tileEntitySleeper.GetSightRange());
					zombieEnt.SetSleeperHearing(tileEntitySleeper.GetHearingPercent());
				}

				//if (zombie.sleeping)
				{
					zombieEnt.TriggerSleeperPose(zombie.sleeperSpawn.pose);
				}

				zombieEnt.SetSleeper();
			}
			else
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

			zombieEnt.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);

			world.SpawnEntityInWorld(zombieEnt);

			zombie.entityId = zombieEnt.entityId;
			zombie.currentZone = zone;

			zone.numZombies++;

#if DEBUG
			Log.Out("Spawned zombie {0} at {1}", zombieEnt, spawnPos);
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
			lock (_spawnQueueLock)
			{
				_spawnQueue.Enqueue(spawn);
			}
		}

		private void ProcessSpawnQueue()
		{
			while (_spawnQueue.Count > 0)
			{
				ZombieSpawnRequest zombieSpawn = null;
				lock (_spawnQueueLock)
				{
					zombieSpawn = _spawnQueue.Dequeue();
				}
				if (zombieSpawn == null)
					break;

				CreateZombie(zombieSpawn.zombie, zombieSpawn.zone);
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
				var zombie = _activeZombies[i];

				bool removeZombie = false;

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
					Log.Out("Failed to get zombie with entity id {0}", zombie.entityId);
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
						if (zombie.sleeperSpawn != null)
						{
							zombie.sleeping = ent.IsSleeping;
						}

						List<PlayerZone> zones = _playerZones.FindAllByPos2D(ent.GetPosition());
						if (zones.Count == 0)
						{
#if DEBUG
							Log.Out("Zombie {0} out of range, turning inactive", ent);
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
				else
				{
					_activeZombies[i] = zombie;
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

		private void UpdateTarget(ZombieAgent zombie)
		{
			Zone target = zombie.target;

			bool newTarget = false;
			if (zombie.ReachedTarget())
			{
				if (zombie.sleeperSpawn != null)
				{
					zombie.sleeping = true;
					return;
				}

				if (_worldState.IsBloodMoon())
				{
					target = _playerZones.GetRandomClosest(zombie.pos, _prng, 200.0f);
					if (target == null)
					{
						target = GetNextTarget(zombie);
					}
				}
				else
					target = GetNextTarget(zombie);

				newTarget = true;
			}
			else
			{
				// During blood moon force them towards player zones.
				if (!zombie.IsSleeper() && _worldState.IsBloodMoon())
				{
					var playerTarget = _playerZones.GetRandomClosest(zombie.pos, _prng, 200.0f);
					if (playerTarget != null)
					{
						target = playerTarget;
						newTarget = true;
					}
				}
			}

			if (newTarget && zombie.target != target && target != null)
			{
				zombie.targetPos = GetTargetPos(target);
			}

			zombie.target = target;
		}

		private void UpdateInactiveZombie(ZombieAgent zombie, float dt)
		{
			UpdateTarget(zombie);

			zombie.dir = zombie.targetPos - zombie.pos;
			zombie.dir.Normalize();

			float speed = _worldState.GetZombieSpeed() * dt;
			if (_spinupTicks > 0)
				speed *= 10.0f;

			float dist = Vector3.Distance(zombie.pos, zombie.targetPos);
			if (dist > 100.0f)
			{
				// When the distance is big enough add noise towards forward vector.
				zombie.dir += GetRandomDir() * 0.4f;
				zombie.dir.Normalize();

				zombie.pos = WrapPos(zombie.pos + (zombie.dir * speed));
			}
			else
			{
				// More accurate and does not overshoot.
				zombie.pos = Vector3.MoveTowards(zombie.pos, zombie.targetPos, speed);
			}
		}

		private bool CanSpawnActiveZombie()
		{
			lock (_spawnQueueLock)
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
			while (_inactiveQueue.Count > 0)
			{
				lock (_inactiveQueueLock)
				{
					var zombie = _inactiveQueue.Dequeue();
					if (_inactiveZombies.Count < _maxZombies)
					{
						_inactiveZombies.Add(zombie);
					}
				}
			}

			// Simulate
			int activatedZombies = 0;
			int maxUpdates = _maxZombies;
			int maxPerZone = MaxZombiesPerZone();
			var world = GameManager.Instance.World;

			for (int i = 0; i < maxUpdates && _inactiveZombies.Count > 0; i++, _updateOffset++)
			{
				lock (_lock)
				{
					int offset = _updateOffset % _inactiveZombies.Count;

					bool removeZombie = false;
					bool activatedZombie = false;

					ZombieAgent zombie = _inactiveZombies[offset];
					UpdateInactiveZombie(zombie, dt);

					if (!CanSpawnActiveZombie())
						continue;

					List<PlayerZone> zones = _playerZones.FindAllByPos2D(zombie.pos);
					if (zones.Count <= 0)
						continue;

					foreach (var zone in zones)
					{
						var player = world.GetEntity(zone.entityId) as EntityPlayer;

						if (zombie.IsSleeper())
						{
							// Obey sleeper spawn triggers.
							var sleeperSpawn = zombie.sleeperSpawn;
							if (!sleeperSpawn.bounds.Intersects(zone.triggerBounds))
								continue;

							if (player.CanSee(sleeperSpawn.pos.ToVector3()))
							{
#if DEBUG
								Log.Out("Player {0} can see position {1}, removing", player, sleeperSpawn.pos);
#endif
								removeZombie = true;
								continue;
							}
						}
						else
						{
							// Use players spawn border.
							if (!zone.InsideSpawnArea2D(zombie.pos))
								continue;
						}

						if (zone.numZombies >= maxPerZone)
						{
#if DEBUG
							Log.Out("Zone {0} is full: {1} / {2}", zombie.pos, zone.numZombies, maxPerZone);
#endif
							continue;
						}

						RequestActiveZombie(zombie, zone);
						activatedZombie = true;
						activatedZombies++;
						break;
					}

					// Zombie inside one or more zones will be always removed.
					if (zombie.IsSleeper() && activatedZombie)
						removeZombie = true;
					else if (!zombie.IsSleeper())
						removeZombie = true;

					if (removeZombie)
					{
						_inactiveZombies.RemoveAt(offset);

						// If the zombie was not activated begin a new cycle.
						if (!activatedZombie)
						{
							RespawnInactiveZombie(zombie);
						}

						// NOTE: This should never happen.
						if (_inactiveZombies.Count == 0)
							break;
					}
				}
			}

			if (activatedZombies > 0)
			{
				Log.Out("Activated {0} zombies", activatedZombies);
			}
		}

		public void Update()
		{
			if (!_running)
				return;

			try
			{
				_worldState.Update();
				_playerZones.Update();
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

			MicroStopwatch stopwatch = new MicroStopwatch();
			stopwatch.Start();

			double totalElapsed = 0.0;
			double dtAverage = 0.0;
			double nextReport = 10.0;
			float updateRate = 1.0f / (float)_config.UpdateInterval;

			BackgroundWorker worker = sender as BackgroundWorker;
			while (worker.CancellationPending == false)
			{
				double dt = stopwatch.ElapsedMicroseconds / 1000000.0;
				stopwatch.ResetAndRestart();

				totalElapsed += dt;
				dtAverage += dt;
				dtAverage *= 0.5;

				double dtScaled = dt;
				dtScaled *= _timeScale;
				_accumulator += dtScaled;

				_server.Update();

				if (_accumulator < updateRate)
				{
					//double remaining = (updateRate - accumulator) * 1000.0;
					//int sleepTime = (int)Math.Floor(remaining);
					System.Threading.Thread.Sleep(1);
				}
				else
				{
					int numUpdates = 0;

					while (_accumulator >= updateRate && numUpdates < 200)
					{
						try
						{
							UpdateInactiveZombies(updateRate);
							CheckAutoSave();
						}
						catch (Exception ex)
						{
							//Log.Out("Exception in worker: {0}", ex.Message);
							Log.Error("Exception in worker");
							Log.Exception(ex);
						}

						_accumulator -= updateRate;
						numUpdates++;

						if (_spinupTicks > 0)
						{
							_spinupTicks--;
							if (_spinupTicks == 0)
							{
								Log.Out("[WalkerSim] Spin-up complete");
								Save();
							}
						}
					}

					// Broadcast at fixed rate.
					BroadcastMapData();
				}

				if (totalElapsed >= nextReport)
				{
					double avgFps = 1 / dtAverage;
					Log.Out("[WalkerSim] FPS Average: {0}", avgFps);
					nextReport = totalElapsed + 60.0;
				}
			}

			Log.Out("Worker Finished");
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

			try
			{
				Viewer.MapData data = new Viewer.MapData();
				data.w = 512;
				data.h = 512;
				data.mapW = Utils.Distance(_worldMins.x, _worldMaxs.x);
				data.mapH = Utils.Distance(_worldMins.z, _worldMaxs.z);
				data.density = _config.PopulationDensity;

				data.inactive = new List<Viewer.DataZombie>();
				data.active = new List<Viewer.DataZombie>();
				data.playerZones = new List<Viewer.DataPlayerZone>();

				lock (_lock)
				{
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
							sleeper = zombie.IsSleeper(),
						});
					}

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
							sleeper = zombie.IsSleeper(),
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
	}
}
