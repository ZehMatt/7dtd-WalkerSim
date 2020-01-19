using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WalkerSim
{
	static class Extends
	{
		public static T GetFieldValue<T>(this object obj, string name)
		{
			var field = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			return (T)field?.GetValue(obj);
		}
	}
}
