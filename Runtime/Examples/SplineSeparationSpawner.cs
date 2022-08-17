using System;
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
        public bool retransformResult;

        public SplineDistance separationDistance => new SplineDistance(separation);

        public static SpawnerParameters Default => new SpawnerParameters() { separation = 1 };

        public bool Equals(SpawnerParameters other)
        {
            return spline == other.spline
                   && prefab == other.prefab
                   && Mathf.Approximately(separation, other.separation)
                   && separationMethod == other.separationMethod
                   && retransformResult == other.retransformResult;
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

    [SerializeField][HideInInspector] SplineSnapshot splineSnapshot;
    [SerializeField][HideInInspector] SpawnerParameters lastParameters = SpawnerParameters.Default;

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

    private void OnEnable()
    {
        instanceBucket = GetComponentInChildren<PrefabInstanceBucket>();
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
        if (!autoRegenerate)
        {
            return;
        }

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
        bool warn = parameters.IsDifferentFrom(lastParameters) || splineSnapshot.IsDifferentFrom(parameters.spline);
        
        if( parameters.spline == null )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "No spline is set.", gameObject );
        }

        if( parameters.separation <= 0.0001f )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "Separation needs to be greater than 0.", gameObject );
        }

        if( parameters.prefab == null )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "No prefab set.", gameObject );
        }

        if( parameters.spline.Length.value < 0.001f )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "Spline has no length.", gameObject );
        }

        lastParameters = parameters;
        splineSnapshot = new SplineSnapshot( parameters.spline );
        
        if( escape )
        {
            return;
        }

        SplineResult splineResult = parameters.spline.GetResultAt( SplinePercent.Start );

        while( !splineResult.AtEnd )
        {
            GameObject instance = instanceBucket.GetInstance( parameters.prefab );
            instance.SetActive( true );

            if (parameters.retransformResult)
            {
                splineResult = splineResult.ConvertTransform(parameters.spline.transform, transform);
            }

            instance.transform.position = splineResult.position;
            instance.transform.rotation = Quaternion.LookRotation( splineResult.tangent, Vector3.up );

            SplineResult newResult;
            switch( parameters.separationMethod )
            {
                case SeparationMethod.SplineDistance:
                    newResult = parameters.spline.GetResultAt( splineResult.distance + parameters.separationDistance );
                    break;
                case SeparationMethod.WorldDistance:
                    newResult = parameters.spline.GetResultAtWorldDistanceFrom( splineResult.distance, parameters.separation, parameters.separationDistance * 0.33f );
                    break;
                default:
                    return;
            }

            if( SplineDistance.Approximately( newResult.distance, splineResult.distance ) )
            {
                // we're stuck for some reason.
                Debug.LogError( "Spline Separation Spawner could not complete spawning. For some reason we got stuck trying to step along the spline." );
                break;
            }

            splineResult = newResult;
        }

        instanceBucket.CleanUpUnusedInstances();
    }
}
