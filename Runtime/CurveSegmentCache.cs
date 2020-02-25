using UnityEngine;

// Authors: Antony Blackett, Matthew Clark
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines
{
    public struct TDMap
    {
        // MUST BE POWER OF 2
        private const int SQRT_ACCURACY = 4;
        private const int ACCURACY = SQRT_ACCURACY ^ 2;

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
            Initialise( bez );
        }

        public void Initialise(Bezier3 bez)
        {
            if( tdMapping == null || tdMapping.Length != ACCURACY )
            {
                tdMapping = new TD[ACCURACY];
            }

            float invAccuracy = 1f / (ACCURACY - 1);
            for( int i = 0; i < ACCURACY; ++i )
            {
                float t = i * invAccuracy;
                float d = bez.CalculateDistanceAt( t );
                tdMapping[i] = new TD( t, d );
            }
        }

        int Split(ref int low, ref int high, int mid, bool isLow)
        {
            if( isLow )
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
            int high = ACCURACY - 1;
            int mid = (low + high) / 2;

            for( int i = 0; i < SQRT_ACCURACY; ++i )
            {
                mid = Split( ref low, ref high, mid, d < tdMapping[mid].d );
            }

#if DEBUG
            Debug.Assert( (SQRT_ACCURACY ^ 2) == ACCURACY );
            Debug.Assert( low + 1 == high );
#endif

            if( low < 0 )
            {
                Debug.Break();
            }
            if( high >= tdMapping.Length )
            {
                Debug.Break();
            }

            return MathsUtils.Remap( d, tdMapping[low].d, tdMapping[high].d, tdMapping[low].t, tdMapping[high].t );
        }

        public float GetDistance(float t)
        {
            int low = 0;
            int high = ACCURACY - 1;
            int mid = (low + high) / 2;

            for( int i = 0; i < SQRT_ACCURACY; ++i )
            {
                mid = Split( ref low, ref high, mid, t < tdMapping[mid].t );
            }

#if DEBUG
            Debug.Assert( (SQRT_ACCURACY ^ 2) == ACCURACY );
            Debug.Assert( low + 1 == high );
#endif

            return MathsUtils.Remap( t, tdMapping[low].t, tdMapping[high].t, tdMapping[low].d, tdMapping[high].d );
        }
    }

    public partial class Curve
    {
        public const int DEFAULT_SEGMENT_LUT_ACCURACY = 8;

        private class SegmentCache
        {
            public Bezier3 bezier;
            private TDMap tdMapping;

            public float startDistanceInSpline;
            public float Length => tdMapping.Length;
            public float GetT(float d) => tdMapping.GetT( d );
            public float GetDistance(float t) => tdMapping.GetDistance( t );

            public SegmentCache(Bezier3 bez, float distanceOnSpline, int accuracy = DEFAULT_SEGMENT_LUT_ACCURACY)
            {
                Initialise( bez, distanceOnSpline, accuracy );
            }

            public void Initialise(Bezier3 bez, float distanceOnSpline,
                int accuracy = DEFAULT_SEGMENT_LUT_ACCURACY)
            {
                bezier = bez;
                startDistanceInSpline = distanceOnSpline;
                tdMapping.Initialise( bez );
            }

            public Vector3 GetPositionAtT(float t) => bezier.GetPosition( t );
            public Vector3 GetTangentAtT(float t) => bezier.GetTangent( t );
            public Vector3 GetPositionAtDistance(float distance) => GetPositionAtT( tdMapping.GetT( distance ) );
            public Vector3 GetTangentAtDistance(float distance) => GetTangentAtT( tdMapping.GetT( distance ) );

        }
    }
}