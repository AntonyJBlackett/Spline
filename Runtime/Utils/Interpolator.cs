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
        public static dynamic Interpolate( dynamic a, dynamic b, float t )
        {
            // Dynamically dispatches to the right overload
            return InterpolateImpl( a, b, t );
        }

        private static T InterpolateImpl<T>( T a, T b, float t )
        {
            Debug.LogWarning( "No interpolator defined for class type: " + typeof( T ).ToString() );
            return a;
        }

        private static float InterpolateImpl( float a, float b, float t )
        {
            return Mathf.Lerp( a, b, t );
        }

        private static Vector2 InterpolateImpl( Vector2 a, Vector2 b, float t )
        {
            return Vector2.Lerp( a, b, t );
        }

        private static Vector3 InterpolateImpl( Vector3 a, Vector3 b, float t )
        {
            return Vector3.Lerp( a, b, t );
        }

        private static Vector4 InterpolateImpl( Vector4 a, Vector4 b, float t )
        {
            return Vector4.Lerp( a, b, t );
        }

        private static Color InterpolateImpl( Color a, Color b, float t )
        {
            return Color.Lerp( a, b, t );
        }

        private static Quaternion InterpolateImpl( Quaternion a, Quaternion b, float t )
        {
            return Quaternion.Slerp( a, b, t );
        }
    }
}