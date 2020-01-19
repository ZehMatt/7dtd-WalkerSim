using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
	public class ZoneManager<T>
	{
		protected List<T> _zones = new List<T>();
		protected System.Object _lock = new System.Object();
		protected int _pickCount = 0;

		public T GetNext()
		{
			lock (_lock)
			{
				if (_zones.Count == 0)
					return default(T);

				int pick = _pickCount % _zones.Count;
				_pickCount++;

				return (T)_zones[pick];
			}
		}

		public T GetRandom(PRNG prng, int lastIndex = -1)
		{
			lock (_lock)
			{
				if (_zones.Count == 0)
					return default(T);

				for (int i = 0; i < 4; i++)
				{
					var idx = prng.Get(0, _zones.Count);
					var zone = _zones[idx] as Zone;
					if (zone.GetIndex() != lastIndex)
					{
						return (T)zone;
					}
				}

				return default(T);
			}
		}

		public T FindByPos2D(Vector3 pos)
		{
			lock (_lock)
			{
				foreach (var zone in _zones)
				{
					var z = zone as Zone;
					if (z.IsInside2D(pos))
						return (T)zone;
				}
				return default(T);
			}
		}

		public List<T> FindAllByPos2D(Vector3 pos)
		{
			List<T> res = new List<T>();
			lock (_lock)
			{
				foreach (var zone in _zones)
				{
					var z = zone as Zone;
					if (z.IsInside2D(pos))
					{
						res.Add((T)zone);
					}
				}
			}
			return res;
		}

		public T GetRandomClosest(Vector3 pos, PRNG prng, float maxDistance, int numAttempts = 5)
		{
			lock (_lock)
			{
				if (_zones.Count == 0)
					return default(T);

				float bestDistance = maxDistance;

				T res = default(T);
				for (int i = 0; i < numAttempts; i++)
				{
					var zone = GetRandom(prng) as Zone;
					float dist = Vector3.Distance(zone.GetCenter(), pos);
					if (dist < bestDistance)
					{
						res = (T)zone;
						bestDistance = dist;
					}
				}

				return res;
			}
		}

		public T GetClosest(Vector3 pos, float maxDistance)
		{
			lock (_lock)
			{
				if (_zones.Count == 0)
					return default(T);

				float bestDistance = float.MaxValue;

				T res = default(T);
				for (int i = 0; i < _zones.Count; i++)
				{
					var zone = _zones[i] as Zone;
					float dist = Vector3.Distance(zone.GetCenter(), pos);
					if (dist < bestDistance)
					{
						res = (T)zone;
						bestDistance = dist;
					}
				}

				return res;
			}
		}
	}
}
