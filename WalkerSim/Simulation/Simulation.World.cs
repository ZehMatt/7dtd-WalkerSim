using UnityEngine;

namespace WalkerSim
{
    partial class Simulation
    {
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
                    res.x = _prng.Get(_worldMins.x + 1, _worldMaxs.x - 1);
                    res.z = _worldMins.z + 1;
                    break;
                case 1:
                    // Bottom
                    res.x = _prng.Get(_worldMins.x + 1, _worldMaxs.x - 1);
                    res.z = _worldMaxs.z - 1;
                    break;
                case 2:
                    // Left
                    res.x = _worldMins.x + 1;
                    res.z = _prng.Get(_worldMins.z + 1, _worldMaxs.z - 1);
                    break;
                case 3:
                    // Right
                    res.x = _worldMaxs.x - 1;
                    res.z = _prng.Get(_worldMins.z + 1, _worldMaxs.z - 1);
                    break;
            }
            return res;
        }

    }
}
