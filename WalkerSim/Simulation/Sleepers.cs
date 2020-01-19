using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
	class SleeperSpawn
	{
		public Vector3i pos;
		public Vector3 look;
		public Bounds bounds;
		public float rot;
		public int pose;
		public int blockType;
	}

	class Sleepers
	{
		private List<SleeperSpawn> _spawns = new List<SleeperSpawn>();

		public void BuildCache()
		{
			// NOTE: Access to the spawn points is done by reflection, this code may break in the future.
			try
			{
				DynamicPrefabDecorator dynamicPrefabDecorator = GameManager.Instance.GetDynamicPrefabDecorator();
				var pois = dynamicPrefabDecorator.GetPOIPrefabs();
				foreach (var poi in pois)
				{
					if (poi.sleeperVolumes != null)
					{
						for (int i = 0; i < poi.sleeperVolumes.Count; i++)
						{
							var sleeperVolume = poi.sleeperVolumes[i];
							var spawnPointList = sleeperVolume.GetFieldValue<List<SleeperVolume.SpawnPoint>>("spawnPointList");
							foreach (var spawnPoint in spawnPointList)
							{
								var bounds = new Bounds();
								bounds.min = sleeperVolume.BoxMin.ToVector3();
								bounds.max = sleeperVolume.BoxMax.ToVector3();
								bounds.center = sleeperVolume.Center;

								_spawns.Add(new SleeperSpawn()
								{
									pos = spawnPoint.pos,
									look = spawnPoint.look,
									pose = spawnPoint.pose,
									rot = spawnPoint.rot,
									bounds = bounds,
									blockType = spawnPoint.blockType,
								});

#if DEBUG
								Log.Out("Sleeper Spawn: {0}, Bounds: {1}", spawnPoint.pos, bounds);
#endif
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}

			Log.Out("[WalkerSim] Loaded {0} sleeper positions", _spawns.Count);
		}

		public List<SleeperSpawn> GetSpawns()
		{
			return _spawns;
		}
	}
}
