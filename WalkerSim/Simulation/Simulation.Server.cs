using System.Collections.Generic;
using UnityEngine;

namespace WalkerSim
{
    partial class Simulation
    {

        public Vector2i WorldToBitmap(Vector3 pos)
        {
            Vector2i res = new Vector2i();
            res.x = (int)Utils.Remap(pos.x, _worldMins.x, _worldMaxs.x, 0, 512);
            res.y = (int)Utils.Remap(pos.z, _worldMins.z, _worldMaxs.z, 0, 512);
            return res;
        }

        void SendState(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            var data = new Viewer.State();
            data.w = 512;
            data.h = 512;
            data.mapW = Utils.Distance(_worldMins.x, _worldMaxs.x);
            data.mapH = Utils.Distance(_worldMins.z, _worldMaxs.z);
            data.density = Config.Instance.PopulationDensity;
            data.zombieSpeed = _state.ZombieSpeed;
            data.timescale = _state.Timescale;

            sender.SendData(cl, Viewer.DataType.Info, data);
        }

        void SendPOIZones(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            var zones = _pois.GetSerializable(this);
            if (zones.Count == 0)
                return;

            var data = new Viewer.POIZones();
            data.zones = zones;
            sender.SendData(cl, Viewer.DataType.POIZones, data);
        }
        void SendWorldZones(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            var data = new Viewer.WorldZones();
            data.zones = _worldZones.GetSerializable(this);
            sender.SendData(cl, Viewer.DataType.WorldZones, data);
        }
        void SendPlayerZones(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            var data = new Viewer.PlayerZones();
            data.zones = _playerZones.GetSerializable(this);
            sender.SendData(cl, Viewer.DataType.PlayerZones, data);
        }

        void SendInactiveZombieList(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            lock (_inactiveZombies)
            {
                if (_inactiveZombies.Count == 0)
                    return;

                var list = new List<Viewer.DataZombie>();
                foreach (var zombie in _inactiveZombies)
                {
                    Vector2i p = WorldToBitmap(zombie.pos);
                    list.Add(new Viewer.DataZombie
                    {
                        id = zombie.id,
                        x = p.x,
                        y = p.y,
                    });
                }

                var data = new Viewer.ZombieList();
                data.list = list;

                sender.SendData(cl, Viewer.DataType.InactiveZombies, data);
            }
        }

        void SendActiveZombieList(ViewServer sender, ViewServer.Client cl)
        {
            if (sender == null)
                return;

            lock (_activeZombies)
            {
                var list = new List<Viewer.DataZombie>();
                foreach (var zombie in _activeZombies)
                {
                    Vector2i p = WorldToBitmap(zombie.pos);
                    list.Add(new Viewer.DataZombie
                    {
                        id = zombie.id,
                        x = p.x,
                        y = p.y,
                    });
                }

                var data = new Viewer.ZombieList();
                data.list = list;

                sender.SendData(cl, Viewer.DataType.ActiveZombies, data);
            }
        }

        void SendSoundEvent(ViewServer sender, Vector3 pos, float radius)
        {
            if (sender == null)
                return;

            var p = WorldToBitmap(pos);
            var data = new Viewer.WorldEventSound();
            data.x = p.x;
            data.y = p.y;
            // FIXME: This is only remapped in one direction.

            var worldSize = Utils.Distance(_worldMins.x, _worldMaxs.x);
            var rescaled = (radius / worldSize) * 512.0f;
            data.distance = (int)rescaled;

            Log.Out("Distance {0}, Scaled: {1}", radius, data.distance);

            sender.Broadcast(Viewer.DataType.WorldEventSound, data);
        }

        void SendStaticState(ViewServer sender, ViewServer.Client cl)
        {
            lock (sender)
            {
                SendState(sender, cl);
                SendWorldZones(sender, cl);
                SendPOIZones(sender, cl);
            }
        }
    }
}
