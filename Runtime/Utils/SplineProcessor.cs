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

        public static void FindCorners( ref List<int> corners, ISpline spline, List<SplineResult> splinePoints, float cornerAngle )
        {
            corners.Clear();
            for( int p = 1; p < splinePoints.Count; ++p )
            {
                SplineResult splinePoint = splinePoints[p];

                // calculate the tangent at the point so we can then
                // calculate a scalar to compensate for pinching corners on acute angled points
                Vector3 pointTangent = splinePoint.tangent.normalized;
                Vector3 inTangent = pointTangent;
                Vector3 outTangent = pointTangent;

                bool firstPoint = p == 0;
                bool lastPoint = p == splinePoints.Count - 1;

                if( (!firstPoint && !lastPoint) || spline.IsLoop() )
                {
                    int previousIndex = p - 1;
                    int nextIndex = p + 1;

                    if( spline.IsLoop() && previousIndex < 0 )
                    {
                        previousIndex = splinePoints.Count - 2; // -2 because the end point is actually the 'same point'
                    }
                    if( spline.IsLoop() && nextIndex >= splinePoints.Count )
                    {
                        nextIndex = 1; // 1 because the first point is actually the 'same point'
                    }

                    SplineResult previous = splinePoints[previousIndex];
                    SplineResult next = splinePoints[nextIndex];

                    inTangent = (splinePoint.position - previous.position).normalized;
                    outTangent = (next.position - splinePoint.position).normalized;
                }
                if( Vector3.Angle( inTangent, outTangent ) > cornerAngle )
                {
                    corners.Add( p );
                }
            }
        }

        public static void AddResultsAtNodes( ref List<SplineResult> results, ISpline spline )
        {
            int nodeCount = spline.GetNodeCount();
            for( int i = 0; i < nodeCount; i++ )
            {
                SplineNode node = spline.GetNode( i );
                SplineResult result = spline.GetResultAtNode( i );
                if( i != 0 )
                {
                    if( node.NodeType == NodeType.Point || node.NodeType == NodeType.Free && Mathf.Approximately( result.segmentResult.t, 1 ) )
                    {
                        results.Add( spline.GetResultAtSegmentT( result.segmentResult.index - 1, 1 ) ); // in node
                    }
                }

                results.Add( result ); // out node
            }

            if( spline.IsLoop() )
            {
                results.Add( spline.GetResultAtSegmentT( nodeCount-1, 1 ) ); // out node of loop segment
            }

            Sort( ref results );
        }

        public static void AddResultsAtKeys<T>( ref List<SplineResult> results, KeyframedSplineParameter<T> keyframedSplineParameter ) where T : new()
        {
            var keys = keyframedSplineParameter.OrderedKeyframes;
            for( int i = 0; i < keys.Count; i++ )
            {
                results.Add( keys[i].location );
            }
            Sort( ref results );
        }

        public static void AddPointsByTollerance( ref List<SplineResult> results, ISpline spline, float minStepDistance, System.Func<SplineResult,SplineResult,bool> tolleranceFunction )
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
                    SplineResult previousResult = results[previousIndex];
                    float segmentStep = segmentLength / segmentDivisions;
                    for( int s = 1; s < segmentDivisions; ++s )
                    {
                        float segmentDistance = segmentStep * s;
                        SplineResult result = spline.GetResultAtDistance( results[previousIndex].distance + segmentDistance );

                        if( tolleranceFunction( previousResult, result ) )
                        {
                            results.Add( result );
                            previousResult = result;
                        }
                    }
                }
            }
            Sort( ref results );
        }

        public static void RemovePointsAtSameLocation( ref List<SplineResult> results, float tollerance = 0.1f )
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
    }
}
