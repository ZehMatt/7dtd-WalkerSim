using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
	public class TargetZone : Zone
	{
		static int BorderSize = 100;
		static int ZoneSize = 10;

		public bool valid = true;
		public int index = -1;
		public Vector3 mins = Vector3.zero;
		public Vector3 maxs = Vector3.zero;
		public Vector3 center = Vector3.zero;

		public static TargetZone CreateRandom(PRNG prng, Vector3i worldMins, Vector3i worldMaxs)
		{
			float x = prng.Get(worldMins.x + BorderSize, worldMaxs.x - BorderSize);
			float z = prng.Get(worldMins.z + BorderSize, worldMaxs.z - BorderSize);

			return new TargetZone
			{
				mins = new Vector3(x - ZoneSize, worldMins.y, z - ZoneSize),
				maxs = new Vector3(x + ZoneSize, worldMaxs.y, z + ZoneSize),
				center = new Vector3(x, worldMins.y, z),
			};
		}

		public ZoneType GetZoneType()
		{
			return ZoneType.Target;
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
}
