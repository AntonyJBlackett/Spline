using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FantasticSplines;

public class ExtrudeShape
{
    public Vector2[] verts = new Vector2[] {
        Vector2.right, Vector2.right + Vector2.up,
        Vector2.right + Vector2.up, Vector2.left + Vector2.up,
        Vector2.up,
        Vector2.left + Vector2.up, Vector2.left,
        Vector2.left, Vector2.right};
    public Vector2[] normals = new Vector2[] {
        Vector2.right, Vector2.right,
        Vector2.up, Vector2.up,
        Vector2.up,
        Vector2.left, Vector2.left,
        Vector2.down, Vector2.down };
    public float[] u = new float[] {
        0, 0.25f,
        0.25f, 0.5f,
        0.5f,
        0.5f, 0.75f,
        0.75f, 1 };
    public Color[] colors = new Color[] {
        Color.white, Color.white,
        Color.white, Color.white,
        Color.white,
        Color.white, Color.white,
        Color.white, Color.white };
    public int[] lines = new int[] {
        0,1,
        2,3,
        3,4,
        5,6,
        7,8};

    public Bounds GetBounds()
    {
        Vector2 min = verts[0];
        Vector2 max = verts[0];
        for( int i = 0; i < verts.Length; ++i )
        {
            min = Vector2.Min( min, verts[i] );
            max = Vector2.Max( max, verts[i] );
        }

        return new Bounds( (min + max) * 0.5f, max - min );
    }
}

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class TubeGenerator : MonoBehaviour
{
    public SplineComponent spline;
    public SplineNormal splineNormal;

    public bool regenerate = false;
    public bool autoRegenerate = true;

    public bool seamlessUVs = true;

    [Range( 0.1f, 15.0f )]
    public float tangentAngleTollerance = 5;
    [Range( 0.1f, 15.0f )]
    public float normalAngleTollerance = 5;
    [Range( 0.01f, 10.0f )]
    public float minStep = 0.05f;

    MeshFilter meshFilter;
    Mesh mesh;
    ExtrudeShape extrudeShape;
    int lastUpdate = 0;

    private void Awake()
    {
        Initialise();
    }
    void Initialise()
    {
        if( meshFilter == null )
        {
            meshFilter = GetComponent<MeshFilter>();
        }
        if( mesh == null )
        {
            mesh = new Mesh();
            meshFilter.mesh = mesh;
        }
        extrudeShape = new ExtrudeShape();
    }

    private void OnDrawGizmos()
    {
        if( autoRegenerate || regenerate )
        {
            //int update = spline.GetUpdateCount() + splineNormal.GetUpdateCount();
            //if( update != lastUpdate || regenerate )
            {
                regenerate = false;
                //lastUpdate = update;
                Initialise();
                GenerateMesh();
            }
        }
    }

    bool ExceedsTangetAngleTollerance( SplineResult first, SplineResult second )
    {
        return Vector3.Angle( first.tangent.normalized, second.tangent.normalized ) > tangentAngleTollerance;
    }

    bool ExceedsNormalAngleTollerance( SplineResult first, SplineResult second )
    {
        Vector3 n1 = splineNormal.GetNormalAtSplineResult( first );
        Vector3 n2 = splineNormal.GetNormalAtSplineResult( second );
        return Vector3.Angle( n1, n2 ) > normalAngleTollerance;
    }

    List<SplineResult> splinePoints = new List<SplineResult>();
    void GenerateMesh()
    {
        splinePoints.Clear();
        // define our points along the spline
        SplineProcessor.AddResultsAtNodes( spline, ref splinePoints );
        SplineProcessor.AddResultsAtKeys( splineNormal, ref splinePoints );
        SplineProcessor.AddPointsByTollerance( ref splinePoints, spline, minStep, ExceedsTangetAngleTollerance );
        SplineProcessor.AddPointsByTollerance( ref splinePoints, spline, minStep, ExceedsNormalAngleTollerance );

        // setup data
        int vertsInShape = extrudeShape.verts.Length;
        int segments = splinePoints.Count - 1;
        int edgeLoops = segments + 1;
        int vertCount = vertsInShape * edgeLoops;
        int triCount = extrudeShape.lines.Length * segments;
        int triIndexCount = triCount * 3;

        int[] triangleIndicies = new int[triIndexCount];
        Vector3[] verticies = new Vector3[ vertCount ];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];

        // generation code
        float vScalar = 1.0f / extrudeShape.GetBounds().size.x;
        float splineVScalar = 1; // used to best fit v's so they begin and end at the end of the spline

        if( seamlessUVs )
        {
            float splineLength = spline.GetLength();
            float vLengthsInSpline = splineLength / extrudeShape.GetBounds().size.x;
            float roundedVLength = Mathf.Round( vLengthsInSpline );
            float stretchedSplineLength = roundedVLength * extrudeShape.GetBounds().size.x;
            splineVScalar = stretchedSplineLength / splineLength;
        }

        for( int p = 0; p < splinePoints.Count; ++p ) 
        {
            SplineResult result = splinePoints[p];
            Vector3 normal = splineNormal.GetValueAtDistance( result.distance, Vector3.up );
            Matrix4x4 pointMatrix = Matrix4x4.TRS( result.position, Quaternion.LookRotation( result.tangent.normalized, normal ), Vector3.one );

            int offset = p * vertsInShape;
            for( int sv = 0; sv < vertsInShape; ++sv )
            {
                int index = offset + sv;
                verticies[index] = pointMatrix.MultiplyPoint( extrudeShape.verts[sv] );
                normals[index] = pointMatrix.MultiplyVector( extrudeShape.normals[sv] );
                colors[index] = Color.white * extrudeShape.colors[sv];
                uvs[index] = new Vector2( extrudeShape.u[sv], splineVScalar * vScalar * Mathf.Clamp( result.distance, 0, result.length ) );
            }
        }

        int ti = 0;
        for( int s = 0; s < segments; ++s )
        {
            int offset = s * vertsInShape;
            for( int l = 0; l < extrudeShape.lines.Length; l+=2 )
            {
                int a = offset + extrudeShape.lines[l] + vertsInShape;
                int b = offset + extrudeShape.lines[l];
                int c = offset + extrudeShape.lines[l+1];
                int d = offset + extrudeShape.lines[l + 1] + vertsInShape;
                triangleIndicies[ti] = a;
                ti++;
                triangleIndicies[ti] = b;
                ti++;
                triangleIndicies[ti] = c;
                ti++;
                triangleIndicies[ti] = c;
                ti++;
                triangleIndicies[ti] = d;
                ti++;
                triangleIndicies[ti] = a;
                ti++;
            }
        }

        // set the mesh
        mesh.Clear();
        mesh.vertices = verticies;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangleIndicies;
    }
}
