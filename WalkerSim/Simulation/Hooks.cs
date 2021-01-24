using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WalkerSim
{
    class Hooks
    {
        public static void Install()
        {
            var harmony = new Harmony("WalkerSim.Hooks");
            harmony.PatchAll();
        }
    }


    [HarmonyPatch(typeof(AIDirector))]
    [HarmonyPatch("NotifyNoise")]
    class NotifyNoiseHook
    {
        // Returns the radius for a given sound name in meters.
        static float GetSoundRadius(string name)
        {
            float distance = 0.0f;
            if (Config.Instance.SoundDistance.TryGetValue(name, out distance))
            {
                return distance;
            }
            return 0.0f;
        }

        static bool Prefix(Entity instigator, Vector3 position, string clipName, float volumeScale)
        {
#if DEBUG
            Log.Out("[WalkerSim] NotifyNoise \"{0}\", \"{1}\", \"{2}\", \"{3}\"", instigator, position, clipName, volumeScale);
#endif
            float radius = GetSoundRadius(clipName);

            var simulation = API._sim;
            if (simulation != null && radius != 0.0f)
            {
                simulation.AddSoundEvent(position, radius);
            }

            return true;
        }

        static void Postfix()
        {
        }
    }

    [HarmonyPatch(typeof(AIDirectorWanderingHordeComponent))]
    [HarmonyPatch("SpawnWanderingHorde")]
    class HordeSpawnHook
    {
        static bool Prefix(bool feral)
        {
#if DEBUG
            //Log.Out("[WalkerSim] Preventing horde spawn");
#endif
            // Prevent hordes from spawning.
            return false;
        }
    }


    [HarmonyPatch(typeof(SpawnManagerBiomes))]
    [HarmonyPatch("SpawnUpdate")]
    class BiomeSpawnerHook
    {
        static void Prefix(string _spawnerName, ref bool _bSpawnEnemyEntities, ChunkAreaBiomeSpawnData _chunkBiomeSpawnData)
        {
#if DEBUG
            //Log.Out("[WalkerSim] Preventing biome spawn");
#endif
            _bSpawnEnemyEntities = false;
        }
    }
}
