using System;
using System.Collections.Generic;
using System.IO;

namespace WalkerSim.Viewer
{
    public enum DataType
    {
        Info,
        WorldZones,
        POIZones,
        PlayerZones,
        ActiveZombies,
        InactiveZombies,
        WorldEventSound,
    }

    public partial class Base
    {
        protected byte[] ReadBytes(Stream stream, int len)
        {
            byte[] buf = new byte[len];
            stream.Read(buf, 0, len);
            return buf;
        }
        protected void Read(Stream stream, out int val)
        {
            val = BitConverter.ToInt32(ReadBytes(stream, 4), 0);
        }
        protected void Read(Stream stream, out float val)
        {
            val = BitConverter.ToSingle(ReadBytes(stream, 4), 0);
        }
        protected void Read(Stream stream, out bool val)
        {
            val = BitConverter.ToBoolean(ReadBytes(stream, 1), 0);
        }
        protected void Write(Stream stream, int val)
        {
            byte[] data = BitConverter.GetBytes(val);
            System.Diagnostics.Debug.Assert(data.Length == 4);
            stream.Write(BitConverter.GetBytes(val), 0, data.Length);
        }
        protected void Write(Stream stream, float val)
        {
            byte[] data = BitConverter.GetBytes(val);
            System.Diagnostics.Debug.Assert(data.Length == 4);
            stream.Write(BitConverter.GetBytes(val), 0, data.Length);
        }
        protected void Write(Stream stream, bool val)
        {
            byte[] data = BitConverter.GetBytes(val);
            System.Diagnostics.Debug.Assert(data.Length == 1);
            stream.Write(BitConverter.GetBytes(val), 0, data.Length);
        }
    }

    class Header : Base
    {
        public int type;
        public int len;

        public void Serialize(Stream stream)
        {
            Write(stream, type);
            Write(stream, len);
        }

        public void Deserialize(Stream stream)
        {
            Read(stream, out type);
            Read(stream, out len);
        }
    }

    public class DataZombie : Base
    {
        public int id;
        public int x;
        public int y;
        public void Serialize(Stream stream)
        {
            Write(stream, id);
            Write(stream, x);
            Write(stream, y);
        }

        public void Deserialize(Stream stream)
        {
            Read(stream, out id);
            Read(stream, out x);
            Read(stream, out y);
        }
    }

    public class DataZone : Base
    {
        // Maximum bounds.
        public int x1;
        public int y1;
        public int x2;
        public int y2;
        public virtual void Serialize(Stream stream)
        {
            Write(stream, x1);
            Write(stream, y1);
            Write(stream, x2);
            Write(stream, y2);
        }

        public virtual void Deserialize(Stream stream)
        {
            Read(stream, out x1);
            Read(stream, out y1);
            Read(stream, out x2);
            Read(stream, out y2);
        }
    }

    public class DataPlayerZone : DataZone
    {
        // Blocked spawn bounds.
        public int x3;
        public int y3;
        public int x4;
        public int y4;

        public override void Serialize(Stream stream)
        {
            base.Serialize(stream);
            Write(stream, x3);
            Write(stream, y3);
            Write(stream, x4);
            Write(stream, y4);
        }

        public override void Deserialize(Stream stream)
        {
            base.Deserialize(stream);
            Read(stream, out x3);
            Read(stream, out y3);
            Read(stream, out x4);
            Read(stream, out y4);
        }
    }

    public class DataPOIZone : DataZone
    {
    }

    public class DataWorldZone : DataZone
    {
    }

    public class State : Base, IWalkerSimMessage
    {
        public int w;
        public int h;
        public int mapW;
        public int mapH;
        public int density;
        public float zombieSpeed;
        public float timescale;

        public void Serialize(Stream stream)
        {
            Write(stream, w);
            Write(stream, h);
            Write(stream, mapW);
            Write(stream, mapH);
            Write(stream, density);
            Write(stream, zombieSpeed);
            Write(stream, timescale);
        }
        public void Deserialize(Stream stream)
        {
            Read(stream, out w);
            Read(stream, out h);
            Read(stream, out mapW);
            Read(stream, out mapH);
            Read(stream, out density);
            Read(stream, out zombieSpeed);
            Read(stream, out timescale);
        }
    }

    public class WorldZones : Base, IWalkerSimMessage
    {
        public List<DataWorldZone> zones;
        public void Serialize(Stream stream)
        {
            var list = zones;
            int len = list == null ? 0 : list.Count;
            Write(stream, len);
            for (int i = 0; i < len; i++)
            {
                var e = list[i];
                e.Serialize(stream);
            }
        }
        public void Deserialize(Stream stream)
        {
            var list = new List<DataWorldZone>();
            int len = 0;
            Read(stream, out len);
            for (int i = 0; i < len; i++)
            {
                DataWorldZone e = new DataWorldZone();
                e.Deserialize(stream);
                list.Add(e);
            }
            zones = list;
        }
    }

    public class POIZones : Base, IWalkerSimMessage
    {
        public List<DataPOIZone> zones;

        public void Serialize(Stream stream)
        {
            var list = zones;
            int len = list == null ? 0 : list.Count;
            Write(stream, len);
            for (int i = 0; i < len; i++)
            {
                var e = list[i];
                e.Serialize(stream);
            }
        }
        public void Deserialize(Stream stream)
        {
            var list = new List<DataPOIZone>();
            int len = 0;
            Read(stream, out len);
            for (int i = 0; i < len; i++)
            {
                DataPOIZone e = new DataPOIZone();
                e.Deserialize(stream);
                list.Add(e);
            }
            zones = list;
        }
    }

    public class PlayerZones : Base, IWalkerSimMessage
    {
        public List<DataPlayerZone> zones;

        public void Serialize(Stream stream)
        {
            var list = zones;
            int len = list == null ? 0 : list.Count;
            Write(stream, len);
            for (int i = 0; i < len; i++)
            {
                var e = list[i];
                e.Serialize(stream);
            }
        }
        public void Deserialize(Stream stream)
        {
            var list = new List<DataPlayerZone>();
            int len = 0;
            Read(stream, out len);
            for (int i = 0; i < len; i++)
            {
                DataPlayerZone e = new DataPlayerZone();
                e.Deserialize(stream);
                list.Add(e);
            }
            zones = list;
        }
    }

    public class ZombieList : Base, IWalkerSimMessage
    {
        public List<DataZombie> list;

        public void Serialize(Stream stream)
        {
            int len = list == null ? 0 : list.Count;
            Write(stream, len);
            for (int i = 0; i < len; i++)
            {
                var e = list[i];
                e.Serialize(stream);
            }
        }
        public void Deserialize(Stream stream)
        {
            var res = new List<DataZombie>();
            int len = 0;
            Read(stream, out len);
            for (int i = 0; i < len; i++)
            {
                DataZombie e = new DataZombie();
                e.Deserialize(stream);
                res.Add(e);
            }
            list = res;
        }
    }

    public class WorldEventSound : Base, IWalkerSimMessage
    {
        public int x;
        public int y;
        public int distance;
        public void Serialize(Stream stream)
        {
            Write(stream, x);
            Write(stream, y);
            Write(stream, distance);
        }
        public void Deserialize(Stream stream)
        {
            Read(stream, out x);
            Read(stream, out y);
            Read(stream, out distance);
        }
    }
}
