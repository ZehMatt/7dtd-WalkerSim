using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalkerSim
{
    public class State
    {
        public delegate void OnChangeDelegate();
        public event OnChangeDelegate OnChange;

        static int ZombieMoveDay = GamePrefs.GetInt(EnumGamePrefs.ZombieMove);
        static int ZombieMoveNight = GamePrefs.GetInt(EnumGamePrefs.ZombieMoveNight);

        bool _invalidated = true;

        float _zombieSpeed = 1.0f;
        public float ZombieSpeed
        {
            get { return _zombieSpeed; }
            set
            {
                if (_zombieSpeed != value)
                {
                    _invalidated = true;
                }
                _zombieSpeed = value;
            }
        }

        bool _isBloodMoon = false;
        public bool IsBloodMoon
        {
            get { return _isBloodMoon; }
            set
            {
                if (_isBloodMoon != value)
                {
                    _invalidated = true;
                }
                _isBloodMoon = value;
            }
        }

        bool _isNight = false;
        public bool IsNight
        {
            get { return _isNight; }
            set
            {
                if (_isNight != value)
                {
                    _invalidated = true;
                }
                _isNight = value;
            }
        }

        float _timeScale = 1.0f;
        public float Timescale
        {
            get { return _timeScale; }
            set
            {
                if (_timeScale != value)
                {
                    _invalidated = true;
                }
                _timeScale = value;
            }
        }

        float _walkSpeedScale = 1.0f;
        public float WalkSpeedScale
        {
            get { return _walkSpeedScale; }
            set
            {
                if (_walkSpeedScale != value)
                {
                    _invalidated = true;
                }
                _walkSpeedScale = value;
            }
        }

        public float ScaledZombieSpeed
        {
            get
            {
                return ZombieSpeed * WalkSpeedScale;
            }
        }

        // Returns how many meters per second they walk.
        float CalculateZombieSpeed()
        {
            int mode = IsNight ? ZombieMoveNight : ZombieMoveDay;
            switch (mode)
            {
                case 1: // Jog
                    return 1.39f;
                case 2: // Run
                    return 1.68f;
                case 3: // Sprint
                    return 2.13f;
                case 4: // Nightmare
                    return 2.50f;
                default:
                    break;
            }
            // Walk
            return 0.74f;
        }

        public void Update()
        {
#if DEBUG
            var oldIsNight = IsNight;
            var oldIsBloodMoon = IsBloodMoon;
            var oldZombieSpeed = ZombieSpeed;
#endif
            var world = GameManager.Instance.World;
            IsNight = world.IsDark();
            IsBloodMoon = SkyManager.BloodMoon();
            ZombieSpeed = CalculateZombieSpeed();
#if DEBUG
            if (oldIsNight != IsNight)
                Log.Out("[WalkerSim] isNight, Old: {0}, New: {1}", oldIsNight, IsNight);
            if (oldIsBloodMoon != IsBloodMoon)
                Log.Out("[WalkerSim] isBloodMoon, Old: {0}, New: {1}", oldIsBloodMoon, IsBloodMoon);
            if (oldZombieSpeed != ZombieSpeed)
                Log.Out("[WalkerSim] zombieSpeed, Old: {0}, New: {1}", oldZombieSpeed, ZombieSpeed);
#endif
            if (_invalidated)
            {
                if (OnChange != null)
                {
                    OnChange.Invoke();
                }
                _invalidated = false;
            }
        }

    }
}
