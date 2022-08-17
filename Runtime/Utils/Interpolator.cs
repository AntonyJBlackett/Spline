using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2021, New Zealand

/// <summary>
/// Contains a bunch of default interpolation functions for various types and a dynamic dispatcher to use them.
/// </summary>
namespace FantasticSplines
{
    public static class Interpolator
    {
        public static object Interpolate( object a, object b, float t )
        {
            if( a is float )
            {
                return InterpolateFloat( (float)a, (float)b, t );
            }
            if( a is Vector2 )
            {
                return InterpolateVector2( (Vector2)a, (Vector2)b, t );
            }
            if( a is Vector3 )
            {
                return InterpolateVector3( (Vector3)a, (Vector3)b, t );
            }
            if( a is Vector4 )
            {
                return InterpolateVector4( (Vector4)a, (Vector4)b, t );
            }
            if( a is Color )
            {
                return InterpolateColor( (Color)a, (Color)b, t );
            }
            if( a is Quaternion )
            {
                return InterpolateQuaternion( (Quaternion)a, (Quaternion)b, t );
            }

            Debug.LogWarning( "No interpolator defined for class type: " + a.GetType().ToString() );
            return a;
        }

        private static float InterpolateFloat( float a, float b, float t )
        {
            return Mathf.Lerp( a, b, t );
        }

        private static Vector2 InterpolateVector2( Vector2 a, Vector2 b, float t )
        {
            return Vector2.Lerp( a, b, t );
        }

        private static Vector3 InterpolateVector3( Vector3 a, Vector3 b, float t )
        {
            return Vector3.Lerp( a, b, t );
        }

        private static Vector4 InterpolateVector4( Vector4 a, Vector4 b, float t )
        {
            return Vector4.Lerp( a, b, t );
        }

        private static Color InterpolateColor( Color a, Color b, float t )
        {
            return Color.Lerp( a, b, t );
        }

        private static Quaternion InterpolateQuaternion( Quaternion a, Quaternion b, float t )
        {
            return Quaternion.Slerp( a, b, t );
        }
    }
}