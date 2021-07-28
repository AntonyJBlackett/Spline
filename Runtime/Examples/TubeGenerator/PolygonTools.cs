using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FantasticSplines
{
    // Triangulator script found here:
    // Javascript version: https://www.flipcode.com/archives/Efficient_Polygon_Triangulation.shtml
    // Converted to c# Unity by Siggle: https://forum.unity.com/threads/polygon-triangulation-code.27223/
    // And a few modifications by me: Antony Blackett
    public static class Triangulator
    {
        public static void Triangulate( List<Vector2> points, ref List<int> triangles )
        {
            triangles.Clear();
            var pointCount = points.Count;
            if( pointCount < 3 )
                return;

            var V = new int[pointCount];
            if( Area( points ) > 0 )
            {
                for( var v = 0; v < pointCount; v++ )
                    V[v] = v;
            }
            else
            {
                for( var v = 0; v < pointCount; v++ )
                    V[v] = (pointCount - 1) - v;
            }

            var nv = pointCount;
            var count = 2 * nv;
            var m = 0;
            for( var v = nv - 1; nv > 2; )
            {
                if( (count--) <= 0 )
                    return;

                var u = v;
                if( nv <= u )
                    u = 0;
                v = u + 1;
                if( nv <= v )
                    v = 0;
                var w = v + 1;
                if( nv <= w )
                    w = 0;

                if( Snip( points, u, v, w, nv, V ) )
                {
                    int a;
                    int b;
                    int c;
                    int s;
                    int t;
                    a = V[u];
                    b = V[v];
                    c = V[w];
                    triangles.Add( a );
                    triangles.Add( c );
                    triangles.Add( b );
                    m++;
                    s = v;
                    for( t = v + 1; t < nv; t++ )
                    {
                        V[s] = V[t];
                        s++;
                    }
                    nv--;
                    count = 2 * nv;
                }
            }

            return;
        }

        private static float Area( List<Vector2> points )
        {
            int n = points.Count;
            float A = 0.0f;
            int q = 0;
            for( var p = n - 1; q < n; p = q++ )
            {
                var pval = points[p];
                var qval = points[q];
                A += pval.x * qval.y - qval.x * pval.y;
            }
            return (A * 0.5f);
        }

        private static bool Snip( List<Vector2> points, int u, int v, int w, int n, int[] V )
        {
            int p;
            var A = points[V[u]];
            var B = points[V[v]];
            var C = points[V[w]];

            if( Mathf.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x))) )
                return false;
            for( p = 0; p < n; p++ )
            {
                if( (p == u) || (p == v) || (p == w) )
                    continue;
                var P = points[V[p]];
                if( InsideTriangle( A, B, C, P ) )
                    return false;
            }
            return true;
        }

        private static bool InsideTriangle( Vector2 A, Vector2 B, Vector2 C, Vector2 P )
        {
            float ax;
            float ay;
            float bx;
            float by;
            float cx;
            float cy;
            float apx;
            float apy;
            float bpx;
            float bpy;
            float cpx;
            float cpy;
            float cCROSSap;
            float bCROSScp;
            float aCROSSbp;

            ax = C.x - B.x; ay = C.y - B.y;
            bx = A.x - C.x; by = A.y - C.y;
            cx = B.x - A.x; cy = B.y - A.y;
            apx = P.x - A.x; apy = P.y - A.y;
            bpx = P.x - B.x; bpy = P.y - B.y;
            cpx = P.x - C.x; cpy = P.y - C.y;

            aCROSSbp = ax * bpy - ay * bpx;
            cCROSSap = cx * apy - cy * apx;
            bCROSScp = bx * cpy - by * cpx;

            return ((aCROSSbp >= 0.0) && (bCROSScp >= 0.0) && (cCROSSap >= 0.0));
        }
    }
}
