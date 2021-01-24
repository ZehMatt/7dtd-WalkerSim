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

    public interface Zone
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
