using System.Collections.Generic;
using UnityEngine;

namespace WalkerSim
{
    enum WorldEventType
    {
        Sound,
    }

    class WorldEvent
    {
        public WorldEventType Type;
        public Vector3 Pos;
        public float Radius;
    }

    partial class Simulation
    {
        Queue<WorldEvent> _worldEvents = new Queue<WorldEvent>();

        public void AddSoundEvent(Vector3 pos, float radius)
        {
            lock (_worldEvents)
            {
                _worldEvents.Enqueue(new WorldEvent()
                {
                    Type = WorldEventType.Sound,
                    Pos = pos,
                    Radius = radius,
                });
            }

            SendSoundEvent(_server, pos, radius);
        }
    }
}
