using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FantasticSplines
{
    public static class SplineProcessor
    {
        public static void Sort( ref List<SplineResult> results )
        {
            results.Sort(
                ( a, b ) =>
                {
                    int sort = a.lapCount.CompareTo( b.lapCount );
                    if( sort != 0 )
                    {
                        return sort;
                    }

                    sort = a.segmentResult.index.CompareTo( b.segmentResult.index );
                    if( sort != 0 )
                    {
                        return sort;
                    }

                    return a.segmentResult.distance.CompareTo( b.segmentResult.distance );
                } );
        }

        public static void Sort( ref List<ExtrudePoint> results )
        {
            results.Sort(
                ( a, b ) =>
                {
                    int compare = a.segment.CompareTo( b.segment );
                    if( compare != 0 )
                    {
                        return compare;
                    }

                    return a.distance.CompareTo( b.distance );
                } );
        }

        public static void AddResultsAtNodes( ref List<ExtrudePoint> results, ISpline spline )
        {
            int nodeCount = spline.GetNodeCount();
            for( int i = 0; i < nodeCount; i++ )
            {
                SplineNode node = spline.GetNode( i );
                SplineResult result1 = spline.GetResultAtNode( i );

                ExtrudePoint point1 = new ExtrudePoint( result1, TubeGenerator.SplineNodePointPriority );

                if( i != 0 )
                {
                    if( node.NodeType == NodeType.Point || node.NodeType == NodeType.Free || Mathf.Approximately( result1.segmentResult.t, 1 ) )
                    {
                        SplineResult result2 = spline.GetResultAtSegmentT( result1.segmentResult.index - 1, 1 );
                        ExtrudePoint point2 = new ExtrudePoint( result2, TubeGenerator.SplineNodePointPriority );
                        results.Add( point2 ); // in node
                    }
                }

                results.Add( point1 ); // out node
            }

            if( spline.IsLoop() )
            {
                SplineResult endOfSplineResult = spline.GetResultAtSegmentT( nodeCount - 1, 1 );
                results.Add( new ExtrudePoint( endOfSplineResult, TubeGenerator.SplineNodePointPriority ) ); // out node of loop segment
            }

            Sort( ref results );
        }

        public static void AddResultsAtKeys<T>( ref List<ExtrudePoint> results, KeyframedSplineParameter<T> keyframedSplineParameter ) where T : new()
        {
            var keys = keyframedSplineParameter.OrderedKeyframes;
            for( int i = 0; i < keys.Count; i++ )
            {
                results.Add( new ExtrudePoint( keys[i].location, TubeGenerator.SplineKeyframePointPriority ) );
            }
            Sort( ref results );
        }

        public static void AddPointsByTollerance( ref List<ExtrudePoint> results, ISpline spline, float minStepDistance, System.Func<ExtrudePoint, ExtrudePoint, bool> tolleranceFunction )
        {
            int resultCount = results.Count;
            for( int i = 1; i < resultCount; ++i )
            {
                int index = i;
                int previousIndex = i - 1;

                float segmentLength = results[index].distance - results[previousIndex].distance;
                int segmentDivisions = Mathf.FloorToInt( segmentLength / minStepDistance );
                if( segmentDivisions > 1 )
                {
                    ExtrudePoint previousResult = results[previousIndex];
                    float segmentStep = segmentLength / segmentDivisions;
                    for( int s = 1; s < segmentDivisions; ++s )
                    {
                        float segmentDistance = segmentStep * s;
                        SplineResult result = spline.GetResultAtDistance( results[previousIndex].distance + segmentDistance );
                        ExtrudePoint newPoint = new ExtrudePoint( result, TubeGenerator.TollerancePointPriority );
                        if( tolleranceFunction( previousResult, newPoint ) )
                        {
                            results.Add( newPoint );
                            previousResult = newPoint;
                        }
                    }
                }
            }
            Sort( ref results );
        }

        public static void RemovePointsAtSameLocation( ref List<ExtrudePoint> results, float tollerance = 0.1f )
        {
            for( int i = results.Count - 2; i >= 0; i-- )
            {
                int previousIndex = i + 1;

                if( Mathf.Abs( results[i].distance - results[previousIndex].distance ) < tollerance )
                {
                    results.RemoveAt( i );
                }
            }
        }

        static List<int> scratch = new List<int>();
        public static void MergePointsAtSameLocation( ref List<SplineResult> results, float tollerance = 0.1f )
        {
            for( int i = 0; i < results.Count; ++i )
            {
                scratch.Clear();
                for( int j = i + 1; j < results.Count; ++j )
                {
                    if( Mathf.Abs( results[i].distance - results[j].distance ) < tollerance )
                    {
                        scratch.Add( j );
                    }
                }

                Vector3 segTangent = results[i].segmentResult.localTangent;
                for( int j = 1; j < scratch.Count; ++j )
                {
                    int index = scratch[j];
                    segTangent += results[index].segmentResult.localTangent;
                }

                for( int j = scratch.Count - 1; j >= 0; --j )
                {
                    int index = scratch[j];
                    results.RemoveAt( index );
                }

                segTangent /= scratch.Count + 1;

                SplineResult result = results[i];
                SegmentResult segmentResult = result.segmentResult;
                segmentResult.localTangent = segTangent;
                result.segmentResult = segmentResult;
                results[i] = result;
            }
        }

        public static float CalculateLength( List<ExtrudePoint> points )
        {
            float length = 0;
            Vector3 previous = points[0].position;
            for( int i = 1; i < points.Count; ++i )
            {
                length += Vector3.Distance( points[i].position, previous );
                previous = points[i].position;
            }
            return length;
        }
    }
}
