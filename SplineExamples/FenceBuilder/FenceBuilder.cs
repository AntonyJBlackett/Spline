using UnityEngine;
using FantasticSplines;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FenceBuilder : MonoBehaviour, IEditorSplineProxy
{
    // can now use the spline editor when this object is selected
    public IEditableSpline GetEditableSpline() { return spline; }
    public Object GetUndoObject() { return spline; }

    public SplineComponent spline;
    public GameObject post;
    public GameObject segment;
    float separation = 1;
    public bool clear = false;
    public bool regenerate = false;
    public bool autoRegenerate = false;

    //SplineComponent lastSpline = null;

    [SerializeField]
    Transform instanceBucket;
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

        //if( lastSpline != spline )
        {
            AutoRegenerate();
        }
    }

    void OnDrawGizmos()
    {
        Update();
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
        DeactivateInstances();

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

        if( segment == null )
        {
            escape = true;
            Debug.LogWarning( "No prefab set.", gameObject );
        }

        if( escape )
        {
            return;
        }

        if( instanceBucket == null )
        {
            instanceBucket = new GameObject( gameObject.name + "-InstanceBucket" ).transform;
            instanceBucket.SetParent( transform );
        }

        SplineResult post1Position = spline.GetResultAtDistance( 0 );
        separation = segment.transform.localScale.z;
        float step = separation * 0.5f;

        SplineResult post2Position = spline.GetResultAtWorldDistanceFrom( post1Position.splineDistance, separation, step );

        // first segment
        if( post != null )
        {
            GameObject postInstance = GetInstance( post, instanceBucket );
            postInstance.SetActive( true );
            postInstance.transform.position = post1Position.position;
            postInstance.transform.rotation = Quaternion.LookRotation( post1Position.tangent, Vector3.up );
        }

        float splineLength = spline.GetLength();

        int limit = Mathf.CeilToInt( 1 + splineLength / separation ); // we should never need more segments than a dead straight spline needs
        while( !post1Position.AtEnd )
        {
            GameObject segmentInstance = GetInstance( segment, instanceBucket );
            segmentInstance.SetActive( true );

            Vector3 segmentDirection = (post2Position.position - post1Position.position).normalized;
            Vector3 segmentPosition = post1Position.position + segmentDirection * separation * 0.5f;

            segmentInstance.transform.position = segmentPosition;
            segmentInstance.transform.rotation = Quaternion.LookRotation( segmentDirection, Vector3.up );

            if( post != null )
            {
                Vector3 nextPostPosition = post1Position.position + segmentDirection * separation;
                GameObject postInstance = GetInstance( post, instanceBucket );
                postInstance.SetActive( true );
                postInstance.transform.position = nextPostPosition;
                postInstance.transform.rotation = Quaternion.LookRotation( post2Position.tangent, Vector3.up );
            }

            post1Position = post2Position;
            post2Position = spline.GetResultAtWorldDistanceFrom( post2Position.splineDistance, separation, step );

            --limit;
            if( limit < 0 )
            {
                Debug.LogWarning( "Segment limit reached" );
                break;
            }
        }

        CleanUpUnusedInstances();
    }

    void Clear()
    {
        clear = false;
        if( instanceBucket == null )
        {
            return;
        }
        int count = instanceBucket.childCount;
        for( int i = 0; i < count; ++i )
        {
            DestroyImmediate( instanceBucket.GetChild( 0 ).gameObject );
        }
    }

    void CleanUpUnusedInstances()
    {
        if( instanceBucket == null )
        {
            return;
        }
        int count = instanceBucket.childCount;
        for( int i = count - 1; i >= 0; --i )
        {
            GameObject child = instanceBucket.GetChild( i ).gameObject;
            if( !child.activeSelf )
            {
                DestroyImmediate( child );
            }
        }
    }

    void DeactivateInstances()
    {
        if( instanceBucket == null )
        {
            return;
        }
        int count = instanceBucket.childCount;
        for( int i = 0; i < count; ++i )
        {
            instanceBucket.GetChild( i ).gameObject.SetActive( false );
        }
    }

    GameObject FindUnusedInstanceInPool(GameObject prefabOrGameObject)
    {
#if UNITY_EDITOR
        if( instanceBucket == null )
        {
            return null;
        }

        if( !PrefabUtility.IsPartOfAnyPrefab( prefabOrGameObject ) )
        {
            return null;
        }

        int count = instanceBucket.childCount;
        for( int i = 0; i < count; ++i )
        {
            GameObject instance = instanceBucket.GetChild( i ).transform.gameObject;
            if( instance.activeSelf )
            {
                continue;
            }
            if( PrefabUtility.GetPrefabInstanceStatus( instance ) != PrefabInstanceStatus.Connected )
            {
                continue;
            }

            if( PrefabUtility.GetCorrespondingObjectFromSource( instance ) == prefabOrGameObject )
            {
                return instance;
            }
        }
#endif
        return null;
    }

    GameObject GetInstance(GameObject prefabOrGameObject, Transform parent)
    {
        if( prefabOrGameObject == null )
        {
            Debug.LogWarning( "Trying to instantiate null object." );
            return null;
        }

        GameObject instance = FindUnusedInstanceInPool( prefabOrGameObject );
        if( instance != null )
        {
            instance.SetActive( true );
            instance.transform.SetSiblingIndex( instance.transform.parent.childCount - 1 );
        }

        if( instance == null )
        {
            return InstantiatePrefabOrGameObject( prefabOrGameObject, parent );
        }

        return instance;
    }

    static T InstantiatePrefabOrGameObject<T>(T prefabOrGameObject, Transform parent) where T : Component
    {
        if( prefabOrGameObject == null )
        {
            Debug.LogWarning( "Trying to instantiate null object." );
            return null;
        }
        return InstantiatePrefabOrGameObject( prefabOrGameObject.gameObject, parent ).GetComponent<T>();
    }

    static GameObject InstantiatePrefabOrGameObject(GameObject prefabOrGameObject, Transform parent)
    {
        if( prefabOrGameObject == null )
        {
            Debug.LogWarning( "Trying to instantiate null object." );
            return null;
        }

#if UNITY_EDITOR
        if( PrefabUtility.IsPartOfAnyPrefab( prefabOrGameObject ) )
        {
            return PrefabUtility.InstantiatePrefab( prefabOrGameObject, parent ) as GameObject;
        }
#endif

        return Instantiate( prefabOrGameObject, parent );
    }
}