using UnityEngine;

namespace FantasticSplines
{
    public interface ISplinePosition
    {
        SplinePosition Move(float stepDistance);
        SplinePosition MoveUntilAtWorldDistance(float worldDistance, float step);
        void MoveToStart();
        void MoveTo( float distance );
        void MoveToEnd();
        bool IsAtEnd();
        bool IsAtStart();
    }

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

        public bool AtStart { get; private set; }
        public bool AtEnd { get; private set; }

        public SplinePosition(SplineBehaviour spline, float distance)
        {
            this.spline = spline;
            this.segmentPosition = spline.GetSegmentAtDistance(distance);

            float length = spline.GetLength();
            float inaccuracy = float.Epsilon * spline.GetPointCount();
            this.AtStart = distance >= length - inaccuracy;
            this.AtEnd = distance < float.Epsilon;
        }

        public SplinePosition(SplineBehaviour newSpline, SegmentPosition newPosition)
        {
            this.spline = newSpline;
            this.segmentPosition = newPosition;

            float length = spline.GetLength();
            float inaccuracy = float.Epsilon * spline.GetPointCount();
            float distance = spline.GetDistanceOnSpline( segmentPosition );
            this.AtStart = distance >= length - inaccuracy;
            this.AtEnd = distance < float.Epsilon;
        }

        public SplinePosition Move(float stepDistance)
        {
            return new SplinePosition(spline, DistanceOnSpline + stepDistance);
        }

        public SplinePosition MoveUntilAtWorldDistance(float worldDistance)
        {
            return MoveUntilAtWorldDistance( worldDistance, worldDistance*0.333f );
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

        public SplinePosition OnSplineWillPrependPoint()
        {
            return new SplinePosition(spline, spline.GetDistanceOnSpline( new SegmentPosition( segmentPosition.index+1, segmentPosition.segmentT ) ) );
        }

        public SplinePosition OnSplineWillInsertPoint( int addPointIndex, float distance )
        {
            int newIndex = segmentPosition.index;
            float newSegmentT = segmentPosition.segmentT;

            if( addPointIndex <= segmentPosition.index )
            {
                newIndex++;
            } 
            else if( segmentPosition.index+1 == addPointIndex )
            {
                // our segment will be split!
                float segmentLength = spline.GetSegmentLength( segmentPosition.index );
                float newSegmentLength = distance - spline.GetDistanceOnSpline( new SegmentPosition( segmentPosition.index, 0 ) );

                float segmentSplitT = MathHelper.Remap( newSegmentLength, 0, segmentLength, 0, 1 );

                if( segmentPosition.segmentT < segmentSplitT )
                {
                    newSegmentT = MathHelper.Remap( segmentPosition.segmentT, 0, segmentSplitT, 0, 1 );
                }
                else
                {
                    newSegmentT = MathHelper.Remap( segmentPosition.segmentT, segmentSplitT, 1, 0, 1 );
                    newIndex++;
                }
            }
            else
            {
                return this;
            }

            return new SplinePosition(spline, spline.GetDistanceOnSpline( new SegmentPosition( newIndex, newSegmentT ) ) );
        }

        public SplinePosition OnSplineWillChangeLoop(bool willLoop)
        {
            if( !spline.IsLoop() )
            {
                // going from !looped to looped
                return this;
            }
            
            // going from looped to !looped
            int currentSegmentIndex = segmentPosition.index;
            int segmentCount = spline.GetPointCount();
            if( segmentPosition.index == segmentCount - 1 )
            {
                // we're on the loop segment and that's about to disappear
                int newSegmentIndex = currentSegmentIndex - 1;
                float newSegmentT = 1f;

                return new SplinePosition(spline, spline.GetDistanceOnSpline( new SegmentPosition( newSegmentIndex, newSegmentT ) ) );
            }

            // we're not on the loop segment
            return this;
        }

        public SplinePosition OnSplineWillRemovePoint(int removePointIndex)
        {
            int currentSegmentIndex = segmentPosition.index;
            int newSegmentIndex = currentSegmentIndex;

            float currentSegmentT = segmentPosition.segmentT;
            float newSegmentT = currentSegmentT;

            int pointCount = spline.GetPointCount();
            int segmentCount = pointCount;
            if( !spline.IsLoop() )
            {
                segmentCount--;
            }

            /// 1,2,3,4 are points in the spline 
            /// '~' is the segment we are on.
            /// '-' other segments 
            /// '|' point being removed
            /// remove point index < segment index
            /// 0-|-2-3~4    ->   0-2-3~4   ->  0-1-2~3   decrease index
            /// 
            /// remove point index == segment index
            /// 0-1-|~3-4    ->   0-1~3-4   ->  0-1~2-3   merge with previous, segment, index == previous Segment Index
            /// 0-1-2-3-|~   ->   0-1-2-3~  ->  0-1-2-3~  merge with previous, segment, index == previous Segment Index
            /// |~1-2-3-4-   ->   1-2-3-4~  ->  0-1-2-3~  merge with loop(last) segment, index == previous Segment Index
            /// 
            /// remove point index == segment index && remove point index == 0
            /// |~1-2-3-4    ->   1~2-3-4   ->  0~1-2-3   is at start --- only for non looping splines
            /// 
            /// remove point index == segment index + 1
            /// 0~|-2-3-4    ->   0~2-3-4   ->  0~1-2-3   merge with next
            /// 0-1-2-3~|    ->   0-1-2~3   ->  0-1-2~3   is at end, decrease index
            /// 
            /// remove point index > segment index + 1
            /// 0~1-|-3-4    ->   0~1-3-4   ->  0~1-2-3   do nothing
            /// 

            if( removePointIndex < currentSegmentIndex )
            {
                // decrease index
                newSegmentIndex--;
            }
            else if( removePointIndex == currentSegmentIndex )
            {
                if( removePointIndex == 0 && !spline.IsLoop() )
                {
                    // is at start
                    newSegmentT = 0;
                }
                else
                {
                    // merge with previous, decrease index
                    int previousSegmentIndex = currentSegmentIndex - 1;
                    if( previousSegmentIndex < 0 ) // loop case
                    {
                        previousSegmentIndex = segmentCount - 1;
                    }

                    float firstLength = spline.GetSegmentLength( previousSegmentIndex );
                    float secondLength = spline.GetSegmentLength( currentSegmentIndex );

                    float totalLength = firstLength + secondLength;
                    float splitT = 0;
                    if( totalLength > 0 )
                    {
                        splitT = Mathf.InverseLerp( 0, 1, firstLength / totalLength );
                    }

                    newSegmentT = MathHelper.Remap( segmentPosition.segmentT, splitT, 1, 0, 1 );
                    newSegmentIndex = previousSegmentIndex;
                }
            }
            else if( removePointIndex == currentSegmentIndex + 1 )
            {
                if( removePointIndex == segmentCount )
                {
                    // is at end, decrease index
                    newSegmentT = 1;
                    newSegmentIndex = currentSegmentIndex - 1;
                }
                else
                {
                    // merge with next
                    float firstLength = spline.GetSegmentLength( segmentPosition.index );
                    float secondLength = spline.GetSegmentLength( segmentPosition.index + 1 );

                    float totalLength = firstLength + secondLength;
                    float splitT = 0;
                    if( totalLength > 0 )
                    {
                        splitT = Mathf.InverseLerp( 0, 1, firstLength / totalLength );
                    }

                    newSegmentT = MathHelper.Remap( segmentPosition.segmentT, splitT, 1, 0, 1 );
                }
            }
            // do nothing
            else if( removePointIndex > currentSegmentIndex + 1 )
            {
                return this;
            }

            return new SplinePosition(spline, new SegmentPosition( newSegmentIndex, newSegmentT ) );
        }
    }

    [System.Serializable]
    public class LiveSplinePosition
    {
        [SerializeField] SplinePosition splinePosition;

        SplineBehaviour spline = null;
        bool initialised => spline == splinePosition.spline;
        public bool IsValid
        {
            get
            {
                if( !initialised ) Initialise(); 
                return spline != null;
            }
        }

        public Vector3 Position => splinePosition.Position;
        public Vector3 Tangent => splinePosition.Tangent;
        public float DistanceOnSpline => splinePosition.DistanceOnSpline;

        public void Initialise(Spline spline, float distance)
        {
            splinePosition = new SplinePosition( spline, distance );
            Initialise();
        }
        
        public void Initialise()
        {
            CleanUp();
            this.spline = splinePosition.spline;
            if( spline == null )
            {
                return;
            }

            spline.onWillPrependPoint += OnSplineWillPrependPoint;
            spline.onWillInsertPoint += OnSplineWillInsertPoint;
            spline.onWillRemovePoint += OnSplineWillRemovePoint;
            spline.onWillChangeLoop += OnSplineWillChangeLoop;
        }

        public void CleanUp()
        {
            if( splinePosition.spline == null )
            {
                return;
            }

            splinePosition.spline.onWillPrependPoint -= OnSplineWillPrependPoint;
            splinePosition.spline.onWillInsertPoint -= OnSplineWillInsertPoint;
            splinePosition.spline.onWillRemovePoint -= OnSplineWillRemovePoint;
            splinePosition.spline.onWillChangeLoop -= OnSplineWillChangeLoop;
        }

        void OnSplineWillPrependPoint(  )
        {
            splinePosition = splinePosition.OnSplineWillPrependPoint();
        }

        void OnSplineWillInsertPoint( int index, float splineDistance )
        {
            splinePosition = splinePosition.OnSplineWillInsertPoint( index, splineDistance );
        }

        void OnSplineWillRemovePoint( int removeIndex )
        {
            splinePosition = splinePosition.OnSplineWillRemovePoint( removeIndex );
        }

        void OnSplineWillChangeLoop( bool willLoop )
        {
            splinePosition = splinePosition.OnSplineWillChangeLoop( willLoop );
        }

        public void SetAtStart( )
        {
            if( !initialised )
            {
                Initialise();
            }
            splinePosition = new SplinePosition( splinePosition.spline, 0 );
        }

        public void SetAtEnd( )
        {
            if( !initialised )
            {
                Initialise();
            }
            splinePosition = new SplinePosition( splinePosition.spline, 1 );
        }

        public void SetDistance( float distance )
        {
            if( !initialised )
            {
                Initialise();
            }
            splinePosition = new SplinePosition( splinePosition.spline, distance );
        }

        public void Move( float stepDistance )
        {
            if( !initialised )
            {
                Initialise();
            }
            splinePosition = splinePosition.Move( stepDistance );
        }

        public void MoveUntilAtWorldDistance(float worldDistance)
        {
            if( !initialised )
            {
                Initialise();
            }
            MoveUntilAtWorldDistance( worldDistance, worldDistance*0.333f );
        }

        public void MoveUntilAtWorldDistance( float worldDistance, float stepDistance )
        {
            if( !initialised )
            {
                Initialise();
            }
            splinePosition = splinePosition.MoveUntilAtWorldDistance( worldDistance, stepDistance );
        }
    }
}