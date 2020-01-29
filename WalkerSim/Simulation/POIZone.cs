using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
	public class POIZone : Zone
	{
		public int index;
		public Vector3 mins;
		public Vector3 maxs;
		public Vector3 center;

		public ZoneType GetZoneType()
		{
			return ZoneType.POI;
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

		public bool Intersects2D(POIZone poi)
		{
			return (poi.mins.x <= maxs.x && poi.maxs.x >= mins.x) &&
					(poi.mins.z <= maxs.z && poi.maxs.z >= mins.z);
		}
	}

	public class POIZoneManager : ZoneManager<POIZone>
	{
		public void BuildCache()
		{
			DynamicPrefabDecorator dynamicPrefabDecorator = GameManager.Instance.World.ChunkCache.ChunkProvider.GetDynamicPrefabDecorator();
			var pois = dynamicPrefabDecorator.GetPOIPrefabs();
			foreach (var poi in pois)
			{
#if DEBUG
				Log.Out("Poi: {0} {1}", poi.name, poi.boundingBoxPosition);
#endif

				Vector3 pos = new Vector3 { x = poi.boundingBoxPosition.x, y = poi.boundingBoxPosition.y, z = poi.boundingBoxPosition.z };
				Vector3 boxSize = new Vector3 { x = poi.boundingBoxSize.x, y = poi.boundingBoxSize.y, z = poi.boundingBoxSize.z };

				// Increase the box size a bit, this gets better results when merging POIS together.
				// boxSize *= 1.85f;

				var entry = new POIZone
				{
					index = _zones.Count,
					mins = pos - boxSize,
					maxs = pos + boxSize,
					center = pos
				};
				_zones.Add(entry);
			}

			// TODO: Make this a configuration.
			bool mergeZones = false;

			if (mergeZones && _zones.Count > 1)
			{
				// Merge overlapping POIs into single boxes.
				while (true)
				{
					bool merged = false;
					for (int i = 0; i < _zones.Count - 1; i++)
					{
						POIZone cur = _zones[i];
						for (int n = i + 1; n < _zones.Count; n++)
						{
							POIZone next = _zones[n];
							if (cur.Intersects2D(next))
							{
								cur.mins.x = Math.Min(cur.mins.x, next.mins.x);
								cur.mins.y = Math.Min(cur.mins.y, next.mins.y);
								cur.mins.z = Math.Min(cur.mins.z, next.mins.z);

								cur.maxs.x = Math.Max(cur.maxs.x, next.maxs.x);
								cur.maxs.y = Math.Max(cur.maxs.y, next.maxs.y);
								cur.maxs.z = Math.Max(cur.maxs.z, next.maxs.z);

								cur.center = (cur.mins + cur.maxs) / 2.0f;

								_zones.RemoveAt(n);
								n--;
								merged = true;
							}
						}
					}
					if (!merged)
						break;
				}
			}

			// Fix the indices.
			for (int i = 0; i < _zones.Count; i++)
			{
				_zones[i].index = i;
			}

			Log.Out("[WalkerSim] Cached {0} POI zones", _zones.Count);
		}

		public List<Viewer.DataPOIZone> GetSerializable(Simulation sim)
		{
			var pois = new List<Viewer.DataPOIZone>();
			foreach (var poi in _zones)
			{
				Vector2i p1 = sim.WorldToBitmap(poi.mins);
				Vector2i p2 = sim.WorldToBitmap(poi.maxs);
				pois.Add(new Viewer.DataPOIZone
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
