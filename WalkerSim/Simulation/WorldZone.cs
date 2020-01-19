using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
	public class WorldZone : Zone
	{
		public int index = -1;
		public Vector3 mins = Vector3.zero;
		public Vector3 maxs = Vector3.zero;
		public Vector3 center = Vector3.zero;

		public ZoneType GetZoneType()
		{
			return ZoneType.World;
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
	}

	public class WorldZoneManager : ZoneManager<WorldZone>
	{
		static int BorderSize = 128;

		public void BuildZones(Vector3i worldMins, Vector3i worldMaxs, Config config)
		{
			int lenX = Utils.Distance(worldMins.x + BorderSize, worldMaxs.x - BorderSize);
			int lenY = Utils.Distance(worldMins.z + BorderSize, worldMaxs.z - BorderSize);

			int zoneSizeX = lenX / config.WorldZoneDivider;
			int zoneSizeY = lenY / config.WorldZoneDivider;

			for (int y = worldMins.z + BorderSize; y < worldMaxs.z - BorderSize; y += zoneSizeY)
			{
				for (int x = worldMins.x + BorderSize; x < worldMaxs.x - BorderSize; x += zoneSizeX)
				{
					var mins = new Vector3(x, worldMaxs.y, y);
					var maxs = new Vector3(x + zoneSizeX, worldMaxs.y, y + zoneSizeY);
					_zones.Add(new WorldZone()
					{
						mins = mins,
						maxs = maxs,
						center = (mins + maxs) * 0.5f,
						index = _zones.Count
					});
				}
			}

			Log.Out("[WalkerSim] Cached {0} world zones", _zones.Count);
		}

		public List<Viewer.DataWorldZone> GetSerializable(Simulation sim)
		{
			var pois = new List<Viewer.DataWorldZone>();
			foreach (var poi in _zones)
			{
				Vector2i p1 = sim.WorldToBitmap(poi.mins);
				Vector2i p2 = sim.WorldToBitmap(poi.maxs);
				pois.Add(new Viewer.DataWorldZone
				{
					x1 = p1.x,
					y1 = p1.y,
					x2 = p2.x,
					y2 = p2.y,
				});
			}
			return pois;
		}
	}
}
