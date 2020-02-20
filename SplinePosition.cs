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

            SplinePosition result = new SplinePosition(spline, DistanceOnSpline + step);
            float splineLength = spline.GetLength();

            Vector3 lastPosition = origin;
            float worldDistanceTest = Vector3.Distance( result.Position, origin );

            int exit = 100;
            while( worldDistanceTest < worldDistance && result.DistanceOnSpline < splineLength )
            {
                lastPosition = result.Position;
                result = new SplinePosition(spline, result.DistanceOnSpline + step );
                worldDistanceTest = Vector3.Distance( result.Position, origin );

                exit--;
                if( exit < 0 )
                {
                    break;
                }
            }

            float lastWorldDistanceTest = Vector3.Distance( lastPosition, origin );
            float t = Mathf.InverseLerp( lastWorldDistanceTest, worldDistanceTest, worldDistance );

            return new SplinePosition(spline, result.DistanceOnSpline - step + (step * t) );
        }
    }
}