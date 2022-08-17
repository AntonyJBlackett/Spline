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
            public SegmentT t;
            public SegmentDistance d;

            public TD( SegmentT t, SegmentDistance d )
            {
                this.t = t;
                this.d = d;
            }
        }

        private TD[] tdMapping;
        public SegmentDistance Length { get; private set; }

        public bool IsValid()
        {
            return tdMapping != null;
        }

        public TDMap(Bezier3 bez)
        {
            tdMapping = null;
            Length = new SegmentDistance( 0 );
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
                SegmentT t = new SegmentT( i * invAccuracy );
                SegmentDistance d = new SegmentDistance( bez.CalculateDistanceAt( t ) );
                tdMapping[i] = new TD( t, d );

                if( i == ACCURACY - 1 )
                {
                    Length = d;
                }
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

        public SegmentT GetT(SegmentDistance d)
        {

#if DEBUG
            Debug.Assert( d.value >= 0 );
#endif

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

            return new SegmentT( MathsUtils.Remap( d.value, tdMapping[low].d.value, tdMapping[high].d.value, tdMapping[low].t.value, tdMapping[high].t.value ) );
        }

        public SegmentDistance GetDistance(SegmentT t)
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

            return new SegmentDistance( MathsUtils.Remap( t.value, tdMapping[low].t.value, tdMapping[high].t.value, tdMapping[low].d.value, tdMapping[high].d.value ) );
        }
    }

    public partial class Spline
    {
        private class SegmentCache
        {
            public Bezier3 bezier;
            private TDMap tdMapping;

            public SplineDistance startDistanceInSpline;
            public SegmentDistance Length { get; private set; }
            public SegmentT GetT(SegmentDistance d) => tdMapping.GetT( d );
            public SegmentT GetT(SegmentPercent p) => tdMapping.GetT( GetDistance(p) );
            public SegmentDistance GetDistance(SegmentT t) => tdMapping.GetDistance( t );
            public SegmentDistance GetDistance( SegmentPercent p ) => Length * p;
            public SegmentPercent GetPercent( SegmentDistance d ) => new SegmentPercent( d / Length );
            public SegmentPercent GetPercent( SegmentT t ) => new SegmentPercent( GetDistance(t) / Length );

            public SegmentCache(Bezier3 bez, SplineDistance distanceOnSpline )
            {
                Initialise( bez, distanceOnSpline );
            }

            public void Initialise(Bezier3 bez, SplineDistance distanceOnSpline )
            {
                bezier = bez;
                startDistanceInSpline = distanceOnSpline;
                tdMapping.Initialise( bez );
                Length = tdMapping.Length;
            }

            public Vector3 GetPositionAt( SegmentT t ) => bezier.GetPosition( t );
            public Vector3 GetTangentAt( SegmentT t ) => bezier.GetTangent( t );
            public float GetCurvatureAt( SegmentT t ) => bezier.CalculateCurvatureAt( t );
            public float GetRadiusAt( SegmentT t ) => bezier.CalculateRadiusAt( t );

            public Vector3 GetPositionAt( SegmentDistance distance ) => GetPositionAt( tdMapping.GetT( distance ) );
            public Vector3 GetTangentAt( SegmentDistance distance ) => GetTangentAt( tdMapping.GetT( distance ) );
            public float GetCurvatureAt( SegmentDistance distance ) => GetCurvatureAt( tdMapping.GetT( distance ) );
            public float GetRadiusAt( SegmentDistance distance ) => GetRadiusAt( tdMapping.GetT( distance ) );

            public Vector3 GetPositionAt( SegmentPercent percent ) => GetPositionAt( GetT( percent ) );
            public Vector3 GetTangentAt( SegmentPercent percent ) => GetTangentAt( GetT( percent ) );
            public float GetCurvatureAt( SegmentPercent percent ) => GetCurvatureAt( GetT( percent ) );
            public float GetRadiusAt( SegmentPercent percent ) => GetRadiusAt( GetT( percent ) );

        }
    }
}