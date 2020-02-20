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
        SplineDistance
    }
    public SeparationMethod separationMethod = SeparationMethod.SplineDistance;

    public bool clear = false;
    public bool regenerate = false;
    public bool autoRegenerate = false;
    
    SplineComponent lastSpline = null;
    void OnEnable()
    {
        // this isn't going to work... hmm
        /*if( spline != null )
        {
            spline.onUpdated += Regenerate;
        }*/
    }

    void OnDisable()
    {
        // this isn't going to work... hmm
        /*if( spline != null )
        {
            spline.onUpdated -= Regenerate;
        }*/
    }

    void Clear()
    {
        clear = false;
        int count = transform.childCount;
        for( int i = 0; i < count; ++i )
        {
            DestroyImmediate( transform.GetChild( 0 ).gameObject );
        }
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

        if( lastSpline != spline )
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

        SplinePosition splinePosition = new SplinePosition( spline, 0 );

        bool spawning = true;
        while( spawning )
        {
            GameObject instance = Instantiate( prefab, transform );
            instance.transform.position = splinePosition.Position;
            instance.transform.rotation = Quaternion.LookRotation( splinePosition.Tangent, Vector3.up );

            spawning = splinePosition.DistanceOnSpline < spline.GetLength();

            switch( separationMethod )
            {
                case SeparationMethod.SplineDistance:
                    splinePosition = splinePosition.Move( separation );
                break;
            case SeparationMethod.WorldDistance:
                    splinePosition = splinePosition.MoveUntilAtWorldDistance( separation, separation * 0.5f );
                break;
            }
        }
    }
}
