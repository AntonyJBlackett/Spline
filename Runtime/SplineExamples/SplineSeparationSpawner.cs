using UnityEngine;
using FantasticSplines;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

[ExecuteInEditMode]
public class SplineSeparationSpawner : MonoBehaviour
{
    public enum SeparationMethod
    {
        WorldDistance,
        SplineDistance,
    }

    [System.Serializable]
    public struct SpawnerParameters
    {
        public SplineComponent spline;
        public GameObject prefab;
        public float separation;
        public SeparationMethod separationMethod;

        public static SpawnerParameters Default => new SpawnerParameters() { separation = 1 };

        public bool Equals(SpawnerParameters other)
        {
            return spline == other.spline
                && prefab == other.prefab
                && Mathf.Approximately( separation, other.separation )
                && separationMethod == other.separationMethod;
        }

        public bool IsDifferentFrom(SpawnerParameters other)
        {
            return !Equals( other );
        }

        public SpawnerParameters Constrain()
        {
            SpawnerParameters result = this;
            if( result.separation <= 0 )
            {
                result.separation = 0;
            }
            return result;
        }
    }

    public SpawnerParameters parameters = SpawnerParameters.Default;

    public bool clear = false;
    public bool regenerate = false;
    public bool autoRegenerate = false;

    SplineSnapshot splineSnapshot;
    SpawnerParameters lastParameters = SpawnerParameters.Default;
    SpawnerParameters warningParameters = SpawnerParameters.Default;

    [HideInInspector]
    [SerializeField]
    PrefabInstanceBucket instanceBucket;

    void Clear()
    {
        clear = false;
        instanceBucket.Clear();
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

        AutoRegenerate();
    }

    void AutoRegenerate()
    {
        if( autoRegenerate )
        {
            if( parameters.spline == null )
            {
                Clear();
                return;
            }
            if( parameters.IsDifferentFrom( lastParameters ) || splineSnapshot.IsDifferentFrom( parameters.spline ) )
            {
                Regenerate();
            }
        }
    }

    void Regenerate()
    {
        if( instanceBucket == null )
        {
            instanceBucket = PrefabInstanceBucket.Instantiate( transform );
        }
        instanceBucket.DeactivateInstances();

        parameters = parameters.Constrain();
        regenerate = false;

        bool escape = false;
        bool warn = warningParameters.IsDifferentFrom( parameters );
        if( parameters.spline == null )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "No spline is set.", gameObject );
        }

        if( parameters.separation <= 0 )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "Separation needs to be greater than 0.", gameObject );
        }

        if( parameters.prefab == null )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "No prefab set.", gameObject );
        }

        warningParameters = parameters;
        if( escape )
        {
            return;
        }

        lastParameters = parameters;

        SplineResult splineResult = parameters.spline.GetResultAtT( 0 );
        splineSnapshot = new SplineSnapshot( parameters.spline );

        while( splineResult.t < 1 )
        {
            GameObject instance = instanceBucket.GetInstance( parameters.prefab );
            instance.SetActive( true );

            splineResult = splineResult.ConvertTransform( parameters.spline.transform, transform );

            instance.transform.position = splineResult.position;
            instance.transform.rotation = Quaternion.LookRotation( splineResult.tangent, Vector3.up );

            switch( parameters.separationMethod )
            {
                case SeparationMethod.SplineDistance:
                    splineResult = parameters.spline.GetResultAtDistance( splineResult.distance + parameters.separation );
                    break;
                case SeparationMethod.WorldDistance:
                    splineResult = parameters.spline.GetResultAtWorldDistanceFrom( splineResult.distance, parameters.separation, parameters.separation * 0.33f );
                    break;
                default:
                    return;
            }
        }

        instanceBucket.CleanUpUnusedInstances();
    }
}
