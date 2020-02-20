using UnityEngine;
using FantasticSplines;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FenceBuilder : MonoBehaviour
{
    public SplineComponent spline;
    public GameObject post;
    public GameObject segment;
    float separation = 1;
    public bool clear = false;
    public bool regenerate = false;
    public bool autoRegenerate = false;
    
    SplineComponent lastSpline = null;

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
            ResetLocalTransform( instanceBucket );
        }

        SplinePosition post1Position = new SplinePosition( spline, 0 );
        separation = segment.transform.localScale.z;
        float step = separation * 0.2f;

        SplinePosition post2Position = post1Position.MoveUntilAtWorldDistance( separation, step );
        
        // first segment
        if( post != null )
        {
            GameObject postInstance = InstantiatePrefabOrGameObject( post, instanceBucket );
            postInstance.SetActive( true );
            postInstance.transform.position = post1Position.Position;
            postInstance.transform.rotation = Quaternion.LookRotation( post1Position.Tangent, Vector3.up );
        }

        int limit = Mathf.CeilToInt(spline.GetLength() / separation); // we should never need more segments than a dead straight spline needs
        while( post1Position.DistanceOnSpline < spline.GetLength() || Mathf.Abs(post1Position.DistanceOnSpline - post2Position.DistanceOnSpline) <= float.Epsilon )
        {
            GameObject segmentInstance = InstantiatePrefabOrGameObject( segment, instanceBucket );
            segmentInstance.SetActive( true );

            Vector3 segmentDirection = (post2Position.Position - post1Position.Position).normalized;
            Vector3 segmentPosition = post1Position.Position + segmentDirection * separation * 0.5f;

            segmentInstance.transform.position = segmentPosition;
            segmentInstance.transform.rotation = Quaternion.LookRotation( segmentDirection, Vector3.up );
            
            if( post != null )
            {
                Vector3 nextPostPosition = post1Position.Position + segmentDirection * separation;
                GameObject postInstance = InstantiatePrefabOrGameObject( post, instanceBucket );
                postInstance.SetActive( true );
                postInstance.transform.position = nextPostPosition;
                postInstance.transform.rotation = Quaternion.LookRotation( post2Position.Tangent, Vector3.up );
            }

            post1Position = post2Position;
            post2Position = post2Position.MoveUntilAtWorldDistance( separation, step );

            --limit;
            if( limit < 0 )
            {
                Debug.LogWarning( "Segment limit reached" );
                break;
            }
        }
    }

    static void ResetLocalTransform(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
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

    void DeactivateInstances()
    {
        if( instanceBucket == null )
        {
            return;
        }
        int count = instanceBucket.childCount;
        for( int i = 0; i < count; ++i )
        {
            instanceBucket.GetChild( 0 ).gameObject.SetActive(false);
        }
    }

    GameObject FindUnusedInstanceInPool( GameObject prefabOrGameObject )
    {
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
            GameObject instance = instanceBucket.GetChild( 0 ).transform.gameObject;
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

        return null;
    }

    GameObject GetInstance( GameObject prefabOrGameObject, Transform parent )
    {
        if( prefabOrGameObject == null )
        {
            Debug.LogWarning( "Trying to instantiate null object." );
            return null;
        }

        GameObject instance = FindUnusedInstanceInPool( prefabOrGameObject );
        if( instance != null )
        {
            ResetLocalTransform( instance.transform );
            instance.SetActive( true );
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