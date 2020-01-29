using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

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

	class ViewerClient
	{
		private Client _client = new Client();
		private bool _connecting = false;
		private string _host = "";
		private int _port = 0;
		private Viewer.MapData _mapData = null;

		public void Update()
		{
		}

		public Viewer.MapData GetMapData()
		{
			if (_connecting || !_client.sock.Connected)
				return null;

			return _mapData;
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

				switch ((Viewer.DataType)header.type)
				{
					case Viewer.DataType.MapData:
						Viewer.MapData mapData = new Viewer.MapData();
						mapData.Deserialize(buffer);
						OnMapData(mapData);
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

		private void OnMapData(WalkerSim.Viewer.MapData mapData)
		{
			_mapData = mapData;
		}
	}
}
