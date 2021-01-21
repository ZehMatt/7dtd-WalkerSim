using System.Collections.Generic;
using UnityEngine;

namespace WalkerSim
{
    public class ZoneManager<T>
    {
        protected List<T> _zones = new List<T>();
        protected System.Object _lock = new System.Object();
        protected int _pickCount = 0;

        public long C
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

        public T GetRandomClosest(Vector3 pos, PRNG prng, float maxDistance)
        {
            lock (_lock)
            {
                if (_zones.Count == 0)
                    return default(T);

                float bestDistance = maxDistance;
                List<Zone> inRange = new List<Zone>();

                foreach (var zone in _zones)
                {
                    var z = zone as Zone;
                    var zonePos = z.GetCenter();
                    if (Vector3.Distance(pos, zonePos) <= maxDistance)
                    {
                        inRange.Add(z);
                        if (inRange.Count >= 128)
                            break;
                    }
                }

                if (inRange.Count == 0)
                    return default(T);

                var res = inRange[prng.Get(inRange.Count)];
                return (T)res;
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

        public void Clear()
        {
            lock (_lock)
            {
                _zones.Clear();
            }
        }
    }
}
