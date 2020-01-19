using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalkerSim
{
	public class WorldState
	{
		static int ZombieMoveDay = GamePrefs.GetInt(EnumGamePrefs.ZombieMove);
		static int ZombieMoveNight = GamePrefs.GetInt(EnumGamePrefs.ZombieMoveNight);

		float _zombieSpeed = 1.0f;
		bool _isBloodMoon = false;
		bool _isNight = false;

		// Returns how many meters per second they walk.
		float CalculateZombieSpeed()
		{
			int mode = _isNight ? ZombieMoveNight : ZombieMoveDay;
			switch (mode)
			{
				case 1: // Jog
					return 1.79f;
				case 2: // Run
					return 2.68f;
				case 3: // Sprint
					return 3.13f;
				case 4: // Nightmare
					return 3.58f;
				default:
					break;
			}
			// Walk
			return 1.34f;
		}

		public void Update()
		{
#if DEBUG
			var oldIsNight = _isNight;
			var oldIsBloodMoon = _isBloodMoon;
			var oldZombieSpeed = _zombieSpeed;
#endif
			var world = GameManager.Instance.World;
			_isNight = world.IsDark();
			_isBloodMoon = SkyManager.BloodMoon();
			_zombieSpeed = CalculateZombieSpeed();
#if DEBUG
			if (oldIsNight != _isNight)
				Log.Out("[WalkerSim] isNight, Old: {0}, New: {1}", oldIsNight, _isNight);
			if (oldIsBloodMoon != _isBloodMoon)
				Log.Out("[WalkerSim] isBloodMoon, Old: {0}, New: {1}", oldIsBloodMoon, _isBloodMoon);
			if (oldZombieSpeed != _zombieSpeed)
				Log.Out("[WalkerSim] zombieSpeed, Old: {0}, New: {1}", oldZombieSpeed, _zombieSpeed);
#endif
		}

		public float GetZombieSpeed()
		{
			return _zombieSpeed;
		}

		public bool IsBloodMoon()
		{
			return _isBloodMoon;
		}

		public bool IsNight()
		{
			return _isNight;
		}
	}
}
