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
            ModEvents.GameStartDone.RegisterHandler(GameStartDone);
            ModEvents.GameUpdate.RegisterHandler(GameUpdate);
            ModEvents.GameShutdown.RegisterHandler(GameShutdown);
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(PlayerSpawnedInWorld);
            ModEvents.PlayerDisconnected.RegisterHandler(PlayerDisconnected);

            Hooks.Install();
        }

        private static void GameStartDone()
        {
            Log.Out("[WalkerSim] GameStartDone");
            _stopWatch.ResetAndRestart();

            _sim = new Simulation();
            _sim.Start();
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
            Log.Out("[WalkerSim] GameShutdown");
            try
            {
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
    }
}

