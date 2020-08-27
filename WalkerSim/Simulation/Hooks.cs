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
            switch (name)
            {
                case "pistol_fire":
                case "44magnum_fire":
                case "mp5_fire":
                case "ak47_fire":
                case "sniperrifle_fire":
                case "pump_shotgun_fire":
                case "autoshotgun_fire":
                case "m136_fire":
                case "explosion_grenade":
                case "sharpshooter_fire":
                case "desertvulture_fire":
                case "blunderbuss_fire":
                case "tacticalar_fire":
                case "explosion1":
                    return 500.0f;
                case "ak47_s_fire":
                case "pistol_s_fire":
                case "sniperrifle_s_fire":
                case "mp5_s_fire":
                case "pump_shotgun_s_fire":
                case "hunting_rifle_s_fire":
                    return 100.0f;
            }
            return 1.0f;
        }

        static bool Prefix(Entity instigator, Vector3 position, string clipName, float volumeScale)
        {
#if DEBUG
            Log.Out("[WalkerSim] NotifyNoise \"{0}\", \"{1}\", \"{2}\", \"{3}\"", instigator, position, clipName, volumeScale);
#endif
            float radius = GetSoundRadius(clipName);

            var simulation = API._sim;
            if (simulation != null)
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
            Log.Out("[WalkerSim] Preventing horde spawn");
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
            Log.Out("[WalkerSim] Preventing biome spawn");
#endif
            _bSpawnEnemyEntities = false;
        }
    }
}
