using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace WalkerSim
{
	public class API : IModApi
	{
		public static string ModPath = Path.Combine(Directory.GetCurrentDirectory(), "Mods", "WalkerSim");

		public static int MaxPlayers = GamePrefs.GetInt(EnumGamePrefs.ServerMaxPlayerCount);

		public static Simulation _sim = null;
		private static DateTime _nextOutputTime = DateTime.Now;
		private static MicroStopwatch _stopWatch = new MicroStopwatch();

		public void InitMod()
		{
			// Override enemy spawning.
			GameStats.Set(EnumGameStats.IsSpawnEnemies, false);

			ModEvents.GameAwake.RegisterHandler(GameAwake);
			ModEvents.GameStartDone.RegisterHandler(GameStartDone);
			ModEvents.GameUpdate.RegisterHandler(GameUpdate);
			ModEvents.GameShutdown.RegisterHandler(GameShutdown);
			ModEvents.PlayerLogin.RegisterHandler(PlayerLogin);
			ModEvents.PlayerSpawning.RegisterHandler(PlayerSpawning);
			ModEvents.PlayerSpawnedInWorld.RegisterHandler(PlayerSpawnedInWorld);
			ModEvents.PlayerDisconnected.RegisterHandler(PlayerDisconnected);
			ModEvents.SavePlayerData.RegisterHandler(SavePlayerData);
			ModEvents.GameMessage.RegisterHandler(GameMessage);
			ModEvents.ChatMessage.RegisterHandler(ChatMessage);
			ModEvents.EntityKilled.RegisterHandler(EntityKilled);
		}

		private void GameAwake()
		{
			try
			{
				Log.Out("API.GameAwake()");
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}

		private static void GameStartDone()
		{
			try
			{
				Log.Out("API.GameStartDone()");

				_sim = new Simulation();
				_sim.Start();
				_stopWatch.ResetAndRestart();
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}

		private static void GameUpdate()
		{
			try
			{
				float dt = (float)((double)_stopWatch.ElapsedMicroseconds / 1000000.0);
				_stopWatch.ResetAndRestart();

				if (_sim != null)
				{
					_sim.Update();
				}
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}

		private static void GameShutdown()
		{
			try
			{
				Log.Out("API.GameShutdown()");
				if (_sim != null)
				{
					_sim.Stop();
				}
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}

		private static bool PlayerLogin(ClientInfo _cInfo, string _message, StringBuilder _stringBuild) //Initiating player login
		{
			try
			{
				Log.Out("API.PlayerLogin({0}, {1}, {2}, {3})", _cInfo, _cInfo.entityId, _message, _stringBuild);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.PlayerLogin: {0}.", e.Message));
			}
			return true;
		}

		private static void PlayerSpawning(ClientInfo _cInfo, int _chunkViewDim, PlayerProfile _playerProfile)
		{
			try
			{
				Log.Out("API.PlayerSpawning");
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.PlayerSpawning: {0}.", e.Message));
			}
		}

		private static void PlayerSpawnedInWorld(ClientInfo _cInfo, RespawnType _respawnReason, Vector3i _pos)
		{
			try
			{
				Log.Out("API.PlayerSpawnedInWorld");
				if (_sim != null)
				{
					switch (_respawnReason)
					{
						case RespawnType.NewGame:
						case RespawnType.LoadedGame:
						case RespawnType.EnterMultiplayer:
						case RespawnType.JoinMultiplayer:
							_sim.AddPlayer(_cInfo.entityId);
							break;

					}
				}
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.PlayerSpawnedInWorld: {0}.", e.Message));
			}
		}

		private static bool ChatMessage(ClientInfo _cInfo, EChatType _type, int _senderId, string _msg, string _mainName, bool _localizeMain, List<int> _recipientEntityIds)
		{
			try
			{
				Log.Out("API.ChatMessage({0}, {1}, {2}, {3}, {4}, {5}, {6})", _cInfo, _type, _senderId, _msg, _mainName, _localizeMain, _recipientEntityIds);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.ChatMessage: {0}.", e.Message));
			}

			return true;
		}

		private static bool GameMessage(ClientInfo _cInfo, EnumGameMessages _type, string _msg, string _mainName, bool _localizeMain, string _secondaryName, bool _localizeSecondary)
		{
			try
			{
				Log.Out("API.GameMessage({0}, {1}, {2}, {3}, {4}, {5}, {6})", _cInfo, _type, _msg, _mainName, _localizeMain, _secondaryName, _localizeSecondary);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.GameMessage: {0}.", e.Message));
			}
			return true;
		}

		private static void SavePlayerData(ClientInfo _cInfo, PlayerDataFile _playerDataFile)
		{
			try
			{
				Log.Out("API.SavePlayerData({0}, {1})", _cInfo, _playerDataFile);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.SavePlayerData: {0}.", e.Message));
			}
		}

		private static void PlayerDisconnected(ClientInfo _cInfo, bool _bShutdown)
		{
			try
			{
				_sim.RemovePlayer(_cInfo.entityId);

				Log.Out("API.PlayerDisconnected({0}, {1})", _cInfo, _bShutdown);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.PlayerDisconnected: {0}.", e.Message));
			}
		}

		private static void EntityKilled(Entity _entity1, Entity _entity2)
		{
			try
			{
				Log.Out("API.EntityKilled({0}, {1})", _entity1, _entity2);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.EntityKilled: {0}.", e.Message));
			}
		}

		public static void NewPlayerExec1(ClientInfo _cInfo)
		{
			try
			{
				Log.Out("API.NewPlayerExec1({0})", _cInfo);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.NewPlayerExec1: {0}.", e.Message));
			}
		}

		public static void NewPlayerExec2(ClientInfo _cInfo, EntityPlayer _player)
		{
			try
			{
				Log.Out("API.NewPlayerExec2({0}, {1})", _cInfo, _player);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.NewPlayerExec2: {0}.", e.Message));
			}
		}

		public static void NewPlayerExec3(ClientInfo _cInfo, EntityPlayer _player)
		{
			try
			{
				Log.Out("API.NewPlayerExec3({0}, {1})", _cInfo, _player);
			}
			catch (Exception e)
			{
				Log.Out(string.Format("[WalkerSim] Error in API.NewPlayerExec3: {0}.", e.Message));
			}
		}
	}
}

