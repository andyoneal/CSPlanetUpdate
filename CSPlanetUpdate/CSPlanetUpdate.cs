using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CSPlanetUpdate
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class CSPlanetUpdate : BaseUnityPlugin
    {
        public const string GUID = "com.andy.csplanetupdate";
        public const string NAME = "ComputePlanetUpdate";
        public const string VERSION = "1.0.0";

        public const int MAX_STARS = 64;
        public const int MAX_PLANETS_PER_STAR = 6;
        
        private static readonly string AssemblyPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(CSPlanetUpdate)).Location);
        public static ManualLogSource logger;
        private static AssetBundle bundle;
        
        private static ComputeShader runtimePoseShader;
        private static int poseCSKernelId;
        private static uint poseCSThreads;
        private static ComputeBuffer posBuffer;
        private static ComputeBuffer planetBuffer;
        private static csPlanetData[] planetData;
        private static csPosData[] posData;
        private static int NUM_PLANETS;
        private static readonly int Time1 = Shader.PropertyToID("_Time");


        private void Awake()
        {
            logger = Logger;
            
            //var path = Path.Combine(AssemblyPath, "csplanetupdate-bundle");
            var path = Path.Combine(AssemblyPath, "dspshaders-bundle");
            bundle = AssetBundle.LoadFromFile(path);
            
            NUM_PLANETS = 1 + MAX_STARS * MAX_PLANETS_PER_STAR;
            runtimePoseShader = bundle?.LoadAsset<ComputeShader>("CSUpdateRuntimePose");
            poseCSKernelId = runtimePoseShader.FindKernel("CSMain");
            runtimePoseShader.GetKernelThreadGroupSizes(poseCSKernelId, out poseCSThreads, out _, out _);
            runtimePoseShader.SetInt("_NumPlanets", NUM_PLANETS);

            Harmony.CreateAndPatchAll(typeof(CSPlanetUpdate));
        }

        [HarmonyPatch(typeof(GalaxyData), nameof(GalaxyData.UpdatePoses))]
        [HarmonyPrefix]
        public static bool GalaxyData_UpdatePoses_Prefix(GalaxyData __instance, double time)
        {
            runtimePoseShader.SetFloat(Time1, (float)time);
            runtimePoseShader.Dispatch(poseCSKernelId, Mathf.Max(1, Mathf.CeilToInt(NUM_PLANETS / (float)poseCSThreads)), 2, 1);
            posBuffer.GetData(posData);

            for(int i = 1; i < NUM_PLANETS; i++)
            {
                int starId = (i - 1) / MAX_PLANETS_PER_STAR;
                if (starId >= __instance.starCount)
                    break;
                StarData star = __instance.stars[starId];
                int planetId = (i - 1) % MAX_PLANETS_PER_STAR;
                if (planetId >= star.planetCount)
                    continue;

                PlanetData planet = star.planets[planetId];
                
                planet.runtimeOrbitPhase = posData[i].runtimeOrbitPhase;
                planet.runtimePosition = posData[i].runtimePosition;
                planet.runtimeRotation.x = posData[i].runtimeRotation.x;
                planet.runtimeRotation.y = posData[i].runtimeRotation.y;
                planet.runtimeRotation.z = posData[i].runtimeRotation.z;
                planet.runtimeRotation.w = posData[i].runtimeRotation.w;
                planet.runtimeLocalSunDirection = posData[i].runtimeLocalSunDirection;
    
                planet.uPosition = star.uPosition + planet.runtimePosition * 40000.0;
    
                int id = planet.id;
                __instance.astrosData[id].uPos = planet.uPosition;
                __instance.astrosData[id].uRot = planet.runtimeRotation;
                __instance.astrosFactory[id] = planet.factory;
            }
            
            for(int i = NUM_PLANETS + 1; i < NUM_PLANETS * 2; i++)
            {
                int starId = (i - NUM_PLANETS - 1) / MAX_PLANETS_PER_STAR;
                if (starId >= __instance.starCount)
                    break;
                StarData star = __instance.stars[starId];
                int planetId = (i - NUM_PLANETS - 1) % MAX_PLANETS_PER_STAR;
                if (planetId >= star.planetCount)
                    continue;
                
                PlanetData planet = star.planets[planetId];
                
                planet.runtimePositionNext = posData[i].runtimePosition;
                planet.runtimeRotationNext.x = posData[i].runtimeRotation.x;
                planet.runtimeRotationNext.y = posData[i].runtimeRotation.y;
                planet.runtimeRotationNext.z = posData[i].runtimeRotation.z;
                planet.runtimeRotationNext.w = posData[i].runtimeRotation.w;
    
                planet.uPositionNext = star.uPosition + planet.runtimePositionNext * 40000.0;
    
                int id = planet.id;
                __instance.astrosData[id].uPosNext = planet.uPositionNext;
                __instance.astrosData[id].uRotNext = planet.runtimeRotationNext;
            }
            
            return false;
        }

        [HarmonyPatch(typeof(UniverseGen), nameof(UniverseGen.CreateGalaxy))]
        [HarmonyPostfix]
        public static void UniverseGen_CreateGalaxy_Postfix(ref GalaxyData __result)
        {
            planetData = new csPlanetData[NUM_PLANETS];
            for (int i = 1; i < NUM_PLANETS; i++)
            {
                int starId = (i - 1) / MAX_PLANETS_PER_STAR;
                if (starId >= __result.starCount)
                    break;
                StarData star = __result.stars[starId];
                int planetId = (i - 1) % MAX_PLANETS_PER_STAR;
                if (planetId >= star.planetCount)
                    continue;

                PlanetData planet = star.planets[planetId];

                planetData[i].orbitalPeriod = (float)planet.orbitalPeriod;
                planetData[i].orbitPhase = planet.orbitPhase;
                planetData[i].rotationPeriod = (float)planet.rotationPeriod;
                planetData[i].rotationPhase = planet.rotationPhase;
                planetData[i].runtimeOrbitRotation = new Vector4(planet.runtimeOrbitRotation.x, planet.runtimeOrbitRotation.y, planet.runtimeOrbitRotation.z, planet.runtimeOrbitRotation.w);
                planetData[i].orbitRadius = planet.orbitRadius;
                planetData[i].runtimeSystemRotation = new Vector4(planet.runtimeSystemRotation.x, planet.runtimeSystemRotation.y, planet.runtimeSystemRotation.z, planet.runtimeSystemRotation.w);
                planetData[i].orbitAroundPlanet = planet.orbitAroundPlanet.id; // wrong
            }

            planetBuffer?.Release();
            planetBuffer = new ComputeBuffer(NUM_PLANETS, 56);
            runtimePoseShader.SetBuffer(poseCSKernelId,"_PlanetBuffer", planetBuffer);
            planetBuffer.SetData(planetData);


            posData = new csPosData[NUM_PLANETS * 2];
            posBuffer?.Release();
            posBuffer = new ComputeBuffer(NUM_PLANETS * 2, 44);
            runtimePoseShader.SetBuffer(poseCSKernelId, "_PosBuffer", posBuffer);
        }
        
    }
}