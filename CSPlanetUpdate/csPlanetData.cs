using UnityEngine;

namespace CSPlanetUpdate
{
    public struct csPlanetData
    {
        public float orbitalPeriod; // double!
        public float orbitPhase; // orbitPhase / 360.0
        public float rotationPeriod; // double!
        public float rotationPhase; // rotationPhase / 360.0
        public Vector4 runtimeOrbitRotation; // quat
        public float orbitRadius;
        public Vector4 runtimeSystemRotation; // quat
        public int orbitAroundPlanet; // can be null?
    }
}