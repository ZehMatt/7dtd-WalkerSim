using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalkerSim
{
	public class PRNG
	{
		private Random _rand = new Random();

		public PRNG(int seed)
		{
			Seed(seed);
		}

		public void Seed(int seed)
		{
			_rand = new Random(seed);
		}

		// min:0, max:int.MaxValue
		public int Get()
		{
			return _rand.Next();
		}

		// min:0, max:exclusive
		public int Get(int max)
		{
			return _rand.Next(max);
		}

		// min:inclusive, max:exclusive
		public int Get(int min, int max)
		{
			return _rand.Next(min, max);
		}

		// min:inclusive, max:inclusive
		public float Get(float min, float max)
		{
			return (float)(_rand.NextDouble() * (max - min) + min);
		}

		public bool Chance(float c)
		{
			if (c < 0.0f || c > 1.0f)
				throw new InvalidOperationException("Parameter range is 0.0 to 1.0 inclusive");

			return Get(0.0f, 1.0f) <= c;
		}
	}
}
