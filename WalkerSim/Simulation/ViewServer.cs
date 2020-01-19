using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace WalkerSim
{
	struct ViewerClient
	{
		public Socket sock;
		public bool disconnected;
	}

	class ViewServer
	{
		private Socket _listener = null;
		private List<ViewerClient> _clients = new List<ViewerClient>();

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
				ViewerClient client = new ViewerClient();
				client.sock = listener.EndAccept(ar);
				client.disconnected = false;
				_clients.Add(client);

				OnClientConnected(client);

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
			// Update
			for (int i = 0; i < _clients.Count; i++)
			{
				ViewerClient client = _clients[i];
				if (client.disconnected || client.sock.Connected == false)
				{
					OnClientDisconnected(client);

					_clients.RemoveAt(i);
					if (_clients.Count == 0)
						break;

					i--;
				}
			}
		}

		public bool HasClients()
		{
			return _clients.Count > 0;
		}

		private void Send(ViewerClient client, byte[] data, int length)
		{
			var sock = client.sock;
			sock.BeginSend(data, 0, length, 0, new AsyncCallback(SendCallback), client);
		}

		private void SendCallback(IAsyncResult ar)
		{
			ViewerClient client = (ViewerClient)ar.AsyncState;
			try
			{
				client.sock.EndSend(ar);
			}
			catch (Exception)
			{
				client.disconnected = true;
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
				//Log.Out("Sending Packet: {0}, bytes: {1}", header.type, header.len);
				foreach (var client in _clients)
				{
					//client.sock.Send(streamHeader.GetBuffer(), (int)streamHeader.Length, SocketFlags.None);
					Send(client, streamHeader.GetBuffer(), (int)streamHeader.Length);
					//client.sock.Send(streamBody.GetBuffer(), (int)streamBody.Length, SocketFlags.None);
					Send(client, streamBody.GetBuffer(), (int)streamBody.Length);
				}
			}
			catch (Exception)
			{
			}
		}

		private void OnClientConnected(ViewerClient cl)
		{
			Log.Out("[DebugServer] Client connected {0}", cl.sock.RemoteEndPoint);
		}

		private void OnClientDisconnected(ViewerClient cl)
		{
			Log.Out("[DebugServer] Client disconnected {0}", cl.sock.RemoteEndPoint);
		}
	}
}
