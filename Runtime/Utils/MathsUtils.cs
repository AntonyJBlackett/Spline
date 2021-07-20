using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines
{
    public static class MathsUtils
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

        public static Vector3 ProjectVectorOnPlane( Vector3 planeNormal, Vector3 vector )
        {
            float prod1 = Vector3.Dot( vector, planeNormal );
            return vector - planeNormal * prod1;
        }

        public static Vector3 ProjectPointOnPlane(Vector3 planePoint, Vector3 planeNormal, Vector3 point)
        {
            Vector3 diff = point - planePoint;
            float prod1 = Vector3.Dot( diff, planeNormal );
            return point - planeNormal * prod1;
        }

        public static Vector3 LinePlaneIntersection(Ray ray, Vector3 planePoint, Vector3 planeNormal)
        {
            return LinePlaneIntersection( ray.origin, ray.direction, planePoint, planeNormal );
        }

        public static Vector3 LinePlaneIntersection(Vector3 rayOrigin, Vector3 rayDirection, Vector3 planePoint, Vector3 planeNormal)
        {
            Vector3 diff = rayOrigin - planePoint;
            float prod1 = Vector3.Dot( diff, planeNormal );
            float prod2 = Vector3.Dot( rayDirection, planeNormal );
            float prod3 = prod1 / prod2;
            return rayOrigin - rayDirection * prod3;
        }

        public static bool LineLineIntersection( out Vector3 intersection, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2 )
        {
            Vector3 lineVec3 = linePoint2 - linePoint1;
            Vector3 crossVec1and2 = Vector3.Cross( lineVec1, lineVec2 );
            Vector3 crossVec3and2 = Vector3.Cross( lineVec3, lineVec2 );

            float planarFactor = Vector3.Dot( lineVec3, crossVec1and2 );

            //is coplanar, and not parallel
            if( Mathf.Abs( planarFactor ) < 0.0001f
                    && crossVec1and2.sqrMagnitude > 0.0001f )
            {
                float s = Vector3.Dot( crossVec3and2, crossVec1and2 ) / crossVec1and2.sqrMagnitude;
                intersection = linePoint1 + (lineVec1 * s);
                return true;
            }
            else
            {
                intersection = Vector3.zero;
                return false;
            }
        }

        //Two non-parallel lines which may or may not touch each other have a point on each line which are closest
        //to each other. This function finds those two points. If the lines are not parallel, the function 
        //outputs true, otherwise false.
        public static bool ClosestPointsOnTwoLines( out Vector3 closestPointLine1, out Vector3 closestPointLine2, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2 )
        {

            closestPointLine1 = Vector3.zero;
            closestPointLine2 = Vector3.zero;

            float a = Vector3.Dot( lineVec1, lineVec1 );
            float b = Vector3.Dot( lineVec1, lineVec2 );
            float e = Vector3.Dot( lineVec2, lineVec2 );

            float d = a * e - b * b;

            //lines are not parallel
            if( d != 0.0f )
            {

                Vector3 r = linePoint1 - linePoint2;
                float c = Vector3.Dot( lineVec1, r );
                float f = Vector3.Dot( lineVec2, r );

                float s = (b * f - c * e) / d;
                float t = (a * f - c * b) / d;

                closestPointLine1 = linePoint1 + lineVec1 * s;
                closestPointLine2 = linePoint2 + lineVec2 * t;

                return true;
            }
            else
            {
                return false;
            }
        }

        // Find a circle through the three points.
        public static void FindCircle( Vector3 a, Vector3 b, Vector3 c, out Vector3 center, out float radius )
        {
            Vector3 aTob = (b - a);
            Vector3 bToc = (c - b);

            Vector3 normal = Vector3.Cross( aTob, bToc ).normalized;

            Vector3 midAToB = Vector3.Lerp( a, b, 0.5f );
            Vector3 midBToC = Vector3.Lerp( b, c, 0.5f );

            Vector3 axisAB = Vector3.Cross( aTob, normal ).normalized;
            Vector3 axisBC = Vector3.Cross( bToc, normal ).normalized;

            bool isCurved = MathsUtils.LineLineIntersection( out center, midAToB, axisAB, midBToC, axisBC );
            if( !isCurved )
            {
                center = b;
            }
            radius = (b - center).magnitude;
        }
    }
}