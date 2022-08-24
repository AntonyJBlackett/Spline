using System.Collections.Generic;
using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines
{
    // used to reposition keys when the spline is edited.
    public enum RepositionMode
    {
        SplineDistance,
        SplinePercent,
        SegmentDistance,
        SegmentT,
        ClosestPointOnSpline,
        ClosestPointOnSegment,
        SegmentPercent,
    }

    [System.Serializable]
    public struct SegmentResult
    {
        public int index; // segment index
        [Range(0,1)]
        public SegmentPercent percent; // percentage along the segment
        public SegmentT t; // t value along the segment 
        public SegmentDistance distance; // distance in world space along the segment
        public SegmentDistance length; // length of the segment
        public Vector3 position; // world space position
        public Vector3 tangent; // world space tangent
        public Vector3 normal; // world space normal
        public Vector3 localPosition; // spline space position
        public Vector3 localTangent; // spline space tangent
        public Vector3 localNormal; // spline space normal
        public float curvature;
        public float radius;

        public const float segmentEndAccuracy = 0.001f;
        public bool AtSegmentEnd => percent.value > 1-segmentEndAccuracy; // true if result is approximately at the start of the segment
        public bool AtSegmentStart => percent.value < segmentEndAccuracy; // true if result is approximately at the end of the segment

        public static SegmentResult Default
        {
            get
            {
                return new SegmentResult()
                {
                    index = 0,
                    percent = new SegmentPercent( 0 ),
                    t = new SegmentT(0),
                    distance = new SegmentDistance( 0 ),
                    length = new SegmentDistance( 0 ),
                    position = Vector3.zero,
                    tangent = Vector3.forward,
                    normal = Vector3.up,
                    localPosition = Vector3.zero,
                    localTangent = Vector3.forward,
                    localNormal = Vector3.up,
                    curvature = 0,
                    radius = float.PositiveInfinity
                };
            }
        }
    }

    [System.Serializable]
    public struct SegmentDistance : System.IEquatable<SegmentDistance>, System.IComparable<SegmentDistance>
    {
        public float value;

        public SegmentDistance( float t )
        {
            value = t;
        }

        public static explicit operator float( SegmentDistance t )
        {
            return t.value;
        }

        public static explicit operator SegmentDistance( float t )
        {
            return new SegmentDistance( t );
        }

        public override string ToString()
        {
            return this.value.ToString();
        }

        public string ToString( string format )
        {
            return this.value.ToString( format );
        }

        public bool Equals( SegmentDistance other )
        {
            return this.value == other.value;
        }

        public override bool Equals( object obj )
        {
            return obj is SegmentDistance && this.Equals( (SegmentDistance)obj );
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public int CompareTo( SegmentDistance other )
        {
            return value.CompareTo( other.value );
        }

        public static bool operator ==( SegmentDistance a, SegmentDistance b )
        {
            return a.Equals( b );
        }

        public static bool operator !=( SegmentDistance a, SegmentDistance b )
        {
            return !a.Equals( b );
        }

        public static bool operator <( SegmentDistance a, SegmentDistance b )
        {
            return a.value < b.value;
        }

        public static bool operator >( SegmentDistance a, SegmentDistance b )
        {
            return a.value > b.value;
        }

        public static bool operator <=( SegmentDistance a, SegmentDistance b )
        {
            return a.value <= b.value;
        }

        public static bool operator >=( SegmentDistance a, SegmentDistance b )
        {
            return a.value >= b.value;
        }

        public static SegmentDistance operator +( SegmentDistance a, SegmentDistance b )
        {
            return new SegmentDistance( a.value + b.value );
        }

        public static SegmentDistance operator +( SegmentDistance a, int b )
        {
            return new SegmentDistance( a.value + b );
        }

        public static SegmentDistance operator +( SegmentDistance a, float b )
        {
            return new SegmentDistance( a.value + b );
        }

        public static SegmentDistance operator -( SegmentDistance a )
        {
            return new SegmentDistance( -a.value );
        }

        public static SegmentDistance operator -( SegmentDistance a, SegmentDistance b )
        {
            return new SegmentDistance( a.value - b.value );
        }

        public static SegmentDistance operator -( SegmentDistance a, int b )
        {
            return new SegmentDistance( a.value - b );
        }

        public static SegmentDistance operator -( SegmentDistance a, float b )
        {
            return new SegmentDistance( a.value - b );
        }

        public static SegmentDistance operator *( float a, SegmentDistance b )
        {
            return new SegmentDistance( a * b.value );
        }

        public static SegmentDistance operator *( SegmentDistance a, SegmentPercent b )
        {
            return new SegmentDistance( a.value * b.value );
        }

        public static SegmentDistance operator *( SegmentPercent a, SegmentDistance b )
        {
            return new SegmentDistance( a.value * b.value );
        }

        public static SegmentDistance operator *( SegmentDistance a, float b )
        {
            return new SegmentDistance( a.value * b );
        }

        public static float operator /( SegmentDistance a, SegmentDistance b )
        {
            return a.value / b.value;
        }

        public static SegmentDistance operator /( SegmentDistance a, float b )
        {
            return new SegmentDistance( a.value / b );
        }

        public static SegmentDistance operator /( SegmentDistance a, int b )
        {
            return new SegmentDistance( a.value / b );
        }

        public SegmentDistance Clamp( SegmentDistance min, SegmentDistance max )
        {
            return new SegmentDistance( Mathf.Clamp( value, min.value, max.value ) );
        }

        public SegmentDistance Clamp( SegmentDistance max )
        {
            return new SegmentDistance( Mathf.Clamp( value, 0, max.value ) );
        }

        public SegmentDistance Clamp( float min, float max )
        {
            return new SegmentDistance( Mathf.Clamp( value, min, max ) );
        }

        public static SegmentDistance Lerp( SegmentDistance a, SegmentDistance b, float t )
        {
            return new SegmentDistance( Mathf.Lerp( a.value, b.value, t ) );
        }

        public static bool Approximately( SegmentDistance a, SegmentDistance b )
        {
            return Mathf.Approximately( a.value, b.value );
        }

        public static SegmentDistance Zero
        {
            get
            {
                return new SegmentDistance( 0 );
            }
        }
    }

    [System.Serializable]
    public struct SplineDistance : System.IEquatable<SplineDistance>, System.IComparable<SplineDistance>
    {
        public float value;

        public SplineDistance( float t )
        {
            value = t;
        }

        public static explicit operator float( SplineDistance t )
        {
            return t.value;
        }

        public static explicit operator SplineDistance( float t )
        {
            return new SplineDistance( t );
        }

        public override string ToString()
        {
            return this.value.ToString();
        }

        public bool Equals( SplineDistance other )
        {
            return this.value == other.value;
        }

        public override bool Equals( object obj )
        {
            return obj is SplineDistance && this.Equals( (SplineDistance)obj );
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public int CompareTo( SplineDistance other )
        {
            return value.CompareTo( other.value );
        }

        public static bool operator ==( SplineDistance a, SplineDistance b )
        {
            return a.Equals( b );
        }

        public static bool operator !=( SplineDistance a, SplineDistance b )
        {
            return !a.Equals( b );
        }

        public static bool operator <( SplineDistance a, SplineDistance b )
        {
            return a.value < b.value;
        }

        public static bool operator >( SplineDistance a, SplineDistance b )
        {
            return a.value > b.value;
        }

        public static bool operator <=( SplineDistance a, SplineDistance b )
        {
            return a.value <= b.value;
        }

        public static bool operator >=( SplineDistance a, SplineDistance b )
        {
            return a.value >= b.value;
        }

        #region spline and segment distance comparrisons
        public static bool operator <( SplineDistance a, SegmentDistance b )
        {
            return a.value < b.value;
        }

        public static bool operator >( SplineDistance a, SegmentDistance b )
        {
            return a.value > b.value;
        }

        public static bool operator <=( SplineDistance a, SegmentDistance b )
        {
            return a.value <= b.value;
        }

        public static bool operator >=( SplineDistance a, SegmentDistance b )
        {
            return a.value >= b.value;
        }
        #endregion

        public static SplineDistance operator +( SplineDistance a, SplineDistance b )
        {
            return new SplineDistance( a.value + b.value );
        }

        public static SplineDistance operator +( SplineDistance a, SegmentDistance b )
        {
            return new SplineDistance( a.value + b.value );
        }

        public static SplineDistance operator +( SegmentDistance a, SplineDistance b )
        {
            return new SplineDistance( a.value + b.value );
        }

        public static SplineDistance operator +( SplineDistance a, int b )
        {
            return new SplineDistance( a.value + b );
        }

        public static SplineDistance operator +( SplineDistance a, float b )
        {
            return new SplineDistance( a.value + b );
        }

        public static SplineDistance operator -( SplineDistance a )
        {
            return new SplineDistance( -a.value );
        }

        public static SplineDistance operator -( SplineDistance a, SplineDistance b )
        {
            return new SplineDistance( a.value - b.value );
        }

        public static SplineDistance operator -( SplineDistance a, SegmentDistance b )
        {
            return new SplineDistance( a.value - b.value );
        }

        public static SplineDistance operator -( SegmentDistance a, SplineDistance b )
        {
            return new SplineDistance( a.value - b.value );
        }

        public static SplineDistance operator -( SplineDistance a, int b )
        {
            return new SplineDistance( a.value - b );
        }

        public static SplineDistance operator -( SplineDistance a, float b )
        {
            return new SplineDistance( a.value - b );
        }

        public static SplineDistance operator *( float a, SplineDistance b )
        {
            return new SplineDistance( a * b.value );
        }

        public static SplineDistance operator *( SplineDistance a, float b )
        {
            return new SplineDistance( a.value * b );
        }

        public static float operator /( SplineDistance a, SplineDistance b )
        {
            return a.value / b.value;
        }

        public static SplineDistance operator /( SplineDistance a, float b )
        {
            return new SplineDistance( a.value / b );
        }

        public static SplineDistance operator /( SplineDistance a, int b )
        {
            return new SplineDistance( a.value / b );
        }

        public static bool Approximately( SplineDistance a, SplineDistance b )
        {
            return Mathf.Approximately( a.value, b.value );
        }

        public static SplineDistance Lerp( SplineDistance a, SplineDistance b, float t )
        {
            return new SplineDistance( Mathf.Lerp( a.value, b.value, t ));
        }

        public SplineDistance Clamp( SplineDistance max )
        {
            return new SplineDistance( Mathf.Clamp( value, 0, max.value ) );
        }

        public SplineDistance Clamp( SplineDistance min, SplineDistance max )
        {
            return new SplineDistance( Mathf.Clamp( value, min.value, max.value ) );
        }

        public SplineDistance Clamp( float min, float max )
        {
            return new SplineDistance( Mathf.Clamp( value, min, max ) );
        }

        public static SplineDistance Zero
        {
            get
            {
                return new SplineDistance( 0 );
            }
        }
    }

    // segment percent can be used as a linear interpolation along a segment. This is different to a SegmentT because a T value is not in world space and is not linear.
    [System.Serializable]
    public struct SegmentPercent : System.IEquatable<SegmentPercent>, System.IComparable<SegmentPercent>
    {
        public float value;

        public SegmentPercent( float t )
        {
            value = t;
        }

        public static explicit operator float( SegmentPercent t )
        {
            return t.value;
        }

        public static explicit operator SegmentPercent( float t )
        {
            return new SegmentPercent( t );
        }

        public override string ToString()
        {
            return this.value.ToString();
        }

        public bool Equals( SegmentPercent other )
        {
            return this.value == other.value;
        }

        public override bool Equals( object obj )
        {
            return obj is SegmentPercent && this.Equals( (SegmentPercent)obj );
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public static bool operator ==( SegmentPercent a, SegmentPercent b )
        {
            return a.Equals( b );
        }

        public static bool operator !=( SegmentPercent a, SegmentPercent b )
        {
            return !a.Equals( b );
        }

        public static bool operator <( SegmentPercent a, SegmentPercent b )
        {
            return a.value < b.value;
        }

        public static bool operator >( SegmentPercent a, SegmentPercent b )
        {
            return a.value > b.value;
        }

        public static bool operator <=( SegmentPercent a, SegmentPercent b )
        {
            return a.value <= b.value;
        }

        public static bool operator >=( SegmentPercent a, SegmentPercent b )
        {
            return a.value >= b.value;
        }

        public int CompareTo( SegmentPercent other )
        {
            return value.CompareTo( other.value );
        }

        public SegmentPercent Clamp01()
        {
            return new SegmentPercent( Mathf.Clamp01( value ) );
        }

        public static SegmentPercent Start
        {
            get { return new SegmentPercent( 0 ); }
        }

        public static SegmentPercent End
        {
            get { return new SegmentPercent( 1 ); }
        }

        public static SegmentPercent Lerp( SegmentPercent a, SegmentPercent b, float t )
        {
            return new SegmentPercent( Mathf.Lerp( a.value, b.value, t ) );
        }

        public static bool Approximately( SegmentPercent a, SegmentPercent b )
        {
            return Mathf.Approximately( a.value, b.value );
        }
    }

    [System.Serializable]
    public struct SegmentT : System.IEquatable<SegmentT>, System.IComparable<SegmentT>
    {
        public float value;

        public SegmentT( float t )
        {
            value = t;
        }

        public static explicit operator float( SegmentT t )
        {
            return t.value;
        }

        public static explicit operator SegmentT( float t )
        {
            return new SegmentT( t );
        }

        public override string ToString()
        {
            return this.value.ToString();
        }

        public bool Equals( SegmentT other )
        {
            return this.value == other.value;
        }

        public override bool Equals( object obj )
        {
            return obj is SegmentT && this.Equals( (SegmentT)obj );
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public static bool operator ==( SegmentT a, SegmentT b )
        {
            return a.Equals( b );
        }

        public static bool operator !=( SegmentT a, SegmentT b )
        {
            return !a.Equals( b );
        }

        public static bool operator <( SegmentT a, SegmentT b )
        {
            return a.value < b.value;
        }

        public static bool operator >( SegmentT a, SegmentT b )
        {
            return a.value > b.value;
        }

        public static bool operator <=( SegmentT a, SegmentT b )
        {
            return a.value <= b.value;
        }

        public static bool operator >=( SegmentT a, SegmentT b )
        {
            return a.value >= b.value;
        }

        public static SegmentT operator +( SegmentT a, SegmentT b )
        {
            return new SegmentT( a.value + b.value );
        }

        public static SegmentT operator -( SegmentT a, SegmentT b )
        {
            return new SegmentT( a.value + b.value );
        }

        public int CompareTo( SegmentT other )
        {
            return value.CompareTo( other.value );
        }

        public SegmentT Clamp01()
        {
            return new SegmentT( Mathf.Clamp01( value ) );
        }

        public static SegmentT Lerp( SegmentT a, SegmentT b, float t )
        {
            return new SegmentT( Mathf.Lerp( a.value, b.value, t ) );
        }

        public static SegmentT Start
        {
            get { return new SegmentT( 0 ); }
        }

        public static SegmentT End
        {
            get { return new SegmentT( 1 ); }
        }

        public static bool Approximately( SegmentT a, SegmentT b )
        {
            return Mathf.Approximately( a.value, b.value );
        }
    }

    [System.Serializable]
    public struct SplinePercent : System.IEquatable<SplinePercent>, System.IComparable<SplinePercent>
    {
        public float value;

        public SplinePercent( float t )
        {
            value = t;
        }

        public static explicit operator float( SplinePercent t )
        {
            return t.value;
        }

        public static explicit operator SplinePercent( float t )
        {
            return new SplinePercent( t );
        }

        public override string ToString()
        {
            return this.value.ToString();
        }

        public bool Equals( SplinePercent other )
        {
            return this.value == other.value;
        }

        public override bool Equals( object obj )
        {
            return obj is SplinePercent && this.Equals( (SplinePercent)obj );
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public static bool operator ==( SplinePercent a, SplinePercent b )
        {
            return a.Equals( b );
        }

        public static bool operator !=( SplinePercent a, SplinePercent b )
        {
            return !a.Equals( b );
        }

        public static bool operator <( SplinePercent a, SplinePercent b )
        {
            return a.value < b.value;
        }

        public static bool operator >( SplinePercent a, SplinePercent b )
        {
            return a.value > b.value;
        }

        public static bool operator <=( SplinePercent a, SplinePercent b )
        {
            return a.value <= b.value;
        }

        public static bool operator >=( SplinePercent a, SplinePercent b )
        {
            return a.value >= b.value;
        }

        public int CompareTo( SplinePercent other )
        {
            return value.CompareTo( other.value );
        }

        public SplinePercent Clamp01()
        {
            return new SplinePercent( Mathf.Clamp01( value ) );
        }

        public SplinePercent Looped
        {
            get
            {
                return new SplinePercent( Mathf.Repeat( value, 1 ) );
            }
        }

        public static SplinePercent Start
        {
            get { return new SplinePercent( 0 ); }
        }

        public static SplinePercent Lerp( SplinePercent a, SplinePercent b, float t )
        {
            return new SplinePercent( Mathf.Lerp( a.value, b.value, t ) );
        }

        public static SplinePercent End
        {
            get { return new SplinePercent( 1 ); }
        }

        public static bool Approximately( SplinePercent a, SplinePercent b )
        {
            return Mathf.Approximately( a.value, b.value );
        }
    }

    [System.Serializable]
    public struct SplineResult
    {
        public int updateCount; // can be used to see if the spline has changed since this result
        [Range( 0, 1 )]
        public SplinePercent percent; // percentage along the spline, distance / length
        public SplineDistance length; // real world distance along spline
        public SplineDistance distance; // real world distance along spline
        public SplineDistance loopDistance; // real world distance along spline
        public bool isLoop; // true if the spline is a loop (closed)

        public Vector3 position => segmentResult.position;
        public Vector3 tangent => segmentResult.tangent;
        public Vector3 normal => segmentResult.normal;
        public Vector3 localPosition => segmentResult.localPosition;
        public Vector3 localTangent => segmentResult.localTangent;
        public Vector3 localNormal => segmentResult.localNormal;
        //public float localCurvature => segmentResult.curvature;
        //public float localRadius => segmentResult.radius;

        public int lapCount;

        public bool AtEnd => !isLoop && percent.value > 1 - SegmentResult.segmentEndAccuracy;
        public bool AtStart => !isLoop && percent.value < SegmentResult.segmentEndAccuracy;

        public Vector3 Right => CalculateRight( normal );
        public Vector3 CalculateRight( Vector3 up )
        {
            return Vector3.Cross( -tangent.normalized, up );
        }

        public SegmentResult segmentResult;

        public static SplineResult Default
        {
            get
            {
                return new SplineResult()
                {
                    segmentResult = SegmentResult.Default,
                };
            }
        }

        // transforms the result from spline component space to transform space.
        // Useful for instancing objects in multiple locations with a single spline.
        public SplineResult ConvertTransform(Transform originalTransform, Transform newTransform)
        {
            SplineResult result = this;
            result.segmentResult.position = newTransform.TransformPoint( originalTransform.InverseTransformPoint( position ) );
            result.segmentResult.tangent = newTransform.TransformVector( originalTransform.InverseTransformVector( tangent ) );
            result.segmentResult.normal = newTransform.TransformVector( originalTransform.InverseTransformVector( normal ) );
            return result;
        }
    }

    [System.Serializable]
    public struct SplineNodeResult
    {
        public SplineResult splineResult;

        public SplineNode splineNode;
        public int nodeIndex;
        public int loopNodeIndex;
        public Vector3 inTangent;
        public Vector3 outTangent;

        public static SplineNodeResult Default
        {
            get
            {
                return new SplineNodeResult()
                {
                    splineResult = SplineResult.Default,
                    nodeIndex = 0,
                    inTangent = Vector3.forward,
                    outTangent = Vector3.forward,
                    splineNode = new SplineNode(),
                };
            }
        }
    }

    // Functions used to update objects on a spline when a spline changes.
    public static class SplineChangedEventHelper
    {
        public static SplineResult OnNodeAdded( SplineResult input, ISpline spline, int nodeIndex, RepositionMode mode = RepositionMode.SplineDistance )
        {
            if( mode == RepositionMode.SegmentDistance
                || mode == RepositionMode.SegmentT
                || mode == RepositionMode.ClosestPointOnSegment
                || mode == RepositionMode.SegmentPercent )
            {
                if( input.segmentResult.index == nodeIndex-1 )
                {
                    // our segment has been chopped in half! We need to re-calculate where we are on the segment.
                    SplineResult firstSegment = spline.GetResultAtSegment( nodeIndex-1, input.segmentResult.percent );
                    if( input.segmentResult.distance < firstSegment.segmentResult.length )
                    {
                        input = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.distance );
                    }
                    else
                    {
                        input.segmentResult.index++;
                        input.segmentResult.distance -= firstSegment.segmentResult.length;
                        input = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.distance );
                    }
                }
                else if( input.segmentResult.index > nodeIndex-1 )
                {
                    // a new segment added before us, shuffle along.
                    input.segmentResult.index++;
                    input = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.distance );
                }
            }

            return OnSplineLengthChanged( input, spline, mode );
        }

        public static SplineResult OnNodeRemoved( SplineResult input, ISpline spline, int nodeIndex, RepositionMode mode = RepositionMode.SplineDistance )
        {
            if( mode == RepositionMode.SegmentDistance
                || mode == RepositionMode.SegmentT
                || mode == RepositionMode.ClosestPointOnSegment
                || mode == RepositionMode.SegmentPercent )
            {
                if( nodeIndex == input.segmentResult.index && nodeIndex == 0 )
                {
                    // first segment removed.
                    if( spline.IsLoop )
                    {
                        // need to handle loop case here where we merge with the last(loop) segment.
                        SplineResult newSegment = spline.GetResultAtSegment( spline.SegmentCount, new SegmentPercent(0.5f) );

                        input.segmentResult.index = newSegment.segmentResult.index;
                        input.segmentResult.distance = newSegment.segmentResult.length - input.segmentResult.length + input.segmentResult.distance;

                        input = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.distance );
                    }
                    else
                    {
                        input.segmentResult.distance = -input.segmentResult.length + input.segmentResult.distance; // negative distance.
                        input = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.distance );
                    }
                }
                else if( nodeIndex == input.segmentResult.index )
                {
                    // merge with previous segment.
                    SplineResult newSegment = spline.GetResultAtSegment( nodeIndex-1, new SegmentPercent( 0.5f ) );

                    input.segmentResult.index = newSegment.segmentResult.index;
                    input.segmentResult.distance = newSegment.segmentResult.length - input.segmentResult.length + input.segmentResult.distance;

                    input = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.distance );
                }
                else if( nodeIndex - 1 == input.segmentResult.index )
                {
                    // merge with next segment. which is still our index and distance so do nothing
                    input = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.distance );
                }
                else if( nodeIndex < input.segmentResult.index )
                {
                    // segment before us was removed, shuffle down
                    input = spline.GetResultAtSegment( input.segmentResult.index-1, input.segmentResult.distance );
                }
                // else, segment was after us, we don't need to do anyhing.
            }

            return OnSplineLengthChanged( input, spline, mode );
        }

        public static SplineResult OnSplineLengthChanged( SplineResult input, ISpline spline, RepositionMode mode = RepositionMode.SplineDistance )
        {
            SplineResult result;

            // stop endless loop created by custom normals on SplineResults.
            spline.repositioningKeyframe = true;

            // internally the spline uses distance lookup for almost everything which means using t values can be a little inaccurate.
            // to compensate we set the t values back to what they were on input for t lookup modes.
            switch( mode )
            {
                case RepositionMode.SplineDistance:
                    result = spline.GetResultAt( input.distance );
                    break;
                case RepositionMode.SplinePercent:
                    result = spline.GetResultAt( input.percent );
                    result.percent = input.percent;
                    break;
                case RepositionMode.SegmentDistance:
                    result = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.distance );
                    break;
                case RepositionMode.SegmentT:
                    result = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.t );
                    result.segmentResult.t = input.segmentResult.t;
                    break;
                case RepositionMode.ClosestPointOnSpline:
                    result = spline.GetResultClosestTo( input.position );
                    break;
                case RepositionMode.ClosestPointOnSegment:
                    result = spline.GetResultClosestToSegment( input.segmentResult.index, input.position );
                    break;
                case RepositionMode.SegmentPercent:
                    result = spline.GetResultAtSegment( input.segmentResult.index, input.segmentResult.percent );
                    break;
                default:
                    result = spline.GetResultAt( input.distance );
                    break;
            }

            spline.repositioningKeyframe = false;
            return result;
        }
    }

    // Proxy interface to enable tools to easily use the spline editor in their own editors
    public interface IEditorSplineProxy
    {
        IEditableSpline GetEditableSpline();
        Object[] GetUndoObjects( );
    }

    // Interface for SplineEditor. Any component that implements this can be used with the editor
    public interface IEditableSpline : IEditorSplineProxy
    {
        Transform Transform { get; }
        Component Component { get; }
        bool IsLoop { get; set; }
        int NodeCount { get; }
        SplineDistance Length { get; }

        int LoopIndex( int index );
        SplineNode GetNode(int index);
        void SetNode(int index, SplineNode node);
        void AppendNode(SplineNode node); // adds the given CurvePoint to the end of the curve
        void PrependNode(SplineNode node); // adds the given CurvePoint to the start of the curve
        void InsertNode(SplineNode node, int index); // inserts a node at node index
        int InsertNode(SplinePercent t); // inserts a point on the curve without changing its shape and return it
        void RemoveNode(int index); // removes a Curve Point at index

        SplineResult GetResultAtNode( int index );
        SplineResult GetResultAt( SplineDistance distance );
        SplineResult GetResultAt( SplinePercent t );
        SplineResult GetResultClosestTo(Vector3 point);
        SplineResult GetResultClosestTo(Ray ray);

        // gizmo options
        void DrawSegmentLengths();
        void DrawNodeCoordinates( Space space );
        public Color color { get; set; }
        public bool zTest { get; set; }
        public float gizmoScale { get; set; }
        public bool alwaysDraw { get; set; }
        public bool showDefaultNormals { get; set; }
    }

    // This is just a curve in 3D space
    // No rotations, no colours, no normals
    public interface ISpline
    {
        Transform Transform { get; }
        bool IsLoop { get; set; }
        int UpdateCount { get; }
        int NodeCount { get; }
        int SegmentCount { get; }
        SplineDistance Length { get; }
        SplineNode GetNode( int index );
        SplineResult GetResultAt(SplinePercent splinePercent);
        SplineResult GetResultAt(SplineDistance splineDistance);
        SplineResult GetResultAtSegment( int segmentIndex, SegmentPercent segmentPercent );
        SplineResult GetResultAtSegment(int segmentIndex, SegmentT segmentT );
        SplineResult GetResultAtSegment(int segmentIndex, SegmentDistance segmentDistance );
        SplineResult GetResultClosestTo(Vector3 point);
        SplineResult GetResultClosestTo(Ray ray);
        SplineResult GetResultClosestToSegment( int segmentIndex, Vector3 point );
        SplineResult GetResultAtWorldDistanceFrom( SplineDistance startDistance, float worldDistance, SplineDistance stepDistance );
        SplineResult GetResultAtNode( int nodeIndex );
        SplineNodeResult GetNodeResult( int nodeIndex );
        int LoopIndex(int index);
        bool repositioningKeyframe { get; set; }
    }

    // Interface for added additional data to a place
    public interface ISplineParameter<T>
    {
        T GetValueAt( SplinePercent t, T defaultValue );
        T GetValueAt( SplineDistance d, T defaultValue );
    }
}