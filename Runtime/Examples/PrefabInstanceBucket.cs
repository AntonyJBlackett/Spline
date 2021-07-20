using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

public class PrefabInstanceBucket : MonoBehaviour
{
    public static PrefabInstanceBucket Instantiate(Transform parent)
    {
        PrefabInstanceBucket instance = new GameObject( parent.name + "-InstanceBucket" ).AddComponent<PrefabInstanceBucket>();
        instance.transform.SetParent( parent );
        instance.transform.position = Vector3.zero;
        return instance;
    }

    public bool IsClear()
    {
        return transform.childCount == 0;
    }

    public void Clear()
    {
        int count = transform.childCount;
        for( int i = 0; i < count; ++i )
        {
            DestroyInstance( transform.GetChild( 0 ).gameObject );
        }
    }

    public void CleanUpUnusedInstances()
    {
        int count = transform.childCount;
        for( int i = count - 1; i >= 0; --i )
        {
            GameObject child = transform.GetChild( i ).gameObject;
            if( !child.activeSelf )
            {
                DestroyInstance( child.gameObject );
            }
        }
    }

    public void DeactivateInstances()
    {
        int count = transform.childCount;
        for( int i = 0; i < count; ++i )
        {
            transform.GetChild( i ).gameObject.SetActive( false );
        }
    }

    public GameObject GetInstance(GameObject prefabOrGameObject)
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
            return InstantiatePrefabOrGameObject( prefabOrGameObject, transform );
        }

        instance.SetActive(prefabOrGameObject.activeSelf);
        return instance;
    }

    GameObject FindUnusedInstanceInPool(GameObject prefabOrGameObject)
    {
#if UNITY_EDITOR
        if( !PrefabUtility.IsPartOfAnyPrefab( prefabOrGameObject ) )
        {
            return null;
        }

        int count = transform.childCount;
        for( int i = 0; i < count; ++i )
        {
            GameObject instance = transform.GetChild( i ).transform.gameObject;
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

    static void DestroyInstance(GameObject destroyMe)
    {
        if( Application.isPlaying )
        {
            Destroy( destroyMe.gameObject );
        }
        else
        {
            DestroyImmediate( destroyMe.gameObject );
        }
    }
}