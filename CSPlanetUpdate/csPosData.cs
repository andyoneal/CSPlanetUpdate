using UnityEngine;

namespace CSPlanetUpdate
{
    public struct csPosData
    {
        public float runtimeOrbitPhase;
        public Vector3 runtimePosition; // double!
        public Vector4 runtimeRotation; //quat
        public Vector3 runtimeLocalSunDirection;
    }
}