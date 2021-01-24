using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace WalkerSim
{
    class Client
    {
        public Socket sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
        public const int BUFFER_SIZE = 1024;
        public byte[] tmpBuffer = new byte[BUFFER_SIZE];
        public MemoryStream buffer = new MemoryStream();
        public Viewer.Header header = new Viewer.Header();
    }

    public class SoundEvent
    {
        public int x;
        public int y;
        public int radius;
        public Stopwatch watch = new Stopwatch();
    }

    public class State
    {
        public Viewer.State worldInfo = new Viewer.State();
        public Viewer.WorldZones worldZones = new Viewer.WorldZones();
        public Viewer.POIZones poiZones = new Viewer.POIZones();
        public Viewer.PlayerZones playerZones = new Viewer.PlayerZones();
        public Viewer.ZombieList inactive = new Viewer.ZombieList();
        public Viewer.ZombieList active = new Viewer.ZombieList();
        public List<SoundEvent> sounds = new List<SoundEvent>();
    }

    class ViewerClient
    {
        private Client _client = new Client();
        private bool _connecting = false;
        private string _host = "";
        private int _port = 0;
        private State _worldState;

        public void Update()
        {
            if (_worldState != null && _worldState.sounds != null)
            {
                for (int i = 0; i < _worldState.sounds.Count; i++)
                {
                    var snd = _worldState.sounds[i];
                    var elapsed = snd.watch.ElapsedMilliseconds;
                    if (elapsed >= 1000)
                    {
                        _worldState.sounds.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public State GetMapData()
        {
            if (_connecting || !_client.sock.Connected)
                return null;

            return _worldState;
        }

        public bool Connect(string host, int port)
        {
            if (_connecting || _client.sock.Connected)
                return true;

            _client = new Client();
            _host = host;
            _port = port;
            try
            {
                _connecting = true;
                _client.sock.BeginConnect(_host, _port, new AsyncCallback(ConnectCallback), _client);
            }
            catch (Exception)
            {
                return false;
            }

            return _connecting;
        }

        public bool Disconnect()
        {
            if (!IsConnecting() && !IsConnected())
                return false;
            try
            {
                _client.sock.Close();
                _connecting = false;
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            Client client = (Client)ar.AsyncState;
            try
            {
                var sock = client.sock;
                sock.EndConnect(ar);

                sock.BeginReceive(client.tmpBuffer, 0, Client.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallback), client);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex.Message);
            }
            _connecting = false;
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            Client client = (Client)ar.AsyncState;

            try
            {
                var sock = client.sock;

                int bytesRead = sock.EndReceive(ar);
                if (bytesRead > 0)
                {
                    var buffer = client.buffer;
                    buffer.Seek(0, SeekOrigin.End);
                    buffer.Write(client.tmpBuffer, 0, bytesRead);

                    ProcessBuffer(ref client);

                    sock.BeginReceive(client.tmpBuffer, 0, Client.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallback), client);
                }
            }
            catch (Exception)
            {
                //throw;
            }
        }

        public bool IsConnecting()
        {
            return _connecting;
        }

        public bool IsConnected()
        {
            return _connecting == false && _client.sock.Connected;
        }

        private void ProcessBuffer(ref Client client)
        {
            var header = client.header;

            var buffer = client.buffer;
            buffer.Seek(0, SeekOrigin.Begin);

            try
            {
                // Read header.
                header.Deserialize(buffer);
                if (header.len > buffer.Length - buffer.Position)
                    return;

                if (_worldState == null)
                    _worldState = new State();

                Viewer.DataType messageType = (Viewer.DataType)header.type;
                switch (messageType)
                {
                    case Viewer.DataType.Info:
                        _worldState.worldInfo.Deserialize(buffer);
                        Console.WriteLine("Received WorldInfo");
                        break;
                    case Viewer.DataType.WorldZones:
                        _worldState.worldZones.Deserialize(buffer);
                        Console.WriteLine("Received WorldZones");
                        break;
                    case Viewer.DataType.POIZones:
                        _worldState.poiZones.Deserialize(buffer);
                        Console.WriteLine("Received POIZones");
                        break;
                    case Viewer.DataType.PlayerZones:
                        _worldState.playerZones.Deserialize(buffer);
                        break;
                    case Viewer.DataType.ActiveZombies:
                        _worldState.active.Deserialize(buffer);
                        break;
                    case Viewer.DataType.InactiveZombies:
                        _worldState.inactive.Deserialize(buffer);
                        break;
                    case Viewer.DataType.WorldEventSound:
                        var ev = new Viewer.WorldEventSound();
                        ev.Deserialize(buffer);

                        var snd = new SoundEvent();
                        snd.x = ev.x;
                        snd.y = ev.y;
                        snd.radius = ev.distance;
                        snd.watch.Restart();

                        _worldState.sounds.Add(snd);

                        break;
                    default:
                        break;
                }

                // Discard current packet.
                MemoryStream newBuffer = new MemoryStream();
                newBuffer.Write(buffer.GetBuffer(), (int)buffer.Position, (int)(buffer.Length - buffer.Position));

                // Set buffer.
                client.buffer = newBuffer;
            }
            catch (Exception)
            {
            }
        }

    }
}
