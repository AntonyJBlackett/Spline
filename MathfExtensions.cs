using System;
using UnityEngine;

public class MathHelper
{
    public static float Remap( float v, float vMin, float vMax, float newMin, float newMax )
    {
        float t = Mathf.InverseLerp( v, vMin, vMax );
        return Mathf.Lerp( newMin, newMax, t );
    }
    
    public static Vector3 LinePlaneIntersection( Ray ray, Vector3 planePoint, Vector3 planeNormal )
    {
        return LinePlaneIntersection( ray.origin, ray.direction, planePoint, planeNormal );
    }

    public static Vector3 LinePlaneIntersection( Vector3 rayOrigin, Vector3 rayDirection, Vector3 planePoint, Vector3 planeNormal )
    {
        Vector3 diff = rayOrigin - planePoint;
        float prod1 = Vector3.Dot( diff, planeNormal );
        float prod2 = Vector3.Dot( rayDirection, planeNormal );
        float prod3 = prod1 / prod2;
        return rayOrigin - rayDirection * prod3;
    }
}
