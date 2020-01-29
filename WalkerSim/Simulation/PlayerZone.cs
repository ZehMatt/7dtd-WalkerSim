using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
	public class PlayerZone : Zone
	{
		public bool valid = true;
		public int index = -1;
		public Vector3 mins = Vector3.zero;
		public Vector3 maxs = Vector3.zero;
		public Vector3 minsSpawnBlock = Vector3.zero;
		public Vector3 maxsSpawnBlock = Vector3.zero;
		public Vector3 center = Vector3.zero;
		public Bounds triggerBounds = new Bounds();
		public int numZombies = 0;
		public int entityId = -1;

		public ZoneType GetZoneType()
		{
			return ZoneType.Player;
		}

		public int GetIndex()
		{
			return index;
		}

		// Zone AABB min
		public Vector3 GetMins()
		{
			return mins;
		}

		// Zone AABB min
		public Vector3 GetMaxs()
		{
			return maxs;
		}

		// Returns the center of the center.
		public Vector3 GetCenter()
		{
			return center;
		}

		// Returns a random position within the zone.
		public Vector3 GetRandomPos(PRNG prng)
		{
			return new Vector3
			{
				x = prng.Get(mins.x, maxs.x),
				y = prng.Get(mins.y, maxs.y),
				z = prng.Get(mins.z, maxs.z),
			};
		}

		public bool IsInside2D(Vector3 pos)
		{
			return pos.x >= mins.x &&
				pos.z >= mins.z &&
				pos.x <= maxs.x &&
				pos.z <= maxs.z;
		}

		public bool InsideSpawnBlock2D(Vector3 pos)
		{
			return pos.x >= minsSpawnBlock.x &&
				pos.z >= minsSpawnBlock.z &&
				pos.x <= maxsSpawnBlock.x &&
				pos.z <= maxsSpawnBlock.z;
		}

		public bool InsideSpawnArea2D(Vector3 pos)
		{
			return IsInside2D(pos) && !InsideSpawnBlock2D(pos);
		}
	}

	public class PlayerZoneManager : ZoneManager<PlayerZone>
	{
		static int ChunkViewDim = (GamePrefs.GetInt(EnumGamePrefs.ServerMaxAllowedViewDistance) / 10);
		static Vector3 VisibleBox = new Vector3(ChunkViewDim, 32, ChunkViewDim);
		static Vector3 SpawnBlockBox = new Vector3(ChunkViewDim - 32, 32, ChunkViewDim - 32);
		static Vector3 TriggerBounds = new Vector3(16, 3, 16);

		public PlayerZoneManager()
		{
			Log.Out("[WalkerSim] Player Chunk View Dim: {0} - {1} - {2}", ChunkViewDim,
				VisibleBox,
				SpawnBlockBox);
		}

		public void AddPlayer(int entityId)
		{
			lock (_lock)
			{
				// This is called from PlayerSpawn, PlayerLogin has no entity id assigned yet.
				// So we have to check if the player is already here.
				foreach (var zone in _zones)
				{
					if (zone.entityId == entityId)
						return;
				}
				PlayerZone area = new PlayerZone
				{
					index = _zones.Count,
					entityId = entityId,
				};
				_zones.Add(UpdatePlayer(area, entityId));
			}
		}

		public void RemovePlayer(int entityId)
		{
			lock (_lock)
			{
				for (int i = 0; i < _zones.Count; i++)
				{
					var ply = _zones[i] as PlayerZone;
					if (ply.entityId == entityId)
					{
						_zones.RemoveAt(i);
						return;
					}
				}
			}
		}

		PlayerZone UpdatePlayer(PlayerZone ply, EntityPlayer ent)
		{
			var pos = ent.GetPosition();
			ply.mins = pos - VisibleBox;
			ply.maxs = pos + VisibleBox;
			ply.minsSpawnBlock = pos - SpawnBlockBox;
			ply.maxsSpawnBlock = pos + SpawnBlockBox;
			ply.center = pos;
			ply.triggerBounds = new Bounds(ply.center, TriggerBounds);
			return ply;
		}

		PlayerZone UpdatePlayer(PlayerZone ply, int entityId)
		{
			var world = GameManager.Instance.World;
			var players = world.Players.dict;

			EntityPlayer ent = null;
			if (players.TryGetValue(entityId, out ent))
			{
				ply = UpdatePlayer(ply, ent);
			}

			return ply;
		}

		public void Update()
		{
			lock (_lock)
			{
				var world = GameManager.Instance.World;
				var players = world.Players.dict;

				for (int i = 0; i < _zones.Count; i++)
				{
					var ply = _zones[i] as PlayerZone;

					EntityPlayer ent = null;
					if (players.TryGetValue(ply.entityId, out ent))
					{
						_zones[i] = UpdatePlayer(ply, ent);
					}
					else
					{
						// Remove player.
						ply.valid = false;
						_zones.RemoveAt(i);
						i--;
					}

					if (_zones.Count == 0)
						break;
				}
			}
		}

		public List<Viewer.DataPlayerZone> GetSerializable(Simulation sim)
		{
			lock (_lock)
			{
				var res = new List<Viewer.DataPlayerZone>();
				foreach (var zone in _zones)
				{
					// Zone
					Vector2i p1 = sim.WorldToBitmap(zone.mins);
					Vector2i p2 = sim.WorldToBitmap(zone.maxs);

					// Spawn Block.
					Vector2i p3 = sim.WorldToBitmap(zone.minsSpawnBlock);
					Vector2i p4 = sim.WorldToBitmap(zone.maxsSpawnBlock);

					res.Add(new Viewer.DataPlayerZone
					{
						x1 = p1.x,
						y1 = p1.y,
						x2 = p2.x,
						y2 = p2.y,
						x3 = p3.x,
						y3 = p3.y,
						x4 = p4.x,
						y4 = p4.y,
					});
				}
				return res;
			}
		}

		public bool HasPlayers()
		{
			lock (_lock)
			{
				return _zones.Count > 0;
			}
		}
	}
}
