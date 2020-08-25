using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
    [Serializable()]
    struct ZombieData
    {
        public int health;
        public float x;
        public float y;
        public float z;
        public float targetX;
        public float targetY;
        public float targetZ;
        public float dirX;
        public float dirY;
        public bool target;
    }

    class ZombieAgent : IComparer, IEquatable<ZombieAgent>
    {
        public int entityId = -1;
        public int id = -1;
        public int classId = -1;
        public int health = -1;
        public Vector3 pos = new Vector3();
        public Vector3 targetPos = new Vector3();
        public Vector3 dir = new Vector3();
        public Zone target = null;
        public Zone currentZone = null;
        int IComparer.Compare(object a, object b)
        {
            return ((ZombieAgent)a).id - ((ZombieAgent)b).id;
        }

        public bool Equals(ZombieAgent other)
        {
            return id == other.id;
        }

        public bool ReachedTarget()
        {
            Vector3 a = new Vector3(pos.x, 0, pos.z);
            Vector3 b = new Vector3(targetPos.x, 0, targetPos.z);

            float dist = Vector3.Distance(a, b);
            if (dist <= 2.0f)
                return true;

            return false;
        }

    }
}
