using System.Collections.Generic;using UnityEditor;using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines{


#if UNITY_EDITOR    public enum SplineEditMode    {        None,        AddNode,        MoveNode    }    public enum SplineAddNodeMode    {        Append,        Prepend,        Insert    }    public enum HandleCapSizeMode    {        WorldSpace,        ScreenSpace    }

    public enum GridSpace
    {
        Node,
        Spline,
        Global
    }    [CustomEditor( typeof( SplineComponent ) )]    public class SplineComponentEditor : SplineEditor    {    }    [CustomEditor( typeof( FenceBuilder ) )]    public class FenceBuilderEditor : SplineEditor    {    }    public static class SplineHandleUtility
    {        public static float HandleCapScale        {            get => EditorPrefs.GetFloat( "FantasticSplinesHandleCapScale", 1 );            set => EditorPrefs.SetFloat( "FantasticSplinesHandleCapScale", value );        }        public static HandleCapSizeMode HandleCapSizeMode        {            get => (HandleCapSizeMode)EditorPrefs.GetInt( "FantasticSplinesHandleCapSizeMode", (int)HandleCapSizeMode.ScreenSpace );            set => EditorPrefs.SetInt( "FantasticSplinesHandleCapSizeMode", (int)value );        }        public const float WorldSpaceHandleCapSize = 0.15f;        public const float ScreenSpaceHandleCapSize = 0.15f;
        public static float GetNodeHandleSize( Vector3 position )
        {
            switch( HandleCapSizeMode )
            {
                case HandleCapSizeMode.WorldSpace:
                    return WorldSpaceHandleCapSize * HandleCapScale;
                case HandleCapSizeMode.ScreenSpace:
                    return HandleUtility.GetHandleSize( position ) * ScreenSpaceHandleCapSize * HandleCapScale;
            }
            return ScreenSpaceHandleCapSize * HandleCapScale;
        }
    }    public class SplineEditor : Editor    {        IEditableSpline GetSpline()        {            return (target as IEditorSplineProxy).GetEditableSpline();        }        static List<SplineNode> clipboard = new List<SplineNode>();        static SplineEditMode editMode = SplineEditMode.None;        static SplineAddNodeMode addNodeMode = SplineAddNodeMode.Append;

        Vector3 planePosition;        Vector3 planeOffset;        Vector3 controlPlanePosition;        Vector3 controlPlaneOffset;

        const float diskRadiusScalar = 0.5f;        const float controlPointRadiusScalar = 0.75f;        float GetNodeHandleSize( Vector3 position )
        {
            return SplineHandleUtility.GetNodeHandleSize( position );
        }        float GetNodeDiscSize( Vector3 position )
        {
            return SplineHandleUtility.GetNodeHandleSize( position ) * 0.5f * diskRadiusScalar;
        }        float GetControlPointHandleSize( Vector3 position )
        {
            return SplineHandleUtility.GetNodeHandleSize( position ) * controlPointRadiusScalar;
        }        float GetControlDiscSize( Vector3 position )
        {
            return SplineHandleUtility.GetNodeHandleSize( position ) * 0.5f * controlPointRadiusScalar * diskRadiusScalar;
        }        public void OnEnable()        {            SceneView.duringSceneGui -= DoSceneGUI;            SceneView.duringSceneGui += DoSceneGUI;            EditorActive = true;            ResetEditMode();        }        public void OnDisable()        {            SceneView.duringSceneGui -= DoSceneGUI;            Tools.current = Tool.Move;        }        Bounds GetSelectionBounds(IEditableSpline spline)        {            if( nodeSelection.Count == 0 )            {                return GetBounds( spline );            }            Bounds bounds = new Bounds( spline.GetNode( nodeSelection[0] ).position, Vector3.one * 0.1f );            for( int i = 1; i < nodeSelection.Count; ++i )            {                bounds.Encapsulate( spline.GetNode( nodeSelection[i] ).position );            }            if( bounds.size.magnitude < 1 )            {                bounds.size = bounds.size.normalized * 1;            }            return bounds;        }        static Bounds GetBounds(IEditableSpline spline)        {            Bounds bounds = new Bounds( spline.GetNode( 0 ).position, Vector3.zero );            for( int i = 1; i < spline.GetNodeCount(); ++i )            {                bounds.Encapsulate( spline.GetNode( i ).position );            }            return bounds;        }        public void FrameCamera(IEditableSpline spline)        {            Bounds bounds = nodeSelection.Count > 0 ? GetSelectionBounds( spline ) : GetBounds( spline );            SceneView.lastActiveSceneView.Frame( bounds, false );        }        public void DeleteNodes(IEditableSpline spline, List<int> nodeIndicies)        {            int removePoints = nodeIndicies.Count;            for( int i = 0; i < removePoints; ++i )            {                RemoveNode( spline, nodeIndicies[0] );            }            ClearNodeSelection( spline );            EditorUtility.SetDirty( spline.GetComponent() );        }        void PasteCurve(IEditableSpline spline)        {            Debug.Log( "paste" );            int insertIndex = spline.GetNodeCount();            for (int i = 0; i < nodeSelection.Count; ++i)
            {
                if (nodeSelection[i] < insertIndex)
                {
                    insertIndex = nodeSelection[i];
                }
            }            int startInsertIndex = insertIndex;

            DeleteNodes( spline, nodeSelection );            for( int i = 0; i < clipboard.Count; i++ )            {                spline.InsertNode( clipboard[i], insertIndex );
                insertIndex++;
            }            nodeSelection.Clear();            for( int i = 0; i < clipboard.Count; i++ )            {                nodeSelection.Add(startInsertIndex+i);            }        }        public void CopyCurve(IEditableSpline spline)        {            clipboard.Clear();            List<int> copyIndicies = new List<int>( nodeSelection );            copyIndicies.Sort();            for( int i = 0; i < copyIndicies.Count; i++ )            {                int index = copyIndicies[i];                clipboard.Add( spline.GetNode( index ) );            }        }        public static void ShowScriptGUI(MonoBehaviour script)        {            if( script != null )            {                MonoScript theScript = MonoScript.FromMonoBehaviour( script );                using( new EditorGUI.DisabledScope( true ) )                {                    EditorGUILayout.ObjectField( "Script", theScript, script.GetType(), false );                }            }        }        void StartAddPointMode(SplineAddNodeMode mode)        {            ResetEditMode();            editMode = SplineEditMode.AddNode;            addNodeMode = mode;            IEditableSpline spline = GetSpline();            Transform transform = spline.GetTransform();            Vector3 adjoiningPointPosition = transform.position;            Vector3 gridUp = GetSnapGridUp( spline );            if( spline.GetNodeCount() > 0 )            {                int adjoiningIndex = 0;                if( addNodeMode == SplineAddNodeMode.Append )                {                    adjoiningIndex = spline.GetNodeCount() - 1;                }                adjoiningPointPosition = spline.GetNode( adjoiningIndex ).position;
            }            planePosition = MathsUtils.LinePlaneIntersection( adjoiningPointPosition, gridUp, GetSnapGridOrigin(spline), gridUp );            planeOffset = adjoiningPointPosition - planePosition;        }        void InspectorAddPointModeButton( SplineAddNodeMode mode, GUIStyle normal, GUIStyle selected, string buttonText = "" )        {            if( string.IsNullOrEmpty(buttonText))
            {
                buttonText = mode.ToString();
            }            if( editMode == SplineEditMode.AddNode && addNodeMode == mode )            {                if( GUILayout.Button( "Cancel " + buttonText, selected ) )                {                    ResetEditMode();                }            }            else if( GUILayout.Button(buttonText, normal) )            {
                doRepaint = true;                StartAddPointMode( mode );            }        }

        static bool DoToggleButton( bool value, string buttonText)        {
            return DoToggleButton( value, buttonText, value );
        }
        static bool DoToggleButton( bool value, string buttonText, bool activated )        {
            return DoToggleButton( value, buttonText, activated, Color.green, Color.white * 0.7f );
        }
        static bool DoToggleButton(bool value, string buttonText, bool activated, Color activeColor, Color inactiveColor )        {
            GUIStyle style = new GUIStyle( EditorStyles.miniButton );            style.normal.textColor = style.hover.textColor = style.active.textColor = activated ? activeColor : inactiveColor;            buttonText = value ? buttonText + " (On)" : buttonText + " (Off)";            if( GUILayout.Button( buttonText, style ) )            {                EditorApplication.QueuePlayerLoopUpdate();                value = !value;            }            return value;        }        static bool foldoutSplineEditor = true;        static bool foldoutGizmos = true;        static bool foldoutDebug = false;        static Vector3 nodeSetPosition;        static float nodeAutomaticTangentLength = 0.3f;        bool HasNodeType( IEditableSpline spline, List<int> selected, NodeType nodeType )
        {
            for( int i = 0; i < selected.Count; ++i )
            {
                if( spline.GetNode(selected[i]).NodeType == nodeType )
                {
                    return true;
                }
            }

            return false;
        }        public override void OnInspectorGUI()        {            DrawDefaultInspector();            IEditableSpline spline = GetSpline();            if( spline == null )            {                return;            }

            GUIStyle selected = new GUIStyle( EditorStyles.miniButton );
            selected.active.textColor = selected.hover.textColor = selected.normal.textColor = Color.green;
            GUIStyle normal = EditorStyles.miniButton;            GUILayout.Space( 20 );
            EditorGUI.BeginChangeCheck();
            EditorActive = DoToggleButton( EditorActive, "Spline Tool", EditorActive, Color.green, Color.red );
            foldoutSplineEditor = EditorGUILayout.Foldout( foldoutSplineEditor, "Spline Editor" );            if( EditorGUI.EndChangeCheck() )
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
            if( foldoutSplineEditor )            {                GUILayout.Label( "Spline Settings" );                string loopText = spline.IsLoop() ? "Looped" : "Unlooped";                if( GUILayout.Button( loopText ) )                {
                    doRepaint = true;                    Undo.RecordObjects( spline.GetUndoObjects(), "Loop Toggle" );                    spline.SetLoop( !spline.IsLoop() );                    EditorUtility.SetDirty( spline.GetComponent() );                }
                GUILayout.Label( "Edit Modes" );                EditorGUILayout.BeginHorizontal();                InspectorAddPointModeButton( SplineAddNodeMode.Prepend, normal, selected );                InspectorAddPointModeButton( SplineAddNodeMode.Insert, normal, selected );                InspectorAddPointModeButton(SplineAddNodeMode.Append, normal, selected );                EditorGUILayout.EndHorizontal();                ShowNodeControls = DoToggleButton( ShowNodeControls, "Edit Control Points" );                GUILayout.Space( 10 );                GUILayout.Label( "Set Selected Node Type" );                EditorGUI.DisabledGroupScope selectionDisableGroup = new EditorGUI.DisabledGroupScope( nodeSelection.Count <= 0 );                EditorGUILayout.BeginHorizontal();                {                    if (GUILayout.Button("Automatic", HasNodeType(spline, nodeSelection, NodeType.Automatic ) ? selected : normal ) )                    {                        Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Automatic");                        SplineEditorTools.SetNodeType(spline, NodeType.Automatic, nodeSelection);                        SplineEditorTools.Smooth(spline, nodeSelection);                    }                    if ( GUILayout.Button( "Point", HasNodeType(spline, nodeSelection, NodeType.Point) ? selected : normal ) )                    {                        Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Point Nodes" );                        SplineEditorTools.SetNodeType( spline, NodeType.Point, nodeSelection );                    }                    if ( GUILayout.Button( "Mirror", HasNodeType(spline, nodeSelection, NodeType.Mirrored) ? selected : normal ) )                    {                        Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Mirror Nodes" );                        SplineEditorTools.SetNodeType( spline, NodeType.Mirrored, nodeSelection );                    }                    if( GUILayout.Button( "Aligned", HasNodeType(spline, nodeSelection, NodeType.Aligned) ? selected : normal ) )                    {                        Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Aligned Nodes" );                        SplineEditorTools.SetNodeType( spline, NodeType.Aligned, nodeSelection );                    }                    if( GUILayout.Button( "Free", HasNodeType(spline, nodeSelection, NodeType.Free) ? selected : normal ) )                    {                        Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Free Nodes" );                        SplineEditorTools.SetNodeType( spline, NodeType.Free, nodeSelection );                    }                    if( GUILayout.Button( "Simplify" ) )                    {                        Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Simplify Nodes" );                        SplineEditorTools.SimplifyNodeType( spline, nodeSelection );                    }                }                EditorGUILayout.EndHorizontal();                // set position tool                // this could be removed and replaced in a Tool with inscene handle gui elements.                EditorGUILayout.BeginHorizontal();                {                    EditorGUI.BeginChangeCheck();                    if( nodeSelection.Count > 0 )                    {                        nodeSetPosition = spline.GetNode( nodeSelection[0] ).position;                    }                    Vector3 beforeChange = nodeSetPosition;                    nodeSetPosition = spline.GetTransform().InverseTransformPoint( nodeSetPosition );                    nodeSetPosition = EditorGUILayout.Vector3Field( "Set Position", nodeSetPosition );                    nodeSetPosition = spline.GetTransform().TransformPoint( nodeSetPosition );                    if( EditorGUI.EndChangeCheck() )                    {                        bool changeX = !Mathf.Approximately( beforeChange.x, nodeSetPosition.x );                        bool changeY = !Mathf.Approximately( beforeChange.y, nodeSetPosition.y );                        bool changeZ = !Mathf.Approximately( beforeChange.z, nodeSetPosition.z );                        if( changeX )                        {                            Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Set Node X Position" );                            SplineEditorTools.SetNodesXPosition( spline, nodeSelection, nodeSetPosition.x );                        }                        if( changeY )                        {                            Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Set Node Y Position" );                            SplineEditorTools.SetNodesYPosition( spline, nodeSelection, nodeSetPosition.y );                        }                        if( changeZ )                        {                            Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Set Node Z Position" );                            SplineEditorTools.SetNodesZPosition( spline, nodeSelection, nodeSetPosition.z );                        }                    }                }                EditorGUILayout.EndHorizontal();                EditorGUILayout.BeginHorizontal();                {
                    GUILayout.Label( "Automatic Tangent Length" );                    float nodeAutomaticTangentLengthBefore = nodeAutomaticTangentLength;                    if( nodeSelection.Count > 0 )                    {                        nodeAutomaticTangentLengthBefore = spline.GetNode( nodeSelection[0] ).automaticTangentLength;                    }
                    nodeAutomaticTangentLength = EditorGUILayout.Slider( nodeAutomaticTangentLengthBefore, 0, 1 );
                    bool change = !Mathf.Approximately( nodeAutomaticTangentLengthBefore, nodeAutomaticTangentLength );
                    if( change )
                    {
                        Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Set Automatic Tangent Length" );
                        SplineEditorTools.SetNodesAutomaticTangentLegnth( spline, nodeSelection, nodeAutomaticTangentLength );
                    }
                }
                EditorGUILayout.EndHorizontal();                selectionDisableGroup.Dispose();                GUILayout.Space( 10 );                GUILayout.Label( "Spline Tools" );                List<int> nodeIndicies = GetSelectedNodeOrAllIndicies( spline );                string workOn = nodeSelection.Count > 0 ? "Selection" : "Spline";                if( GUILayout.Button( "Smooth " + workOn ) )                {                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Smooth " + workOn );                    SplineEditorTools.Smooth( spline, nodeIndicies );
                    EditorApplication.QueuePlayerLoopUpdate();                }                if( GUILayout.Button( "Arc Smooth " + workOn ) )                {                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Arc Smooth " + workOn );                    SplineEditorTools.ArcSmooth( spline );
                    EditorApplication.QueuePlayerLoopUpdate();                }                if( GUILayout.Button( "Flatten " + workOn ) )                {                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Flatten " + workOn );                    SplineEditorTools.Flatten( spline, nodeIndicies );
                    EditorApplication.QueuePlayerLoopUpdate();                }                if( GUILayout.Button( "Simplify " + workOn ) )                {                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Simplify " + workOn );                    SplineEditorTools.Simplify( spline, nodeIndicies );
                    EditorApplication.QueuePlayerLoopUpdate();                }                if( GUILayout.Button( "Reverse " + workOn ) )                {                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Reverse " + workOn );                    SplineEditorTools.Reverse( spline );
                    EditorApplication.QueuePlayerLoopUpdate();                }                GUILayout.Space( 10 );                // grid                {                    GUILayout.Label( "Snap to Grid (" + snapGridModifierString + ")" );                    GUILayout.BeginHorizontal();                    if( GUILayout.Button( "Global Space", GridSpace == GridSpace.Global ? selected : normal ) )
                    {
                        GridSpace = GridSpace.Global;
                        EditorApplication.QueuePlayerLoopUpdate();
                    }                    if( GUILayout.Button( "Spline Space", GridSpace == GridSpace.Spline ? selected : normal ) )
                    {
                        GridSpace = GridSpace.Spline;
                        EditorApplication.QueuePlayerLoopUpdate();
                    }                    if( GUILayout.Button( "Node Space", GridSpace == GridSpace.Node ? selected : normal ) )
                    {
                        GridSpace = GridSpace.Node;
                        EditorApplication.QueuePlayerLoopUpdate();
                    }                    GUILayout.EndHorizontal();                    GUILayout.BeginHorizontal();                    ShowGrid = DoToggleButton( ShowGrid, "Show Grid" );                    SnapToHalfGrid = DoToggleButton( SnapToHalfGrid, "Half Grid Snap" );                    GUILayout.EndHorizontal();                    EditorGUI.BeginChangeCheck();                    UniformSnapGrid = DoToggleButton( UniformSnapGrid, "Uniform Grid Snap Scale" );                    if( UniformSnapGrid )
                    {
                        float scalar = EditorGUILayout.FloatField( "Uniform Grid Scale", SnapToGridScale.x );
                        Vector3 grid = Vector3.one * scalar;
                        SnapToGridScale = grid;
                    }                    else
                    {                        SnapToGridScale = EditorGUILayout.Vector3Field( "", SnapToGridScale );
                    }                    if( EditorGUI.EndChangeCheck() )
                    {
                        EditorApplication.QueuePlayerLoopUpdate();
                    }                    GUILayout.BeginHorizontal();                    SnapToGridX = DoToggleButton( SnapToGridX, "Snap X", SnapToGridXActive );                    SnapToGridY = DoToggleButton( SnapToGridY, "Snap Y", SnapToGridYActive );                    SnapToGridZ = DoToggleButton( SnapToGridZ, "Snap Z", SnapToGridZActive );                    GUILayout.EndHorizontal();                    GUILayout.Space( 10 );                }                foldoutGizmos = EditorGUILayout.Foldout( foldoutGizmos, "Gizmos" );                if( foldoutGizmos )                {                    EditorGUI.BeginChangeCheck();                    spline.SetColor( EditorGUILayout.ColorField( "Spline Colour", spline.GetColor() ) );                    GUILayout.BeginHorizontal();                    SplineHandleUtility.HandleCapScale = EditorGUILayout.Slider( "Node Gizmo Size", SplineHandleUtility.HandleCapScale, 0.1f, 10 );                    spline.SetGizmoScale( SplineHandleUtility.HandleCapScale );                    SplineHandleUtility.HandleCapSizeMode = (HandleCapSizeMode)EditorGUILayout.EnumPopup( SplineHandleUtility.HandleCapSizeMode, GUILayout.Width(100) );                    GUILayout.EndHorizontal();                    spline.SetZTest( DoToggleButton( spline.GetZTest(), "ZTest" ) );                    ShowSplineInfo = DoToggleButton( ShowSplineInfo, "Spline Info" );                    ShowProjectionLines = DoToggleButton( ShowProjectionLines, "Projection Lines" );                    ShowProjectedSpline = DoToggleButton( ShowProjectedSpline, "Projected Spline" );                    ShowSelectionDisks = DoToggleButton( ShowSelectionDisks, "Selection Disks" );                    if( EditorGUI.EndChangeCheck() )
                    {
                        EditorUtility.SetDirty( spline.GetComponent() );
                    }                    GUILayout.Space( 10 );                }                foldoutDebug = EditorGUILayout.Foldout( foldoutDebug, "Debug" );                if( foldoutDebug )                {                    GUILayout.Label( "Editor State" );                    GUILayout.Label( "Edit Mode: " + editMode.ToString() );                    GUILayout.Label( "Add Point Mode: " + addNodeMode.ToString() );                    GUILayout.Label( "Moving Point: " + moveNodeState.Moving.ToString() );                    GUILayout.Space( 10 );                    GUILayout.Label( "Spline Info" );                    GUILayout.Label( "Length: " + spline.GetLength() );                    GUILayout.Label( "Node Count: " + spline.GetNodeCount() );                }            }        }        public static bool EditorActive
        {            get => EditorPrefs.GetBool( "FantasticSplinesEditorActive", true );            set => EditorPrefs.SetBool( "FantasticSplinesEditorActive", value );
        }        public static bool ShowSplineInfo        {            get => EditorPrefs.GetBool( "FantasticSplinesShowSplineInfo", false );            set => EditorPrefs.SetBool( "FantasticSplinesShowSplineInfo", value );        }        public static bool ShowNodeControls        {            get => EditorPrefs.GetBool( "FantasticSplinesShowNodeControls", true );            set => EditorPrefs.SetBool( "FantasticSplinesShowNodeControls", value );        }        public static bool ShowProjectionLines        {            get => EditorPrefs.GetBool( "FantasticSplinesShowProjectionLines", true );            set => EditorPrefs.SetBool( "FantasticSplinesShowProjectionLines", value );        }        public static bool ShowProjectedSpline        {            get => EditorPrefs.GetBool( "FantasticSplinesShowProjectedSpline", true );            set => EditorPrefs.SetBool( "FantasticSplinesShowProjectedSpline", value );        }        public static bool ShowSelectionDisks        {            get => EditorPrefs.GetBool( "FantasticSplinesShowSelectionDisks", true );            set => EditorPrefs.SetBool( "FantasticSplinesShowSelectionDisks", value );        }        public static GridSpace GridSpace        {            get => (GridSpace)EditorPrefs.GetInt( "FantasticSplinesGridSpace", (int)GridSpace.Spline );            set => EditorPrefs.SetInt( "FantasticSplinesGridSpace", (int)value );        }        public static bool ShowGrid        {            get => EditorPrefs.GetBool( "FantasticSplinesShowGrid", false );            set => EditorPrefs.SetBool( "FantasticSplinesShowGrid", value );        }        public static bool SnapToGridX        {            get => EditorPrefs.GetBool( "FantasticSplinesSnapToGridX", true );            set => EditorPrefs.SetBool( "FantasticSplinesSnapToGridX", value );        }
        public static bool SnapToGridXActive        {
            get
            {
                return SnapToGridX && !MoveVerticalModifier( Event.current );
            }
        }        public static bool SnapToGridY        {            get => EditorPrefs.GetBool( "FantasticSplinesSnapToGridY", true );            set => EditorPrefs.SetBool( "FantasticSplinesSnapToGridY", value );        }
        public static bool SnapToGridYActive        {
            get
            {
                return SnapToGridY && MoveVerticalModifier( Event.current );
            }
        }        public static bool SnapToGridZ        {            get => EditorPrefs.GetBool( "FantasticSplinesSnapToGridZ", true );            set => EditorPrefs.SetBool( "FantasticSplinesSnapToGridZ", value );        }
        public static bool SnapToGridZActive        {
            get
            {
                return SnapToGridZ && !MoveVerticalModifier( Event.current );
            }
        }        public static bool SnapToHalfGrid        {            get => EditorPrefs.GetBool( "FantasticSplinesSnapToHalfGrid", false );            set => EditorPrefs.SetBool( "FantasticSplinesSnapToHalfGrid", value );        }        public static bool UniformSnapGrid        {            get => EditorPrefs.GetBool( "FantasticSplinesUniformSnapGrid", true );            set => EditorPrefs.SetBool( "FantasticSplinesUniformSnapGrid", value );        }        public static Vector3 SnapToGridScale        {            get            {                string sVector = EditorPrefs.GetString( "GridSnapScale", Vector3.one.ToString() );                // Remove the parentheses                if( sVector.StartsWith( "(", System.StringComparison.Ordinal ) && sVector.EndsWith( ")", System.StringComparison.Ordinal ) )                {                    sVector = sVector.Substring( 1, sVector.Length - 2 );                }                // split the items                string[] sArray = sVector.Split( ',' );                // store as a Vector3                Vector3 result = new Vector3(                    float.Parse( sArray[0] ),                    float.Parse( sArray[1] ),                    float.Parse( sArray[2] ) );                return result;            }            set => EditorPrefs.SetString( "GridSnapScale", value.ToString() );        }        float rightClickStart;        const float rightClickTime = 0.2f;        bool useEvent = false;        static bool doRepaint = false;        Tool lastTool = Tool.Move;        void SetTool(Tool newTool)        {            lastTool = newTool;            if( nodeSelection.Count == 0 )            {                Tools.current = lastTool;            }        }        Vector2 rightClickMovement = Vector2.zero;        void RightClickCancel(IEditableSpline spline, Event guiEvent)        {            if( guiEvent.button == 1 && guiEvent.type == EventType.MouseDown )            {                rightClickMovement = Vector2.zero;                rightClickStart = Time.unscaledTime;            }            if( guiEvent.button == 1 && guiEvent.type == EventType.MouseDrag )            {                rightClickMovement += guiEvent.delta;                // cancel right click                rightClickStart = 0;            }            if( startMovementThreshold > rightClickMovement.magnitude                && guiEvent.button == 1 && guiEvent.type == EventType.MouseUp )            {                if( Time.unscaledTime - rightClickStart < rightClickTime )                {                    ResetEditMode();                }            }        }        void KeyboardInputs(IEditableSpline spline, Event guiEvent)        {            if( guiEvent.type == EventType.KeyDown )            {                bool commandOrControl = ShortcutModifer( guiEvent );                if ( editMode != SplineEditMode.None )                {                    if( guiEvent.keyCode == KeyCode.Escape )                    {                        guiEvent.Use();                        ResetEditMode();                    }                }                if( guiEvent.keyCode == KeyCode.W )                {                    SetTool( Tool.Move );                    guiEvent.Use();                }                if( guiEvent.keyCode == KeyCode.E )                {                    SetTool( Tool.Rotate );                    guiEvent.Use();                }                if( guiEvent.keyCode == KeyCode.R )                {                    SetTool( Tool.Scale );                    guiEvent.Use();                }                if( guiEvent.keyCode == KeyCode.F )                {                    FrameCamera( spline );                    guiEvent.Use();                }                if(commandOrControl && guiEvent.keyCode == KeyCode.A )                {                    nodeSelection.Clear();                    for( int i = 0; i < spline.GetNodeCount(); ++i )                    {                        nodeSelection.Add( i );                    }                    guiEvent.Use();                }                if( nodeSelection.Count > 0                    && commandOrControl && guiEvent.keyCode == KeyCode.C )                {                    CopyCurve( spline );                    guiEvent.Use();                }                if( commandOrControl && guiEvent.keyCode == KeyCode.X )                {                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Cut Nodes" );                    CopyCurve( spline );                    DeleteNodes( spline, nodeSelection );                    guiEvent.Use();                }                if( commandOrControl && guiEvent.keyCode == KeyCode.V )                {                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Paste Nodes" );                    PasteCurve( spline );                    guiEvent.Use();                }                if( nodeSelection.Count > 0                    && (guiEvent.keyCode == KeyCode.Delete || guiEvent.keyCode == KeyCode.Backspace) )                {                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Delete Nodes" );                    DeleteNodes( spline, nodeSelection );                    guiEvent.Use();                }            }        }        void RemoveNode(IEditableSpline spline, int index)        {            spline.RemoveNode( index );            for( int i = nodeSelection.Count - 1; i >= 0; --i )            {                if( nodeSelection[i] == index )                {                    nodeSelection.RemoveAt( i );                    continue;                }                if( nodeSelection[i] > index )                {                    nodeSelection[i]--;                }            }        }        void SceneViewEventSetup(Event guiEvent)        {
            useEvent = false;
            if( guiEvent.type == EventType.MouseEnterWindow )            {                SceneView.currentDrawingSceneView.Focus();            }            if( nodeSelection.Count > 0 )            {                lastTool = Tools.current;                Tools.current = Tool.None;                editMode = SplineEditMode.MoveNode;            }            else            {                Tools.current = lastTool;            }        }        void DoSceneViewInput( IEditableSpline spline, Event guiEvent )
        {

            //hack click deselect node.
            // maintain as selected active object if we click when we have a node selected
            if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )            {                hadSelectionOnMouseDown = nodeSelection.Count > 0;            }            if( guiEvent.type == EventType.Used && (nodeSelection.Count > 0 || hadSelectionOnMouseDown) )            {                Selection.activeObject = (target as Component).gameObject;            }            else if( guiEvent.type != EventType.Repaint && guiEvent.type != EventType.Layout && guiEvent.type != EventType.Used && !IsMouseButtonEvent( guiEvent, 0 ) )            {                hadSelectionOnMouseDown = false;            }            SceneViewEventSetup( guiEvent );            RightClickCancel( spline, guiEvent );            KeyboardInputs( spline, guiEvent );            if( guiEvent.isMouse            || guiEvent.type == EventType.Layout || guiEvent.type == EventType.Repaint )            {                bool edited = false;                switch( editMode )                {                    case SplineEditMode.None:                        DoMoveNode( spline, guiEvent );                        edited = true;                        break;                    case SplineEditMode.MoveNode:                        DoMoveNode( spline, guiEvent );                        edited = true;                        break;                    case SplineEditMode.AddNode:                        if( addNodeMode == SplineAddNodeMode.Insert                            && spline.GetNodeCount() >= 2 )                        {                            DoInsertNode( spline, guiEvent );
                            edited = true;                        }                        else                        {                            DoAddNode( spline, guiEvent );
                            edited = true;                        }                        break;                }                if( edited )
                {
                    SplineEditorTools.AutomaticSmooth( spline );
                }            }



            // hacks: intercept unity object drag select
            Vector3 handlePosition = Camera.current.transform.position + Camera.current.transform.forward * 10;            float handleSize = HandleUtility.GetHandleSize( handlePosition ) * 15;            if( useEvent                || (editMode == SplineEditMode.AddNode && guiEvent.button == 0)                || (DragSelectionModifier( guiEvent ) && (guiEvent.type == EventType.Layout || guiEvent.type == EventType.Repaint)) )            {                if( Handles.Button( handlePosition, Camera.current.transform.rotation, 0, handleSize, Handles.DotHandleCap ) )                {                    guiEvent.Use();                }            }
        }        void DoSceneViewDraw( IEditableSpline spline, Event guiEvent )
        {
            // draw things
            if( guiEvent.type == EventType.Repaint )            {                if( EditorActive && !moveNodeState.MovingNodeControlPoint && (ShowGrid || SnapToGridModifier( guiEvent )) )                {                    DrawGrid( spline );                }                Handles.zTest = spline.GetZTest() ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always;                DrawSpline( spline );                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;            }            if( guiEvent.isMouse || doRepaint )            {                SceneView.currentDrawingSceneView.Repaint();            }            doRepaint = false;
        }        void DoSceneGUI(SceneView view)        {            if( target == null )            {                return;            }            Event guiEvent = Event.current;            IEditableSpline spline = GetSpline();            if( spline == null )            {                return;            }            if( spline.GetTransform() == null )            {                return;            }            if( EditorActive ) DoSceneViewInput( spline, guiEvent );             DoSceneViewDraw( spline, guiEvent );            Repaint();        }

        Color horizontalGridColour = new Color( .7f, .7f, 1, .2f );
        void DrawGrid( IEditableSpline spline )        {
            Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix(spline);

            Vector3 origin = gridMatrix.MultiplyPoint( Vector3.zero );
            Vector3 forward = gridMatrix.MultiplyVector( Vector3.forward );
            Vector3 right = gridMatrix.MultiplyVector( Vector3.right );

            DrawGrid( origin, forward, right, 1, 10 );
            DrawGrid( origin, forward, right, 1, 10 );        }        void DrawGrid(Vector3 origin, Vector3 forward, Vector3 right, float scale, int largeGridInterval)        {            DrawGridLines( origin, forward, right, 1f * scale, 50, 50, horizontalGridColour );            DrawGridLines( origin, forward, right, largeGridInterval * scale, 5, 5, horizontalGridColour );        }        void DrawGridLines(Vector3 origin, Vector3 forward, Vector3 right, float spacing, int halfLength, int halfWidth, Color color)        {            Handles.color = color;            DrawGridLines( origin, halfLength * spacing * forward, SnapToGridScale.x * spacing * right, halfWidth );            DrawGridLines( origin, Mathf.Max( 0.2f, halfWidth ) * spacing * right, SnapToGridScale.z * spacing * forward, halfLength );            Handles.color = Color.white;        }        void DrawGridLines(Vector3 origin, Vector3 halfLength, Vector3 spacing, int halfCount)        {            Vector3 lineStart = origin - halfLength - halfCount * spacing;            Vector3 lineEnd = origin + halfLength - halfCount * spacing;            for( int i = 0; i < halfCount * 2 + 1; ++i )            {                Handles.DrawLine( lineStart, lineEnd );                lineStart = lineStart + spacing;                lineEnd = lineEnd + spacing;            }        }        static Vector2 TransformMouseDeltaToScreenDelta(Vector2 mouseDelta)        {            mouseDelta.y = -mouseDelta.y;            return mouseDelta * EditorGUIUtility.pixelsPerPoint;        }        static Vector3 TransformMousePositionToScreenPoint(Camera camera, Vector3 mousePosition)        {            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;            mousePosition.y = camera.pixelHeight - mousePosition.y * pixelsPerPoint;            mousePosition.x *= pixelsPerPoint;            return mousePosition;        }        static Ray MousePositionToRay(Camera camera, Vector3 mousePosition)        {            return camera.ScreenPointToRay( TransformMousePositionToScreenPoint( camera, mousePosition ) );        }        void ResetEditMode()        {            lastTool = Tool.Move;            Tools.current = Tool.Move;            ClearNodeSelection( GetSpline() );            editMode = SplineEditMode.None;            planeOffset = Vector3.zero;            controlPlanePosition = Vector3.zero;            addNodeState = AddNodeState.NodePosition;        }        void DrawSplineNodes(IEditableSpline spline)        {            List<int> sortedNodeIndicies = GetNodeIndiciesSelectedFirst( spline );            sortedNodeIndicies.Reverse(); // draw selected on top            for( int sortedI = 0; sortedI < sortedNodeIndicies.Count; ++sortedI )            {                int i = sortedNodeIndicies[sortedI];                if( nodeSelection.Contains( i ) )                {                    Handles.color = Color.green;                }                else                {                    Handles.color = Color.white;                }                SplineNode node = spline.GetNode( i );                Vector3 point = node.position;                Handles.SphereHandleCap( 0, point, spline.GetTransform().rotation, SplineHandleUtility.GetNodeHandleSize( point ), EventType.Repaint );                SplineResult result = spline.GetResultAtNode( i );
                Vector3 tangent = result.tangent;
                if( tangent.sqrMagnitude == 0 )
                {
                    tangent = spline.GetTransform().forward;
                }                if( i == 0 )
                {
                    // start node.
                    using( new Handles.DrawingScope( Color.green ) )
                    {
                        Handles.ConeHandleCap( 0, point, Quaternion.LookRotation( tangent ), SplineHandleUtility.GetNodeHandleSize( point ) * 0.5f, EventType.Repaint );
                    }
                }                if( i == spline.GetNodeCount()-1 )
                {
                    // start node.
                    using( new Handles.DrawingScope( Color.grey ) )
                    {
                        Handles.ConeHandleCap( 0, point, Quaternion.LookRotation( tangent ), SplineHandleUtility.GetNodeHandleSize( point ) * 0.5f, EventType.Repaint );
                    }
                }            }            Handles.color = Color.white;        }        void DrawSplinePlaneProjectionLines(IEditableSpline spline)        {            if( !ShowProjectionLines )            {                return;            }            for( int i = 0; i < spline.GetNodeCount(); ++i )            {                Vector3 point = spline.GetNode( i ).position;

                Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix( spline );                Vector3 planePoint = MathsUtils.ProjectPointOnPlane( gridMatrix.MultiplyPoint( Vector3.zero ), gridMatrix.MultiplyVector( Vector3.up ), point );                Handles.DrawDottedLine( planePoint, point, 2 );            }        }        void DrawSplineLines(IEditableSpline spline)        {            for( int i = 0; i < spline.GetNodeCount(); ++i )            {                Vector3 point = spline.GetNode( i ).position;                Vector3 pointOnPlane = MathsUtils.ProjectPointOnPlane( spline.GetTransform().position, spline.GetTransform().up, point );                if( i < spline.GetNodeCount() - 1 )                {                    Vector3 nextPoint = spline.GetNode( i + 1 ).position;                    Handles.DrawLine( nextPoint, point );                }            }        }        static void DrawBezierSegment(SplineNode node1, SplineNode node2, Color color)        {            Handles.DrawBezier( node1.position, node2.position, node1.position + node1.LocalOutControlPoint, node2.position + node2.LocalInControlPoint, color, null, 3 );        }        static void DrawBezierSegmentOnPlane(Vector3 planeOrigin, Vector3 planeNormal, SplineNode node1, SplineNode node2)        {            Vector3 pointOnPlane1 = MathsUtils.ProjectPointOnPlane( planeOrigin, planeNormal, node1.position );            Vector3 pointOnPlane2 = MathsUtils.ProjectPointOnPlane( planeOrigin, planeNormal, node2.position );            Vector3 tangentFlat1 = MathsUtils.ProjectPointOnPlane( planeOrigin, planeNormal, node1.position + node1.LocalOutControlPoint );            Vector3 tangentFlat2 = MathsUtils.ProjectPointOnPlane( planeOrigin, planeNormal, node2.position + node2.LocalInControlPoint );            Handles.DrawBezier( pointOnPlane1, pointOnPlane2, tangentFlat1, tangentFlat2, Color.grey, null, 2.5f );        }        void DrawBezierSplineLines(IEditableSpline spline)        {            for( int i = 1; i < spline.GetNodeCount(); ++i )            {                DrawBezierSegment( spline.GetNode( i - 1 ), spline.GetNode( i ), spline.GetColor() );            }            if( spline.IsLoop() && spline.GetNodeCount() > 1 )            {                DrawBezierSegment( spline.GetNode( spline.GetNodeCount() - 1 ), spline.GetNode( 0 ), Color.Lerp( spline.GetColor(), Color.red, 0.5f ) );            }        }        void DrawBezierPlaneProjectedSplineLines(IEditableSpline spline)        {            if( !ShowProjectedSpline )            {                return;            }

            Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix( spline );            for( int i = 1; i < spline.GetNodeCount(); ++i )            {                DrawBezierSegmentOnPlane( gridMatrix.MultiplyPoint( Vector3.zero ), gridMatrix.MultiplyVector( Vector3.up ), spline.GetNode( i - 1 ), spline.GetNode( i ) );            }            if( spline.IsLoop() && spline.GetNodeCount() > 1 )            {                DrawBezierSegmentOnPlane( gridMatrix.MultiplyPoint( Vector3.zero ), gridMatrix.MultiplyVector( Vector3.up ), spline.GetNode( spline.GetNodeCount() - 1 ), spline.GetNode( 0 ) );            }        }        void DrawSplinePlaneProjectedSplineLines(IEditableSpline spline)        {            for( int i = 0; i < spline.GetNodeCount(); ++i )            {                Vector3 point = spline.GetNode( i ).position;
                Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix( spline );
                Vector3 pointOnPlane = MathsUtils.ProjectPointOnPlane( gridMatrix.MultiplyPoint( Vector3.zero ), gridMatrix.MultiplyVector( Vector3.up ), point );                if( i < spline.GetNodeCount() - 1 )                {                    Vector3 nextPoint = spline.GetNode( i + 1 ).position;
                    Vector3 nextPointOnPlane = MathsUtils.ProjectPointOnPlane( gridMatrix.MultiplyPoint( Vector3.zero ), gridMatrix.MultiplyVector( Vector3.up ), nextPoint );                    Handles.DrawDottedLine( pointOnPlane, nextPointOnPlane, 2 );                }            }        }        void DrawSplineSelectionDisks(IEditableSpline spline)        {            if( !ShowSelectionDisks )            {                return;            }            ValidateNodeSelection( spline );            Transform transform = spline.GetTransform();            Handles.color = Color.grey;            for( int i = 0; i < nodeSelection.Count; ++i )            {                int index = nodeSelection[i];                Vector3 point = spline.GetNode( index ).position;                Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix( spline );                Vector3 gridUp = GetSnapGridUp(spline);                Vector3 planePoint = MathsUtils.ProjectPointOnPlane( gridMatrix.MultiplyPoint( Vector3.zero ), gridUp, point );                DrawWireDisk( point, gridUp, GetNodeDiscSize( point ) * spline.GetGizmoScale(), Camera.current.transform.position );                DrawWireDisk( planePoint, gridUp, GetNodeDiscSize( planePoint ) * spline.GetGizmoScale(), Camera.current.transform.position );                RaycastHit hitDown;                Ray down = new Ray( point, -gridUp );                if( Physics.Raycast( down, out hitDown ) )                {                    DrawWireDisk( hitDown.point, hitDown.normal, GetNodeDiscSize( hitDown.point ) * spline.GetGizmoScale(), Camera.current.transform.position );                    Handles.DrawDottedLine( planePoint, hitDown.point, 2 );                }            }        }        Color ControlPointEditColor => (Color.cyan + Color.grey + Color.grey) * 0.33f;        void DrawNodeControlPoints(IEditableSpline spline, int index)        {            SplineNode node = spline.GetNode( index );            if( node.NodeType == NodeType.Point )            {                return;            }            if (node.NodeType == NodeType.Automatic)            {                return;            }            if ( nodeSelection.Contains( index ) )            {                Handles.color = ControlPointEditColor;            }            else            {                Handles.color = Color.grey;            }            Vector3 control1 = node.position + node.LocalInControlPoint;            Vector3 control2 = node.position + node.LocalOutControlPoint;            if( index > 0 || spline.IsLoop() )            {                Handles.SphereHandleCap( 0, control1, Quaternion.identity, GetControlPointHandleSize( control1 ), EventType.Repaint );                Handles.DrawLine( node.position, control1 );            }            if( index < spline.GetNodeCount() - 1 || spline.IsLoop() )            {                Handles.SphereHandleCap( 0, control2, Quaternion.identity, GetControlPointHandleSize( control2 ), EventType.Repaint );                Handles.DrawLine( node.position, control2 );            }        }        void DrawSplineSelectionNodeControlPoints( IEditableSpline spline, List<int> nodeIndicies )        {            for( int i = 0; i < nodeIndicies.Count; ++i )            {                DrawNodeControlPoints( spline, nodeIndicies[i] );            }        }        void DrawSplineSelectionNodeControlPoints(IEditableSpline spline)        {            if( !ShowNodeControls )            {                return;            }            DrawSplineSelectionNodeControlPoints( spline, GetSelectedNodeOrAllIndicies( spline ) );        }        void DrawSpline(IEditableSpline spline)        {            if( EditorActive ) DrawBezierPlaneProjectedSplineLines( spline );            if( EditorActive ) DrawSplinePlaneProjectionLines( spline );            if( EditorActive ) DrawSplineSelectionDisks( spline );            DrawBezierSplineLines( spline );            if( EditorActive ) DrawSegmentLengths( spline );            if( EditorActive ) DrawNodeCoordinates( spline );            DrawSplineNodes( spline );            if( EditorActive ) DrawSplineSelectionNodeControlPoints( spline );        }        void DrawSegmentLengths(IEditableSpline spline)        {            if( ShowSplineInfo )            {                spline.DrawSegmentLengths();            }        }        void DrawNodeCoordinates( IEditableSpline spline )        {            if( ShowSplineInfo )            {                spline.DrawNodeCoordinates( GridSpace == GridSpace.Global ? Space.World : Space.Self );            }        }        bool IsIndexInRange(IEditableSpline spline, int index)        {            return index >= 0 && index < spline.GetNodeCount();        }        static List<int> nodeSelection = new List<int>();        void SelectionAddNode(IEditableSpline spline, int index)        {            ValidateNodeSelection( spline );            if( !IsIndexInRange( spline, index ) )            {                return;            }            if( nodeSelection.Contains( index ) )            {                return;            }            nodeSelection.Add( index );        }        void ValidateNodeSelection(IEditableSpline spline)        {            for( int i = nodeSelection.Count - 1; i >= 0; i-- )            {                if( !IsIndexInRange( spline, nodeSelection[i] ) )                {                    nodeSelection.RemoveAt( i );                }            }        }        void ClearNodeSelection(IEditableSpline spline)        {            if(spline == null)            {                return;            }            nodeSelection.Clear();            editMode = SplineEditMode.None;        }        List<int> GetSelectedNodeOrAllIndicies(IEditableSpline spline)        {            List<int> nodeIndicies = new List<int>();            if( nodeSelection.Count > 0 )            {                nodeIndicies.AddRange( nodeSelection );                return nodeIndicies;            }            for( int i = 0; i < spline.GetNodeCount(); ++i )            {                nodeIndicies.Add( i );            }            return nodeIndicies;        }        List<int> GetNodeIndicies(IEditableSpline spline)        {            List<int> nodeIndicies = new List<int>();            for( int i = 0; i < spline.GetNodeCount(); ++i )            {                nodeIndicies.Add( i );            }            return nodeIndicies;        }        List<int> GetNodeIndiciesSelectedFirst(IEditableSpline spline)        {            List<int> nodeIndicies = GetNodeIndicies( spline );            // selected points take priority            nodeIndicies.Sort( (int one, int two) =>            {                if( nodeSelection.Contains( one ) == nodeSelection.Contains( two ) )                {                    return 0;                }                if( nodeSelection.Contains( one ) && !nodeSelection.Contains( two ) )                {                    return -1;                }                return 1;            } );            return nodeIndicies;        }        bool IsMouseButtonEvent(Event guiEvent, int button)        {            return guiEvent.button == button && guiEvent.isMouse && (guiEvent.type == EventType.MouseDown || guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp);        }        struct DetectClickSelectionResult        {            public bool Detected => nodeIndex >= 0;            public int nodeIndex;            public MoveControlPointId selectedType;            public DetectClickSelectionResult(int nodeIndex, MoveControlPointId selectedType)            {                this.nodeIndex = nodeIndex;                this.selectedType = selectedType;            }        }        DetectClickSelectionResult DetectClickSelection(IEditableSpline spline, Event guiEvent)        {            List<int> sortedNodes = GetNodeIndiciesSelectedFirst( spline );            for( int s = 0; s < sortedNodes.Count; ++s )            {                int index = sortedNodes[s];                SplineNode node = spline.GetNode( index );                bool overControl1 = IsMouseOverPoint( node.InControlPoint, guiEvent.mousePosition, GetControlPointHandleSize( node.InControlPoint) );                bool overControl2 = IsMouseOverPoint( node.OutControlPoint, guiEvent.mousePosition, GetControlPointHandleSize( node.OutControlPoint ) );                bool control1Interactable = spline.IsLoop() || index > 0;                bool control2Interactable = spline.IsLoop() || index < spline.GetNodeCount() - 1;                bool control1Detected = overControl1 && control1Interactable;                bool control2Detected = overControl2 && control2Interactable;                bool controlsEnabled = ShowNodeControls && (nodeSelection.Contains( index ) || nodeSelection.Count == 0);                if( controlsEnabled &&                    node.NodeType != NodeType.Point &&                    (control1Detected || control2Detected) )                {                    if( control1Detected )                    {                        return new DetectClickSelectionResult( index, MoveControlPointId.Control1 );                    }                    if( control2Detected )                    {                        return new DetectClickSelectionResult( index, MoveControlPointId.Control2 );                    }                }                else if( IsMouseOverPoint( node.position, guiEvent.mousePosition, GetControlPointHandleSize( node.position ) ) )                {                    return new DetectClickSelectionResult( index, MoveControlPointId.None );                }            }            return new DetectClickSelectionResult()            {                nodeIndex = -1,                selectedType = MoveControlPointId.None,            };        }        void DoClickSelection(IEditableSpline spline, Event guiEvent, out bool selectionChanged, out bool clearSelection)        {            selectionChanged = false;            clearSelection = false;            bool removeSelectedModifier = AddRemoveSelectionModifier( guiEvent );            bool isSelectionEvent = AddSelectionModifier( guiEvent ) | removeSelectedModifier;            DetectClickSelectionResult result = DetectClickSelection( spline, guiEvent );            if( result.Detected )            {
                useEvent = true;                if( result.selectedType != MoveControlPointId.None )                {                    if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )                    {                        if( nodeSelection.Count == 0 )                        {                            SelectionAddNode( spline, result.nodeIndex );                            editMode = SplineEditMode.MoveNode;                            selectionChanged = true;                        }                        clearSelection = false;
                        return;                    }                }                else                {                    if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )                    {                        if( nodeSelection.Contains( result.nodeIndex ) && removeSelectedModifier )                        {                            nodeSelection.Remove( result.nodeIndex );                            selectionChanged = true;                        }                        else if( !nodeSelection.Contains( result.nodeIndex ) )                        {                            if( !isSelectionEvent )                            {                                ClearNodeSelection( spline );
                            }                            SelectionAddNode( spline, result.nodeIndex );                            selectionChanged = true;                            editMode = SplineEditMode.MoveNode;                        }                        clearSelection = false;
                        return;                    }                    if( guiEvent.type == EventType.MouseUp && guiEvent.button == 0 )
                    {
                        if( !moveNodeState.HasMoved && nodeSelection.Contains( result.nodeIndex ) && !isSelectionEvent )
                        {
                            ClearNodeSelection( spline );
                            SelectionAddNode( spline, result.nodeIndex );
                            selectionChanged = true;
                            clearSelection = false;                            return;                        }
                    }                }            }            clearSelection = guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && !isSelectionEvent;        }        Vector2 mouseDragSelectionStart;        bool dragSelectActive = false;        bool DoDragSelection(IEditableSpline spline, Event guiEvent)        {            bool clearSelection = IsMouseButtonEvent( guiEvent, 0 ) && !dragSelectActive;            if( spline.GetNodeCount() > 0 && !dragSelectActive && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && DragSelectionModifier( guiEvent ) )            {                mouseDragSelectionStart = guiEvent.mousePosition;                dragSelectActive = true;                for( int i = 0; i < spline.GetNodeCount(); ++i )                {                    SplineNode node = spline.GetNode( i );                    if( IsMouseOverPoint( node.position + node.LocalInControlPoint, guiEvent.mousePosition, GetControlPointHandleSize( node.position ) ) )                    {                        dragSelectActive = false;                        break;                    }                    if( IsMouseOverPoint( node.position + node.LocalOutControlPoint, guiEvent.mousePosition, GetControlPointHandleSize( node.position ) ) )                    {                        dragSelectActive = false;                        break;                    }                    if( IsMouseOverPoint( node.position, guiEvent.mousePosition, GetControlPointHandleSize( node.position ) ) )                    {                        dragSelectActive = false;                        break;                    }                }            }            if( dragSelectActive )            {                Vector2 diff = mouseDragSelectionStart - guiEvent.mousePosition;                Vector2 extents = Vector2.Max( diff, -diff ) * 0.5f;                Vector2 position = (mouseDragSelectionStart + guiEvent.mousePosition) * 0.5f;                Vector2 pos1 = new Vector2( position.x + extents.x, position.y - extents.y );                Vector2 pos2 = new Vector2( position.x + extents.x, position.y + extents.y );                Vector2 pos3 = new Vector2( position.x - extents.x, position.y + extents.y );                Vector2 pos4 = new Vector2( position.x - extents.x, position.y - extents.y );                Ray ray1 = MousePositionToRay( Camera.current, pos1 );                Ray ray2 = MousePositionToRay( Camera.current, pos2 );                Ray ray3 = MousePositionToRay( Camera.current, pos3 );                Ray ray4 = MousePositionToRay( Camera.current, pos4 );                Vector3[] verts = new Vector3[]                {                    ray1.origin + ray1.direction,                    ray2.origin + ray2.direction,                    ray3.origin + ray3.direction,                    ray4.origin + ray4.direction                };                Color colour = Color.green;                Color fill = (Color.green + Color.black) * 0.5f;                fill.a = 0.1f;                Handles.DrawSolidRectangleWithOutline( verts, fill, colour );            }            if( dragSelectActive && guiEvent.type == EventType.MouseUp && guiEvent.button == 0 )            {                Vector3 dragSelectionEnd = TransformMousePositionToScreenPoint( Camera.current, guiEvent.mousePosition );                Vector3 dragSelectionStart = TransformMousePositionToScreenPoint( Camera.current, mouseDragSelectionStart );                Bounds dragBounds = new Bounds( (dragSelectionStart + dragSelectionEnd) * 0.5f, Vector3.zero );                dragBounds.Encapsulate( dragSelectionStart );                dragBounds.Encapsulate( dragSelectionEnd );                int pointsInDragBounds = 0;                for( int i = 0; i < spline.GetNodeCount(); ++i )                {                    Vector3 point = spline.GetNode( i ).position;                    Vector3 pointScreenPosition = Camera.current.WorldToScreenPoint( point );                    pointScreenPosition.z = 0;                    if( dragBounds.Contains( pointScreenPosition ) )                    {                        ++pointsInDragBounds;                        SelectionAddNode( spline, i );                        editMode = SplineEditMode.MoveNode;                    }                }                if( pointsInDragBounds == 0 )                {                    clearSelection = !AddSelectionModifier( guiEvent ) && !DragSelectionModifier( guiEvent );                }                dragSelectActive = false;                doRepaint = true;            }            if( dragSelectActive )            {                doRepaint = true;                useEvent = true;            }            return clearSelection;        }        bool hadSelectionOnMouseDown = false;        bool DoNodeSelection(IEditableSpline spline, Event guiEvent)        {            bool clearClick;            bool clickSelectionChanged;            DoClickSelection( spline, guiEvent, out clickSelectionChanged, out clearClick );            bool clearDrag = DoDragSelection( spline, guiEvent );            bool clearSelection = clearClick && clearDrag;            if( clearSelection )            {                clickSelectionChanged = true;                ClearNodeSelection( spline );            }            return clickSelectionChanged;        }        enum AddNodeState        {            NodePosition,            ControlPosition        }        static Vector3 GetPointPlacement(IEditableSpline spline, Camera camera, Vector2 mousePosition, Vector3 origin, Vector3 up, ref Vector3 planePoint, ref Vector3 planeOffset, bool verticalDisplace, bool intersectPhsyics, bool snapToGrid)        {            Ray mouseRay = MousePositionToRay( camera, mousePosition );            Vector3 mouseWorldPosition = MathsUtils.LinePlaneIntersection( mouseRay, origin + planeOffset, up );            if( intersectPhsyics )            {                if( Physics.Raycast( mouseRay, out RaycastHit hit ) )                {                    mouseWorldPosition = hit.point;                    planePoint = MathsUtils.LinePlaneIntersection( mouseWorldPosition, up, origin, up );                    planeOffset = mouseWorldPosition - planePoint;                }            }            else if( verticalDisplace )            {                Vector3 verticalPlaneNormal = Vector3.Cross( up, Vector3.Cross( up, mouseRay.direction ) );                mouseWorldPosition = MathsUtils.LinePlaneIntersection( mouseRay, planePoint, verticalPlaneNormal );                planeOffset += up * Vector3.Dot( up, mouseWorldPosition - (planePoint + planeOffset) );            }            else            {                planePoint = mouseWorldPosition - planeOffset;            }            if( snapToGrid )            {                planePoint = SnapWorldPointToGrid( planePoint, GetSnapGridLocalToWorldMatrix( spline, origin ) );            }            Vector3 worldPosition = planePoint + planeOffset;            if( verticalDisplace )            {
                if( snapToGrid )
                {
                    worldPosition = SnapWorldPointToGrid( worldPosition, GetSnapGridLocalToWorldMatrix( spline, origin ) );
                    planeOffset = worldPosition - planePoint;
                }
            }            return worldPosition;        }        void DrawNodeControlPointMovementGuides(IEditableSpline spline, Vector3 point, Vector3 control1, Vector3 control2)        {            DrawNodeControlPointMovementGuides( spline, point, control1, control2, ControlPointEditColor, Color.white );        }        void DrawNodeControlPointMovementGuides(IEditableSpline spline, Vector3 point, Vector3 control1, Vector3 control2, Color controlColour, Color controlPlaneColour)        {            Camera camera = Camera.current;            Vector3 gridUp = GetSnapGridUp( spline );            Vector3 controlPlane1 = MathsUtils.ProjectPointOnPlane( point, gridUp, control1 );            Vector3 controlPlane2 = MathsUtils.ProjectPointOnPlane( point, gridUp, control2 );            Handles.color = controlColour;            // draw control placement GUI            Handles.SphereHandleCap( 0, control1, Quaternion.identity, GetControlPointHandleSize( control1 ), EventType.Repaint );            Handles.SphereHandleCap( 0, control2, Quaternion.identity, GetControlPointHandleSize( control2 ), EventType.Repaint );            DrawWireDisk( control1, gridUp, GetControlDiscSize( control1 ), camera.transform.position );            DrawWireDisk( control2, gridUp, GetControlDiscSize( control2 ), camera.transform.position );            Handles.DrawDottedLine( point, control1, 2 );            Handles.DrawDottedLine( point, control2, 2 );            Handles.color = controlPlaneColour;            DrawWireDisk( controlPlane1, gridUp, GetControlDiscSize( controlPlane1 ), camera.transform.position );            DrawWireDisk( controlPlane2, gridUp, GetControlDiscSize( controlPlane2 ), camera.transform.position );            Handles.DrawDottedLine( point, controlPlane1, 2 );            Handles.DrawDottedLine( point, controlPlane2, 2 );            Handles.DrawDottedLine( control1, controlPlane1, 2 );            Handles.DrawDottedLine( control2, controlPlane2, 2 );            Handles.color = Color.white;        }        static Vector3 SnapWorldPointToGrid(Vector3 worldSpacePoint, Matrix4x4 gridMatrix, bool forceAllAxis = false, float scale = 1)        {            return gridMatrix.MultiplyPoint( SnapLocalPointToGrid( gridMatrix.inverse.MultiplyPoint( worldSpacePoint ), forceAllAxis, scale ) );        }        static Vector3 SnapLocalPointToGrid(Vector3 point, bool forceAllAxis, float scale = 1)        {            Vector3 result = point;            SnapAxis snapAxis = SnapAxis.None;            if( forceAllAxis || SnapToGridXActive ) snapAxis = SnapAxis.X;            if( forceAllAxis || SnapToGridYActive ) snapAxis |= SnapAxis.Y;            if( forceAllAxis || SnapToGridZActive ) snapAxis |= SnapAxis.Z;

            float halfGridScale = SnapToHalfGrid ? 0.5f : 1;            result = Snapping.Snap( result, SnapToGridScale * scale * halfGridScale, snapAxis );            return result;        }        AddNodeState addNodeState = AddNodeState.NodePosition;        Vector3 addNodePosition;        bool canShift = false;        void DoAddNode(IEditableSpline spline, Event guiEvent)        {            if( !MoveVerticalModifier( guiEvent ) )            {                canShift = true;            }            Selection.activeObject = (target as Component).gameObject;            Camera camera = Camera.current;
            Vector3 gridUp = GetSnapGridUp( spline );            if( addNodeState == AddNodeState.NodePosition )            {                addNodePosition = GetPointPlacement(                    spline,                    camera,                    guiEvent.mousePosition,                    GetSnapGridOrigin( spline ),                    gridUp,                    ref planePosition,                    ref planeOffset,                    MoveVerticalModifier( guiEvent ) && canShift && !RaycastModifier( guiEvent ),                    RaycastModifier( guiEvent ),                    SnapToGridModifier( guiEvent ) );

                // draw point placement GUI
                Handles.color = Color.yellow;                Ray down = new Ray( addNodePosition, -gridUp );                if( Physics.Raycast( down, out RaycastHit hitDown ) )                {                    DrawWireDisk( hitDown.point, hitDown.normal, GetNodeDiscSize( hitDown.point ) * spline.GetGizmoScale(), camera.transform.position );                    Handles.DrawDottedLine( planePosition, hitDown.point, 2 );                }

                DrawWireDisk( addNodePosition, gridUp, GetNodeDiscSize( addNodePosition ) * spline.GetGizmoScale(), camera.transform.position );                DrawWireDisk( planePosition, gridUp, GetNodeDiscSize( planePosition ) * spline.GetGizmoScale(), camera.transform.position );                Handles.DrawDottedLine( planePosition, addNodePosition, 2 );                if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )                {                    DetectClickSelectionResult result = DetectClickSelection( spline, guiEvent );                    if( result.Detected && !spline.IsLoop() )                    {                        if( addNodeMode == SplineAddNodeMode.Append && result.nodeIndex == 0 )                        {                            spline.SetLoop( true );                            guiEvent.Use();                            editMode = SplineEditMode.None;                            return;                        }                        if( addNodeMode == SplineAddNodeMode.Prepend && result.nodeIndex == spline.GetNodeCount() - 1 )                        {                            spline.SetLoop( true );                            guiEvent.Use();                            editMode = SplineEditMode.None;                            return;                        }                    }                    canShift = false;                    addNodeState = AddNodeState.ControlPosition;                    controlPlanePosition = addNodePosition;                    controlPlaneOffset = Vector3.zero;                    guiEvent.Use();                }            }            Vector3 newControlPointPosition = addNodePosition;            Vector3 relativeControlPoint = newControlPointPosition - addNodePosition;            if( addNodeMode == SplineAddNodeMode.Prepend )            {                relativeControlPoint *= -1;            }            if( addNodeState == AddNodeState.ControlPosition )            {                newControlPointPosition = GetPointPlacement(                    spline,                    camera,                    guiEvent.mousePosition,                    addNodePosition,                    gridUp,                    ref controlPlanePosition,                    ref controlPlaneOffset,                    MoveVerticalModifier( guiEvent ) && canShift && !RaycastModifier( guiEvent ),                    RaycastModifier( guiEvent ),                    SnapToGridModifier( guiEvent ) );                relativeControlPoint = newControlPointPosition - addNodePosition;                if( addNodeMode == SplineAddNodeMode.Prepend )                {                    relativeControlPoint *= -1;                }                Vector3 worldControl1 = addNodePosition - relativeControlPoint;                Vector3 worldControl2 = addNodePosition + relativeControlPoint;                DrawNodeControlPointMovementGuides( spline, addNodePosition, worldControl1, worldControl2, Color.yellow, Color.white );                if( guiEvent.type == EventType.MouseUp && guiEvent.button == 0 )                {                    canShift = false;                    addNodeState = AddNodeState.NodePosition;                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Add Node" );                    SplineNode newNode = new SplineNode( addNodePosition, 0.3f );                    if( relativeControlPoint.magnitude > 0.01f )                    {                        newNode.SetNodeType( NodeType.Mirrored );                        newNode.LocalOutControlPoint = relativeControlPoint; // this sets control 1 as well as it's mirrored                    }                    switch( addNodeMode )                    {                        case SplineAddNodeMode.Append:                            spline.AppendNode( newNode );                            break;                        case SplineAddNodeMode.Prepend:                            spline.PrependNode( newNode );                            break;                        default:                            spline.AppendNode( newNode );                            break;                    }                    EditorUtility.SetDirty( spline.GetComponent() );                    guiEvent.Use();                }            }

            Handles.color = Color.yellow;
            {
                // draw beziers
                int nodeCount = spline.GetNodeCount();                if( nodeCount > 0 )                {                    SplineNode newNode = new SplineNode( addNodePosition, 0.3f );                    newNode.LocalInControlPoint = -relativeControlPoint; // this sets control 1 as well as it's mirrored                    if( relativeControlPoint.magnitude > 0.01f )                    {                        newNode.SetNodeType( NodeType.Mirrored );                    }                    newNode.LocalOutControlPoint = relativeControlPoint; // this sets control 1 as well as it's mirrored

                    // addNodeMode == SplineAddNodeMode.Append
                    SplineNode node1 = spline.GetNode( nodeCount - 1 );                    Vector3 newNodeDirection = newNode.position - node1.position;                    node1.LocalOutControlPoint = newNodeDirection.normalized * 0.1f * newNodeDirection.magnitude;                    SplineNode node2 = newNode;                    if( addNodeMode == SplineAddNodeMode.Prepend )                    {                        node1 = newNode;                        node2 = spline.GetNode( 0 );
                        newNodeDirection = newNode.position - node2.position;
                        node2.LocalInControlPoint = newNodeDirection.normalized * 0.1f * newNodeDirection.magnitude;                        newNode.automaticTangentLength = node2.automaticTangentLength;                    }                    Bezier3 addSegment = new Bezier3( node1, node2 );                    Bezier3 projectedAddSegment = Bezier3.ProjectToPlane( addSegment, planePosition, gridUp );                    Handles.DrawBezier( projectedAddSegment.A, projectedAddSegment.D, projectedAddSegment.B, projectedAddSegment.C, Color.grey, null, 1 );                    Handles.DrawBezier( addSegment.A, addSegment.D, addSegment.B, addSegment.C, Color.yellow, null, 1 );                    if( spline.IsLoop() )                    {                        node1 = newNode;                        node2 = spline.GetNode( 0 );                        newNode.automaticTangentLength = node2.automaticTangentLength;                        if( addNodeMode == SplineAddNodeMode.Prepend )                        {                            node1 = spline.GetNode( nodeCount - 1 );                            node2 = newNode;
                            newNode.automaticTangentLength = node1.automaticTangentLength;                        }                        Bezier3 loopSegment = new Bezier3( node1, node2 );                        Bezier3 projectedLoopSegment = Bezier3.ProjectToPlane( loopSegment, planePosition, gridUp );                        Handles.DrawBezier( projectedLoopSegment.A, projectedLoopSegment.D, projectedLoopSegment.B, projectedLoopSegment.C, Color.grey, null, 1 );                        Handles.DrawBezier( loopSegment.A, loopSegment.D, loopSegment.B, loopSegment.C, Color.yellow, null, 1 );                    }                }            }

            // draw new point being added
            Handles.SphereHandleCap( 0, addNodePosition, Quaternion.identity, GetNodeHandleSize( addNodePosition ), guiEvent.type );            Handles.color = Color.white;        }        void DoInsertNode(IEditableSpline spline, Event guiEvent)        {            Selection.activeObject = (target as Component).gameObject;            Transform transform = spline.GetTransform();            Camera camera = Camera.current;
            Vector3 gridUp = GetSnapGridUp( spline );            Ray mouseRay = MousePositionToRay( camera, guiEvent.mousePosition );            SplineResult closestToRay = spline.GetResultClosestTo( mouseRay );            Vector3 newNodePosition = closestToRay.position;            planePosition = MathsUtils.LinePlaneIntersection( newNodePosition, gridUp, GetSnapGridOrigin( spline ), gridUp );            planeOffset = newNodePosition - planePosition;

            // new point
            Handles.color = Color.yellow;            Handles.SphereHandleCap( 0, newNodePosition, transform.rotation, GetNodeHandleSize( newNodePosition ), guiEvent.type );            DrawWireDisk( newNodePosition, gridUp, GetNodeDiscSize( newNodePosition ) * spline.GetGizmoScale(), camera.transform.position );            DrawWireDisk( planePosition, gridUp, GetNodeDiscSize( planePosition ) * spline.GetGizmoScale(), camera.transform.position );            Handles.DrawLine( planePosition, newNodePosition );            Handles.color = Color.white;            RaycastHit hitDown;            Ray down = new Ray( newNodePosition, -gridUp );            if( Physics.Raycast( down, out hitDown ) )            {                DrawWireDisk( hitDown.point, hitDown.normal, GetNodeDiscSize( hitDown.point ) * spline.GetGizmoScale(), camera.transform.position );                Handles.DrawDottedLine( planePosition, hitDown.point, 2 );            }            if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )            {                Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Insert Node" );                spline.InsertNode( closestToRay.t );                EditorUtility.SetDirty( spline.GetComponent() );                guiEvent.Use();            }        }        bool IsMouseOverPoint(Vector3 point, Vector3 mousePosition, float size)        {            Vector3 screenPoint = Camera.current.WorldToScreenPoint( point );            Vector3 screenPoint2 = Camera.current.WorldToScreenPoint( point + Camera.current.transform.right * size );            Vector3 mouseScreenPoint = TransformMousePositionToScreenPoint( Camera.current, mousePosition );            screenPoint.z = 0;            screenPoint2.z = 0;            return Vector3.Distance( screenPoint, mouseScreenPoint ) < Vector3.Distance( screenPoint, screenPoint2 );        }        enum MoveControlPointId        {            None,            Control1,            Control2,            Unknown        }        struct MoveNodeState        {            public int NodeIndex { get; private set; }            public MoveControlPointId ControlPointId { get; set; }            Vector2 movementAccumulator;
            public Vector2 MovementAccumulator
            {
                get
                {
                    return movementAccumulator;
                }
                set
                {
                    movementAccumulator += value;
                    if( movementAccumulator.magnitude > 0.01f )
                    {
                        HasMoved = true;
                    }
                }
            }            public Vector3 LastSnappedMovement { get; set; }            public Vector3 CurrentPosition { get; set; }            public Vector3 StartPosition { get; private set; }            public enum MovingModes
            {
                Plane,
                Verticle
            }            public MovingModes MovingMode { get; set; }            public bool Moving { get; private set; }            public bool MovingNodeControlPoint { get { return NodeIndex != -1 && ControlPointId != MoveControlPointId.None; } }            public bool WasSnapping { get; set; }            public bool HasMoved { get; private set; }            public void Stop()
            {
                Moving = false;
            }            public MoveNodeState(int nodeIndex, MoveControlPointId controlPointId, Vector3 startPosition, MovingModes movingMode)            {                Moving = true;                HasMoved = false;                movementAccumulator = Vector2.zero;                LastSnappedMovement = Vector3.zero;                WasSnapping = false;                WasSnapping = false;                this.ControlPointId = controlPointId;                this.NodeIndex = nodeIndex;                this.StartPosition = CurrentPosition = startPosition;                MovingMode = movingMode;            }        }        MoveNodeState moveNodeState;        const float startMovementThreshold = 5;        void DetectNodeToMove(IEditableSpline spline, MoveNodeState.MovingModes currentMovingMode, Event guiEvent)        {            DetectClickSelectionResult result = DetectClickSelection( spline, guiEvent );            if( result.Detected )            {                InitialiseMoveNodeState( spline, result.nodeIndex, result.selectedType, currentMovingMode );            }        }

        void InitialiseMoveNodeState( IEditableSpline spline, int nodeIndex, MoveControlPointId selectedType, MoveNodeState.MovingModes currentMovingMode )
        {            SplineNode node = spline.GetNode( nodeIndex );

            if( selectedType == MoveControlPointId.Control2
                || selectedType == MoveControlPointId.Unknown )
            {
                controlPlanePosition = MathsUtils.ProjectPointOnPlane( node.position, spline.GetTransform().up, node.OutControlPoint );
                controlPlaneOffset = node.OutControlPoint - controlPlanePosition;

                moveNodeState = new MoveNodeState( nodeIndex, selectedType, node.OutControlPoint, currentMovingMode );
            }
            else if( selectedType == MoveControlPointId.Control1 )
            {
                controlPlanePosition = MathsUtils.ProjectPointOnPlane( node.position, spline.GetTransform().up, node.InControlPoint );
                controlPlaneOffset = node.InControlPoint - controlPlanePosition;
                moveNodeState = new MoveNodeState( nodeIndex, selectedType, node.InControlPoint, currentMovingMode );
            }
            else
            {
                moveNodeState = new MoveNodeState( nodeIndex, selectedType, node.position, currentMovingMode );
            }

            if( moveNodeState.Moving )
            {
                planePosition = MathsUtils.ProjectPointOnPlane( spline.GetTransform().position, spline.GetTransform().up, node.position );
                planeOffset = node.position - planePosition;
            }
        }

        static Vector3 GetSnapGridUp( IEditableSpline spline )
        {
            return GetSnapGridLocalToWorldMatrix( spline, GetSnapGridOrigin( spline ) ).MultiplyVector( Vector3.up );
        }
        static Matrix4x4 GetSnapGridLocalToWorldMatrix( IEditableSpline spline )
        {
            return GetSnapGridLocalToWorldMatrix( spline, GetSnapGridOrigin( spline ) );
        }
        static Matrix4x4 GetSnapGridLocalToWorldMatrix( IEditableSpline spline, Vector3 customOrigin )
        {
            Transform transform = spline.GetTransform();
            Matrix4x4 matrix = Matrix4x4.TRS( customOrigin, transform.rotation, transform.lossyScale );            switch( GridSpace )
            {
                case GridSpace.Node:
                    return matrix;
                case GridSpace.Spline:
                    return matrix;
            }

            return Matrix4x4.TRS( customOrigin, Quaternion.identity, Vector3.one );
        }

        static Vector3 GetSnapGridOrigin( IEditableSpline spline )
        {
            Transform transform = spline.GetTransform();
            Vector3 origin = Vector3.zero;

            // Editing context position
            Vector3 position = transform.position;

            if( editMode == SplineEditMode.AddNode )
            {
                int nodeCount = spline.GetNodeCount();
                if( nodeCount > 0 )
                {
                    switch( addNodeMode )
                    {
                        case SplineAddNodeMode.Append:
                            position = spline.GetNode( nodeCount - 1 ).position;
                            break;
                        case SplineAddNodeMode.Prepend:
                            position = spline.GetNode( 0 ).position;
                            break;
                    }
                }
            }
            else
            {
                if( nodeSelection.Count > 0 )
                {
                    position = spline.GetNode( nodeSelection[nodeSelection.Count - 1] ).position;
                }
            }            switch( GridSpace )
            {
                case GridSpace.Node:
                    {
                        origin = position;
                    }
                    break;
                case GridSpace.Spline:
                    {
                        origin = SnapWorldPointToGrid( position, transform.localToWorldMatrix, true, 20 );
                        origin = MathsUtils.ProjectPointOnPlane( transform.position, transform.up, origin );
                    }
                    break;
                case GridSpace.Global:
                    {
                        origin = SnapWorldPointToGrid( transform.position, Matrix4x4.identity, true, 20 );
                        origin.y = 0;
                    }
                    break;
            }

            return origin;
        }        void DoMoveNode(IEditableSpline spline, Event guiEvent)        {            MoveNodeState.MovingModes currentMovingMode = MoveVerticalModifier( guiEvent ) ? MoveNodeState.MovingModes.Verticle : MoveNodeState.MovingModes.Plane;            Vector3 gridUp = GetSnapGridUp( spline );            if( !moveNodeState.Moving )
            {                bool selectionChanged = DoNodeSelection( spline, guiEvent );                if( selectionChanged )
                {
                    moveNodeState = new MoveNodeState();
                    useEvent = true;
                    return;
                }
            }            if( moveNodeState.MovingMode != currentMovingMode && moveNodeState.Moving && guiEvent.type == EventType.MouseDrag && guiEvent.button == 0 )
            {                InitialiseMoveNodeState( spline, moveNodeState.NodeIndex, moveNodeState.ControlPointId, currentMovingMode );
            }            if( !moveNodeState.Moving && guiEvent.type == EventType.MouseDown && guiEvent.button == 0)            {                DetectNodeToMove( spline, currentMovingMode, guiEvent );            }            else if( moveNodeState.Moving && (guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp) && guiEvent.button == 0 )            {                Vector2 screenDelta = TransformMouseDeltaToScreenDelta( guiEvent.delta );                Vector3 newPoint = moveNodeState.CurrentPosition;                bool physicsHit = false;                if( RaycastModifier( guiEvent ) )                {                    // snap to physics                    Ray mouseRay = MousePositionToRay( Camera.current, guiEvent.mousePosition );                    if( Physics.Raycast( mouseRay, out RaycastHit hit ) )                    {                        physicsHit = true;                        newPoint = hit.point;                    }                }                if( !physicsHit )                {                    if( MoveVerticalModifier( guiEvent ) )                    {                        // move along up axis                        Vector3 screenPoint = Camera.current.WorldToScreenPoint( moveNodeState.CurrentPosition ) + new Vector3( screenDelta.x, screenDelta.y, 0 );                        Ray projectionRay = Camera.current.ScreenPointToRay( screenPoint );                        Vector3 verticalPlaneNormal = Vector3.Cross( gridUp, Vector3.Cross( gridUp, projectionRay.direction ) );                        Vector3 screenWorldPosition = MathsUtils.LinePlaneIntersection( projectionRay, moveNodeState.CurrentPosition, verticalPlaneNormal );                        newPoint = moveNodeState.CurrentPosition + gridUp * Vector3.Dot( gridUp, screenWorldPosition - moveNodeState.CurrentPosition );                    }                    else                    {                        // relative pointer tracking                        Vector3 screenPoint = Camera.current.WorldToScreenPoint( moveNodeState.CurrentPosition ) + new Vector3( screenDelta.x, screenDelta.y, 0 );                        newPoint = MathsUtils.LinePlaneIntersection( Camera.current.ScreenPointToRay( screenPoint ), moveNodeState.CurrentPosition, gridUp );                    }                }                moveNodeState.CurrentPosition = newPoint;                Vector3 totalMovement = newPoint - moveNodeState.StartPosition;                if( Mathf.Abs( moveNodeState.MovementAccumulator.magnitude ) < startMovementThreshold )                {                    moveNodeState.MovementAccumulator += guiEvent.delta;                }                else if( totalMovement.magnitude > 0 )                {                    Vector3 snappedMovement = totalMovement;                    if( SnapToGridModifier( guiEvent ) )                    {                        Vector3 newWorldPoint = totalMovement + moveNodeState.StartPosition;                        Vector3 origin = GetSnapGridOrigin( spline );                        if( moveNodeState.MovingNodeControlPoint )                        {                            origin = spline.GetNode( moveNodeState.NodeIndex ).position;                        }                        float gridScale = (moveNodeState.MovingNodeControlPoint) ? 0.2f : 1.0f;                        Vector3 newSnappedWorldPoint = SnapWorldPointToGrid( newWorldPoint, GetSnapGridLocalToWorldMatrix( spline, origin ), false, gridScale );                        snappedMovement = newSnappedWorldPoint - moveNodeState.StartPosition;                    }                    Vector3 snappedDelta = snappedMovement - moveNodeState.LastSnappedMovement;                    Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Move Nodes" );                    if( moveNodeState.MovingNodeControlPoint )                    {                        SplineNode node = spline.GetNode( moveNodeState.NodeIndex );                        if( moveNodeState.ControlPointId == MoveControlPointId.Unknown )                        {                            Vector3 worldControl;                            float directionTestForward = 0;                            float directionTestBackward = 0;                            if( spline.GetNodeCount() > moveNodeState.NodeIndex + 1 || spline.IsLoop() )                            {                                int nextIndex = MathsUtils.WrapIndex( moveNodeState.NodeIndex + 1, spline.GetNodeCount() );                                directionTestForward = Vector3.Dot( totalMovement.normalized, (spline.GetNode( nextIndex ).position - node.position).normalized );                            }                            if( moveNodeState.NodeIndex - 1 >= 0 || spline.IsLoop() )                            {                                int nextIndex = MathsUtils.WrapIndex( moveNodeState.NodeIndex - 1, spline.GetNodeCount() );                                directionTestBackward = Vector3.Dot( totalMovement.normalized, (spline.GetNode( moveNodeState.NodeIndex - 1 ).position - node.position).normalized );                            }                            if( directionTestForward >= directionTestBackward )                            {                                moveNodeState.ControlPointId = MoveControlPointId.Control2;                                worldControl = node.position + node.LocalOutControlPoint;                            }                            else                            {                                moveNodeState.ControlPointId = MoveControlPointId.Control1;                                worldControl = node.position + node.LocalInControlPoint;                            }                            controlPlanePosition = MathsUtils.ProjectPointOnPlane( node.position, spline.GetTransform().up, worldControl );                            controlPlaneOffset = worldControl - controlPlanePosition;                        }                        if( moveNodeState.ControlPointId == MoveControlPointId.Control1 )                        {                            node.LocalInControlPoint += snappedDelta;                        }                        else                        {                            node.LocalOutControlPoint += snappedDelta;                        }                        spline.SetNode( moveNodeState.NodeIndex, node );
                        EditorUtility.SetDirty( spline.GetComponent() );                    }                    else                    {                        for( int i = 0; i < nodeSelection.Count; ++i )                        {                            int index = nodeSelection[i];                            SplineNode node = spline.GetNode( index );                            node.position += snappedDelta;                            spline.SetNode( index, node );
                            EditorUtility.SetDirty( spline.GetComponent() );                        }                    }                    moveNodeState.LastSnappedMovement = snappedMovement;                    SplineNode latestNode = spline.GetNode( moveNodeState.NodeIndex );                    planePosition = MathsUtils.ProjectPointOnPlane( GetSnapGridOrigin(spline), gridUp, latestNode.position );                    planeOffset = latestNode.position - planePosition;                }                if( guiEvent.type == EventType.MouseUp )                {                    moveNodeState.Stop();
                    if( !moveNodeState.HasMoved )
                    {
                        DoNodeSelection( spline, guiEvent );
                    }
                    useEvent = true;                }            }            if( moveNodeState.Moving )            {                useEvent = true;            }            if( moveNodeState.MovingNodeControlPoint )            {                SplineNode node = spline.GetNode( moveNodeState.NodeIndex );                if( ShowGrid || SnapToGridModifier( guiEvent ) )                {                    Matrix4x4 matrix = GetSnapGridLocalToWorldMatrix( spline );                    Vector3 gridForward = matrix.MultiplyVector( Vector3.forward );                    Vector3 gridRight = matrix.MultiplyVector( Vector3.right );                    DrawGrid( node.position, gridForward, gridRight, 0.2f, 5 );                }                DrawNodeControlPointMovementGuides( spline, node.position, node.LocalInControlPoint + node.position, node.LocalOutControlPoint + node.position );            }        }        // functions to help stop points disappearing into infinity!        bool IsSafeToProjectFromPlane(Camera camera, Vector3 up)        {            return Vector3.Dot( camera.transform.forward, up ) < 0.95f;        }        bool TwoDimentionalMode(Camera camera, Vector3 up)        {            return Vector3.Dot( camera.transform.up, up ) < 0.95f;        }        float DepthScale(Vector3 point, Vector3 cameraPosition)        {            IEditableSpline spline = GetSpline();            return Vector3.Distance( spline.GetTransform().position, cameraPosition ) / Vector3.Distance( point, cameraPosition );        }        void DrawWireDisk(Vector3 point, Vector3 normal, float radius, Vector3 cameraPosition)        {            Handles.DrawWireDisc( point, normal, radius * DepthScale( point, cameraPosition ) );        }        void DebugLogEvent(Event guiEvent)        {            if( guiEvent.type != EventType.Layout && guiEvent.type != EventType.Repaint )            {                Debug.Log( guiEvent.type );            }        }

        #region modifier key definitions

        static string snapGridModifierString = "hold control/toggle capsLock";
        static bool SnapToGridModifier( Event guiEvent )
        {
            return guiEvent.control | guiEvent.capsLock;
        }

        static bool MoveVerticalModifier( Event guiEvent )
        {
            return guiEvent.shift;
        }

        static bool RaycastModifier( Event guiEvent )
        {
            return guiEvent.alt;
        }

        static bool DragSelectionModifier( Event guiEvent )
        {
            return guiEvent.shift;
        }

        static bool AddSelectionModifier( Event guiEvent )
        {
            return guiEvent.shift;
        }

        static bool AddRemoveSelectionModifier( Event guiEvent )
        {
#if UNITY_EDITOR_OSX
            return guiEvent.command;
#else
            return guiEvent.control;
#endif
        }

        static bool ShortcutModifer( Event guiEvent )
        {
#if UNITY_EDITOR_OSX
            return guiEvent.command;
#else
            return guiEvent.control;
#endif
        }

#endregion    }    public static class SplineEditorTools    {        public static void SetNodeType(IEditableSpline spline, NodeType type, List<int> nodeIndicies)        {            for( int i = 0; i < nodeIndicies.Count; ++i )            {                int index = nodeIndicies[i];                SplineNode node = spline.GetNode( index );                node.SetNodeType( type );                spline.SetNode( index, node );            }            EditorUtility.SetDirty( spline.GetComponent() );        }        public static void SetNodesXPosition(IEditableSpline spline, List<int> nodeIndicies, float x)        {            for( int i = 0; i < nodeIndicies.Count; ++i )            {                int index = nodeIndicies[i];                SplineNode node = spline.GetNode( index );                 node.position = new Vector3( x, node.position.y, node.position.z );                spline.SetNode( index, node );            }            EditorUtility.SetDirty( spline.GetComponent() );        }        public static void SetNodesYPosition(IEditableSpline spline, List<int> nodeIndicies, float y)        {            for( int i = 0; i < nodeIndicies.Count; ++i )            {                int index = nodeIndicies[i];                SplineNode node = spline.GetNode( index );                node.position = new Vector3( node.position.x, y, node.position.z );                spline.SetNode( index, node );            }            EditorUtility.SetDirty( spline.GetComponent() );        }        public static void SetNodesZPosition(IEditableSpline spline, List<int> nodeIndicies, float z)        {            for( int i = 0; i < nodeIndicies.Count; ++i )            {                int index = nodeIndicies[i];                SplineNode node = spline.GetNode( index );                node.position = new Vector3( node.position.x, node.position.y, z );                spline.SetNode( index, node );            }            EditorUtility.SetDirty( spline.GetComponent() );        }        public static void SetNodesAutomaticTangentLegnth( IEditableSpline spline, List<int> nodeIndicies, float tangentLength )
        {            for( int i = 0; i < nodeIndicies.Count; ++i )            {                int index = nodeIndicies[i];                SplineNode node = spline.GetNode( index );                node.automaticTangentLength = tangentLength;                spline.SetNode( index, node );            }            AutomaticSmooth( spline );            EditorUtility.SetDirty( spline.GetComponent() );
        }        public static void SimplifyNodeType(IEditableSpline spline, List<int> nodeIndicies)        {            for( int i = 0; i < nodeIndicies.Count; ++i )            {                int index = nodeIndicies[i];                SplineNode node = spline.GetNode( index );                node.SetNodeType( SplineNode.GetNodeTypeFromControls( node ) );                spline.SetNode( index, node );            }            EditorUtility.SetDirty( spline.GetComponent() );        }        public static void Flatten(IEditableSpline spline, List<int> nodeIndicies)        {            for( int i = 0; i < nodeIndicies.Count; ++i )            {                int index = nodeIndicies[i];                SplineNode point = spline.GetNode( index );                point.position = MathsUtils.ProjectPointOnPlane( spline.GetTransform().position, spline.GetTransform().up, point.position );                point.LocalInControlPoint = MathsUtils.ProjectPointOnPlane( spline.GetTransform().position, spline.GetTransform().up, point.LocalInControlPoint + point.position ) - point.position;                point.LocalOutControlPoint = MathsUtils.ProjectPointOnPlane( spline.GetTransform().position, spline.GetTransform().up, point.LocalOutControlPoint + point.position ) - point.position;                spline.SetNode( index, point );            }            EditorUtility.SetDirty( spline.GetComponent() );        }

        public static void AutomaticSmooth(IEditableSpline spline)        {
            if (spline.GetNodeCount() < 2)
            {
                return;
            }

            List<int> automaticNodes = new List<int>();
            for( int i = 0; i < spline.GetNodeCount(); ++i )
            {
                if(spline.GetNode(i).NodeType == NodeType.Automatic)
                {
                    automaticNodes.Add(i);
                }
            }

            Smooth( spline, automaticNodes );        }

        public static void ArcSmooth( IEditableSpline spline )
        {
            if( spline.GetNodeCount() < 2 )
            {
                return;
            }

            // set control lengths
            for( int i = 0; i < spline.GetNodeCount() - 1; ++i )
            {
                bool segmentIsCurve = (i + 1) % 2 == 0;

                SplineNode node = spline.GetNode( i );
                SplineNode nextNode = spline.GetNode( i + 1 );

                node.SetNodeType( NodeType.Aligned );
                nextNode.SetNodeType( NodeType.Aligned );

                bool nextNodeIsLastNode = i == spline.GetNodeCount() - 1;

                if( i == 0 )
                {
                    node.LocalInControlPoint = Vector3.zero;
                }
                if( nextNodeIsLastNode )
                {
                    nextNode.LocalOutControlPoint = Vector3.zero;
                }

                if( segmentIsCurve )
                {
                    float length = 0.552284749831f;
                    // default to right angle corners.

                    // TODO:
                    // calculate the length of controls needed to approximate a perfect arc.
                    // This is the equation = l ≈ r + r * PI*0.1 * pow(α/90, 2)
                    // https://stackoverflow.com/questions/1734745/how-to-create-circle-with-b%C3%A9zier-curves
                    if( i > 0 && !nextNodeIsLastNode )
                    {
                        SplineNode previousNode = spline.GetNode( i-1 );
                        Vector3 tangent1 = (node.position - previousNode.position).normalized;

                        SplineNode nextNextNode = spline.GetNode( i + 2 );
                        Vector3 tangent2 = (nextNextNode.position - nextNode.position).normalized;

                        Vector3 circleCenter;
                        bool intersection = MathsUtils.LineLineIntersection( out circleCenter, node.position, tangent1, nextNode.position, tangent2 );

                        if( intersection )
                        {
                            float angle = Vector3.Angle( tangent1, tangent2 );
                            float r = (circleCenter - node.position).magnitude;
                            float n = 360.0f / angle;
                            float magicValue = (4f / 3f) * Mathf.Tan( Mathf.PI / (2*n) );
                            length = r * magicValue;
                        }
                    }

                    // assumes there's a node before node as curved segments start on the second segment
                    Vector3 direction = nextNode.position - node.position;
                    node.LocalOutControlPoint = -node.LocalInControlPoint.normalized * length;

                    if( !nextNodeIsLastNode )
                    {
                        SplineNode nextNextNode = spline.GetNode( i + 2 );

                        Vector3 direction2 = nextNode.position - nextNextNode.position;
                        nextNode.LocalInControlPoint = -direction2.normalized * length;
                    }
                    else
                    {
                        nextNode.LocalInControlPoint = Vector3.zero;
                    }
                }
                else
                {
                    Vector3 midPoint = (node.position + nextNode.position) * 0.5f;
                    Vector3 qPoint = (node.position + midPoint) * 0.5f;
                    Vector3 q3Point = (nextNode.position + midPoint) * 0.5f;

                    node.LocalOutControlPoint = qPoint - node.position;
                    nextNode.LocalInControlPoint = q3Point - nextNode.position;
                }

                spline.SetNode( i, node );
                spline.SetNode( i + 1, nextNode );
            }
        }

        public static void Smooth(IEditableSpline spline, List<int> nodeIndicies)        {
            if( spline.GetNodeCount() < 2 )
            {
                return;
            }            if( spline.IsLoop() && spline.GetNodeCount() == 2 )
            {
                // special case
                SmoothLoopedStartAndEndPoints( spline );
                return;
            }            for (int i = 0; i < nodeIndicies.Count; ++i)            {                if( !spline.IsLoop() && nodeIndicies[i] == 0 )
                {
                    SmoothStartPoint( spline );
                }                else if( !spline.IsLoop() && nodeIndicies[i] == spline.GetNodeCount() - 1 )
                {
                    SmoothEndPoint( spline );
                }                else
                {                    SmoothPoint( spline, nodeIndicies[i] );
                }
            }        }

        static void SmoothPoint( IEditableSpline spline, int index )
        {
            if (spline.GetNodeCount() < 2)
            {
                return;
            }

            SplineNode node = spline.GetNode( index );            SplineNode compare = new SplineNode( node );            Vector3 direction = Vector3.zero;            float[] neighbourDistances = new float[2];            if( index > 0 || spline.IsLoop() )            {
                Vector3 offset = spline.GetNode( spline.LoopIndex(index - 1) ).position - node.position;                direction = offset.normalized;                neighbourDistances[0] = offset.magnitude;            }            if ( index+1 < spline.GetNodeCount()|| spline.IsLoop() )
            {                Vector3 offset = spline.GetNode(spline.LoopIndex(index + 1)).position - node.position;                direction -= offset.normalized;                neighbourDistances[1] = -offset.magnitude;            }            direction.Normalize();

            if (node.NodeType != NodeType.Automatic)
            {
                node.SetNodeType(NodeType.Aligned);
            }            node.LocalInControlPoint = direction * neighbourDistances[0] * node.automaticTangentLength;            node.LocalOutControlPoint = direction * neighbourDistances[1] * node.automaticTangentLength;            if( compare.IsDifferentFrom( node ) )
            {
                spline.SetNode( index, node );
                EditorUtility.SetDirty( spline.GetComponent() );
            }        }

        static void SmoothStartPoint( IEditableSpline spline )
        {
            if (spline.GetNodeCount() < 2)
            {
                return;
            }

            SplineNode startNode = spline.GetNode(0);
            SplineNode nextNode = spline.GetNode(1);            SplineNode compare = new SplineNode( startNode );

            if (startNode.NodeType != NodeType.Automatic)
            {
                startNode.SetNodeType(NodeType.Aligned);
            }

            if (spline.GetNodeCount() == 2)
            {
                startNode.LocalOutControlPoint = (nextNode.position - startNode.position) * 0.25f;
            }
            else
            {
                startNode.LocalOutControlPoint = ((startNode.position + nextNode.InControlPoint) * 0.5f) - startNode.position;
            }

            if( compare.IsDifferentFrom( startNode ))
            {
                spline.SetNode( 0, startNode );
                EditorUtility.SetDirty( spline.GetComponent() );
            }
        }

        static void SmoothEndPoint(IEditableSpline spline)
        {
            if (spline.GetNodeCount() < 2)
            {
                return;
            }

            SplineNode previousNode = spline.GetNode(spline.GetNodeCount() - 2);
            SplineNode endNode = spline.GetNode(spline.GetNodeCount()-1);            SplineNode compare = new SplineNode( endNode );

            if (endNode.NodeType != NodeType.Automatic)
            {
                endNode.SetNodeType(NodeType.Aligned);
            }

            if (spline.GetNodeCount() == 2)
            {
                endNode.LocalInControlPoint = (previousNode.position - endNode.position) * 0.25f;
            }
            else
            {
                endNode.LocalInControlPoint = ((endNode.position + previousNode.OutControlPoint) * 0.5f) - endNode.position;
            }

            if( compare.IsDifferentFrom( endNode ) )
            {
                spline.SetNode( spline.GetNodeCount() - 1, endNode );
                EditorUtility.SetDirty( spline.GetComponent() );
            }
        }

        static void SmoothLoopedStartAndEndPoints( IEditableSpline spline )
        {
            if( spline.GetNodeCount() != 2 )
            {
                return;
            }

            SplineNode startNode = spline.GetNode( 0 );
            SplineNode endNode = spline.GetNode( 1 );

            SplineNode startNodeCompare = new SplineNode( startNode );
            SplineNode endNodeCompare = new SplineNode( endNode );

            Vector3 dirAnchorAToB = (endNode.position - startNode.position).normalized;
            float dstBetweenAnchors = (startNode.position - endNode.position).magnitude;
            Vector3 perp = Vector3.Cross( dirAnchorAToB, Vector3.up );
            startNode.LocalOutControlPoint = -perp * dstBetweenAnchors * startNode.automaticTangentLength;
            startNode.LocalInControlPoint = perp * dstBetweenAnchors * startNode.automaticTangentLength;
            endNode.LocalInControlPoint = -perp * dstBetweenAnchors * startNode.automaticTangentLength;
            endNode.LocalOutControlPoint = perp * dstBetweenAnchors * startNode.automaticTangentLength;

            if( startNodeCompare.IsDifferentFrom( startNode )
                || endNodeCompare.IsDifferentFrom( endNode ) )
            {
                spline.SetNode( 0, startNode );
                spline.SetNode( 1, endNode );
                EditorUtility.SetDirty( spline.GetComponent() );
            }
        }

        public static void DeleteDuplicateNodes(IEditableSpline spline, List<int> nodeIndicies)        {            List<int> removeNodes = new List<int>();            for( int n = 1; n < nodeIndicies.Count; ++n )            {                int i = nodeIndicies[n];                if( i < 1 )                {                    continue;                }                SplineNode previousNode = spline.GetNode( i - 1 );                SplineNode node = spline.GetNode( i );                if( node.position == previousNode.position )                {                    if( previousNode.LocalOutControlPoint.sqrMagnitude <= 0                        && node.LocalInControlPoint.magnitude <= 0 )                    {                        removeNodes.Add( i );                    }                }            }            for( int i = removeNodes.Count - 1; i >= 0; --i )            {                int previousNodeIndex = removeNodes[i] - 1;                int removeIndex = removeNodes[i];                SplineNode previousNode = spline.GetNode( previousNodeIndex );                SplineNode node = spline.GetNode( removeIndex );                SplineNode newNode = new SplineNode( previousNode.position, previousNode.LocalInControlPoint, node.LocalOutControlPoint, previousNode.automaticTangentLength );                spline.SetNode( previousNodeIndex, newNode );                spline.RemoveNode( removeIndex );            }            EditorUtility.SetDirty( spline.GetComponent() );        }        public static void RemoveLinearSegments(IEditableSpline spline, List<int> nodeIndicies)        {            // remove nodes in linear sections            for( int n = 2; n < nodeIndicies.Count; ++n )            {                int i = nodeIndicies[n];                if( i < 2 )                {                    continue;                }                SplineNode node1 = spline.GetNode( i - 2 );                SplineNode node2 = spline.GetNode( i - 1 );                SplineNode node3 = spline.GetNode( i );                Vector3[] points = new Vector3[7];                points[0] = node1.position;                points[1] = node1.OutControlPoint;                points[2] = node2.InControlPoint;                points[3] = node2.position;                points[4] = node2.OutControlPoint;                points[5] = node3.InControlPoint;                points[6] = node3.position;                Vector3 testVector = (points[0] - points[6]).normalized;                bool linear = true;                for( int p = 1; p < points.Length; ++p )                {                    Vector3 dif = points[p - 1] - points[p];                    if( dif.sqrMagnitude <= float.Epsilon )                    {                        continue;                    }                    Vector3 vector = dif.normalized;                    if( Vector3.Dot( vector, testVector ) < 0.98f )                    {                        linear = false;                        break;                    }                }                if( !linear )                {                    continue;                }                spline.RemoveNode( i - 1 );            }        }        public static void Simplify(IEditableSpline spline, List<int> nodeIndicies)        {            if( nodeIndicies.Count == 0 )            {                return;            }            SimplifyNodeType( spline, nodeIndicies );            DeleteDuplicateNodes( spline, nodeIndicies );            RemoveLinearSegments( spline, nodeIndicies );        }        public static void Reverse( IEditableSpline spline )        {            int nodeCount = spline.GetNodeCount();            for( int i = 0; i < nodeCount/2; ++i )
            {
                int ai = i;
                int bi = nodeCount - i - 1;

                SplineNode a = spline.GetNode( ai );
                SplineNode b = spline.GetNode( bi );

                a.SetLocalControls( a.LocalOutControlPoint, a.LocalInControlPoint );
                b.SetLocalControls( b.LocalOutControlPoint, b.LocalInControlPoint );

                spline.SetNode( ai, b );
                spline.SetNode( bi, a );
            }        }    }
#endif}