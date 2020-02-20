using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FantasticSplines
{

    public partial class Curve
    {
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
                segmentIndex = segPos.index;
                segmentDistance = curve._segments[segmentIndex].GetDistance(segPos.segmentT);
            }

            public Curve curve;
            public int segmentIndex;
            public float segmentDistance;
            public float segmentT => curve._segments[segmentIndex].GetT(segmentDistance);

            public Vector3 Position => curve._segments[segmentIndex].GetPositionAtDistance(segmentDistance);
            public Vector3 Tangent => curve._segments[segmentIndex].GetTangentAtDistance(segmentDistance);

            public float DistanceOnSpline => curve._segments[segmentIndex].startDistanceInSpline + segmentDistance;
        }

        private class SegmentCache
        {
            public Bezier3 bezier;
            private Vector2[] tdMapping;

            public float startDistanceInSpline;
            public float Length => tdMapping[tdMapping.Length - 1].y;

            public SegmentCache(Bezier3 bez, float distanceOnSpline, int accuracy = DEFAULT_SEGMENT_LUT_ACCURACY)
            {
                Initialise(bez, distanceOnSpline, accuracy);
            }

            public void Initialise(Bezier3 bez, float distanceOnSpline,
                int accuracy = DEFAULT_SEGMENT_LUT_ACCURACY)
            {
                bezier = bez;
                startDistanceInSpline = distanceOnSpline;
                if (tdMapping == null || tdMapping.Length != accuracy)
                {
                    tdMapping = new Vector2[accuracy];
                }
                float invAccuracy = 1f / (accuracy - 1);
                for (int i = 0; i < accuracy; ++i)
                {
                    float t = i * invAccuracy;
                    float d = bez.CalculateDistanceAt(t);
                    tdMapping[i] = new Vector2(t, d);
                }
            }

            public Vector3 GetPositionAtT(float t) => bezier.GetPos(t);
            public Vector3 GetTangentAtT(float t) => bezier.GetTangent(t);
            public Vector3 GetPositionAtDistance(float distance) => GetPositionAtT(GetT(distance));
            public Vector3 GetTangentAtDistance(float distance) => GetTangentAtT(GetT(distance));

            public float GetT(float d)
            {
                // TODO: Binary search this
                for (int i = 1; i < tdMapping.Length; ++i)
                {
                    if (d <= tdMapping[i].y)
                    {
                        float ratio = Mathf.InverseLerp(tdMapping[i - 1].y, tdMapping[i].y, d);
                        return Mathf.Lerp(tdMapping[i - 1].x, tdMapping[i].x, ratio);
                    }
                }

                return 1f;
            }

            public float GetDistance(float t)
            {
                // TODO: Binary search this
                for (int i = 1; i < tdMapping.Length; ++i)
                {
                    if (t <= tdMapping[i].x)
                    {
                        float ratio = Mathf.InverseLerp(tdMapping[i - 1].x, tdMapping[i].x, t);
                        return Mathf.Lerp(tdMapping[i - 1].y, tdMapping[i].y, ratio);
                    }
                }

                return Length;
            }
        }
    }
}