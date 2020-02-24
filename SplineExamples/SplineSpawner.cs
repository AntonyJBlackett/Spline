using UnityEngine;
using FantasticSplines;

[ExecuteInEditMode]
public class SplineSpawner : MonoBehaviour
{
    public SplineComponent spline;
    public GameObject prefab;
    public float separation = 1;
    public enum SeparationMethod
    {
        WorldDistance,
        SplineDistance,
    }
    public SeparationMethod separationMethod = SeparationMethod.SplineDistance;

    public bool clear = false;
    public bool regenerate = false;
    public bool autoRegenerate = false;

    SplineChangeDetector changeDetector;

    void Clear()
    {
        clear = false;
        int count = transform.childCount;
        for( int i = 0; i < count; ++i )
        {
            DestroyImmediate( transform.GetChild( 0 ).gameObject );
        }
    }

    private void OnDrawGizmos()
    {
        Update();
    }

    void Update()
    {
        if( regenerate )
        {
            Regenerate();
        }

        if( clear )
        {
            Clear();
        }

        if( changeDetector.IsDifferentFrom( spline ) )
        {
            AutoRegenerate();
        }
    }

    void AutoRegenerate()
    {
        if( autoRegenerate )
        {
            Regenerate();
        }
    }

    void Regenerate()
    {
        regenerate = false;
        Clear();

        bool escape = false;
        if( spline == null )
        {
            escape = true;
            Debug.LogWarning( "No spline is set.", gameObject );
        }

        if( separation <= 0 )
        {
            escape = true;
            Debug.LogWarning( "Separation needs to be greater than 0.", gameObject );
        }

        if( prefab == null )
        {
            escape = true;
            Debug.LogWarning( "No prefab set.", gameObject );
        }

        if( escape )
        {
            return;
        }

        SplineResult splineResult = spline.GetResultAtT( 0 );
        changeDetector = new SplineChangeDetector( spline );

        while( splineResult.t < 1 )
        {
            GameObject instance = Instantiate( prefab, transform );
            instance.SetActive( true );
            instance.transform.position = splineResult.position;
            instance.transform.rotation = Quaternion.LookRotation( splineResult.tangent, Vector3.up );

            switch( separationMethod )
            {
                case SeparationMethod.SplineDistance:
                    splineResult = spline.GetResultAtDistance( splineResult.distance + separation );
                    break;
                case SeparationMethod.WorldDistance:
                    splineResult = spline.GetResultAtWorldDistanceFrom( splineResult.distance, separation, separation * 0.33f );
                    break;
                default:
                    return;
            }
        }
    }
}
