using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace WalkerSim
{
    public class ViewServer
    {
        public class Client
        {
            public Socket sock;
            public bool disconnected;
        }

        private Socket _listener = null;
        private List<Client> _clients = new List<Client>();

        public delegate void OnClientConnectedDelegate(ViewServer sender, Client client);
        public delegate void OnClientDisconnectedDelegate(ViewServer sender, Client client);

        public event OnClientConnectedDelegate OnClientConnected;
        public event OnClientDisconnectedDelegate OnClientDisconnected;

        public bool Start(int port)
        {
            IPAddress addr = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);

            try
            {
                _listener = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _listener.Bind(localEndPoint);
                _listener.Listen(100);
                _listener.BeginAccept(new AsyncCallback(AcceptCallback), _listener);
            }
            catch (Exception ex)
            {
                Log.Out("Unable to start server: {0}", ex.Message);
                return false;
            }

            return true;
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;

            try
            {
                Client client = new Client();
                client.sock = listener.EndAccept(ar);
                client.disconnected = false;

                _clients.Add(client);

                Log.Out("[WalkerSim] Client connected {0}", client.sock.RemoteEndPoint);

                if (OnClientConnected != null)
                {
                    OnClientConnected.Invoke(this, client);
                }

                // Accept next.
                _listener.BeginAccept(new AsyncCallback(AcceptCallback), _listener);
            }
            catch (Exception ex)
            {
                Log.Out("Unable to accept new client: {0}", ex.Message);
            }
        }

        public void Update()
        {
            try
            {
                // Update
                for (int i = 0; i < _clients.Count; i++)
                {
                    Client client = _clients[i];
                    if (client.disconnected)
                    {
                        Log.Out("[WalkerSim] Client disconnected {0}", client.sock.RemoteEndPoint);

                        if (OnClientDisconnected != null)
                        {
                            OnClientDisconnected.Invoke(this, client);
                        }

                        _clients.RemoveAt(i);
                        if (_clients.Count == 0)
                            break;

                        i--;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Out("Server::Update caused exception");
                Log.Exception(ex);
            }

        }

        public bool HasClients()
        {
            return _clients.Count > 0;
        }

        private void Send(Client client, byte[] data, int length)
        {
            if (client.disconnected)
                return;

            try
            {
                var sock = client.sock;
                sock.BeginSend(data, 0, length, 0, new AsyncCallback(SendCallback), client);
            }
            catch (Exception ex)
            {
                client.disconnected = true;
#if DEBUG
                Log.Out("ViewServer::Send caused exception");
                Log.Exception(ex);
#endif
            }

        }

        private void SendCallback(IAsyncResult ar)
        {
            Client client = (Client)ar.AsyncState;
            try
            {
                client.sock.EndSend(ar);
            }
            catch (Exception ex)
            {
#if DEBUG
                if (!client.disconnected)
                {
                    Log.Out("ViewServer::SendCallback caused exception");
                    Log.Exception(ex);
                }
#endif
                client.disconnected = true;
            }
        }

        public void SendData(Client cl, Viewer.DataType type, Viewer.IWalkerSimMessage data)
        {
            if (cl == null)
            {
                Broadcast(type, data);
                return;
            }

            MemoryStream streamBody = new MemoryStream();
            data.Serialize(streamBody);

            Viewer.Header header = new Viewer.Header();
            header.type = (int)type;
            header.len = (int)streamBody.Length;

            MemoryStream streamHeader = new MemoryStream();
            header.Serialize(streamHeader);

            try
            {
                Send(cl, streamHeader.GetBuffer(), (int)streamHeader.Length);
                Send(cl, streamBody.GetBuffer(), (int)streamBody.Length);
            }
            catch (Exception ex)
            {
                cl.disconnected = true;

#if DEBUG
                Log.Out("ViewServer::SendData caused exception");
                Log.Exception(ex);
#endif
            }
        }

        public void Broadcast(Viewer.DataType type, Viewer.IWalkerSimMessage data)
        {
            if (_clients.Count == 0)
                return;

            MemoryStream streamBody = new MemoryStream();
            data.Serialize(streamBody);

            Viewer.Header header = new Viewer.Header();
            header.type = (int)type;
            header.len = (int)streamBody.Length;

            MemoryStream streamHeader = new MemoryStream();
            header.Serialize(streamHeader);

            try
            {
                lock (_clients)
                {
                    //Log.Out("Sending Packet: {0}, bytes: {1}", type.ToString(), header.len);
                    foreach (var client in _clients)
                    {
                        Send(client, streamHeader.GetBuffer(), (int)streamHeader.Length);
                        Send(client, streamBody.GetBuffer(), (int)streamBody.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Out("ViewServer::Broadcast caused exception");
                Log.Exception(ex);
            }
        }

    }
}
