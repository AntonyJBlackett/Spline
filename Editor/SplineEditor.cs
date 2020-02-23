using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FantasticSplines
{
    public enum SplineEditMode
    {
        None,
        AddPoint,
        MovePoint
    }

    public enum SplineAddPointMode
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

        static List<CurvePoint> clipboard = new List<CurvePoint>();

        SplineEditMode editMode = SplineEditMode.None;
        SplineAddPointMode addPointMode = SplineAddPointMode.Append;
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

        void SetSelectionPointType(IEditableSpline spline, PointType type)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Set Point Type" );
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];
                CurvePoint point = spline.GetCurvePoint( index );
                point.SetPointType( type );
                spline.SetCurvePoint( index, point );
            }
            EditorUtility.SetDirty( target );
        }

        Bounds GetSelectionBounds(IEditableSpline spline)
        {
            if( pointSelection.Count == 0 )
            {
                return GetBounds( spline );
            }

            Bounds bounds = new Bounds( spline.GetCurvePoint( pointSelection[0] ).position, Vector3.one * 0.1f );
            for( int i = 1; i < pointSelection.Count; ++i )
            {
                bounds.Encapsulate( spline.GetCurvePoint( pointSelection[i] ).position );
            }

            if( bounds.size.magnitude < 1 )
            {
                bounds.size = bounds.size.normalized * 1;
            }

            return bounds;
        }

        Bounds GetBounds(IEditableSpline spline)
        {
            Bounds bounds = new Bounds( spline.GetCurvePoint( 0 ).position, Vector3.zero );
            for( int i = 1; i < spline.GetCurvePointCount(); ++i )
            {
                bounds.Encapsulate( spline.GetCurvePoint( i ).position );
            }
            return bounds;
        }

        public void FrameCamera(IEditableSpline spline)
        {
            Bounds bounds;
            if( pointSelection.Count > 0 )
            {
                bounds = GetSelectionBounds( spline );
            }
            else
            {
                bounds = GetBounds( spline );
            }
            SceneView.lastActiveSceneView.Frame( bounds, false );
        }

        void FlattenSelection(IEditableSpline spline, PointType type)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Flatten Selection" );
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];
                CurvePoint point = spline.GetCurvePoint( index );

                point.position = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.position );
                point.Control1 = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.Control1 + point.position ) - point.position;
                point.Control2 = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.Control2 + point.position ) - point.position;

                spline.SetCurvePoint( index, point );
            }
            EditorUtility.SetDirty( target );
        }

        void SmoothSelection(IEditableSpline spline, PointType type)
        {
            Undo.RecordObject( spline.GetUndoObject(), "Set Point Type" );
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];
                CurvePoint point = spline.GetCurvePoint( index );
                CurvePoint before = point;
                CurvePoint after = point;

                int beforeIndex = index - 1;
                if( beforeIndex < 0 )
                {
                    beforeIndex = spline.GetCurvePointCount() - 1;
                }
                before = spline.GetCurvePoint( beforeIndex );

                int afterIndex = (index + 1) % spline.GetCurvePointCount();
                after = spline.GetCurvePoint( afterIndex );

                Vector3 direction = after.position - before.position;
                float dist1 = Mathf.Min( (point.position - before.position).magnitude, direction.magnitude );
                float dist2 = Mathf.Min( (point.position - after.position).magnitude, direction.magnitude );

                point.SetPointType( PointType.Aligned );
                point.Control1 = -direction.normalized * dist1 * 0.4f;
                point.Control2 = direction.normalized * dist2 * 0.4f;

                if( !spline.IsLoop() )
                {
                    if( index == 0 || index == spline.GetCurvePointCount() - 1 )
                    {
                        point.SetPointType( PointType.Point );
                    }
                }

                spline.SetCurvePoint( index, point );
            }
            EditorUtility.SetDirty( target );
        }

        public void DeleteSelectedPoints(IEditableSpline spline)
        {
            int removePoints = pointSelection.Count;
            for( int i = 0; i < removePoints; ++i )
            {
                RemovePoint( spline, pointSelection[0] );
            }
            ClearPointSelection();
            EditorUtility.SetDirty( target );
        }

        void PasteCurve(IEditableSpline spline)
        {
            DeleteSelectedPoints( spline );
            int oldPointCount = spline.GetCurvePointCount();
            for( int i = 0; i < clipboard.Count; i++ )
            {
                spline.AppendCurvePoint( clipboard[i] );
            }

            pointSelection.Clear();
            for( int i = oldPointCount; i < spline.GetCurvePointCount(); i++ )
            {
                pointSelection.Add( i );
            }
        }

        public void CopyCurve(IEditableSpline spline)
        {
            clipboard.Clear();
            List<int> copyIndicies = new List<int>( pointSelection );
            copyIndicies.Sort();
            for( int i = 0; i < copyIndicies.Count; i++ )
            {
                int index = copyIndicies[i];
                clipboard.Add( spline.GetCurvePoint( index ) );
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

        void StartAddPointMode(SplineAddPointMode mode)
        {
            ResetEditMode();
            editMode = SplineEditMode.AddPoint;
            addPointMode = mode;

            IEditableSpline spline = GetSpline();
            Transform transform = spline.GetTransform();
            Vector3 adjoiningPointPosition = transform.position;

            if( spline.GetCurvePointCount() > 0 )
            {
                int adjoiningIndex = 0;
                if( addPointMode == SplineAddPointMode.Append )
                {
                    adjoiningIndex = spline.GetCurvePointCount() - 1;
                }
                adjoiningPointPosition = spline.GetCurvePoint( adjoiningIndex ).position;
            }

            planePosition = MathHelper.LinePlaneIntersection( adjoiningPointPosition, transform.up, transform.position, transform.up );
            planeOffset = adjoiningPointPosition - planePosition;
        }

        void InspectorAddPointModeButton(SplineAddPointMode mode)
        {
            if( editMode == SplineEditMode.AddPoint && addPointMode == mode )
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

        public override void OnInspectorGUI()
        {
            IEditableSpline spline = GetSpline();
            ShowScriptGUI( spline as MonoBehaviour );
            if( spline == null )
            {
                DrawDefaultInspector();
                return;
            }

            GUILayout.Label( "Edit Modes" );
            InspectorAddPointModeButton( SplineAddPointMode.Append );
            InspectorAddPointModeButton( SplineAddPointMode.Prepend );
            InspectorAddPointModeButton( SplineAddPointMode.Insert );


            EditorGUI.BeginChangeCheck();
            bool loop = GUILayout.Toggle( spline.IsLoop(), "Loop" );
            if( EditorGUI.EndChangeCheck() )
            {
                Undo.RecordObject( spline.GetUndoObject(), "Loop Toggle" );
                spline.SetLoop( loop );
                EditorUtility.SetDirty( target );
            }

            GUILayout.Space( 10 );
            GUILayout.Label( "Point Types" );
            if( GUILayout.Button( "Linear Points" ) )
            {
                SetSelectionPointType( spline, PointType.Point );
            }
            if( GUILayout.Button( "Mirror Control Points" ) )
            {
                SetSelectionPointType( spline, PointType.Mirrored );
            }
            if( GUILayout.Button( "Aligned Control Points" ) )
            {
                SetSelectionPointType( spline, PointType.Aligned );
            }
            if( GUILayout.Button( "Free Control Points" ) )
            {
                SetSelectionPointType( spline, PointType.Free );
            }

            GUILayout.Space( 10 );
            GUILayout.Label( "Tools" );
            if( GUILayout.Button( "Smooth Selection" ) )
            {
                SmoothSelection( spline, PointType.Free );
            }
            if( GUILayout.Button( "Flatten Selection" ) )
            {
                FlattenSelection( spline, PointType.Free );
            }
            GUILayout.Space( 10 );
            EditorGUI.BeginChangeCheck();

            GUILayout.Label( "Grid" );
            ShowGrid = GUILayout.Toggle( ShowGrid, "Show Grid" );
            SnapToHalfGrid = GUILayout.Toggle( SnapToHalfGrid, "Snap To Half Grid Size" );
            SnapToGridScale = EditorGUILayout.Vector3Field( "Grid Snap Scale", SnapToGridScale );
            GUILayout.BeginHorizontal();
            GUILayout.Label( "Snap Axies (hold alt)" );
            SnapToGridX = GUILayout.Toggle( SnapToGridX, "X" );
            SnapToGridY = GUILayout.Toggle( SnapToGridY, "Y" );
            SnapToGridZ = GUILayout.Toggle( SnapToGridZ, "Z" );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );
            GUILayout.Label( "Gizmos" );
            HandleCapScale = EditorGUILayout.FloatField( "Editor Point Scale", HandleCapScale );
            spline.SetColor( EditorGUILayout.ColorField( "Spline Colour", spline.GetColor() ) );
            spline.SetZTest( GUILayout.Toggle( spline.GetZTest(), "ZTest" ) );
            ShowSegmentLengths = GUILayout.Toggle( ShowSegmentLengths, "Show Segment Lengths" );
            ShowCurvePointControls = GUILayout.Toggle( ShowCurvePointControls, "Show Point Controls" );

            if( EditorGUI.EndChangeCheck() )
            {
                EditorUtility.SetDirty( target );
            }

            GUILayout.Space( 10 );
            GUILayout.Label( "Debug" );
            GUILayout.Label( "Edit Mode: " + editMode.ToString() );
            GUILayout.Label( "Add Point Mode: " + addPointMode.ToString() );
            GUILayout.Label( "Moving Point: " + movePointState.Moving.ToString() );


            GUILayout.Space( 10 );
            GUILayout.Label( "Info" );
            GUILayout.Label( "Length: " + spline.GetLength() );
            GUILayout.Label( "Point Count: " + spline.GetCurvePointCount() );

            GUILayout.Space( 10 );
            DrawDefaultInspector();
        }

        public static bool ShowSegmentLengths
        {
            get => EditorPrefs.GetBool( "FantasticSplinesShowSegmentLengths", false );
            set => EditorPrefs.SetBool( "FantasticSplinesShowSegmentLengths", value );
        }

        public static bool ShowCurvePointControls
        {
            get => EditorPrefs.GetBool( "FantasticSplinesShowCurvePointControls", true );
            set => EditorPrefs.SetBool( "FantasticSplinesShowCurvePointControls", value );
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
            if( pointSelection.Count == 0 )
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
                    pointSelection.Clear();
                    for( int i = 0; i < spline.GetCurvePointCount(); ++i )
                    {
                        pointSelection.Add( i );
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
                    Undo.RecordObject( spline.GetUndoObject(), "Cut Points" );
                    CopyCurve( spline );
                    DeleteSelectedPoints( spline );
                    guiEvent.Use();
                }

                if( guiEvent.command && guiEvent.keyCode == KeyCode.V )
                {
                    Undo.RecordObject( spline.GetUndoObject(), "Paste Points" );
                    PasteCurve( spline );
                    guiEvent.Use();
                }


                if( pointSelection.Count > 0
                    && (guiEvent.keyCode == KeyCode.Delete || guiEvent.keyCode == KeyCode.Backspace) )
                {
                    Undo.RecordObject( spline.GetUndoObject(), "Delete Points" );
                    DeleteSelectedPoints( spline );
                    guiEvent.Use();
                }
            }
        }

        void RemovePoint(IEditableSpline spline, int index)
        {
            spline.RemoveCurvePoint( index );
            for( int i = pointSelection.Count - 1; i >= 0; --i )
            {
                if( pointSelection[i] == index )
                {
                    pointSelection.RemoveAt( i );
                    continue;
                }

                if( pointSelection[i] > index )
                {
                    pointSelection[i]--;
                }
            }
        }

        void SceneViewEventSetup(Event guiEvent)
        {
            doRepaint = false;
            useEvent = false;

            if( guiEvent.type == EventType.MouseEnterWindow )
            {
                SceneView.currentDrawingSceneView.Focus();
            }

            if( pointSelection.Count > 0 )
            {
                lastTool = Tools.current;
                Tools.current = Tool.None;
                editMode = SplineEditMode.MovePoint;
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

            switch( editMode )
            {
                case SplineEditMode.None:
                    DoMovePoint( spline, guiEvent );
                    break;
                case SplineEditMode.MovePoint:
                    DoMovePoint( spline, guiEvent );
                    break;
                case SplineEditMode.AddPoint:
                    if( addPointMode == SplineAddPointMode.Insert
                        && spline.GetCurvePointCount() >= 2 )
                    {
                        DoInsertPoint( spline, guiEvent );
                    }
                    else
                    {
                        DoAddPoint( spline, guiEvent );
                    }
                    break;
            }

            if( !movePointState.MovingControlPoint && (ShowGrid || guiEvent.alt) )
            {
                DrawGrid( spline, guiEvent );
            }

            // draw things
            DrawSpline( spline );

            if( guiEvent.isMouse || doRepaint )
            {
                SceneView.currentDrawingSceneView.Repaint();
            }

            // hacks: intercept unity object drag select
            Vector3 handlePosition = Camera.current.transform.position + Camera.current.transform.forward * 10;
            float handleSize = HandleUtility.GetHandleSize( handlePosition ) * 15;
            if( useEvent
                || (editMode == SplineEditMode.AddPoint && guiEvent.button == 0)
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
            ClearPointSelection();
            editMode = SplineEditMode.None;
            planeOffset = Vector3.zero;
            controlPlanePosition = Vector3.zero;
            addPointState = AddPointState.PointPosition;
            EditorUtility.SetDirty( target );
        }

        void DrawSplinePoints(IEditableSpline spline)
        {
            List<int> sortedPoints = GetPointIndiciesSelectedFirst( spline );
            sortedPoints.Reverse(); // draw selected on top
            for( int sortedI = 0; sortedI < sortedPoints.Count; ++sortedI )
            {
                int i = sortedPoints[sortedI];
                if( pointSelection.Contains( i ) )
                {
                    Handles.color = Color.green;
                }
                else
                {
                    Handles.color = Color.white;
                }

                Vector3 point = spline.GetCurvePoint( i ).position;
                Handles.SphereHandleCap( 0, point, spline.GetTransform().rotation, HandleCapSize, EventType.Repaint );
            }
            Handles.color = Color.white;
        }

        void DrawSplinePlaneProjectionLines(IEditableSpline spline)
        {
            for( int i = 0; i < spline.GetCurvePointCount(); ++i )
            {
                Vector3 point = spline.GetCurvePoint( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                Handles.DrawDottedLine( pointOnPlane, point, 2 );
            }
        }

        void DrawSplineLines(IEditableSpline spline)
        {
            for( int i = 0; i < spline.GetCurvePointCount(); ++i )
            {
                Vector3 point = spline.GetCurvePoint( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                if( i < spline.GetCurvePointCount() - 1 )
                {
                    Vector3 nextPoint = spline.GetCurvePoint( i + 1 ).position;
                    Handles.DrawLine( nextPoint, point );
                }
            }
        }

        static void DrawBezierSegment(CurvePoint point1, CurvePoint point2, Color color)
        {
            Handles.DrawBezier( point1.position, point2.position, point1.position + point1.Control2, point2.position + point2.Control1, color, null, 3 );
        }

        static void DrawBezierSegmentOnPlane(Vector3 planeOrigin, Vector3 planeNormal, CurvePoint point1, CurvePoint point2)
        {
            Vector3 pointOnPlane1 = GetPointOnPlaneY( planeOrigin, planeNormal, point1.position );
            Vector3 pointOnPlane2 = GetPointOnPlaneY( planeOrigin, planeNormal, point2.position );

            Vector3 tangentFlat1 = GetPointOnPlaneY( planeOrigin, planeNormal, point1.position + point1.Control2 );
            Vector3 tangentFlat2 = GetPointOnPlaneY( planeOrigin, planeNormal, point2.position + point2.Control1 );

            Handles.DrawBezier( pointOnPlane1, pointOnPlane2, tangentFlat1, tangentFlat2, Color.grey, null, 2.5f );
        }

        void DrawBezierSplineLines(IEditableSpline spline)
        {
            for( int i = 1; i < spline.GetCurvePointCount(); ++i )
            {
                DrawBezierSegment( spline.GetCurvePoint( i - 1 ), spline.GetCurvePoint( i ), spline.GetColor() );
            }

            if( spline.IsLoop() && spline.GetCurvePointCount() > 1 )
            {
                DrawBezierSegment( spline.GetCurvePoint( spline.GetCurvePointCount() - 1 ), spline.GetCurvePoint( 0 ), spline.GetColor() );
            }
        }

        void DrawBezierPlaneProjectedSplineLines(IEditableSpline spline)
        {
            for( int i = 1; i < spline.GetCurvePointCount(); ++i )
            {
                DrawBezierSegmentOnPlane( spline.GetTransform().position, spline.GetTransform().up, spline.GetCurvePoint( i - 1 ), spline.GetCurvePoint( i ) );
            }

            if( spline.IsLoop() && spline.GetCurvePointCount() > 1 )
            {
                DrawBezierSegmentOnPlane( spline.GetTransform().position, spline.GetTransform().up, spline.GetCurvePoint( spline.GetCurvePointCount() - 1 ), spline.GetCurvePoint( 0 ) );
            }
        }

        void DrawSplinePlaneProjectedSplineLines(IEditableSpline spline)
        {
            for( int i = 0; i < spline.GetCurvePointCount(); ++i )
            {
                Vector3 point = spline.GetCurvePoint( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                if( i < spline.GetCurvePointCount() - 1 )
                {
                    Vector3 nextPoint = spline.GetCurvePoint( i + 1 ).position;
                    Vector3 nextPointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, nextPoint );
                    Handles.DrawDottedLine( pointOnPlane, nextPointOnPlane, 2 );
                }
            }
        }

        void DrawSplineSelectionDisks(IEditableSpline spline)
        {
            ValidatePointSelection( spline );
            Transform transform = spline.GetTransform();
            Handles.color = Color.grey;
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];

                Vector3 point = spline.GetCurvePoint( index ).position;
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

        void DrawControlPoints(IEditableSpline spline, int index)
        {
            CurvePoint point = spline.GetCurvePoint( index );
            if( point.PointType == PointType.Point )
            {
                return;
            }

            if( pointSelection.Contains( index ) )
            {
                Handles.color = ControlPointEditColor;
            }
            else
            {
                Handles.color = Color.grey;
            }

            Vector3 control1 = point.position + point.Control1;
            Vector3 control2 = point.position + point.Control2;

            if( index > 0 || spline.IsLoop() )
            {
                Handles.SphereHandleCap( 0, control1, Quaternion.identity, HandleCapSize, EventType.Repaint );
                Handles.DrawLine( point.position, control1 );
            }

            if( index < spline.GetCurvePointCount() - 1 || spline.IsLoop() )
            {
                Handles.SphereHandleCap( 0, control2, Quaternion.identity, HandleCapSize, EventType.Repaint );
                Handles.DrawLine( point.position, control2 );
            }
        }

        void DrawSplineSelectionControlPoint(IEditableSpline spline)
        {
            if( !ShowCurvePointControls )
            {
                return;
            }

            if( pointSelection.Count > 0 )
            {
                for( int i = 0; i < pointSelection.Count; ++i )
                {
                    DrawControlPoints( spline, pointSelection[i] );
                }
            }
            else
            {
                for( int i = 0; i < spline.GetCurvePointCount(); ++i )
                {
                    DrawControlPoints( spline, i );
                }
            }
        }

        void DrawSpline(IEditableSpline spline)
        {
            DrawBezierPlaneProjectedSplineLines( spline );
            DrawSplinePlaneProjectionLines( spline );
            ;
            DrawSplineSelectionDisks( spline );
            DrawBezierSplineLines( spline );

            DrawSegmentLengths( spline );
            DrawSplinePoints( spline );
            DrawSplineSelectionControlPoint( spline );
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
            return index >= 0 && index < spline.GetCurvePointCount();
        }

        List<int> pointSelection = new List<int>();
        void SelectionAddPoint(IEditableSpline spline, int index)
        {
            ValidatePointSelection( spline );
            if( !IsIndexInRange( spline, index ) )
            {
                return;
            }

            if( pointSelection.Contains( index ) )
            {
                return;
            }
            pointSelection.Add( index );
        }

        void ValidatePointSelection(IEditableSpline spline)
        {
            for( int i = pointSelection.Count - 1; i >= 0; i-- )
            {
                if( !IsIndexInRange( spline, pointSelection[i] ) )
                {
                    pointSelection.RemoveAt( i );
                }
            }
        }

        void ClearPointSelection()
        {
            pointSelection.Clear();
            editMode = SplineEditMode.None;
        }

        List<int> GetPointIndiciesSelectedFirst(IEditableSpline spline)
        {
            List<int> pointIndicies = new List<int>();
            for( int i = 0; i < spline.GetCurvePointCount(); ++i )
            {
                pointIndicies.Add( i );
            }

            // selected points take priority
            pointIndicies.Sort( (int one, int two) =>
            {
                if( pointSelection.Contains( one ) == pointSelection.Contains( two ) )
                {
                    return 0;
                }
                if( pointSelection.Contains( one ) && !pointSelection.Contains( two ) )
                {
                    return -1;
                }
                return 1;
            } );

            return pointIndicies;
        }

        bool IsMouseButtonEvent(Event guiEvent, int button)
        {
            return guiEvent.button == button && guiEvent.isMouse && (guiEvent.type == EventType.MouseDown || guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp);
        }

        bool DoClickSelection(IEditableSpline spline, Event guiEvent)
        {
            bool clearSelection = IsMouseButtonEvent( guiEvent, 0 ) && !guiEvent.shift && !guiEvent.command;
            bool hasSelection = pointSelection.Count > 0;

            List<int> sortedPoints = GetPointIndiciesSelectedFirst( spline );
            for( int s = 0; s < sortedPoints.Count; ++s )
            {
                int index = sortedPoints[s];

                if( pointSelection.Contains( index ) )
                {
                    Handles.color = Color.green;
                }
                else
                {
                    Handles.color = Color.white;
                }

                CurvePoint curvePoint = spline.GetCurvePoint( index );

                bool overControl1 = IsMouseOverPoint( Camera.current, curvePoint.position + curvePoint.Control1, guiEvent.mousePosition );
                bool overControl2 = IsMouseOverPoint( Camera.current, curvePoint.position + curvePoint.Control2, guiEvent.mousePosition );

                bool control1Interactable = spline.IsLoop() || index > 0;
                bool control2Interactable = spline.IsLoop() || index < spline.GetCurvePointCount() - 1;

                bool control1Detected = overControl1 && control1Interactable;
                bool control2Detected = overControl2 && control2Interactable;

                bool controlsEnabled = ShowCurvePointControls && (pointSelection.Contains( index ) || pointSelection.Count == 0);

                if( controlsEnabled &&
                    curvePoint.PointType != PointType.Point &&
                    (control1Detected || control2Detected) )
                {
                    useEvent = true;
                    if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
                    {
                        clearSelection = false;
                        if( pointSelection.Count == 0 )
                        {
                            SelectionAddPoint( spline, index );
                            editMode = SplineEditMode.MovePoint;
                        }
                        break;
                    }
                }
                else if( IsMouseOverPoint( Camera.current, curvePoint.position, guiEvent.mousePosition ) )
                {
                    useEvent = true;
                    if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
                    {
                        clearSelection = false;
                        if( pointSelection.Contains( index ) && guiEvent.command )
                        {
                            pointSelection.Remove( index );
                        }
                        else if( !pointSelection.Contains( index ) )
                        {
                            if( !guiEvent.shift && !guiEvent.command )
                            {
                                ClearPointSelection();
                            }
                            SelectionAddPoint( spline, index );
                            editMode = SplineEditMode.MovePoint;
                        }
                        break;
                    }
                }
            }

            Handles.color = Color.white;
            return clearSelection;
        }

        void DebugLogEvent(Event guiEvent)
        {
            if( guiEvent.type != EventType.Layout && guiEvent.type != EventType.Repaint )
            {
                Debug.Log( guiEvent.type );
            }
        }

        Vector2 mouseDragSelectionStart;
        bool dragSelectActive = false;
        bool DoDragSelection(IEditableSpline spline, Event guiEvent)
        {
            bool clearSelection = IsMouseButtonEvent( guiEvent, 0 ) && !dragSelectActive;
            if( spline.GetCurvePointCount() > 0 && !dragSelectActive && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && guiEvent.shift )
            {
                mouseDragSelectionStart = guiEvent.mousePosition;
                dragSelectActive = true;

                for( int i = 0; i < spline.GetCurvePointCount(); ++i )
                {
                    CurvePoint curvePoint = spline.GetCurvePoint( i );
                    if( IsMouseOverPoint( Camera.current, curvePoint.position + curvePoint.Control1, guiEvent.mousePosition ) )
                    {
                        dragSelectActive = false;
                        break;
                    }
                    if( IsMouseOverPoint( Camera.current, curvePoint.position + curvePoint.Control2, guiEvent.mousePosition ) )
                    {
                        dragSelectActive = false;
                        break;
                    }
                    if( IsMouseOverPoint( Camera.current, curvePoint.position, guiEvent.mousePosition ) )
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
                for( int i = 0; i < spline.GetCurvePointCount(); ++i )
                {
                    Vector3 point = spline.GetCurvePoint( i ).position;
                    Vector3 pointScreenPosition = Camera.current.WorldToScreenPoint( point );
                    pointScreenPosition.z = 0;
                    if( dragBounds.Contains( pointScreenPosition ) )
                    {
                        ++pointsInDragBounds;
                        SelectionAddPoint( spline, i );
                        editMode = SplineEditMode.MovePoint;
                    }
                }

                if( pointsInDragBounds == 0 )
                {
                    clearSelection = !guiEvent.shift && !guiEvent.command;
                }

                dragSelectActive = false;
            }

            if( dragSelectActive )
            {
                useEvent = true;
            }
            return clearSelection;
        }

        bool hadSelectionOnMouseDown = false;
        void DoPointSelection(IEditableSpline spline, Event guiEvent)
        {
            if( guiEvent.type == EventType.MouseDown )
            {
                hadSelectionOnMouseDown = pointSelection.Count > 0;
            }
            if( guiEvent.type == EventType.Used && (pointSelection.Count > 0 || hadSelectionOnMouseDown) )
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
                ClearPointSelection();
            }
        }

        enum AddPointState
        {
            PointPosition,
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

        void DrawControlPointMovementGuides(IEditableSpline spline, Vector3 point, Vector3 control1, Vector3 control2)
        {
            DrawControlPointMovementGuides( spline, point, control1, control2, ControlPointEditColor, Color.white );
        }

        void DrawControlPointMovementGuides(IEditableSpline spline, Vector3 point, Vector3 control1, Vector3 control2, Color controlColour, Color controlPlaneColour)
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

        AddPointState addPointState = AddPointState.PointPosition;
        Vector3 addPointPosition;
        bool canShift = false;
        void DoAddPoint(IEditableSpline spline, Event guiEvent)
        {
            if( !guiEvent.shift )
            {
                canShift = true;
            }

            Selection.activeObject = (target as Component).gameObject;
            Transform handleTransform = spline.GetTransform();
            Transform transform = spline.GetTransform();
            Camera camera = Camera.current;

            Vector3 connectingPoint = transform.position;

            if( spline.GetCurvePointCount() > 0 )
            {
                int adjoiningIndex = 0;
                if( addPointMode == SplineAddPointMode.Append )
                {
                    adjoiningIndex = spline.GetCurvePointCount() - 1;
                }
                connectingPoint = spline.GetCurvePoint( adjoiningIndex ).position;
            }

            Vector3 connectingPlanePoint = GetPointOnPlaneY( transform.position, transform.up, connectingPoint );

            if( addPointState == AddPointState.PointPosition )
            {
                addPointPosition = GetPointPlacement(
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
                Ray down = new Ray( addPointPosition, -transform.up );
                if( Physics.Raycast( down, out hitDown ) )
                {
                    DrawWireDisk( hitDown.point, hitDown.normal, diskRadius, camera.transform.position );
                    Handles.DrawDottedLine( planePosition, hitDown.point, 2 );
                }

                DrawWireDisk( addPointPosition, transform.up, diskRadius, camera.transform.position );
                DrawWireDisk( planePosition, transform.up, diskRadius, camera.transform.position );
                Handles.DrawDottedLine( planePosition, addPointPosition, 2 );

                if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
                {
                    canShift = false;
                    addPointState = AddPointState.ControlPosition;
                    controlPlanePosition = addPointPosition;
                    controlPlaneOffset = Vector3.zero;
                    guiEvent.Use();
                }
            }

            Vector3 newControlPointPosition = addPointPosition;
            Vector3 relativeControlPoint = newControlPointPosition - addPointPosition;
            if( addPointMode == SplineAddPointMode.Prepend )
            {
                relativeControlPoint *= -1;
            }

            if( addPointState == AddPointState.ControlPosition )
            {
                newControlPointPosition = GetPointPlacement(
                        camera,
                        guiEvent.mousePosition,
                        addPointPosition,
                        transform.up,
                        ref controlPlanePosition,
                        ref controlPlaneOffset,
                        guiEvent.shift && canShift,
                        guiEvent.command,
                        guiEvent.alt );
                relativeControlPoint = newControlPointPosition - addPointPosition;
                if( addPointMode == SplineAddPointMode.Prepend )
                {
                    relativeControlPoint *= -1;
                }

                Vector3 worldControl1 = addPointPosition - relativeControlPoint;
                Vector3 worldControl2 = addPointPosition + relativeControlPoint;
                DrawControlPointMovementGuides( spline, addPointPosition, worldControl1, worldControl2, Color.yellow, Color.white );

                if( guiEvent.type == EventType.MouseUp && guiEvent.button == 0 )
                {
                    canShift = false;
                    addPointState = AddPointState.PointPosition;
                    Undo.RecordObject( spline.GetUndoObject(), "Add Point" );

                    CurvePoint newPoint = new CurvePoint( addPointPosition );
                    if( relativeControlPoint.magnitude > 0.01f )
                    {
                        newPoint.SetPointType( PointType.Mirrored );
                        newPoint.Control2 = relativeControlPoint; // this sets control 1 as well as it's mirrored
                    }

                    switch( addPointMode )
                    {
                        case SplineAddPointMode.Append:
                            spline.AppendCurvePoint( newPoint );
                            break;
                        case SplineAddPointMode.Prepend:
                            spline.PrependCurvePoint( newPoint );
                            break;
                        default:
                            spline.AppendCurvePoint( newPoint );
                            break;
                    }

                    EditorUtility.SetDirty( spline.GetComponent() );
                    guiEvent.Use();
                }
            }

            // draw beziers new point GUI
            Handles.color = Color.yellow;
            {
                int pointCount = spline.GetCurvePointCount();
                if( pointCount > 0 )
                {
                    CurvePoint newPoint = new CurvePoint( addPointPosition );
                    newPoint.Control1 = -relativeControlPoint; // this sets control 1 as well as it's mirrored
                    if( relativeControlPoint.magnitude > 0.01f )
                    {
                        newPoint.SetPointType( PointType.Mirrored );
                    }
                    newPoint.Control2 = relativeControlPoint; // this sets control 1 as well as it's mirrored


                    CurvePoint point1 = spline.GetCurvePoint( pointCount - 1 );
                    CurvePoint point2 = newPoint;
                    if( addPointMode == SplineAddPointMode.Prepend )
                    {
                        point1 = newPoint;
                        point2 = spline.GetCurvePoint( 0 );
                    }
                    Bezier3 addSegment = new Bezier3( point1, point2 );
                    Bezier3 projectedAddSegment = Bezier3.ProjectToPlane( addSegment, planePosition, transform.up );
                    Handles.DrawBezier( projectedAddSegment.A, projectedAddSegment.D, projectedAddSegment.B, projectedAddSegment.C, Color.grey, null, 1 );
                    Handles.DrawBezier( addSegment.A, addSegment.D, addSegment.B, addSegment.C, Color.yellow, null, 1 );


                    if( spline.IsLoop() )
                    {
                        point1 = newPoint;
                        point2 = spline.GetCurvePoint( 0 );
                        if( addPointMode == SplineAddPointMode.Prepend )
                        {
                            point1 = spline.GetCurvePoint( pointCount - 1 );
                            point2 = newPoint;
                        }
                        Bezier3 loopSegment = new Bezier3( point1, point2 );
                        Bezier3 projectedLoopSegment = Bezier3.ProjectToPlane( loopSegment, planePosition, transform.up );
                        Handles.DrawBezier( projectedLoopSegment.A, projectedLoopSegment.D, projectedLoopSegment.B, projectedLoopSegment.C, Color.grey, null, 1 );
                        Handles.DrawBezier( loopSegment.A, loopSegment.D, loopSegment.B, loopSegment.C, Color.yellow, null, 1 );
                    }
                }
            }

            Handles.SphereHandleCap( 0, addPointPosition, Quaternion.identity, HandleCapSize, guiEvent.type );
            Handles.color = Color.white;
        }

        void DoInsertPoint(IEditableSpline spline, Event guiEvent)
        {
            Selection.activeObject = (target as Component).gameObject;

            Transform handleTransform = spline.GetTransform();
            Transform transform = spline.GetTransform();
            Camera camera = Camera.current;

            Ray mouseRay = MousePositionToRay( camera, guiEvent.mousePosition );
            SplineResult closestToRay = spline.GetResultClosestTo( mouseRay );
            Vector3 newPointPosition = closestToRay.position;

            planePosition = MathHelper.LinePlaneIntersection( newPointPosition, transform.up, transform.position, transform.up );
            planeOffset = newPointPosition - planePosition;

            // new point
            Handles.color = Color.yellow;
            Handles.SphereHandleCap( 0, newPointPosition, transform.rotation, HandleCapSize, guiEvent.type );
            DrawWireDisk( newPointPosition, transform.up, diskRadius, camera.transform.position );
            DrawWireDisk( planePosition, transform.up, diskRadius, camera.transform.position );
            Handles.DrawLine( planePosition, newPointPosition );
            Handles.color = Color.white;

            RaycastHit hitDown;
            Ray down = new Ray( newPointPosition, -transform.up );
            if( Physics.Raycast( down, out hitDown ) )
            {
                DrawWireDisk( hitDown.point, hitDown.normal, diskRadius, camera.transform.position );
                Handles.DrawDottedLine( planePosition, hitDown.point, 2 );
            }

            if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
            {
                Undo.RecordObject( spline.GetUndoObject(), "Insert Point" );
                spline.InsertCurvePoint( closestToRay.splineT );
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


        struct MovePointState
        {
            public int CurvePointIndex { get; private set; }
            public MoveControlPointId ControlPointId { get; set; }

            public Vector2 StartMovementAccumulator { get; set; }
            public Vector3 LastSnappedMovement { get; set; }

            public Vector3 CurrentPosition { get; set; }
            public Vector3 StartPosition { get; private set; }

            public bool Moving { get; private set; }
            public bool MovingControlPoint { get { return CurvePointIndex != -1 && ControlPointId != MoveControlPointId.None; } }

            public bool WasSnapping { get; set; }

            public MovePointState(int curvePointIndex, MoveControlPointId controlPointId, Vector3 startPosition)
            {
                Moving = true;
                StartMovementAccumulator = Vector2.zero;
                LastSnappedMovement = Vector3.zero;
                WasSnapping = false;
                WasSnapping = false;
                this.ControlPointId = controlPointId;
                this.CurvePointIndex = curvePointIndex;
                this.StartPosition = CurrentPosition = startPosition;
            }
        }

        MovePointState movePointState;

        const float startMovementThreshold = 5;

        void DetectPointToMove(IEditableSpline spline, Event guiEvent)
        {
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];
                CurvePoint point = spline.GetCurvePoint( index );
                Vector3 worldControl1 = point.Control1 + point.position;
                Vector3 worldControl2 = point.Control2 + point.position;

                bool overControl1 = IsMouseOverPoint( Camera.current, worldControl1, guiEvent.mousePosition );
                bool overControl2 = IsMouseOverPoint( Camera.current, worldControl2, guiEvent.mousePosition );

                bool control1Interactable = spline.IsLoop() || index > 0;
                bool control2Interactable = spline.IsLoop() || index < spline.GetCurvePointCount() - 1;

                bool control1Detected = overControl1 && control1Interactable && point.PointType != PointType.Point;
                bool control2Detected = overControl2 && control2Interactable && point.PointType != PointType.Point;

                if( control1Detected
                    && control2Detected
                    && Vector3.Distance( worldControl1, worldControl2 ) <= HandleCapSize )
                {
                    // if control points are ontop of eachother 
                    // we need to determin which to move later on when we know the drag direction
                    controlPlanePosition = GetPointOnPlaneY( point.position, spline.GetTransform().up, worldControl2 );
                    controlPlaneOffset = worldControl2 - controlPlanePosition;

                    movePointState = new MovePointState( index, MoveControlPointId.Unknown, worldControl2 );
                }
                else if( control1Detected )
                {
                    controlPlanePosition = GetPointOnPlaneY( point.position, spline.GetTransform().up, worldControl1 );
                    controlPlaneOffset = worldControl1 - controlPlanePosition;
                    movePointState = new MovePointState( index, MoveControlPointId.Control1, worldControl1 );
                }
                else if( control2Detected )
                {
                    controlPlanePosition = GetPointOnPlaneY( point.position, spline.GetTransform().up, worldControl2 );
                    controlPlaneOffset = worldControl2 - controlPlanePosition;
                    movePointState = new MovePointState( index, MoveControlPointId.Control2, worldControl2 );
                }
                // curve point
                else if( IsMouseOverPoint( Camera.current, point.position, guiEvent.mousePosition ) )
                {
                    movePointState = new MovePointState( index, MoveControlPointId.None, point.position );
                }

                if( movePointState.Moving )
                {
                    planePosition = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.position );
                    planeOffset = point.position - planePosition;
                    break;
                }
            }
        }

        void DoMovePoint(IEditableSpline spline, Event guiEvent)
        {
            Transform transform = spline.GetTransform();

            if( !movePointState.Moving )
            {
                DoPointSelection( spline, guiEvent );
            }

            if( !movePointState.Moving && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
            {
                DetectPointToMove( spline, guiEvent );
            }
            else if( movePointState.Moving && (guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp) && guiEvent.button == 0 )
            {
                Vector2 screenDelta = TransformMouseDeltaToScreenDelta( guiEvent.delta );
                Vector3 newPoint = movePointState.CurrentPosition;

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
                        Vector3 screenPoint = Camera.current.WorldToScreenPoint( movePointState.CurrentPosition ) + new Vector3( screenDelta.x, screenDelta.y, 0 );
                        Ray projectionRay = Camera.current.ScreenPointToRay( screenPoint );
                        Vector3 verticalPlaneNormal = Vector3.Cross( transform.up, Vector3.Cross( transform.up, projectionRay.direction ) );
                        Vector3 screenWorldPosition = MathHelper.LinePlaneIntersection( projectionRay, movePointState.CurrentPosition, verticalPlaneNormal );
                        newPoint = movePointState.CurrentPosition + transform.up * Vector3.Dot( transform.up, screenWorldPosition - movePointState.CurrentPosition );
                    }
                    else
                    {
                        // relative pointer tracking
                        Vector3 screenPoint = Camera.current.WorldToScreenPoint( movePointState.CurrentPosition ) + new Vector3( screenDelta.x, screenDelta.y, 0 );
                        newPoint = MathHelper.LinePlaneIntersection( Camera.current.ScreenPointToRay( screenPoint ), movePointState.CurrentPosition, transform.up );
                    }
                }

                movePointState.CurrentPosition = newPoint;

                Vector3 totalMovement = newPoint - movePointState.StartPosition;

                if( Mathf.Abs( movePointState.StartMovementAccumulator.magnitude ) < startMovementThreshold )
                {
                    movePointState.StartMovementAccumulator += guiEvent.delta;
                }
                else if( totalMovement.magnitude > 0 )
                {
                    Vector3 snappedMovement = totalMovement;

                    if( guiEvent.alt )
                    {
                        Vector3 newWorldPoint = totalMovement + movePointState.StartPosition;

                        Vector3 origin = transform.position;
                        if( movePointState.MovingControlPoint )
                        {
                            origin = spline.GetCurvePoint( movePointState.CurvePointIndex ).position;
                        }

                        Vector3 filter = Vector3.one;
                        /*if( Mathf.Abs(snappedMovement.x) <= 0 || guiEvent.shift )
                        {
                            filter.x = 0;
                        }
                        if( Mathf.Abs(snappedMovement.z) <= 0 || guiEvent.shift )
                        {
                            filter.z = 0;
                        }

                        if( Mathf.Abs(snappedMovement.y) <= 0 || !guiEvent.shift )
                        {
                            filter.y = 0;
                        }*/

                        float gridScale = (movePointState.MovingControlPoint) ? 0.2f : 1.0f;

                        Vector3 newSnappedWorldPoint = SnapWorldPointToGrid( newWorldPoint, origin, filter, gridScale );
                        snappedMovement = newSnappedWorldPoint - movePointState.StartPosition;
                    }

                    Vector3 snappedDelta = snappedMovement - movePointState.LastSnappedMovement;

                    Undo.RecordObject( spline.GetUndoObject(), "Move Points" );
                    if( movePointState.MovingControlPoint )
                    {
                        CurvePoint curvePoint = spline.GetCurvePoint( movePointState.CurvePointIndex );
                        if( movePointState.ControlPointId == MoveControlPointId.Unknown )
                        {
                            Vector3 worldControl = curvePoint.position + curvePoint.Control2;
                            float directionTestForward = 0;
                            float directionTestBackward = 0;
                            if( spline.GetCurvePointCount() > movePointState.CurvePointIndex + 1 || spline.IsLoop() )
                            {
                                int nextIndex = MathHelper.WrapIndex( movePointState.CurvePointIndex + 1, spline.GetCurvePointCount() );
                                directionTestForward = Vector3.Dot( totalMovement.normalized, (spline.GetCurvePoint( nextIndex ).position - curvePoint.position).normalized );
                            }

                            if( movePointState.CurvePointIndex - 1 >= 0 || spline.IsLoop() )
                            {
                                int nextIndex = MathHelper.WrapIndex( movePointState.CurvePointIndex - 1, spline.GetCurvePointCount() );
                                directionTestBackward = Vector3.Dot( totalMovement.normalized, (spline.GetCurvePoint( movePointState.CurvePointIndex - 1 ).position - curvePoint.position).normalized );
                            }

                            if( directionTestForward >= directionTestBackward )
                            {
                                movePointState.ControlPointId = MoveControlPointId.Control2;
                                worldControl = curvePoint.position + curvePoint.Control2;
                            }
                            else
                            {
                                movePointState.ControlPointId = MoveControlPointId.Control1;
                                worldControl = curvePoint.position + curvePoint.Control1;
                            }

                            controlPlanePosition = GetPointOnPlaneY( curvePoint.position, spline.GetTransform().up, worldControl );
                            controlPlaneOffset = worldControl - controlPlanePosition;
                        }

                        if( movePointState.ControlPointId == MoveControlPointId.Control1 )
                        {
                            curvePoint.Control1 = curvePoint.Control1 + snappedDelta;
                        }
                        else
                        {
                            curvePoint.Control2 = curvePoint.Control2 + snappedDelta;
                        }
                        spline.SetCurvePoint( movePointState.CurvePointIndex, curvePoint );
                    }
                    else
                    {
                        for( int i = 0; i < pointSelection.Count; ++i )
                        {
                            int index = pointSelection[i];
                            CurvePoint curvePoint = spline.GetCurvePoint( index );
                            curvePoint.position = curvePoint.position + snappedDelta;
                            spline.SetCurvePoint( index, curvePoint );
                        }
                    }

                    movePointState.LastSnappedMovement = snappedMovement;

                    CurvePoint latestCurvePoint = spline.GetCurvePoint( movePointState.CurvePointIndex );
                    planePosition = GetPointOnPlaneY( transform.position, transform.up, latestCurvePoint.position );
                    planeOffset = latestCurvePoint.position - planePosition;
                }

                if( guiEvent.type == EventType.MouseUp )
                {
                    movePointState = new MovePointState();
                }
            }

            if( movePointState.Moving )
            {
                useEvent = true;
            }

            if( movePointState.MovingControlPoint )
            {
                CurvePoint curvePoint = spline.GetCurvePoint( movePointState.CurvePointIndex );
                if( ShowGrid || guiEvent.alt )
                {
                    DrawGrid( curvePoint.position, transform.forward, transform.right, 0.2f, 5, guiEvent );
                }
                DrawControlPointMovementGuides( spline, curvePoint.position, curvePoint.Control1 + curvePoint.position, curvePoint.Control2 + curvePoint.position );
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
    }
}
