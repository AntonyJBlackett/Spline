using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FantasticSplines
{
    public struct TDMap
    {
        // MUST BE POWER OF 2
        private const int ACCURACY = 16;

        private struct TD
        {
            public float t, d;

            public TD(float t, float d)
            {
                this.t = t;
                this.d = d;
            }
        }

        private TD[] tdMapping;
        public float Length => tdMapping[tdMapping.Length - 1].d;

        public bool IsValid()
        {
            return tdMapping != null;
        }

        public TDMap(Bezier3 bez)
        {
            tdMapping = null;
            Initialise(bez);
        }

        public void Initialise(Bezier3 bez)
        {
            if (tdMapping == null || tdMapping.Length != ACCURACY)
            {
                tdMapping = new TD[ACCURACY];
            }

            float invAccuracy = 1f / (ACCURACY - 1);
            for (int i = 0; i < ACCURACY; ++i)
            {
                float t = i * invAccuracy;
                float d = bez.CalculateDistanceAt(t);
                tdMapping[i] = new TD(t, d);
            }
        }

        int Split(ref int low, ref int high, int mid, bool isLow)
        {
            if (isLow)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }

            return (low + high) / 2;
        }

        public float GetT(float d)
        {
            int low = 0;
            int high = ACCURACY;
            int mid = (low + high) / 2;

            mid = Split(ref low, ref high, mid, d < tdMapping[mid].d); // 8
            mid = Split(ref low, ref high, mid, d < tdMapping[mid].d); // 4 
            mid = Split(ref low, ref high, mid, d < tdMapping[mid].d); // 2
            mid = Split(ref low, ref high, mid, d < tdMapping[mid].d); // 1

            #if DEBUG
            Debug.Assert(ACCURACY == 16);
            Debug.Assert(low + 1 == high);
            #endif

            return MathHelper.Remap(d, tdMapping[low].d, tdMapping[high].d, tdMapping[low].t, tdMapping[high].t);
        }

        public float GetDistance(float t)
        {
            int low = 0;
            int high = ACCURACY;
            int mid = (low + high) / 2;

            mid = Split(ref low, ref high, mid, t < tdMapping[mid].t); // 8
            mid = Split(ref low, ref high, mid, t < tdMapping[mid].t); // 4 
            mid = Split(ref low, ref high, mid, t < tdMapping[mid].t); // 2
            mid = Split(ref low, ref high, mid, t < tdMapping[mid].t); // 1

            #if DEBUG
            Debug.Assert(ACCURACY == 16);
            Debug.Assert(low + 1 == high);
            #endif

            return MathHelper.Remap(t, tdMapping[low].t, tdMapping[high].t, tdMapping[low].d, tdMapping[high].d);
        }
    }

    public partial class Curve
    {
        public const int DEFAULT_SEGMENT_LUT_ACCURACY = 8;
              
        public struct SegmentPointer
        {
            public SegmentPointer(Curve c, int segIndex, float distance)
            {
                curve = c;
                segmentIndex = segIndex;
                segmentDistance = distance;
            }
            public SegmentPointer(Curve c, SegmentPosition segPos)
            {
                curve = c;
                segmentIndex = c.LoopSegementIndex( segPos.index );
                segmentDistance = (curve._segments.Length > 0 ) ? curve._segments[segmentIndex].GetDistance(segPos.segmentT) : 1;
            }

            public Curve curve;
            public int segmentIndex;
            public float segmentDistance;
            public float segmentT => curve._segments[segmentIndex].GetT(segmentDistance);

            public Vector3 Position => (curve._segments.Length > 0 ) ? curve._segments[segmentIndex].GetPositionAtDistance(segmentDistance) : Vector3.zero;
            public Vector3 Tangent => (curve._segments.Length > 0 ) ? curve._segments[segmentIndex].GetTangentAtDistance(segmentDistance) : Vector3.forward;

            public float DistanceOnSpline => (curve._segments.Length > 0 ) ? curve._segments[segmentIndex].startDistanceInSpline + segmentDistance : 1;
        }

        
        private class SegmentCache
        {
            public Bezier3 bezier;
            private TDMap tdMapping;

            public float startDistanceInSpline;
            public float Length => tdMapping.Length;
            public float GetT(float d) => tdMapping.GetT(d);
            public float GetDistance(float t) => tdMapping.GetDistance(t);

            public SegmentCache(Bezier3 bez, float distanceOnSpline, int accuracy = DEFAULT_SEGMENT_LUT_ACCURACY)
            {
                Initialise(bez, distanceOnSpline, accuracy);
            }

            public void Initialise(Bezier3 bez, float distanceOnSpline,
                int accuracy = DEFAULT_SEGMENT_LUT_ACCURACY)
            {
                bezier = bez;
                startDistanceInSpline = distanceOnSpline;
                tdMapping.Initialise(bez);
            }

            public Vector3 GetPositionAtT(float t) => bezier.GetPos(t);
            public Vector3 GetTangentAtT(float t) => bezier.GetTangent(t);
            public Vector3 GetPositionAtDistance(float distance) => GetPositionAtT(tdMapping.GetT(distance));
            public Vector3 GetTangentAtDistance(float distance) => GetTangentAtT(tdMapping.GetT(distance));

        }
    }
}