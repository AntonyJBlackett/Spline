using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace FantasticSplines
{
    public enum SplineEditMode
    {
        None,
        AddNode,
        MoveNode
    }

    public enum SplineAddNodeMode
    {
        Append,
        Prepend,
        Insert
    }

    [CustomEditor( typeof( SplineComponent ) )]
    public class SplineComponentEditor : SplineEditor
    {
    }

    [CustomEditor( typeof( FenceBuilder ) )]
    public class FenceBuilderEditor : SplineEditor
    {
    }

    public class SplineEditor : Editor
    {
        IEditableSpline GetSpline()
        {
            return (target as IEditorSplineProxy).GetEditableSpline();
        }

        static List<SplineNode> clipboard = new List<SplineNode>();

        SplineEditMode editMode = SplineEditMode.None;
        SplineAddNodeMode addNodeMode = SplineAddNodeMode.Append;
        float diskRadius = 0.2f;
        float handleCapSize = 0.1f;
        float HandleCapSize => handleCapSize * HandleCapScale;
        Vector3 planePosition;
        Vector3 planeOffset;
        Vector3 controlPlanePosition;
        Vector3 controlPlaneOffset;

        int registered = 0;
        public void OnEnable()
        {
            ++registered;
            SceneView.duringSceneGui += DoSceneGUI;
            ResetEditMode();
        }
        public void OnDisable()
        {
            --registered;
            SceneView.duringSceneGui -= DoSceneGUI;
            Tools.current = Tool.Move;

            if( registered > 0 )
            {
                Debug.LogWarning( "Our editor is slowing down!" );
            }
        }

        void SetSelectionNodeType(IEditableSpline spline, NodeType type)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Set Point Type" );
            for( int i = 0; i < nodeSelection.Count; ++i )
            {
                int index = nodeSelection[i];
                SplineNode node = spline.GetNode( index );
                node.SetNodeType( type );
                spline.SetNode( index, node );
            }
            EditorUtility.SetDirty( spline.GetComponent() );
        }

        void SimplifySelectionNodeType(IEditableSpline spline)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Simplify Node Type" );
            for( int i = 0; i < nodeSelection.Count; ++i )
            {
                int index = nodeSelection[i];
                SplineNode node = spline.GetNode( index );
                node.SetNodeType( SplineNode.GetNodeTypeFromControls( node ) );
                spline.SetNode( index, node );
            }
            EditorUtility.SetDirty( spline.GetComponent() );
        }

        Bounds GetSelectionBounds(IEditableSpline spline)
        {
            if( nodeSelection.Count == 0 )
            {
                return GetBounds( spline );
            }

            Bounds bounds = new Bounds( spline.GetNode( nodeSelection[0] ).position, Vector3.one * 0.1f );
            for( int i = 1; i < nodeSelection.Count; ++i )
            {
                bounds.Encapsulate( spline.GetNode( nodeSelection[i] ).position );
            }

            if( bounds.size.magnitude < 1 )
            {
                bounds.size = bounds.size.normalized * 1;
            }

            return bounds;
        }

        Bounds GetBounds(IEditableSpline spline)
        {
            Bounds bounds = new Bounds( spline.GetNode( 0 ).position, Vector3.zero );
            for( int i = 1; i < spline.GetNodeCount(); ++i )
            {
                bounds.Encapsulate( spline.GetNode( i ).position );
            }
            return bounds;
        }

        public void FrameCamera(IEditableSpline spline)
        {
            Bounds bounds;
            if( nodeSelection.Count > 0 )
            {
                bounds = GetSelectionBounds( spline );
            }
            else
            {
                bounds = GetBounds( spline );
            }
            SceneView.lastActiveSceneView.Frame( bounds, false );
        }

        void FlattenSelection(IEditableSpline spline)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Flatten Selection" );
            for( int i = 0; i < nodeSelection.Count; ++i )
            {
                int index = nodeSelection[i];
                SplineNode point = spline.GetNode( index );

                point.position = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.position );
                point.Control1 = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.Control1 + point.position ) - point.position;
                point.Control2 = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.Control2 + point.position ) - point.position;

                spline.SetNode( index, point );
            }
            EditorUtility.SetDirty( spline.GetComponent() );
        }

        void SmoothSelection(IEditableSpline spline)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Smooth Selection" );
            for( int i = 0; i < nodeSelection.Count; ++i )
            {
                int index = nodeSelection[i];
                SplineNode point = spline.GetNode( index );
                SplineNode before = point;
                SplineNode after = point;

                int beforeIndex = index - 1;
                if( beforeIndex < 0 )
                {
                    beforeIndex = spline.GetNodeCount() - 1;
                }
                before = spline.GetNode( beforeIndex );

                int afterIndex = (index + 1) % spline.GetNodeCount();
                after = spline.GetNode( afterIndex );

                Vector3 direction = after.position - before.position;
                float dist1 = Mathf.Min( (point.position - before.position).magnitude, direction.magnitude );
                float dist2 = Mathf.Min( (point.position - after.position).magnitude, direction.magnitude );

                point.SetNodeType( NodeType.Aligned );
                point.Control1 = -direction.normalized * dist1 * 0.4f;
                point.Control2 = direction.normalized * dist2 * 0.4f;

                if( !spline.IsLoop() )
                {
                    if( index == 0 || index == spline.GetNodeCount() - 1 )
                    {
                        point.SetNodeType( NodeType.Point );
                    }
                }

                spline.SetNode( index, point );
            }
            EditorUtility.SetDirty( spline.GetComponent() );
        }

        void DeleteDuplicateNodes(IEditableSpline spline)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Delete Duplicate Nodes" );
            List<int> removeNodes = new List<int>();
            int nodeCount = spline.GetNodeCount();

            for( int i = 1; i < nodeCount; ++i )
            {
                SplineNode previousNode = spline.GetNode( i - 1 );
                SplineNode node = spline.GetNode( i );

                if( node.position == previousNode.position )
                {
                    if( previousNode.Control2.sqrMagnitude <= 0
                        && node.Control1.magnitude <= 0 )
                    {
                        removeNodes.Add( i );
                    }
                }
            }

            for( int i = removeNodes.Count - 1; i >= 0; --i )
            {
                int previousNodeIndex = removeNodes[i] - 1;
                int removeIndex = removeNodes[i];

                SplineNode previousNode = spline.GetNode( previousNodeIndex );
                SplineNode node = spline.GetNode( removeIndex );

                SplineNode newNode = new SplineNode( previousNode.position, previousNode.Control1, node.Control2 );
                spline.SetNode( previousNodeIndex, newNode );
                spline.RemoveNode( removeIndex );
            }

            ValidateNodeSelection( spline );
            EditorUtility.SetDirty( spline.GetComponent() );
        }

        void SimplifySpline(IEditableSpline spline)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Simplify Spline" );

            // simplify all node types
            for( int i = 1; i < spline.GetNodeCount(); ++i )
            {
                SplineNode previousNode = spline.GetNode( i - 1 );
                SplineNode node = spline.GetNode( i );
                node.SetNodeType( SplineNode.GetNodeTypeFromControls( node ) );
                spline.SetNode( i, node );
            }

            DeleteDuplicateNodes( spline );

            // remove nodes in linear sections
            for( int i = 2; i < spline.GetNodeCount(); ++i )
            {
                SplineNode node1 = spline.GetNode( i - 2 );
                SplineNode node2 = spline.GetNode( i - 1 );
                SplineNode node3 = spline.GetNode( i );

                Vector3[] points = new Vector3[7];
                points[0] = node1.position;
                points[1] = node1.Control2Position;
                points[2] = node2.Control1Position;
                points[3] = node2.position;
                points[4] = node2.Control2Position;
                points[5] = node3.Control1Position;
                points[6] = node3.position;

                Vector3 testVector = (points[0] - points[6]).normalized;
                bool linear = true;

                for( int p = 1; p < points.Length; ++p )
                {
                    Vector3 dif = points[p - 1] - points[p];
                    if( dif.sqrMagnitude <= float.Epsilon )
                    {
                        continue;
                    }

                    Vector3 vector = dif.normalized;
                    if( Vector3.Dot( vector, testVector ) < 0.98f )
                    {
                        linear = false;
                        break;
                    }
                }

                if( !linear )
                {
                    continue;
                }

                spline.RemoveNode( i - 1 );
                i--;
            }
        }

        public void DeleteSelectedNodes(IEditableSpline spline)
        {
            int removePoints = nodeSelection.Count;
            for( int i = 0; i < removePoints; ++i )
            {
                RemoveNode( spline, nodeSelection[0] );
            }
            ClearNodeSelection();
            EditorUtility.SetDirty( spline.GetComponent() );
        }

        void PasteCurve(IEditableSpline spline)
        {
            DeleteSelectedNodes( spline );
            int oldPointCount = spline.GetNodeCount();
            for( int i = 0; i < clipboard.Count; i++ )
            {
                spline.AppendNode( clipboard[i] );
            }

            nodeSelection.Clear();
            for( int i = oldPointCount; i < spline.GetNodeCount(); i++ )
            {
                nodeSelection.Add( i );
            }
        }

        public void CopyCurve(IEditableSpline spline)
        {
            clipboard.Clear();
            List<int> copyIndicies = new List<int>( nodeSelection );
            copyIndicies.Sort();
            for( int i = 0; i < copyIndicies.Count; i++ )
            {
                int index = copyIndicies[i];
                clipboard.Add( spline.GetNode( index ) );
            }
        }

        public static void ShowScriptGUI(MonoBehaviour script)
        {
            if( script != null )
            {
                MonoScript theScript = MonoScript.FromMonoBehaviour( script );
                using( new EditorGUI.DisabledScope( true ) )
                {
                    EditorGUILayout.ObjectField( "Script", theScript, script.GetType(), false );
                }
            }
        }

        void StartAddPointMode(SplineAddNodeMode mode)
        {
            ResetEditMode();
            editMode = SplineEditMode.AddNode;
            addNodeMode = mode;

            IEditableSpline spline = GetSpline();
            Transform transform = spline.GetTransform();
            Vector3 adjoiningPointPosition = transform.position;

            if( spline.GetNodeCount() > 0 )
            {
                int adjoiningIndex = 0;
                if( addNodeMode == SplineAddNodeMode.Append )
                {
                    adjoiningIndex = spline.GetNodeCount() - 1;
                }
                adjoiningPointPosition = spline.GetNode( adjoiningIndex ).position;
            }

            planePosition = MathHelper.LinePlaneIntersection( adjoiningPointPosition, transform.up, transform.position, transform.up );
            planeOffset = adjoiningPointPosition - planePosition;
        }

        void InspectorAddPointModeButton(SplineAddNodeMode mode)
        {
            if( editMode == SplineEditMode.AddNode && addNodeMode == mode )
            {
                if( GUILayout.Button( "Cancel " + mode.ToString() + " Point" ) )
                {
                    ResetEditMode();
                }
            }
            else if( GUILayout.Button( mode.ToString() + " Point" ) )
            {
                StartAddPointMode( mode );
            }
        }

        static bool foldoutSplineEditor = true;
        static bool foldoutGizmos = true;
        static bool foldoutDebug = false;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            IEditableSpline spline = GetSpline();
            if( spline == null )
            {
                return;
            }

            GUILayout.Space( 20 );
            foldoutSplineEditor = EditorGUILayout.Foldout( foldoutSplineEditor, "Spline Editor" );

            if( foldoutSplineEditor )
            {
                GUILayout.Label( "Spline Settings" );
                EditorGUI.BeginChangeCheck();
                bool loop = GUILayout.Toggle( spline.IsLoop(), "Loop" );
                if( EditorGUI.EndChangeCheck() )
                {
                    Undo.RecordObject( spline.GetUndoObject(), "Loop Toggle" );
                    spline.SetLoop( loop );
                    EditorUtility.SetDirty( spline.GetComponent() );
                }
                GUILayout.Space( 10 );

                GUILayout.Label( "Edit Modes" );
                InspectorAddPointModeButton( SplineAddNodeMode.Append );
                InspectorAddPointModeButton( SplineAddNodeMode.Prepend );
                InspectorAddPointModeButton( SplineAddNodeMode.Insert );


                GUILayout.Space( 10 );
                GUILayout.Label( "Control Point Types" );
                if( GUILayout.Button( "Linear" ) )
                {
                    SetSelectionNodeType( spline, NodeType.Point );
                }
                if( GUILayout.Button( "Mirror" ) )
                {
                    SetSelectionNodeType( spline, NodeType.Mirrored );
                }
                if( GUILayout.Button( "Aligned" ) )
                {
                    SetSelectionNodeType( spline, NodeType.Aligned );
                }
                if( GUILayout.Button( "Free" ) )
                {
                    SetSelectionNodeType( spline, NodeType.Free );
                }
                if( GUILayout.Button( "Simplify" ) )
                {
                    SimplifySelectionNodeType( spline );
                }

                GUILayout.Space( 10 );
                GUILayout.Label( "Tools" );
                if( GUILayout.Button( "Smooth Selection" ) )
                {
                    SmoothSelection( spline );
                }
                if( GUILayout.Button( "Flatten Selection" ) )
                {
                    FlattenSelection( spline );
                }
                if( GUILayout.Button( "Simplify Spline" ) )
                {
                    SimplifySpline( spline );
                }
                GUILayout.Space( 10 );

                EditorGUI.BeginChangeCheck();
                // grid
                {
                    GUILayout.Label( "Snap to Grid (hold alt)" );
                    ShowGrid = GUILayout.Toggle( ShowGrid, "Show Grid" );
                    SnapToHalfGrid = GUILayout.Toggle( SnapToHalfGrid, "Snap To Half Grid Size" );
                    SnapToGridScale = EditorGUILayout.Vector3Field( "Grid Snap Scale", SnapToGridScale );
                    GUILayout.BeginHorizontal();
                    GUILayout.Label( "Snap Axies" );
                    SnapToGridX = GUILayout.Toggle( SnapToGridX, "X" );
                    SnapToGridY = GUILayout.Toggle( SnapToGridY, "Y" );
                    SnapToGridZ = GUILayout.Toggle( SnapToGridZ, "Z" );
                    GUILayout.EndHorizontal();
                    GUILayout.Space( 10 );
                }

                foldoutGizmos = EditorGUILayout.Foldout( foldoutGizmos, "Gizmos" );
                if( foldoutGizmos )
                {
                    HandleCapScale = EditorGUILayout.FloatField( "Editor Point Scale", HandleCapScale );
                    spline.SetColor( EditorGUILayout.ColorField( "Spline Colour", spline.GetColor() ) );
                    spline.SetZTest( GUILayout.Toggle( spline.GetZTest(), "ZTest" ) );
                    ShowSegmentLengths = GUILayout.Toggle( ShowSegmentLengths, "Show Segment Lengths" );
                    ShowNodeControls = GUILayout.Toggle( ShowNodeControls, "Show Point Controls" );
                    GUILayout.Space( 10 );
                    ShowProjectionLines = GUILayout.Toggle( ShowProjectionLines, "Show Projection Lines" );
                    ShowProjectedSpline = GUILayout.Toggle( ShowProjectedSpline, "Show Y Plane Projected Spline" );
                    ShowSelectionDisks = GUILayout.Toggle( ShowSelectionDisks, "Show Selection Disks" );
                    GUILayout.Space( 10 );
                }

                if( EditorGUI.EndChangeCheck() )
                {
                    EditorUtility.SetDirty( spline.GetComponent() );
                }

                foldoutDebug = EditorGUILayout.Foldout( foldoutDebug, "Debug" );
                if( foldoutDebug )
                {
                    GUILayout.Label( "Editor State" );
                    GUILayout.Label( "Edit Mode: " + editMode.ToString() );
                    GUILayout.Label( "Add Point Mode: " + addNodeMode.ToString() );
                    GUILayout.Label( "Moving Point: " + moveNodeState.Moving.ToString() );


                    GUILayout.Space( 10 );
                    GUILayout.Label( "Spline Info" );
                    GUILayout.Label( "Length: " + spline.GetLength() );
                    GUILayout.Label( "Node Count: " + spline.GetNodeCount() );
                }
            }
        }

        public static bool ShowSegmentLengths
        {
            get => EditorPrefs.GetBool( "FantasticSplinesShowSegmentLengths", false );
            set => EditorPrefs.SetBool( "FantasticSplinesShowSegmentLengths", value );
        }

        public static bool ShowNodeControls
        {
            get => EditorPrefs.GetBool( "FantasticSplinesShowNodeControls", true );
            set => EditorPrefs.SetBool( "FantasticSplinesShowNodeControls", value );
        }

        public static bool ShowProjectionLines
        {
            get => EditorPrefs.GetBool( "FantasticSplinesShowProjectionLines", true );
            set => EditorPrefs.SetBool( "FantasticSplinesShowProjectionLines", value );
        }

        public static bool ShowProjectedSpline
        {
            get => EditorPrefs.GetBool( "FantasticSplinesShowProjectedSpline", true );
            set => EditorPrefs.SetBool( "FantasticSplinesShowProjectedSpline", value );
        }

        public static bool ShowSelectionDisks
        {
            get => EditorPrefs.GetBool( "FantasticSplinesShowSelectionDisks", true );
            set => EditorPrefs.SetBool( "FantasticSplinesShowSelectionDisks", value );
        }

        public static bool ShowGrid
        {
            get => EditorPrefs.GetBool( "FantasticSplinesShowGrid", false );
            set => EditorPrefs.SetBool( "FantasticSplinesShowGrid", value );
        }

        public static bool SnapToGridX
        {
            get => EditorPrefs.GetBool( "FantasticSplinesSnapToGridX", true );
            set => EditorPrefs.SetBool( "FantasticSplinesSnapToGridX", value );
        }

        public static bool SnapToGridY
        {
            get => EditorPrefs.GetBool( "FantasticSplinesSnapToGridY", true );
            set => EditorPrefs.SetBool( "FantasticSplinesSnapToGridY", value );
        }

        public static bool SnapToGridZ
        {
            get => EditorPrefs.GetBool( "FantasticSplinesSnapToGridZ", true );
            set => EditorPrefs.SetBool( "FantasticSplinesSnapToGridZ", value );
        }

        public static bool SnapToHalfGrid
        {
            get => EditorPrefs.GetBool( "FantasticSplinesSnapToHalfGrid", false );
            set => EditorPrefs.SetBool( "FantasticSplinesSnapToHalfGrid", value );
        }

        public static float HandleCapScale
        {
            get => EditorPrefs.GetFloat( "FantasticSplinesHandleCapScale", 1 );
            set => EditorPrefs.SetFloat( "FantasticSplinesHandleCapScale", value );
        }

        public static Vector3 SnapToGridScale
        {
            get
            {
                string sVector = EditorPrefs.GetString( "GridSnapScale", Vector3.one.ToString() );

                // Remove the parentheses
                if( sVector.StartsWith( "(", System.StringComparison.Ordinal ) && sVector.EndsWith( ")", System.StringComparison.Ordinal ) )
                {
                    sVector = sVector.Substring( 1, sVector.Length - 2 );
                }

                // split the items
                string[] sArray = sVector.Split( ',' );

                // store as a Vector3
                Vector3 result = new Vector3(
                    float.Parse( sArray[0] ),
                    float.Parse( sArray[1] ),
                    float.Parse( sArray[2] ) );

                return result;
            }
            set => EditorPrefs.SetString( "GridSnapScale", value.ToString() );
        }

        float rightClickStart;
        const float rightClickTime = 0.2f;
        bool useEvent = false;
        bool doRepaint = false;

        Tool lastTool = Tool.Move;
        void SetTool(Tool newTool)
        {
            lastTool = newTool;
            if( nodeSelection.Count == 0 )
            {
                Tools.current = lastTool;
            }
        }

        Vector2 rightClickMovement = Vector2.zero;
        void RightClickCancel(Event guiEvent)
        {
            if( guiEvent.button == 1 && guiEvent.type == EventType.MouseDown )
            {
                rightClickMovement = Vector2.zero;
                rightClickStart = Time.unscaledTime;
            }
            if( guiEvent.button == 1 && guiEvent.type == EventType.MouseDrag )
            {
                rightClickMovement += guiEvent.delta;
                // cancel right click
                rightClickStart = 0;
            }
            if( startMovementThreshold > rightClickMovement.magnitude
                && guiEvent.button == 1 && guiEvent.type == EventType.MouseUp )
            {
                if( Time.unscaledTime - rightClickStart < rightClickTime )
                {
                    ResetEditMode();
                }
            }
        }

        void KeyboardInputs(IEditableSpline spline, Event guiEvent)
        {
            if( guiEvent.type == EventType.KeyDown )
            {
                if( editMode != SplineEditMode.None )
                {
                    if( guiEvent.keyCode == KeyCode.Escape )
                    {
                        guiEvent.Use();
                        ResetEditMode();
                    }
                }

                if( guiEvent.keyCode == KeyCode.W )
                {
                    SetTool( Tool.Move );
                    guiEvent.Use();
                }
                if( guiEvent.keyCode == KeyCode.E )
                {
                    SetTool( Tool.Rotate );
                    guiEvent.Use();
                }
                if( guiEvent.keyCode == KeyCode.R )
                {
                    SetTool( Tool.Scale );
                    guiEvent.Use();
                }
                if( guiEvent.keyCode == KeyCode.F )
                {
                    FrameCamera( spline );
                    guiEvent.Use();
                }

                if( guiEvent.command && guiEvent.keyCode == KeyCode.A )
                {
                    nodeSelection.Clear();
                    for( int i = 0; i < spline.GetNodeCount(); ++i )
                    {
                        nodeSelection.Add( i );
                    }
                    guiEvent.Use();
                }

                if( guiEvent.command && guiEvent.keyCode == KeyCode.C )
                {
                    CopyCurve( spline );
                    guiEvent.Use();
                }

                if( guiEvent.command && guiEvent.keyCode == KeyCode.X )
                {
                    Undo.RecordObject( spline.GetUndoObject(), "Cut Nodes" );
                    CopyCurve( spline );
                    DeleteSelectedNodes( spline );
                    guiEvent.Use();
                }

                if( guiEvent.command && guiEvent.keyCode == KeyCode.V )
                {
                    Undo.RecordObject( spline.GetUndoObject(), "Paste Nodes" );
                    PasteCurve( spline );
                    guiEvent.Use();
                }


                if( nodeSelection.Count > 0
                    && (guiEvent.keyCode == KeyCode.Delete || guiEvent.keyCode == KeyCode.Backspace) )
                {
                    Undo.RecordObject( spline.GetUndoObject(), "Delete Nodes" );
                    DeleteSelectedNodes( spline );
                    guiEvent.Use();
                }
            }
        }

        void RemoveNode(IEditableSpline spline, int index)
        {
            spline.RemoveNode( index );
            for( int i = nodeSelection.Count - 1; i >= 0; --i )
            {
                if( nodeSelection[i] == index )
                {
                    nodeSelection.RemoveAt( i );
                    continue;
                }

                if( nodeSelection[i] > index )
                {
                    nodeSelection[i]--;
                }
            }
        }

        void SceneViewEventSetup(Event guiEvent)
        {
            useEvent = false;

            if( guiEvent.type == EventType.MouseEnterWindow )
            {
                SceneView.currentDrawingSceneView.Focus();
            }

            if( nodeSelection.Count > 0 )
            {
                lastTool = Tools.current;
                Tools.current = Tool.None;
                editMode = SplineEditMode.MoveNode;
            }
            else
            {
                Tools.current = lastTool;
            }
        }

        void DoSceneGUI(SceneView view)
        {
            Event guiEvent = Event.current;
            IEditableSpline spline = GetSpline();
            if( spline == null )
            {
                return;
            }
            Handles.zTest = spline.GetZTest() ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always;


            SceneViewEventSetup( guiEvent );

            RightClickCancel( guiEvent );

            KeyboardInputs( spline, guiEvent );

            if( guiEvent.isMouse
            || guiEvent.type == EventType.Layout )
            {
                switch( editMode )
                {
                    case SplineEditMode.None:
                        DoMoveNode( spline, guiEvent );
                        break;
                    case SplineEditMode.MoveNode:
                        DoMoveNode( spline, guiEvent );
                        break;
                    case SplineEditMode.AddNode:
                        if( addNodeMode == SplineAddNodeMode.Insert
                            && spline.GetNodeCount() >= 2 )
                        {
                            DoInsertNode( spline, guiEvent );
                        }
                        else
                        {
                            DoAddNode( spline, guiEvent );
                        }
                        break;
                }
            }

            if( !moveNodeState.MovingNodeControlPoint && (ShowGrid || guiEvent.alt) )
            {
                DrawGrid( spline, guiEvent );
            }

            // draw things
            if( guiEvent.type == EventType.Repaint )
            {
                DrawSpline( spline );
            }

            if( guiEvent.isMouse && doRepaint )
            {
                SceneView.currentDrawingSceneView.Repaint();
            }
            doRepaint = false;

            // hacks: intercept unity object drag select
            Vector3 handlePosition = Camera.current.transform.position + Camera.current.transform.forward * 10;
            float handleSize = HandleUtility.GetHandleSize( handlePosition ) * 15;
            if( useEvent
                || (editMode == SplineEditMode.AddNode && guiEvent.button == 0)
                || (guiEvent.shift && (guiEvent.type == EventType.Layout || guiEvent.type == EventType.Repaint)) )
            {
                if( Handles.Button( handlePosition, Camera.current.transform.rotation, 0, handleSize, Handles.DotHandleCap ) )
                {
                    guiEvent.Use();
                }
            }

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        }

        Color horizontalGridColour = new Color( .7f, .7f, 1, .2f );
        void DrawGrid(IEditableSpline spline, Event guiEvent)
        {
            Transform transform = spline.GetTransform();
            DrawGrid( transform.position, transform.forward, transform.right, 1, 10, guiEvent );
            DrawGrid( transform.position, transform.forward, transform.right, 1, 10, guiEvent );
        }

        void DrawGrid(Vector3 origin, Vector3 forward, Vector3 right, float scale, int largeGridInterval, Event guiEvent)
        {
            DrawGridLines( origin, forward, right, 1f * scale, 50, 50, horizontalGridColour );
            DrawGridLines( origin, forward, right, largeGridInterval * scale, 5, 5, horizontalGridColour );
        }

        void DrawGridLines(Vector3 origin, Vector3 forward, Vector3 right, float spacing, int halfLength, int halfWidth, Color color)
        {
            Handles.color = color;

            DrawGridLines( origin, forward * halfLength * spacing, right * spacing, halfWidth );
            DrawGridLines( origin, right * Mathf.Max( 0.2f, halfWidth ) * spacing, forward * spacing, halfLength );

            Handles.color = Color.white;
        }

        void DrawGridLines(Vector3 origin, Vector3 halfLength, Vector3 spacing, int halfCount)
        {
            Vector3 lineStart = origin - halfLength - halfCount * spacing;
            Vector3 lineEnd = origin + halfLength - halfCount * spacing;
            for( int i = 0; i < halfCount * 2 + 1; ++i )
            {
                Handles.DrawLine( lineStart, lineEnd );
                lineStart = lineStart + spacing;
                lineEnd = lineEnd + spacing;
            }
        }

        static Vector2 TransformMouseDeltaToScreenDelta(Vector2 mouseDelta)
        {
            mouseDelta.y = -mouseDelta.y;
            return mouseDelta * EditorGUIUtility.pixelsPerPoint;
        }

        static Vector3 TransformMousePositionToScreenPoint(Camera camera, Vector3 mousePosition)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            mousePosition.y = camera.pixelHeight - mousePosition.y * pixelsPerPoint;
            mousePosition.x *= pixelsPerPoint;
            return mousePosition;
        }

        static Ray MousePositionToRay(Camera camera, Vector3 mousePosition)
        {
            return camera.ScreenPointToRay( TransformMousePositionToScreenPoint( camera, mousePosition ) );
        }

        void ResetEditMode()
        {
            lastTool = Tool.Move;
            Tools.current = Tool.Move;
            ClearNodeSelection();
            editMode = SplineEditMode.None;
            planeOffset = Vector3.zero;
            controlPlanePosition = Vector3.zero;
            addNodeState = AddNodeState.NodePosition;
        }

        void DrawSplineNodes(IEditableSpline spline)
        {
            List<int> sortedNodeIndicies = GetNodeIndiciesSelectedFirst( spline );
            sortedNodeIndicies.Reverse(); // draw selected on top
            for( int sortedI = 0; sortedI < sortedNodeIndicies.Count; ++sortedI )
            {
                int i = sortedNodeIndicies[sortedI];
                if( nodeSelection.Contains( i ) )
                {
                    Handles.color = Color.green;
                }
                else
                {
                    Handles.color = Color.white;
                }

                Vector3 point = spline.GetNode( i ).position;
                Handles.SphereHandleCap( 0, point, spline.GetTransform().rotation, HandleCapSize, EventType.Repaint );
            }
            Handles.color = Color.white;
        }

        void DrawSplinePlaneProjectionLines(IEditableSpline spline)
        {
            if( !ShowProjectionLines )
            {
                return;
            }

            for( int i = 0; i < spline.GetNodeCount(); ++i )
            {
                Vector3 point = spline.GetNode( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                Handles.DrawDottedLine( pointOnPlane, point, 2 );
            }
        }

        void DrawSplineLines(IEditableSpline spline)
        {
            for( int i = 0; i < spline.GetNodeCount(); ++i )
            {
                Vector3 point = spline.GetNode( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                if( i < spline.GetNodeCount() - 1 )
                {
                    Vector3 nextPoint = spline.GetNode( i + 1 ).position;
                    Handles.DrawLine( nextPoint, point );
                }
            }
        }

        static void DrawBezierSegment(SplineNode node1, SplineNode node2, Color color)
        {
            Handles.DrawBezier( node1.position, node2.position, node1.position + node1.Control2, node2.position + node2.Control1, color, null, 3 );
        }

        static void DrawBezierSegmentOnPlane(Vector3 planeOrigin, Vector3 planeNormal, SplineNode node1, SplineNode node2)
        {
            Vector3 pointOnPlane1 = GetPointOnPlaneY( planeOrigin, planeNormal, node1.position );
            Vector3 pointOnPlane2 = GetPointOnPlaneY( planeOrigin, planeNormal, node2.position );

            Vector3 tangentFlat1 = GetPointOnPlaneY( planeOrigin, planeNormal, node1.position + node1.Control2 );
            Vector3 tangentFlat2 = GetPointOnPlaneY( planeOrigin, planeNormal, node2.position + node2.Control1 );

            Handles.DrawBezier( pointOnPlane1, pointOnPlane2, tangentFlat1, tangentFlat2, Color.grey, null, 2.5f );
        }

        void DrawBezierSplineLines(IEditableSpline spline)
        {
            for( int i = 1; i < spline.GetNodeCount(); ++i )
            {
                DrawBezierSegment( spline.GetNode( i - 1 ), spline.GetNode( i ), spline.GetColor() );
            }

            if( spline.IsLoop() && spline.GetNodeCount() > 1 )
            {
                DrawBezierSegment( spline.GetNode( spline.GetNodeCount() - 1 ), spline.GetNode( 0 ), spline.GetColor() );
            }
        }

        void DrawBezierPlaneProjectedSplineLines(IEditableSpline spline)
        {
            if( !ShowProjectedSpline )
            {
                return;
            }

            for( int i = 1; i < spline.GetNodeCount(); ++i )
            {
                DrawBezierSegmentOnPlane( spline.GetTransform().position, spline.GetTransform().up, spline.GetNode( i - 1 ), spline.GetNode( i ) );
            }

            if( spline.IsLoop() && spline.GetNodeCount() > 1 )
            {
                DrawBezierSegmentOnPlane( spline.GetTransform().position, spline.GetTransform().up, spline.GetNode( spline.GetNodeCount() - 1 ), spline.GetNode( 0 ) );
            }
        }

        void DrawSplinePlaneProjectedSplineLines(IEditableSpline spline)
        {
            for( int i = 0; i < spline.GetNodeCount(); ++i )
            {
                Vector3 point = spline.GetNode( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                if( i < spline.GetNodeCount() - 1 )
                {
                    Vector3 nextPoint = spline.GetNode( i + 1 ).position;
                    Vector3 nextPointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, nextPoint );
                    Handles.DrawDottedLine( pointOnPlane, nextPointOnPlane, 2 );
                }
            }
        }

        void DrawSplineSelectionDisks(IEditableSpline spline)
        {
            if( !ShowSelectionDisks )
            {
                return;
            }

            ValidateNodeSelection( spline );
            Transform transform = spline.GetTransform();
            Handles.color = Color.grey;
            for( int i = 0; i < nodeSelection.Count; ++i )
            {
                int index = nodeSelection[i];

                Vector3 point = spline.GetNode( index ).position;
                Vector3 planePoint = GetPointOnPlaneY( transform.position, transform.up, point );
                DrawWireDisk( point, transform.up, diskRadius, Camera.current.transform.position );
                DrawWireDisk( planePoint, transform.up, diskRadius, Camera.current.transform.position );

                RaycastHit hitDown;
                Ray down = new Ray( point, -transform.up );
                if( Physics.Raycast( down, out hitDown ) )
                {
                    DrawWireDisk( hitDown.point, hitDown.normal, diskRadius, Camera.current.transform.position );
                    Handles.DrawDottedLine( planePoint, hitDown.point, 2 );
                }
            }
        }

        Color ControlPointEditColor => (Color.cyan + Color.grey + Color.grey) * 0.33f;

        void DrawNodeControlPoints(IEditableSpline spline, int index)
        {
            SplineNode node = spline.GetNode( index );
            if( node.NodeType == NodeType.Point )
            {
                return;
            }

            if( nodeSelection.Contains( index ) )
            {
                Handles.color = ControlPointEditColor;
            }
            else
            {
                Handles.color = Color.grey;
            }

            Vector3 control1 = node.position + node.Control1;
            Vector3 control2 = node.position + node.Control2;

            if( index > 0 || spline.IsLoop() )
            {
                Handles.SphereHandleCap( 0, control1, Quaternion.identity, HandleCapSize, EventType.Repaint );
                Handles.DrawLine( node.position, control1 );
            }

            if( index < spline.GetNodeCount() - 1 || spline.IsLoop() )
            {
                Handles.SphereHandleCap( 0, control2, Quaternion.identity, HandleCapSize, EventType.Repaint );
                Handles.DrawLine( node.position, control2 );
            }
        }

        void DrawSplineSelectionNodeControlPoints(IEditableSpline spline)
        {
            if( !ShowNodeControls )
            {
                return;
            }

            if( nodeSelection.Count > 0 )
            {
                for( int i = 0; i < nodeSelection.Count; ++i )
                {
                    DrawNodeControlPoints( spline, nodeSelection[i] );
                }
            }
            else
            {
                for( int i = 0; i < spline.GetNodeCount(); ++i )
                {
                    DrawNodeControlPoints( spline, i );
                }
            }
        }

        void DrawSpline(IEditableSpline spline)
        {
            DrawBezierPlaneProjectedSplineLines( spline );
            DrawSplinePlaneProjectionLines( spline );

            DrawSplineSelectionDisks( spline );
            DrawBezierSplineLines( spline );

            DrawSegmentLengths( spline );
            DrawSplineNodes( spline );
            DrawSplineSelectionNodeControlPoints( spline );
        }

        void DrawSegmentLengths(IEditableSpline spline)
        {
            if( ShowSegmentLengths )
            {
                spline.DrawSegmentLengths();
            }
        }

        bool IsIndexInRange(IEditableSpline spline, int index)
        {
            return index >= 0 && index < spline.GetNodeCount();
        }

        List<int> nodeSelection = new List<int>();
        void SelectionAddNode(IEditableSpline spline, int index)
        {
            ValidateNodeSelection( spline );
            if( !IsIndexInRange( spline, index ) )
            {
                return;
            }

            if( nodeSelection.Contains( index ) )
            {
                return;
            }
            nodeSelection.Add( index );
        }

        void ValidateNodeSelection(IEditableSpline spline)
        {
            for( int i = nodeSelection.Count - 1; i >= 0; i-- )
            {
                if( !IsIndexInRange( spline, nodeSelection[i] ) )
                {
                    nodeSelection.RemoveAt( i );
                }
            }
        }

        void ClearNodeSelection()
        {
            nodeSelection.Clear();
            editMode = SplineEditMode.None;
        }

        List<int> GetNodeIndiciesSelectedFirst(IEditableSpline spline)
        {
            List<int> nodeIndicies = new List<int>();
            for( int i = 0; i < spline.GetNodeCount(); ++i )
            {
                nodeIndicies.Add( i );
            }

            // selected points take priority
            nodeIndicies.Sort( (int one, int two) =>
            {
                if( nodeSelection.Contains( one ) == nodeSelection.Contains( two ) )
                {
                    return 0;
                }
                if( nodeSelection.Contains( one ) && !nodeSelection.Contains( two ) )
                {
                    return -1;
                }
                return 1;
            } );

            return nodeIndicies;
        }

        bool IsMouseButtonEvent(Event guiEvent, int button)
        {
            return guiEvent.button == button && guiEvent.isMouse && (guiEvent.type == EventType.MouseDown || guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp);
        }

        struct DetectClickSelectionResult
        {
            public bool Detected => nodeIndex >= 0;
            public int nodeIndex;
            public MoveControlPointId selectedType;

            public DetectClickSelectionResult(int nodeIndex, MoveControlPointId selectedType)
            {
                this.nodeIndex = nodeIndex;
                this.selectedType = selectedType;
            }
        }

        DetectClickSelectionResult DetectClickSelection(IEditableSpline spline, Event guiEvent)
        {
            List<int> sortedNodes = GetNodeIndiciesSelectedFirst( spline );
            for( int s = 0; s < sortedNodes.Count; ++s )
            {
                int index = sortedNodes[s];

                SplineNode node = spline.GetNode( index );

                bool overControl1 = IsMouseOverPoint( Camera.current, node.Control1Position, guiEvent.mousePosition );
                bool overControl2 = IsMouseOverPoint( Camera.current, node.Control2Position, guiEvent.mousePosition );

                bool control1Interactable = spline.IsLoop() || index > 0;
                bool control2Interactable = spline.IsLoop() || index < spline.GetNodeCount() - 1;

                bool control1Detected = overControl1 && control1Interactable;
                bool control2Detected = overControl2 && control2Interactable;

                bool controlsEnabled = ShowNodeControls && (nodeSelection.Contains( index ) || nodeSelection.Count == 0);

                if( controlsEnabled &&
                    node.NodeType != NodeType.Point &&
                    (control1Detected || control2Detected) )
                {
                    if( control1Detected )
                    {
                        return new DetectClickSelectionResult( index, MoveControlPointId.Control1 );
                    }
                    if( control2Detected )
                    {
                        return new DetectClickSelectionResult( index, MoveControlPointId.Control2 );
                    }
                }
                else if( IsMouseOverPoint( Camera.current, node.position, guiEvent.mousePosition ) )
                {
                    return new DetectClickSelectionResult( index, MoveControlPointId.None );
                }
            }

            return new DetectClickSelectionResult()
            {
                nodeIndex = -1,
                selectedType = MoveControlPointId.None,
            };
        }

        bool DoClickSelection(IEditableSpline spline, Event guiEvent)
        {
            DetectClickSelectionResult result = DetectClickSelection( spline, guiEvent );
            if( result.Detected )
            {
                if( result.selectedType != MoveControlPointId.None )
                {
                    useEvent = true;
                    if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
                    {
                        if( nodeSelection.Count == 0 )
                        {
                            SelectionAddNode( spline, result.nodeIndex );
                            editMode = SplineEditMode.MoveNode;
                        }
                        return false;
                    }
                }
                else
                {
                    useEvent = true;
                    if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
                    {
                        if( nodeSelection.Contains( result.nodeIndex ) && guiEvent.command )
                        {
                            nodeSelection.Remove( result.nodeIndex );
                        }
                        else if( !nodeSelection.Contains( result.nodeIndex ) )
                        {
                            if( !guiEvent.shift && !guiEvent.command )
                            {
                                ClearNodeSelection();
                            }
                            SelectionAddNode( spline, result.nodeIndex );
                            editMode = SplineEditMode.MoveNode;
                        }
                        return false;
                    }
                }
            }

            return IsMouseButtonEvent( guiEvent, 0 ) && !guiEvent.shift && !guiEvent.command;
        }

        Vector2 mouseDragSelectionStart;
        bool dragSelectActive = false;
        bool DoDragSelection(IEditableSpline spline, Event guiEvent)
        {
            bool clearSelection = IsMouseButtonEvent( guiEvent, 0 ) && !dragSelectActive;
            if( spline.GetNodeCount() > 0 && !dragSelectActive && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && guiEvent.shift )
            {
                mouseDragSelectionStart = guiEvent.mousePosition;
                dragSelectActive = true;

                for( int i = 0; i < spline.GetNodeCount(); ++i )
                {
                    SplineNode node = spline.GetNode( i );
                    if( IsMouseOverPoint( Camera.current, node.position + node.Control1, guiEvent.mousePosition ) )
                    {
                        dragSelectActive = false;
                        break;
                    }
                    if( IsMouseOverPoint( Camera.current, node.position + node.Control2, guiEvent.mousePosition ) )
                    {
                        dragSelectActive = false;
                        break;
                    }
                    if( IsMouseOverPoint( Camera.current, node.position, guiEvent.mousePosition ) )
                    {
                        dragSelectActive = false;
                        break;
                    }
                }
            }

            if( dragSelectActive )
            {
                Vector2 diff = mouseDragSelectionStart - guiEvent.mousePosition;
                Vector2 extents = Vector2.Max( diff, -diff ) * 0.5f;
                Vector2 position = (mouseDragSelectionStart + guiEvent.mousePosition) * 0.5f;

                Vector2 pos1 = new Vector2( position.x + extents.x, position.y - extents.y );
                Vector2 pos2 = new Vector2( position.x + extents.x, position.y + extents.y );
                Vector2 pos3 = new Vector2( position.x - extents.x, position.y + extents.y );
                Vector2 pos4 = new Vector2( position.x - extents.x, position.y - extents.y );

                Ray ray1 = MousePositionToRay( Camera.current, pos1 );
                Ray ray2 = MousePositionToRay( Camera.current, pos2 );
                Ray ray3 = MousePositionToRay( Camera.current, pos3 );
                Ray ray4 = MousePositionToRay( Camera.current, pos4 );

                Vector3[] verts = new Vector3[]
                {
                    ray1.origin + ray1.direction,
                    ray2.origin + ray2.direction,
                    ray3.origin + ray3.direction,
                    ray4.origin + ray4.direction
                };

                Color colour = Color.green;
                Color fill = (Color.green + Color.black) * 0.5f;
                fill.a = 0.1f;
                Handles.DrawSolidRectangleWithOutline( verts, fill, colour );
            }

            if( dragSelectActive && guiEvent.type == EventType.MouseUp && guiEvent.button == 0 )
            {
                Vector3 dragSelectionEnd = TransformMousePositionToScreenPoint( Camera.current, guiEvent.mousePosition );
                Vector3 dragSelectionStart = TransformMousePositionToScreenPoint( Camera.current, mouseDragSelectionStart );
                Bounds dragBounds = new Bounds( (dragSelectionStart + dragSelectionEnd) * 0.5f, Vector3.zero );
                dragBounds.Encapsulate( dragSelectionStart );
                dragBounds.Encapsulate( dragSelectionEnd );

                int pointsInDragBounds = 0;
                for( int i = 0; i < spline.GetNodeCount(); ++i )
                {
                    Vector3 point = spline.GetNode( i ).position;
                    Vector3 pointScreenPosition = Camera.current.WorldToScreenPoint( point );
                    pointScreenPosition.z = 0;
                    if( dragBounds.Contains( pointScreenPosition ) )
                    {
                        ++pointsInDragBounds;
                        SelectionAddNode( spline, i );
                        editMode = SplineEditMode.MoveNode;
                    }
                }

                if( pointsInDragBounds == 0 )
                {
                    clearSelection = !guiEvent.shift && !guiEvent.command;
                }

                dragSelectActive = false;
                doRepaint = true;
            }

            if( dragSelectActive )
            {
                doRepaint = true;
                useEvent = true;
            }
            return clearSelection;
        }

        bool hadSelectionOnMouseDown = false;
        void DoNodeSelection(IEditableSpline spline, Event guiEvent)
        {
            if( guiEvent.type == EventType.MouseDown )
            {
                hadSelectionOnMouseDown = nodeSelection.Count > 0;
            }
            if( guiEvent.type == EventType.Used && (nodeSelection.Count > 0 || hadSelectionOnMouseDown) )
            {
                Selection.activeObject = (target as Component).gameObject;
            }
            else if( guiEvent.type != EventType.Repaint && guiEvent.type != EventType.Layout && guiEvent.type != EventType.Used && !IsMouseButtonEvent( guiEvent, 0 ) )
            {
                hadSelectionOnMouseDown = false;
            }

            bool clearClick = DoClickSelection( spline, guiEvent );
            bool clearDrag = DoDragSelection( spline, guiEvent );
            bool clearSelection = clearClick && clearDrag;

            if( clearSelection )
            {
                ClearNodeSelection();
            }
        }

        enum AddNodeState
        {
            NodePosition,
            ControlPosition
        }

        static Vector3 GetPointPlacement(Camera camera, Vector2 mousePosition, Vector3 origin, Vector3 up, ref Vector3 planePoint, ref Vector3 planeOffset, bool verticalDisplace, bool intersectPhsyics, bool snapToGrid)
        {
            Ray mouseRay = MousePositionToRay( camera, mousePosition );
            Vector3 mouseWorldPosition = MathHelper.LinePlaneIntersection( mouseRay, origin + planeOffset, up );

            if( intersectPhsyics )
            {
                RaycastHit hit;
                if( Physics.Raycast( mouseRay, out hit ) )
                {
                    mouseWorldPosition = hit.point;
                    planePoint = MathHelper.LinePlaneIntersection( mouseWorldPosition, up, origin, up );
                    planeOffset = mouseWorldPosition - planePoint;
                }
            }
            else if( verticalDisplace )
            {
                Vector3 verticalPlaneNormal = Vector3.Cross( up, Vector3.Cross( up, mouseRay.direction ) );
                mouseWorldPosition = MathHelper.LinePlaneIntersection( mouseRay, planePoint, verticalPlaneNormal );
                planeOffset = planeOffset + up * Vector3.Dot( up, mouseWorldPosition - (planePoint + planeOffset) );
            }
            else
            {
                planePoint = mouseWorldPosition - planeOffset;
            }

            if( snapToGrid )
            {
                planeOffset = SnapLocalPointToGrid( planeOffset, Vector3.one );
                planePoint = SnapWorldPointToGrid( planePoint, origin );
            }

            return planePoint + planeOffset;
        }

        void DrawNodeControlPointMovementGuides(IEditableSpline spline, Vector3 point, Vector3 control1, Vector3 control2)
        {
            DrawNodeControlPointMovementGuides( spline, point, control1, control2, ControlPointEditColor, Color.white );
        }

        void DrawNodeControlPointMovementGuides(IEditableSpline spline, Vector3 point, Vector3 control1, Vector3 control2, Color controlColour, Color controlPlaneColour)
        {
            Camera camera = Camera.current;
            Transform transform = spline.GetTransform();

            Vector3 controlPlane1 = GetPointOnPlaneY( point, transform.up, control1 );
            Vector3 controlPlane2 = GetPointOnPlaneY( point, transform.up, control2 );

            Handles.color = controlColour;
            // draw control placement GUI
            Handles.SphereHandleCap( 0, control1, Quaternion.identity, HandleCapSize * 0.5f, EventType.Repaint );
            Handles.SphereHandleCap( 0, control2, Quaternion.identity, HandleCapSize * 0.5f, EventType.Repaint );

            DrawWireDisk( control1, transform.up, diskRadius * 0.5f, camera.transform.position );
            DrawWireDisk( control2, transform.up, diskRadius * 0.5f, camera.transform.position );

            Handles.DrawDottedLine( point, control1, 2 );
            Handles.DrawDottedLine( point, control2, 2 );

            Handles.color = controlPlaneColour;
            DrawWireDisk( controlPlane1, transform.up, diskRadius * 0.5f, camera.transform.position );
            DrawWireDisk( controlPlane2, transform.up, diskRadius * 0.5f, camera.transform.position );

            Handles.DrawDottedLine( point, controlPlane1, 2 );
            Handles.DrawDottedLine( point, controlPlane2, 2 );

            Handles.DrawDottedLine( control1, controlPlane1, 2 );
            Handles.DrawDottedLine( control2, controlPlane2, 2 );

            Handles.color = Color.white;
        }

        static Vector3 SnapWorldPointToGrid(Vector3 worldSpacePoint, Vector3 origin, float scale = 1)
        {
            return SnapLocalPointToGrid( worldSpacePoint - origin, Vector3.one, scale ) + origin;
        }

        static Vector3 SnapWorldPointToGrid(Vector3 worldSpacePoint, Vector3 origin, Vector3 filter, float scale = 1)
        {
            float globalScale = SnapToHalfGrid ? 0.5f : 1;
            scale *= globalScale;
            return SnapLocalPointToGrid( worldSpacePoint - origin, filter, scale ) + origin;
        }

        static Vector3 SnapLocalPointToGrid(Vector3 point, Vector3 filter, float scale = 1)
        {
            Vector3 result = point;

            SnapAxis snapAxis = SnapAxis.None;
            if( SnapToGridX && Mathf.Abs( filter.x ) > 0 ) snapAxis = SnapAxis.X;
            if( SnapToGridY && Mathf.Abs( filter.y ) > 0 ) snapAxis |= SnapAxis.Y;
            if( SnapToGridZ && Mathf.Abs( filter.z ) > 0 ) snapAxis |= SnapAxis.Z;

            result = Snapping.Snap( result, SnapToGridScale * scale, snapAxis );

            return result;
        }

        AddNodeState addNodeState = AddNodeState.NodePosition;
        Vector3 addNodePosition;
        bool canShift = false;
        void DoAddNode(IEditableSpline spline, Event guiEvent)
        {
            if( !guiEvent.shift )
            {
                canShift = true;
            }

            Selection.activeObject = (target as Component).gameObject;
            Transform handleTransform = spline.GetTransform();
            Transform transform = spline.GetTransform();
            Camera camera = Camera.current;

            Vector3 connectingNodePosition = transform.position;

            if( spline.GetNodeCount() > 0 )
            {
                int adjoiningIndex = 0;
                if( addNodeMode == SplineAddNodeMode.Append )
                {
                    adjoiningIndex = spline.GetNodeCount() - 1;
                }
                connectingNodePosition = spline.GetNode( adjoiningIndex ).position;
            }

            Vector3 connectingPlanePoint = GetPointOnPlaneY( transform.position, transform.up, connectingNodePosition );

            if( addNodeState == AddNodeState.NodePosition )
            {
                addNodePosition = GetPointPlacement(
                    camera,
                    guiEvent.mousePosition,
                    transform.position,
                    transform.up,
                    ref planePosition,
                    ref planeOffset,
                    guiEvent.shift && canShift,
                    guiEvent.command,
                    guiEvent.alt );

                // draw point placement GUI
                Handles.color = Color.yellow;
                RaycastHit hitDown;
                Ray down = new Ray( addNodePosition, -transform.up );
                if( Physics.Raycast( down, out hitDown ) )
                {
                    DrawWireDisk( hitDown.point, hitDown.normal, diskRadius, camera.transform.position );
                    Handles.DrawDottedLine( planePosition, hitDown.point, 2 );
                }

                DrawWireDisk( addNodePosition, transform.up, diskRadius, camera.transform.position );
                DrawWireDisk( planePosition, transform.up, diskRadius, camera.transform.position );
                Handles.DrawDottedLine( planePosition, addNodePosition, 2 );

                if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
                {
                    DetectClickSelectionResult result = DetectClickSelection( spline, guiEvent );
                    if( result.Detected && !spline.IsLoop() )
                    {
                        if( addNodeMode == SplineAddNodeMode.Append && result.nodeIndex == 0 )
                        {
                            spline.SetLoop( true );
                            guiEvent.Use();
                            editMode = SplineEditMode.None;
                            return;
                        }
                        if( addNodeMode == SplineAddNodeMode.Prepend && result.nodeIndex == spline.GetNodeCount() - 1 )
                        {
                            spline.SetLoop( true );
                            guiEvent.Use();
                            editMode = SplineEditMode.None;
                            return;
                        }
                    }

                    canShift = false;
                    addNodeState = AddNodeState.ControlPosition;
                    controlPlanePosition = addNodePosition;
                    controlPlaneOffset = Vector3.zero;
                    guiEvent.Use();
                }
            }

            Vector3 newControlPointPosition = addNodePosition;
            Vector3 relativeControlPoint = newControlPointPosition - addNodePosition;
            if( addNodeMode == SplineAddNodeMode.Prepend )
            {
                relativeControlPoint *= -1;
            }

            if( addNodeState == AddNodeState.ControlPosition )
            {
                newControlPointPosition = GetPointPlacement(
                        camera,
                        guiEvent.mousePosition,
                        addNodePosition,
                        transform.up,
                        ref controlPlanePosition,
                        ref controlPlaneOffset,
                        guiEvent.shift && canShift,
                        guiEvent.command,
                        guiEvent.alt );
                relativeControlPoint = newControlPointPosition - addNodePosition;
                if( addNodeMode == SplineAddNodeMode.Prepend )
                {
                    relativeControlPoint *= -1;
                }

                Vector3 worldControl1 = addNodePosition - relativeControlPoint;
                Vector3 worldControl2 = addNodePosition + relativeControlPoint;
                DrawNodeControlPointMovementGuides( spline, addNodePosition, worldControl1, worldControl2, Color.yellow, Color.white );

                if( guiEvent.type == EventType.MouseUp && guiEvent.button == 0 )
                {
                    canShift = false;
                    addNodeState = AddNodeState.NodePosition;
                    Undo.RecordObject( spline.GetUndoObject(), "Add Node" );

                    SplineNode newNode = new SplineNode( addNodePosition );
                    if( relativeControlPoint.magnitude > 0.01f )
                    {
                        newNode.SetNodeType( NodeType.Mirrored );
                        newNode.Control2 = relativeControlPoint; // this sets control 1 as well as it's mirrored
                    }

                    switch( addNodeMode )
                    {
                        case SplineAddNodeMode.Append:
                            spline.AppendNode( newNode );
                            break;
                        case SplineAddNodeMode.Prepend:
                            spline.PrependNode( newNode );
                            break;
                        default:
                            spline.AppendNode( newNode );
                            break;
                    }

                    EditorUtility.SetDirty( spline.GetComponent() );
                    guiEvent.Use();
                }
            }

            // draw beziers new point GUI
            Handles.color = Color.yellow;
            {
                int nodeCount = spline.GetNodeCount();
                if( nodeCount > 0 )
                {
                    SplineNode newNode = new SplineNode( addNodePosition );
                    newNode.Control1 = -relativeControlPoint; // this sets control 1 as well as it's mirrored
                    if( relativeControlPoint.magnitude > 0.01f )
                    {
                        newNode.SetNodeType( NodeType.Mirrored );
                    }
                    newNode.Control2 = relativeControlPoint; // this sets control 1 as well as it's mirrored


                    SplineNode node1 = spline.GetNode( nodeCount - 1 );
                    SplineNode node2 = newNode;
                    if( addNodeMode == SplineAddNodeMode.Prepend )
                    {
                        node1 = newNode;
                        node2 = spline.GetNode( 0 );
                    }
                    Bezier3 addSegment = new Bezier3( node1, node2 );
                    Bezier3 projectedAddSegment = Bezier3.ProjectToPlane( addSegment, planePosition, transform.up );
                    Handles.DrawBezier( projectedAddSegment.A, projectedAddSegment.D, projectedAddSegment.B, projectedAddSegment.C, Color.grey, null, 1 );
                    Handles.DrawBezier( addSegment.A, addSegment.D, addSegment.B, addSegment.C, Color.yellow, null, 1 );


                    if( spline.IsLoop() )
                    {
                        node1 = newNode;
                        node2 = spline.GetNode( 0 );
                        if( addNodeMode == SplineAddNodeMode.Prepend )
                        {
                            node1 = spline.GetNode( nodeCount - 1 );
                            node2 = newNode;
                        }
                        Bezier3 loopSegment = new Bezier3( node1, node2 );
                        Bezier3 projectedLoopSegment = Bezier3.ProjectToPlane( loopSegment, planePosition, transform.up );
                        Handles.DrawBezier( projectedLoopSegment.A, projectedLoopSegment.D, projectedLoopSegment.B, projectedLoopSegment.C, Color.grey, null, 1 );
                        Handles.DrawBezier( loopSegment.A, loopSegment.D, loopSegment.B, loopSegment.C, Color.yellow, null, 1 );
                    }
                }
            }

            Handles.SphereHandleCap( 0, addNodePosition, Quaternion.identity, HandleCapSize, guiEvent.type );
            Handles.color = Color.white;
        }

        void DoInsertNode(IEditableSpline spline, Event guiEvent)
        {
            Selection.activeObject = (target as Component).gameObject;

            Transform handleTransform = spline.GetTransform();
            Transform transform = spline.GetTransform();
            Camera camera = Camera.current;

            Ray mouseRay = MousePositionToRay( camera, guiEvent.mousePosition );
            SplineResult closestToRay = spline.GetResultClosestTo( mouseRay );
            Vector3 newNodePosition = closestToRay.position;

            planePosition = MathHelper.LinePlaneIntersection( newNodePosition, transform.up, transform.position, transform.up );
            planeOffset = newNodePosition - planePosition;

            // new point
            Handles.color = Color.yellow;
            Handles.SphereHandleCap( 0, newNodePosition, transform.rotation, HandleCapSize, guiEvent.type );
            DrawWireDisk( newNodePosition, transform.up, diskRadius, camera.transform.position );
            DrawWireDisk( planePosition, transform.up, diskRadius, camera.transform.position );
            Handles.DrawLine( planePosition, newNodePosition );
            Handles.color = Color.white;

            RaycastHit hitDown;
            Ray down = new Ray( newNodePosition, -transform.up );
            if( Physics.Raycast( down, out hitDown ) )
            {
                DrawWireDisk( hitDown.point, hitDown.normal, diskRadius, camera.transform.position );
                Handles.DrawDottedLine( planePosition, hitDown.point, 2 );
            }

            if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
            {
                Undo.RecordObject( spline.GetUndoObject(), "Insert Node" );
                spline.InsertNode( closestToRay.t );
                EditorUtility.SetDirty( spline.GetComponent() );
                guiEvent.Use();
            }
        }

        bool IsMouseOverPoint(Camera camera, Vector3 point, Vector3 mousePosition)
        {
            Vector3 screenPoint = Camera.current.WorldToScreenPoint( point );
            Vector3 screenPoint2 = Camera.current.WorldToScreenPoint( point + Camera.current.transform.right * HandleCapSize );
            Vector3 mouseScreenPoint = TransformMousePositionToScreenPoint( Camera.current, mousePosition );
            screenPoint.z = 0;
            screenPoint2.z = 0;
            return Vector3.Distance( screenPoint, mouseScreenPoint ) < Vector3.Distance( screenPoint, screenPoint2 );
        }

        enum MoveControlPointId
        {
            None,
            Control1,
            Control2,
            Unknown
        }


        struct MoveNodeState
        {
            public int NodeIndex { get; private set; }
            public MoveControlPointId ControlPointId { get; set; }

            public Vector2 StartMovementAccumulator { get; set; }
            public Vector3 LastSnappedMovement { get; set; }

            public Vector3 CurrentPosition { get; set; }
            public Vector3 StartPosition { get; private set; }

            public bool Moving { get; private set; }
            public bool MovingNodeControlPoint { get { return NodeIndex != -1 && ControlPointId != MoveControlPointId.None; } }

            public bool WasSnapping { get; set; }

            public MoveNodeState(int nodeIndex, MoveControlPointId controlPointId, Vector3 startPosition)
            {
                Moving = true;
                StartMovementAccumulator = Vector2.zero;
                LastSnappedMovement = Vector3.zero;
                WasSnapping = false;
                WasSnapping = false;
                this.ControlPointId = controlPointId;
                this.NodeIndex = nodeIndex;
                this.StartPosition = CurrentPosition = startPosition;
            }
        }

        MoveNodeState moveNodeState;
        const float startMovementThreshold = 5;

        void DetectNodeToMove(IEditableSpline spline, Event guiEvent)
        {
            DetectClickSelectionResult result = DetectClickSelection( spline, guiEvent );

            if( result.Detected )
            {
                SplineNode node = spline.GetNode( result.nodeIndex );

                if( result.selectedType == MoveControlPointId.Control2
                    || result.selectedType == MoveControlPointId.Unknown )
                {
                    controlPlanePosition = GetPointOnPlaneY( node.position, spline.GetTransform().up, node.Control2Position );
                    controlPlaneOffset = node.Control2Position - controlPlanePosition;

                    moveNodeState = new MoveNodeState( result.nodeIndex, result.selectedType, node.Control2Position );
                }
                else if( result.selectedType == MoveControlPointId.Control1 )
                {
                    controlPlanePosition = GetPointOnPlaneY( node.position, spline.GetTransform().up, node.Control1Position );
                    controlPlaneOffset = node.Control1Position - controlPlanePosition;
                    moveNodeState = new MoveNodeState( result.nodeIndex, result.selectedType, node.Control1Position );
                }
                else
                {
                    moveNodeState = new MoveNodeState( result.nodeIndex, result.selectedType, node.position );
                }

                if( moveNodeState.Moving )
                {
                    planePosition = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, node.position );
                    planeOffset = node.position - planePosition;
                }
            }
        }

        void DoMoveNode(IEditableSpline spline, Event guiEvent)
        {
            Transform transform = spline.GetTransform();

            if( !moveNodeState.Moving )
            {
                DoNodeSelection( spline, guiEvent );
            }

            if( !moveNodeState.Moving && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
            {
                DetectNodeToMove( spline, guiEvent );
            }
            else if( moveNodeState.Moving && (guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp) && guiEvent.button == 0 )
            {
                Vector2 screenDelta = TransformMouseDeltaToScreenDelta( guiEvent.delta );
                Vector3 newPoint = moveNodeState.CurrentPosition;

                bool physicsHit = false;
                if( guiEvent.command )
                {
                    // snap to physics
                    RaycastHit hit;
                    Ray mouseRay = MousePositionToRay( Camera.current, guiEvent.mousePosition );
                    if( Physics.Raycast( mouseRay, out hit ) )
                    {
                        physicsHit = true;
                        newPoint = hit.point;
                    }
                }

                if( !physicsHit )
                {
                    if( guiEvent.shift )
                    {
                        // move along up axis
                        Vector3 screenPoint = Camera.current.WorldToScreenPoint( moveNodeState.CurrentPosition ) + new Vector3( screenDelta.x, screenDelta.y, 0 );
                        Ray projectionRay = Camera.current.ScreenPointToRay( screenPoint );
                        Vector3 verticalPlaneNormal = Vector3.Cross( transform.up, Vector3.Cross( transform.up, projectionRay.direction ) );
                        Vector3 screenWorldPosition = MathHelper.LinePlaneIntersection( projectionRay, moveNodeState.CurrentPosition, verticalPlaneNormal );
                        newPoint = moveNodeState.CurrentPosition + transform.up * Vector3.Dot( transform.up, screenWorldPosition - moveNodeState.CurrentPosition );
                    }
                    else
                    {
                        // relative pointer tracking
                        Vector3 screenPoint = Camera.current.WorldToScreenPoint( moveNodeState.CurrentPosition ) + new Vector3( screenDelta.x, screenDelta.y, 0 );
                        newPoint = MathHelper.LinePlaneIntersection( Camera.current.ScreenPointToRay( screenPoint ), moveNodeState.CurrentPosition, transform.up );
                    }
                }

                moveNodeState.CurrentPosition = newPoint;

                Vector3 totalMovement = newPoint - moveNodeState.StartPosition;

                if( Mathf.Abs( moveNodeState.StartMovementAccumulator.magnitude ) < startMovementThreshold )
                {
                    moveNodeState.StartMovementAccumulator += guiEvent.delta;
                }
                else if( totalMovement.magnitude > 0 )
                {
                    Vector3 snappedMovement = totalMovement;

                    if( guiEvent.alt )
                    {
                        Vector3 newWorldPoint = totalMovement + moveNodeState.StartPosition;

                        Vector3 origin = transform.position;
                        if( moveNodeState.MovingNodeControlPoint )
                        {
                            origin = spline.GetNode( moveNodeState.NodeIndex ).position;
                        }

                        Vector3 filter = Vector3.one;
                        float gridScale = (moveNodeState.MovingNodeControlPoint) ? 0.2f : 1.0f;

                        Vector3 newSnappedWorldPoint = SnapWorldPointToGrid( newWorldPoint, origin, filter, gridScale );
                        snappedMovement = newSnappedWorldPoint - moveNodeState.StartPosition;
                    }

                    Vector3 snappedDelta = snappedMovement - moveNodeState.LastSnappedMovement;

                    Undo.RecordObject( spline.GetUndoObject(), "Move Nodes" );
                    if( moveNodeState.MovingNodeControlPoint )
                    {
                        SplineNode node = spline.GetNode( moveNodeState.NodeIndex );
                        if( moveNodeState.ControlPointId == MoveControlPointId.Unknown )
                        {
                            Vector3 worldControl = node.position + node.Control2;
                            float directionTestForward = 0;
                            float directionTestBackward = 0;
                            if( spline.GetNodeCount() > moveNodeState.NodeIndex + 1 || spline.IsLoop() )
                            {
                                int nextIndex = MathHelper.WrapIndex( moveNodeState.NodeIndex + 1, spline.GetNodeCount() );
                                directionTestForward = Vector3.Dot( totalMovement.normalized, (spline.GetNode( nextIndex ).position - node.position).normalized );
                            }

                            if( moveNodeState.NodeIndex - 1 >= 0 || spline.IsLoop() )
                            {
                                int nextIndex = MathHelper.WrapIndex( moveNodeState.NodeIndex - 1, spline.GetNodeCount() );
                                directionTestBackward = Vector3.Dot( totalMovement.normalized, (spline.GetNode( moveNodeState.NodeIndex - 1 ).position - node.position).normalized );
                            }

                            if( directionTestForward >= directionTestBackward )
                            {
                                moveNodeState.ControlPointId = MoveControlPointId.Control2;
                                worldControl = node.position + node.Control2;
                            }
                            else
                            {
                                moveNodeState.ControlPointId = MoveControlPointId.Control1;
                                worldControl = node.position + node.Control1;
                            }

                            controlPlanePosition = GetPointOnPlaneY( node.position, spline.GetTransform().up, worldControl );
                            controlPlaneOffset = worldControl - controlPlanePosition;
                        }

                        if( moveNodeState.ControlPointId == MoveControlPointId.Control1 )
                        {
                            node.Control1 = node.Control1 + snappedDelta;
                        }
                        else
                        {
                            node.Control2 = node.Control2 + snappedDelta;
                        }
                        spline.SetNode( moveNodeState.NodeIndex, node );
                    }
                    else
                    {
                        for( int i = 0; i < nodeSelection.Count; ++i )
                        {
                            int index = nodeSelection[i];
                            SplineNode node = spline.GetNode( index );
                            node.position = node.position + snappedDelta;
                            spline.SetNode( index, node );
                        }
                    }

                    moveNodeState.LastSnappedMovement = snappedMovement;

                    SplineNode latestNode = spline.GetNode( moveNodeState.NodeIndex );
                    planePosition = GetPointOnPlaneY( transform.position, transform.up, latestNode.position );
                    planeOffset = latestNode.position - planePosition;
                }

                if( guiEvent.type == EventType.MouseUp )
                {
                    moveNodeState = new MoveNodeState();
                }

                EditorUtility.SetDirty( spline.GetComponent() );
            }

            if( moveNodeState.Moving )
            {
                useEvent = true;
            }

            if( moveNodeState.MovingNodeControlPoint )
            {
                SplineNode node = spline.GetNode( moveNodeState.NodeIndex );
                if( ShowGrid || guiEvent.alt )
                {
                    DrawGrid( node.position, transform.forward, transform.right, 0.2f, 5, guiEvent );
                }
                DrawNodeControlPointMovementGuides( spline, node.position, node.Control1 + node.position, node.Control2 + node.position );
            }
        }

        // stop points disappearing into infinity!
        bool IsSafeToProjectFromPlane(Camera camera, Transform transform)
        {
            return Vector3.Dot( camera.transform.forward, transform.up ) < 0.95f;
        }
        bool TwoDimentionalMode(Camera camera, Transform transform)
        {
            return Vector3.Dot( camera.transform.up, transform.up ) < 0.95f;
        }

        static Vector3 GetPointOnPlaneY(Vector3 planePosition, Vector3 planeNormal, Vector3 point)
        {
            return MathHelper.LinePlaneIntersection( point, planeNormal, planePosition, planeNormal );
        }

        float DepthScale(Vector3 point, Vector3 cameraPosition)
        {
            IEditableSpline spline = GetSpline();
            return Vector3.Distance( spline.GetTransform().position, cameraPosition ) / Vector3.Distance( point, cameraPosition );
        }

        void DrawWireDisk(Vector3 point, Vector3 normal, float radius, Vector3 cameraPosition)
        {
            Handles.DrawWireDisc( point, normal, radius * DepthScale( point, cameraPosition ) );
        }

        void DebugLogEvent(Event guiEvent)
        {
            if( guiEvent.type != EventType.Layout && guiEvent.type != EventType.Repaint )
            {
                Debug.Log( guiEvent.type );
            }
        }
    }
}
