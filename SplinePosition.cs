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
    }
    
    [System.Serializable]
    public struct SplinePosition
    {
        public SplineComponent spline;
        public SegmentPosition segmentPosition;
        
        public float DistanceOnSpline => spline.GetDistanceOnSpline(segmentPosition);
        public Vector3 Position => spline.GetPositionAtDistance(DistanceOnSpline);
        public Vector3 Tangent => spline.GetTangentAtDistance(DistanceOnSpline);

        public SplinePosition(SplineComponent spline, float distance)
        {
            this.spline = spline;
            this.segmentPosition = spline.GetSegmentAtDistance(distance);
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

            int exit = 100;
            while( worldDistanceTest < worldDistance && distanceOnSpline < splineLength )
            {
                lastPosition = stepPosition.Position;
                stepPosition = new SplinePosition(spline, distanceOnSpline + step );
                distanceOnSpline = stepPosition.DistanceOnSpline;
                worldDistanceTest = Vector3.Distance( stepPosition.Position, origin );

                exit--;
                if( exit < 0 )
                {
                    Debug.LogWarning( "Hit iterations limit in MoveUntilAtWorldDistance() on spline: " + spline.name, spline.gameObject );
                    break;
                }
            }

            float lastWorldDistanceTest = Vector3.Distance( lastPosition, origin );
            float t = Mathf.InverseLerp( lastWorldDistanceTest, worldDistanceTest, worldDistance );

            return new SplinePosition(spline, distanceOnSpline - step + (step * t) );
        }
    }
}