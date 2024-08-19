using UnityEngine;

namespace CSPlanetUpdate
{
    public struct csPlanetData
    {
        public float orbitalPeriod; // double!
        public float orbitPhase;
        public float rotationPeriod; // double!
        public float rotationPhase;
        public Vector4 runtimeOrbitRotation; // quat
        public float orbitRadius;
        public Vector4 runtimeSystemRotation; // quat
        public int orbitAroundPlanet; // can be null?
    }
}