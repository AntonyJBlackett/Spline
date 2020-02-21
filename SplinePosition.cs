using UnityEngine;

namespace FantasticSplines
{
    [System.Serializable]
    public struct SegmentPosition
    {
        [SerializeField] private int _index;
        [SerializeField] private float _segmentT;

        public int index { get { return _index; } }
        public float segmentT { get { return _segmentT; } }
        
        public SegmentPosition(int index, float segmentT)
        {
            this._index = index; 
            this._segmentT = segmentT; 
        }

        public override string ToString()
        {
            return string.Format("Seg[{0},{1}]", index, segmentT);
        }
    }

    [System.Serializable]
    public struct SplinePosition
    {
        public SplineBehaviour spline;
        public SegmentPosition segmentPosition;

        public float DistanceOnSpline => spline.GetDistanceOnSpline( segmentPosition );
        public Vector3 Position => spline.GetPosition( segmentPosition );
        public Vector3 Tangent => spline.GetDirection( segmentPosition );

        public bool AtEnd { get; private set; }
        public bool AtStart { get; private set; }

        public SplinePosition(SplineBehaviour spline, float distance)
        {
            this.spline = spline;
            this.segmentPosition = spline.GetSegmentAtDistance(distance);
            
            float length = spline.GetLength();
            float inaccuracy = float.Epsilon * spline.GetPointCount();
            this.AtEnd = distance >= length - inaccuracy;
            this.AtStart = distance < float.Epsilon;
        }

        public SplinePosition Move(float stepDistance)
        {
            return new SplinePosition(spline, DistanceOnSpline + stepDistance);
        }

        public SplinePosition MoveUntilAtWorldDistance(float worldDistance, float step)
        {
            Vector3 origin = Position;

            SplinePosition stepPosition = new SplinePosition(spline, DistanceOnSpline + step);
            float splineLength = spline.GetLength();

            Vector3 lastPosition = origin;
            float worldDistanceTest = Vector3.Distance( stepPosition.Position, origin );

            float distanceOnSpline = stepPosition.DistanceOnSpline;

            if( step < float.Epsilon )
            {
                step = Mathf.Max( 0.001f, worldDistance / 20.0f );
            }
            int maxIterations = Mathf.CeilToInt(worldDistance * 5f  / step);
            int iterationsLeft = maxIterations;
            while( worldDistanceTest < worldDistance && !stepPosition.AtEnd )
            {
                lastPosition = stepPosition.Position;
                stepPosition = new SplinePosition(spline, distanceOnSpline + step );
                distanceOnSpline = stepPosition.DistanceOnSpline;
                worldDistanceTest = Vector3.Distance( stepPosition.Position, origin );

                --iterationsLeft;
                if( iterationsLeft < 0 )
                {
                    Debug.LogWarning( "Hit iterations limit of " + maxIterations + " in MoveUntilAtWorldDistance() on spline: " + spline.name, spline.gameObject );
                    break;
                }
            }

            if( stepPosition.AtEnd )
            {
                return stepPosition;
            }

            float lastWorldDistanceTest = Vector3.Distance( lastPosition, origin );
            float t = Mathf.InverseLerp( lastWorldDistanceTest, worldDistanceTest, worldDistance );

            return new SplinePosition(spline, distanceOnSpline - step + (step * t) );
        }
    }
}