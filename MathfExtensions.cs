using System;
using UnityEngine;

namespace FantasticSplines
{
    public static class MathHelper
    {
        public static bool IsInArrayRange(int i, int Count)
        {
            return i >= 0 && i < Count;
        }

        public static int WrapIndex(int i, int N)
        {
            return ((i % N) + N) % N;
        }

        public static float Remap(float v, float vMin, float vMax, float newMin, float newMax)
        {
            float t = Mathf.InverseLerp( vMin, vMax, v );
            return Mathf.Lerp( newMin, newMax, t );
        }

        public static Vector3 LinePlaneIntersection(Ray ray, Vector3 planePoint, Vector3 planeNormal)
        {
            return LinePlaneIntersection( ray.origin, ray.direction, planePoint, planeNormal );
        }

        public static Vector3 LinePlaneIntersection(Vector3 rayOrigin, Vector3 rayDirection, Vector3 planePoint,
            Vector3 planeNormal)
        {
            Vector3 diff = rayOrigin - planePoint;
            float prod1 = Vector3.Dot( diff, planeNormal );
            float prod2 = Vector3.Dot( rayDirection, planeNormal );
            float prod3 = prod1 / prod2;
            return rayOrigin - rayDirection * prod3;
        }
    }
}