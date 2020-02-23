using UnityEngine;
using System.Collections.Generic;

namespace FantasticSplines 
{
    [System.Serializable]
    public struct FloatKey
    {
        public float d;
        public float value;
    }

    public class SplineFloat : MonoBehaviour
    {
        public List<FloatKey> keys = new List<FloatKey>();
        public SplineBehaviour spline;

        public float TestValueT = 0;
        public float TestResult = 0;

        private void OnDrawGizmos()
        {
            if( spline == null )
            {
                return;
            }

            for( int i = 0; i < keys.Count; ++i )
            {
                Gizmos.DrawCube( spline.GetPositionAtDistance( keys[i].d ), Vector3.one * 0.5f );
            }

            Gizmos.color = Color.red;
            Gizmos.DrawCube( spline.GetPositionAtDistance( TestValueT * spline.GetLength() ), Vector3.one * 0.3f );
            Gizmos.color = Color.white;

            TestResult = GetValueAt( TestValueT * spline.GetLength() );
        }

        public float GetValueAt( float d )
        {
            if( keys.Count == 0 )
            {
                return 0;
            }
            if( keys.Count == 1 )
            {
                return keys[0].value;
            }

            FloatKey key1;
            FloatKey key2;

            GetKeysEitherSideOfDistance( d, out key1, out key2 );

            return MathHelper.Remap( d, key1.d, key2.d, key1.value, key2.value );
        }

        void GetKeysEitherSideOfDistance( float d, out FloatKey key1, out FloatKey key2 )
        {
            key1 = keys[0];
            key2 = keys[0];

            for( int i = 0; i < keys.Count; ++i )
            {
                if( d < keys[i].d )
                {
                    key1 = keys[i];
                    key2 = keys[i];
                    if( i > 0 )
                    {
                        key1 = keys[i-1];
                    }
                    return;
                }
            }
            
            key1 = keys[keys.Count-1];
            key2 = keys[keys.Count-1];
        }
    }
}
