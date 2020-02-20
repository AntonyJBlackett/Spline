using UnityEngine;
using FantasticSplines;

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

        if( segment == null )
        {
            escape = true;
            Debug.LogWarning( "No prefab set.", gameObject );
        }

        if( escape )
        {
            return;
        }

        SplinePosition post1Position = new SplinePosition( spline, 0 );
        separation = segment.transform.localScale.z;
        float step = separation * 0.2f;

        SplinePosition post2Position = post1Position.MoveUntilAtWorldDistance( separation, step );
        
        // first segment
        if( post != null )
        {
            GameObject postInstance = Instantiate( post, transform );
            postInstance.SetActive( true );
            postInstance.transform.position = post1Position.Position;
            postInstance.transform.rotation = Quaternion.LookRotation( post1Position.Tangent, Vector3.up );
        }

        while( post1Position.DistanceOnSpline < spline.GetLength() )
        {

            GameObject segmentInstance = Instantiate( segment, transform );
            segmentInstance.SetActive( true );

            Vector3 segmentDirection = (post2Position.Position - post1Position.Position).normalized;
            Vector3 segmentPosition = post1Position.Position + segmentDirection * separation * 0.5f;

            segmentInstance.transform.position = segmentPosition;
            segmentInstance.transform.rotation = Quaternion.LookRotation( segmentDirection, Vector3.up );
            
            if( post != null )
            {
                Vector3 nextPostPosition = post1Position.Position + segmentDirection * separation;
                GameObject postInstance = Instantiate( post, transform );
                postInstance.SetActive( true );
                postInstance.transform.position = nextPostPosition;
                postInstance.transform.rotation = Quaternion.LookRotation( post2Position.Tangent, Vector3.up );
            }

            post1Position = post2Position;
            post2Position = post2Position.MoveUntilAtWorldDistance( separation, step );
        }
    }
}
