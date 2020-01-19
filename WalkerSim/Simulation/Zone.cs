using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
	public enum ZoneType
	{
		Player,
		POI,
		Target,
		World,
	}

	interface Zone
	{
		ZoneType GetZoneType();

		int GetIndex();

		bool IsInside2D(Vector3 pos);

		// Zone AABB min
		Vector3 GetMins();

		// Zone AABB min
		Vector3 GetMaxs();

		// Returns the center of the center.
		Vector3 GetCenter();

		// Returns a random position within the zone.
		Vector3 GetRandomPos(PRNG prng);
	}
}
