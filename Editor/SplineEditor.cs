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

    
    [CustomEditor( typeof( SplineBehaviour ),true )]
    public class SplineEditor : Editor
    {
        static List<CurvePoint> clipboard = new List<CurvePoint>();

        SplineEditMode editMode = SplineEditMode.None;
        SplineAddPointMode addPointMode = SplineAddPointMode.Append;
        float diskRadius = 0.2f;
        float handleCapSize = 0.1f;
        Vector3 planePosition;
        Vector3 planeOffset;
        Vector3 controlPlanePosition;
        Vector3 controlPlaneOffset;

        void OnEnable()
        {
            ResetEditMode();
        }
        void OnDisable()
        {
            Tools.current = Tool.Move;
        }

        void SetSelectionPointType( IEditableSpline spline, PointType type)
        {
            Undo.RecordObject( target, "Set Point Type" );
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];
                CurvePoint point = spline.GetPoint( index );
                point.SetPointType( type );
                spline.SetPoint( index, point );
            }
            EditorUtility.SetDirty( target );
        }

        Bounds GetSelectionBounds( IEditableSpline spline )
        {
            if( pointSelection.Count == 0 )
            {
                return GetBounds( spline );
            }

            Bounds bounds = new Bounds( spline.GetPoint(pointSelection[0]).position, Vector3.one * 0.1f );
            for( int i = 1; i < pointSelection.Count; ++i )
            {
                bounds.Encapsulate( spline.GetPoint(pointSelection[i]).position );
            }

            if( bounds.size.magnitude < 1 )
            {
                bounds.size = bounds.size.normalized * 1;
            }
            
            return bounds;
        }

        Bounds GetBounds( IEditableSpline spline )
        {
            Bounds bounds = new Bounds( spline.GetPoint(0).position, Vector3.zero );
            for( int i = 1; i < spline.GetPointCount(); ++i )
            {
                bounds.Encapsulate( spline.GetPoint(i).position );
            }
            return bounds;
        }

        public void FrameCamera( IEditableSpline spline )
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
            SceneView.lastActiveSceneView.Frame(bounds, false);
        }

        void FlattenSelection(IEditableSpline spline, PointType type)
        {
            Undo.RecordObject( target, "Flatten Selection" );
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];
                CurvePoint point = spline.GetPoint( index );

                point.position = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.position );
                point.Control1 = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.Control1 + point.position ) - point.position;
                point.Control2 = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.Control2 + point.position ) - point.position;

                spline.SetPoint( index, point );
            }
            EditorUtility.SetDirty( target );
        }

        void SmoothSelection( IEditableSpline spline, PointType type )
        {
            Undo.RecordObject( target, "Set Point Type" );
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];
                CurvePoint point = spline.GetPoint( index );
                CurvePoint before = point;
                CurvePoint after = point;

                int beforeIndex = index - 1;
                if( beforeIndex < 0 )
                {
                    beforeIndex = spline.GetPointCount()-1;
                }
                before = spline.GetPoint( beforeIndex );

                int afterIndex = (index + 1) % spline.GetPointCount();
                after = spline.GetPoint( afterIndex );

                Vector3 direction = after.position - before.position;
                float dist1 = Mathf.Min( (point.position - before.position).magnitude, direction.magnitude );
                float dist2 = Mathf.Min( (point.position - after.position).magnitude, direction.magnitude );
                
                point.SetPointType( PointType.Aligned );
                point.Control1 = -direction.normalized * dist1 * 0.4f;
                point.Control2 = direction.normalized * dist2 * 0.4f;

                if( !spline.IsLoop() )
                {
                    if( index == 0 || index == spline.GetPointCount() - 1 )
                    {
                        point.SetPointType( PointType.Point );
                    }
                }

                spline.SetPoint( index, point );
            }
            EditorUtility.SetDirty( target );
        }

        public void DeleteSelectedPoints( IEditableSpline spline )
        {
            int removePoints = pointSelection.Count;
            for( int i = 0; i < removePoints; ++i )
            {
                RemovePoint( spline, pointSelection[0] );
            }
            ClearPointSelection();
            EditorUtility.SetDirty( target );
        }

        void PasteCurve( IEditableSpline spline )
        {
            DeleteSelectedPoints( spline );
            int oldPointCount = spline.GetPointCount();
            for( int i = 0; i < clipboard.Count; i++ )
            {
                spline.AddPoint( clipboard[i] );
            }

            pointSelection.Clear();
            for( int i = oldPointCount; i < spline.GetPointCount(); i++ )
            {
                pointSelection.Add( i );
            }
        }

        public void CopyCurve( IEditableSpline spline )
        {
            clipboard.Clear();
            List<int> copyIndicies = new List<int>( pointSelection );
            copyIndicies.Sort();
            for( int i = 0; i < copyIndicies.Count; i++ )
            {
                int index = copyIndicies[i];
                clipboard.Add( spline.GetPoint(index) );
            }
        }

        public static void ShowScriptGUI(MonoBehaviour script)
        {           
            if (script != null)
            {
                MonoScript theScript = MonoScript.FromMonoBehaviour(script);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Script", theScript, script.GetType(), false);
                }
            }
        }

        void StartAddPointMode( SplineAddPointMode mode )
        {
            ResetEditMode();
            editMode = SplineEditMode.AddPoint;
            addPointMode = mode;
            
            IEditableSpline spline = target as IEditableSpline;
            Transform transform = spline.GetTransform();
            Vector3 adjoiningPointPosition = transform.position;

            if( spline.GetPointCount() > 0 )
            {
                int adjoiningIndex = 0;
                if( addPointMode == SplineAddPointMode.Append )
                {
                    adjoiningIndex = spline.GetPointCount() - 1;
                }
                adjoiningPointPosition = spline.GetPoint( adjoiningIndex ).position;
            }

            planePosition = MathHelper.LinePlaneIntersection( adjoiningPointPosition, transform.up, transform.position, transform.up );
            planeOffset = adjoiningPointPosition - planePosition;
        }

        void InspectorAddPointModeButton( SplineAddPointMode mode )
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
            IEditableSpline spline = target as IEditableSpline;
            ShowScriptGUI(spline as MonoBehaviour);

            GUILayout.Label( "Edit Modes" );
            InspectorAddPointModeButton( SplineAddPointMode.Append );
            InspectorAddPointModeButton( SplineAddPointMode.Prepend );
            InspectorAddPointModeButton( SplineAddPointMode.Insert );


            EditorGUI.BeginChangeCheck();
            bool loop = GUILayout.Toggle( spline.IsLoop(), "Loop" );
            if( EditorGUI.EndChangeCheck() )
            {
                Undo.RecordObject( target, "Loop Toggle" );
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
            GUILayout.Label( "Gizmos" );
            EditorGUI.BeginChangeCheck();
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
            GUILayout.Label( "Moving Point: " + Moving.ToString() );
            GUILayout.Label( "Length: " + (spline as ISpline).GetLength(0f,1f) );

            DrawDefaultInspector();
        }

        public static bool ShowSegmentLengths
        {
            get => EditorPrefs.GetBool("FantasticSplinesShowSegmentLengths", false);
            set => EditorPrefs.SetBool("FantasticSplinesShowSegmentLengths", value);
        }

        public static bool ShowCurvePointControls
        {
            get => EditorPrefs.GetBool("FantasticSplinesShowCurvePointControls", true);
            set => EditorPrefs.SetBool("FantasticSplinesShowCurvePointControls", value);
        }

        float rightClickStart;
        const float rightClickTime = 0.2f;
        bool useEvent = false;

        Tool lastTool = Tool.Move;
        void SetTool( Tool newTool )
        {
            lastTool = newTool;
            if( pointSelection.Count == 0 )
            {
                Tools.current = lastTool;
            }
        }

        Vector2 rightClickMovement = Vector2.zero;
        void RightClickCancel( Event guiEvent )
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

        void KeyboardInputs( IEditableSpline spline, Event guiEvent )
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
                    for( int i = 0; i < spline.GetPointCount(); ++i )
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
                    Undo.RecordObject( target, "Cut Points" );
                    CopyCurve( spline );
                    DeleteSelectedPoints( spline );
                    guiEvent.Use();
                }

                if( guiEvent.command && guiEvent.keyCode == KeyCode.V )
                {
                    Undo.RecordObject( target, "Paste Points" );
                    PasteCurve( spline );
                    guiEvent.Use();
                }


                if( pointSelection.Count > 0
                    && (guiEvent.keyCode == KeyCode.Delete || guiEvent.keyCode == KeyCode.Backspace) )
                {
                    Undo.RecordObject( target, "Delete Points" );
                    DeleteSelectedPoints( spline );
                    guiEvent.Use();
                }
            }
        }

        void RemovePoint( IEditableSpline spline, int index )
        {
            spline.RemovePoint( index );
            for( int i = pointSelection.Count-1; i >= 0; --i )
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

        void SceneViewEventSetup( Event guiEvent )
        {
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

        void OnSceneGUI()
        {
            Event guiEvent = Event.current;
            IEditableSpline spline = target as IEditableSpline;

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
                        && spline.GetPointCount() >= 2 )
                    {
                        DoInsertPoint( spline, guiEvent );
                    }
                    else
                    {
                        DoAddPoint( spline, guiEvent );
                    }
                    break;
            }

            // draw things
            DrawSpline( spline );
            if( guiEvent.isMouse )
            {
                SceneView.currentDrawingSceneView.Repaint();
            }
            
            // hacks: intercept unity object drag select
            Vector3 handlePosition = Camera.current.transform.position + Camera.current.transform.forward * 10;
            float handleSize = HandleUtility.GetHandleSize( handlePosition ) * 15;
            if( useEvent 
                || (editMode == SplineEditMode.AddPoint && guiEvent.button == 0)
                || (guiEvent.shift && ( guiEvent.type == EventType.Layout || guiEvent.type == EventType.Repaint ) ) )
            {
                if( Handles.Button( handlePosition, Camera.current.transform.rotation, 0, handleSize, Handles.DotHandleCap ) )
                {
                    guiEvent.Use();
                }
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

                Vector3 point = spline.GetPoint( i ).position;
                Handles.SphereHandleCap( 0, point, spline.GetTransform().rotation, handleCapSize, EventType.Repaint );
            }
            Handles.color = Color.white;
        }

        void DrawSplinePlaneProjectionLines(IEditableSpline spline)
        {
            for( int i = 0; i < spline.GetPointCount(); ++i )
            {
                Vector3 point = spline.GetPoint( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                Handles.DrawDottedLine( pointOnPlane, point, 2 );
            }
        }

        void DrawSplineLines(IEditableSpline spline)
        {
            for( int i = 0; i < spline.GetPointCount(); ++i )
            {
                Vector3 point = spline.GetPoint( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                if( i < spline.GetPointCount() - 1 )
                {
                    Vector3 nextPoint = spline.GetPoint( i + 1 ).position;
                    Handles.DrawLine( nextPoint, point );
                }
            }
        }

        static void DrawBezierSegment( CurvePoint point1, CurvePoint point2 )
        {
            Handles.DrawBezier( point1.position, point2.position, point1.position + point1.Control2, point2.position + point2.Control1, Color.white, null, 2 );
        }

        static void DrawBezierSegmentOnPlane( Vector3 planeOrigin, Vector3 planeNormal, CurvePoint point1, CurvePoint point2 )
        {
            Vector3 pointOnPlane1 = GetPointOnPlaneY( planeOrigin, planeNormal, point1.position );
            Vector3 pointOnPlane2 = GetPointOnPlaneY( planeOrigin, planeNormal, point2.position );

            Vector3 tangentFlat1 = GetPointOnPlaneY( planeOrigin, planeNormal, point1.position + point1.Control2 );
            Vector3 tangentFlat2 = GetPointOnPlaneY( planeOrigin, planeNormal, point2.position + point2.Control1 );

            Handles.DrawBezier( pointOnPlane1, pointOnPlane2, tangentFlat1, tangentFlat2, Color.grey, null, 2 );
        }

        void DrawBezierSplineLines(IEditableSpline spline)
        {
            for( int i = 1; i < spline.GetPointCount(); ++i )
            {
                DrawBezierSegment( spline.GetPoint( i-1 ), spline.GetPoint( i ) );
            }
        
            if( spline.IsLoop() && spline.GetPointCount() > 1)
            {
                DrawBezierSegment( spline.GetPoint( spline.GetPointCount()-1 ), spline.GetPoint( 0 ) );
            }
        }

        void DrawBezierPlaneProjectedSplineLines(IEditableSpline spline)
        {
            for( int i = 1; i < spline.GetPointCount(); ++i )
            {
                DrawBezierSegmentOnPlane( spline.GetTransform().position, spline.GetTransform().up, spline.GetPoint( i-1 ), spline.GetPoint( i ) );
            }
        
            if( spline.IsLoop() && spline.GetPointCount() > 1)
            {
                DrawBezierSegmentOnPlane( spline.GetTransform().position, spline.GetTransform().up, spline.GetPoint( spline.GetPointCount()-1 ), spline.GetPoint( 0 ) );
            }
        }

        void DrawSplinePlaneProjectedSplineLines(IEditableSpline spline)
        {
            for( int i = 0; i < spline.GetPointCount(); ++i )
            {
                Vector3 point = spline.GetPoint( i ).position;
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point );
                if( i < spline.GetPointCount() - 1 )
                {
                    Vector3 nextPoint = spline.GetPoint( i + 1 ).position;
                    Vector3 nextPointOnPlane = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, nextPoint );
                    Handles.DrawDottedLine( pointOnPlane, nextPointOnPlane, 2 );
                }
            }
        }

        void DrawSplineSelectionDisks( IEditableSpline spline)
        {
            ValidatePointSelection( spline );
            Transform transform = spline.GetTransform();
            Handles.color = Color.grey;
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];

                Vector3 point = spline.GetPoint( index ).position;
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

        void DrawControlPoints( IEditableSpline spline, int index )
        {
            CurvePoint point = spline.GetPoint( index );
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
                Handles.SphereHandleCap( 0, control1, Quaternion.identity, handleCapSize, EventType.Repaint );
                Handles.DrawLine( point.position, control1 );
            }

            if( index < spline.GetPointCount()-1 || spline.IsLoop() )
            {
                Handles.SphereHandleCap( 0, control2, Quaternion.identity, handleCapSize, EventType.Repaint );
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
                for( int i = 0; i < spline.GetPointCount(); ++i )
                {
                    DrawControlPoints( spline, i );
                }
            }
        }

        void DrawSpline(IEditableSpline spline)
        {
            DrawBezierSplineLines( spline );
            DrawSplinePlaneProjectionLines( spline );
            DrawBezierPlaneProjectedSplineLines( spline );
            DrawSplineSelectionDisks( spline );

            DrawSplinePoints( spline );
            DrawSplineSelectionControlPoint( spline );

            DrawSegmentLengths( spline );
        }

        void DrawSegmentLengths( IEditableSpline spline )
        {
            if( ShowSegmentLengths )
            {
                spline.DrawSegmentLengths();
            }
        }

        bool IsIndexInRange( IEditableSpline spline, int index )
        {
            return index >= 0 && index < spline.GetPointCount();
        }

        List<int> pointSelection = new List<int>();
        void SelectionAddPoint(IEditableSpline spline, int index)
        {
            ValidatePointSelection( spline );
            if(!IsIndexInRange(spline, index) )
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

        List<int> GetPointIndiciesSelectedFirst( IEditableSpline spline )
        {
            List<int> pointIndicies = new List<int>();
            for( int i = 0; i < spline.GetPointCount(); ++i )
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

        bool IsMouseButtonEvent(Event guiEvent, int button )
        {
            return guiEvent.button == button && guiEvent.isMouse && ( guiEvent.type == EventType.MouseDown || guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp );
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
                
                CurvePoint curvePoint = spline.GetPoint( index );

                bool overControl1 = IsMouseOverPoint( Camera.current, curvePoint.position + curvePoint.Control1, guiEvent.mousePosition );
                bool overControl2 = IsMouseOverPoint( Camera.current, curvePoint.position + curvePoint.Control2, guiEvent.mousePosition );

                bool control1Interactable = spline.IsLoop() || index > 0;
                bool control2Interactable = spline.IsLoop() || index < spline.GetPointCount()-1;

                bool control1Detected = overControl1 && control1Interactable;
                bool control2Detected = overControl2 && control2Interactable;

                bool controlsEnabled = ShowCurvePointControls && (pointSelection.Contains( index ) || pointSelection.Count == 0);

                if( controlsEnabled &&
                    curvePoint.PointType != PointType.Point &&
                    ( control1Detected || control2Detected ) )
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
            if( spline.GetPointCount() > 0 && !dragSelectActive && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && guiEvent.shift )
            {
                mouseDragSelectionStart = guiEvent.mousePosition;
                dragSelectActive = true;

                for( int i = 0; i < spline.GetPointCount(); ++i )
                {
                    CurvePoint curvePoint = spline.GetPoint( i );
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
                Vector2 extents = Vector2.Max( diff, -diff )*0.5f;
                Vector2 position = (mouseDragSelectionStart + guiEvent.mousePosition) * 0.5f;

                Vector2 pos1 = new Vector2( position.x + extents.x, position.y - extents.y);
                Vector2 pos2 = new Vector2( position.x + extents.x, position.y + extents.y);
                Vector2 pos3 = new Vector2( position.x - extents.x, position.y + extents.y);
                Vector2 pos4 = new Vector2( position.x - extents.x, position.y - extents.y);

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
                for( int i = 0; i < spline.GetPointCount(); ++i )
                {
                    Vector3 point = spline.GetPoint( i ).position;
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
                Selection.activeObject = spline.GetTransform().gameObject;
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

        static Vector3 GetPointPlacement( Camera camera, Vector2 mousePosition, Vector3 origin, Vector3 up, ref Vector3 planePoint, ref Vector3 planeOffset, bool verticalDisplace, bool intersectPhsyics )
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

            return planePoint + planeOffset;
        }

        void DrawControlPointMovementGuides( IEditableSpline spline, Vector3 point, Vector3 control1, Vector3 control2)
        {
            Camera camera = Camera.current;
            Transform transform = spline.GetTransform();

            Vector3 controlPlane1 = GetPointOnPlaneY( point, transform.up, control1 );
            Vector3 controlPlane2 = GetPointOnPlaneY( point, transform.up, control2 );
            
            Handles.color = ControlPointEditColor;
            // draw control placement GUI
            Handles.SphereHandleCap( 0, control1, Quaternion.identity, handleCapSize*0.5f, EventType.Repaint );
            Handles.SphereHandleCap( 0, control2, Quaternion.identity, handleCapSize*0.5f, EventType.Repaint );

            DrawWireDisk( control1, transform.up, diskRadius*0.5f, camera.transform.position );
            DrawWireDisk( control2, transform.up, diskRadius*0.5f, camera.transform.position );

            Handles.DrawDottedLine( point, control1, 2 );
            Handles.DrawDottedLine( point, control2, 2 );

            Handles.color = Color.white;
            DrawWireDisk( controlPlane1, transform.up, diskRadius*0.5f, camera.transform.position );
            DrawWireDisk( controlPlane2, transform.up, diskRadius*0.5f, camera.transform.position );

            Handles.DrawDottedLine( point, controlPlane1, 2 );
            Handles.DrawDottedLine( point, controlPlane2, 2 );

            Handles.DrawDottedLine( control1, controlPlane1, 2 );
            Handles.DrawDottedLine( control2, controlPlane2, 2 );
            
            Handles.color = Color.white;
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

            Selection.activeGameObject = spline.GetTransform().gameObject;
            Transform handleTransform = spline.GetTransform();
            Transform transform = spline.GetTransform();
            Camera camera = Camera.current;

            Vector3 connectingPoint = transform.position;
            if( spline.GetPointCount() > 0 )
            {
                int adjoiningIndex = 0;
                if( addPointMode == SplineAddPointMode.Append )
                {
                    adjoiningIndex = spline.GetPointCount() - 1;
                }
                connectingPoint = spline.GetPoint( adjoiningIndex ).position;
            }

            Vector3 connectingPlanePoint = GetPointOnPlaneY( transform.position, transform.up, connectingPoint );

            if( addPointState == AddPointState.PointPosition )
            {
                addPointPosition = GetPointPlacement( camera, guiEvent.mousePosition, transform.position, transform.up, ref planePosition, ref planeOffset, guiEvent.shift && canShift, guiEvent.command );
            
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
                Handles.DrawDottedLine( connectingPlanePoint, planePosition, 2 );
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
            if( addPointState == AddPointState.ControlPosition )
            {
                Vector3 newControlPointPosition = GetPointPlacement( 
                    camera, 
                    guiEvent.mousePosition, 
                    addPointPosition, 
                    transform.up, 
                    ref controlPlanePosition, 
                    ref controlPlaneOffset, 
                    guiEvent.shift && canShift, 
                    guiEvent.command );
            
                Vector3 relativeControlPoint = newControlPointPosition - addPointPosition;

                Vector3 control1 = addPointPosition + relativeControlPoint;
                Vector3 control2 = addPointPosition - relativeControlPoint;

                DrawControlPointMovementGuides( spline, addPointPosition, control1, control2 );

                if( guiEvent.type == EventType.MouseUp && guiEvent.button == 0 )
                {
                    canShift = false;
                    addPointState = AddPointState.PointPosition;
                    Undo.RecordObject( target, "Add Point" );
                    Vector3 controlPosition = newControlPointPosition - addPointPosition;

                    CurvePoint newPoint = new CurvePoint( addPointPosition );
                    if( controlPosition.magnitude > 0.01f )
                    {
                        Vector3 control = newControlPointPosition - addPointPosition;
                        newPoint.SetPointType( PointType.Mirrored );
                        newPoint.Control2 = control; // this sets control 1 as well as it's mirrored
                    }

                    switch( addPointMode )
                    {
                        case SplineAddPointMode.Append:
                            spline.AddPoint( newPoint );
                            break;
                        case SplineAddPointMode.Prepend:
                            spline.AddPointAt( 0, newPoint );
                            break;
                        default:
                            spline.AddPoint( newPoint );
                            break;
                    }

                    EditorUtility.SetDirty( spline.GetComponent() );
                    guiEvent.Use();
                }
            }
            
            // draw common new point GUI
            Handles.color = Color.yellow;
            Handles.DrawDottedLine( connectingPoint, addPointPosition, 5 );
            Handles.SphereHandleCap( 0, addPointPosition, Quaternion.identity, handleCapSize, guiEvent.type );

            Handles.color = Color.white;
        }

        void DoInsertPoint(IEditableSpline spline, Event guiEvent)
        {
            Selection.activeGameObject = spline.GetTransform().gameObject;

            Transform handleTransform = spline.GetTransform();
            Transform transform = spline.GetTransform();
            Camera camera = Camera.current;
            
            Ray mouseRay = MousePositionToRay( camera, guiEvent.mousePosition );
            Vector3 newPointPosition = spline.GetClosestPoint( mouseRay );

            planePosition = MathHelper.LinePlaneIntersection( newPointPosition, transform.up, transform.position, transform.up );
            planeOffset = newPointPosition - planePosition;

            // new point
            Handles.color = Color.yellow;
            Handles.SphereHandleCap( 0, newPointPosition, transform.rotation, handleCapSize, guiEvent.type );
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
                float t = spline.GetClosestT( newPointPosition );

                Undo.RecordObject( target, "Insert Point" );
                spline.InsertPoint( t );
                EditorUtility.SetDirty( spline.GetComponent() );
                guiEvent.Use();
            }
        }

        bool IsMouseOverPoint(Camera camera, Vector3 point, Vector3 mousePosition)
        {
            Vector3 screenPoint = Camera.current.WorldToScreenPoint( point );
            Vector3 screenPoint2 = Camera.current.WorldToScreenPoint( point + Camera.current.transform.right * handleCapSize );
            Vector3 mouseScreenPoint = TransformMousePositionToScreenPoint( Camera.current, mousePosition );
            screenPoint.z = 0;
            screenPoint2.z = 0;
            return Vector3.Distance( screenPoint, mouseScreenPoint ) < Vector3.Distance( screenPoint, screenPoint2 );
        }

        bool Moving { get { return movePointCurvePointIndex != -1; } }
        bool MovingControlPoint { get { return movePointCurvePointIndex != -1 && movePointControlPointId != MoveControlPointId.None; } }
        int movePointCurvePointIndex = -1;

        enum MoveControlPointId
        {
            None,
            Control1,
            Control2,
            Unknown
        }
        MoveControlPointId movePointControlPointId = MoveControlPointId.None;
        Vector2 startMovementAccumulator = Vector2.zero;
        const float startMovementThreshold = 5;
        void DoMovePoint(IEditableSpline spline, Event guiEvent)
        {
            Transform transform = spline.GetTransform();

            if( !Moving )
            {
                DoPointSelection( spline, guiEvent );
            }

            if( !Moving && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
            {
                startMovementAccumulator = Vector2.zero;
                for( int i = 0; i < pointSelection.Count; ++i )
                {
                    int index = pointSelection[i];
                    CurvePoint point = spline.GetPoint( index );
                    // control points
                    Vector3 worldControl1 = point.Control1 + point.position;
                    Vector3 worldControl2 = point.Control2 + point.position;
                    
                    bool overControl1 = IsMouseOverPoint( Camera.current, worldControl1, guiEvent.mousePosition );
                    bool overControl2 = IsMouseOverPoint( Camera.current, worldControl2, guiEvent.mousePosition );

                    bool control1Interactable = spline.IsLoop() || index > 0;
                    bool control2Interactable = spline.IsLoop() || index < spline.GetPointCount()-1;

                    bool control1Detected = overControl1 && control1Interactable;
                    bool control2Detected = overControl2 && control2Interactable;

                    if( point.PointType != PointType.Point && control1Detected
                        && point.PointType != PointType.Point && control2Detected
                        && Vector3.Distance( worldControl1, worldControl2 ) <= handleCapSize )
                    {
                        // if control points are ontop of eachother 
                        // we need to determin which to move later on when we know the drag direction
                        controlPlanePosition = GetPointOnPlaneY( point.position, spline.GetTransform().up, worldControl2 );
                        controlPlaneOffset = worldControl2 - controlPlanePosition;

                        movePointCurvePointIndex = index;
                        movePointControlPointId = MoveControlPointId.Unknown;
                    }
                    else if( point.PointType != PointType.Point && control1Detected )
                    {
                        controlPlanePosition = GetPointOnPlaneY( point.position, spline.GetTransform().up, worldControl1 );
                        controlPlaneOffset = worldControl1 - controlPlanePosition;

                        movePointCurvePointIndex = index;
                        movePointControlPointId = MoveControlPointId.Control1;
                    }
                    else if( point.PointType != PointType.Point && control2Detected )
                    {
                        controlPlanePosition = GetPointOnPlaneY( point.position, spline.GetTransform().up, worldControl2 );
                        controlPlaneOffset = worldControl2 - controlPlanePosition;

                        movePointCurvePointIndex = index;
                        movePointControlPointId = MoveControlPointId.Control2;
                    }
                    // actual points
                    else if( IsMouseOverPoint( Camera.current, point.position, guiEvent.mousePosition ) )
                    {
                        movePointCurvePointIndex = index;
                        movePointControlPointId = MoveControlPointId.None;
                    }

                    if( Moving )
                    {
                        planePosition = GetPointOnPlaneY( spline.GetTransform().position, spline.GetTransform().up, point.position );
                        planeOffset = point.position - planePosition;
                        break;
                    }
                }
            }
            else if( Moving && (guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp) && guiEvent.button == 0 )
            {
                Vector3 pointMovement = Vector3.zero;
                Vector2 screenDelta = TransformMouseDeltaToScreenDelta( guiEvent.delta );
                Ray mouseRay = MousePositionToRay( Camera.current, guiEvent.mousePosition );

                if( MovingControlPoint )
                { 
                     // move the selected control point
                    CurvePoint curvePoint = spline.GetPoint( movePointCurvePointIndex );
                    Vector3 point = curvePoint.position + curvePoint.Control1;

                    if( movePointControlPointId == MoveControlPointId.Control2 )
                    {
                        point = curvePoint.position + curvePoint.Control2;
                    }
                    
                    Vector3 newPoint = point;
                    Vector3 mouseWorldPosition = MathHelper.LinePlaneIntersection( mouseRay, controlPlanePosition + planeOffset, transform.up );
                    
                    bool physicsHit = false;
                    if( guiEvent.command )
                    {
                        // snap to physics
                        RaycastHit hit;
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
                            Vector3 screenPoint = Camera.current.WorldToScreenPoint( point ) + new Vector3( screenDelta.x, screenDelta.y, 0 );
                            Ray projectionRay = Camera.current.ScreenPointToRay( screenPoint );
                            Vector3 verticalPlaneNormal = Vector3.Cross( transform.up, Vector3.Cross( transform.up, projectionRay.direction ) );
                            Vector3 screenWorldPosition = MathHelper.LinePlaneIntersection( projectionRay, controlPlanePosition, verticalPlaneNormal );
                            newPoint = point + transform.up * Vector3.Dot( transform.up, screenWorldPosition - point );
                        }
                        else
                        {
                            // relative pointer tracking
                            Vector3 screenPoint = Camera.current.WorldToScreenPoint( point ) + new Vector3( screenDelta.x, screenDelta.y, 0 );
                            newPoint = MathHelper.LinePlaneIntersection( Camera.current.ScreenPointToRay( screenPoint ), controlPlanePosition + controlPlaneOffset, transform.up );
                        }
                    }

                    controlPlanePosition = GetPointOnPlaneY( controlPlanePosition, transform.up, point );
                    controlPlaneOffset = point - controlPlanePosition;
                    pointMovement = newPoint - point;
                }
                else
                {
                     // move the selected curve points
                    int index = movePointCurvePointIndex;
                    Vector3 point = spline.GetPoint( index ).position;
                    Vector3 newPoint = point;
                    Vector3 mouseWorldPosition = MathHelper.LinePlaneIntersection( mouseRay, transform.position + planeOffset, transform.up );

                    bool physicsHit = false;
                    if( guiEvent.command )
                    {
                        // snap to physics
                        RaycastHit hit;
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
                            Vector3 screenPoint = Camera.current.WorldToScreenPoint( point ) + new Vector3( screenDelta.x, screenDelta.y, 0 );
                            Ray projectionRay = Camera.current.ScreenPointToRay( screenPoint );
                            Vector3 verticalPlaneNormal = Vector3.Cross( transform.up, Vector3.Cross( transform.up, projectionRay.direction ) );
                            Vector3 screenWorldPosition = MathHelper.LinePlaneIntersection( projectionRay, planePosition, verticalPlaneNormal );
                            newPoint = point + transform.up * Vector3.Dot( transform.up, screenWorldPosition - point );
                        }
                        else
                        {
                            // relative pointer tracking
                            Vector3 screenPoint = Camera.current.WorldToScreenPoint( point ) + new Vector3( screenDelta.x, screenDelta.y, 0 );
                            newPoint = MathHelper.LinePlaneIntersection( Camera.current.ScreenPointToRay( screenPoint ), transform.position + planeOffset, transform.up );
                        }
                    }

                    planePosition = GetPointOnPlaneY( transform.position, transform.up, point );
                    planeOffset = point - planePosition;
                    pointMovement = newPoint - point;
                }

                if( startMovementAccumulator.magnitude < startMovementThreshold )
                {
                    startMovementAccumulator += guiEvent.delta;
                }
                if( startMovementAccumulator.magnitude > startMovementThreshold && pointMovement.magnitude > 0 )
                {
                    Undo.RecordObject( target, "Move Points" );

                    if( MovingControlPoint )
                    {
                        CurvePoint curvePoint = spline.GetPoint( movePointCurvePointIndex );
                        if( movePointControlPointId == MoveControlPointId.Unknown )
                        {
                            Vector3 worldControl = curvePoint.position + curvePoint.Control2;
                            float directionTestForward = -1;
                            float directionTestBackward = -1;
                            if( spline.GetPointCount() > movePointCurvePointIndex + 1 )
                            {
                                directionTestForward = Vector3.Dot( pointMovement.normalized, (spline.GetPoint( movePointCurvePointIndex + 1 ).position - curvePoint.position).normalized );
                            }

                            if( movePointCurvePointIndex - 1 >= 0 )
                            {
                                directionTestBackward = Vector3.Dot( pointMovement.normalized, (spline.GetPoint( movePointCurvePointIndex - 1 ).position - curvePoint.position).normalized );
                            }

                            if( directionTestForward >= directionTestBackward )
                            {
                                movePointControlPointId = MoveControlPointId.Control2;
                                worldControl = curvePoint.position + curvePoint.Control2;
                            }
                            else
                            {
                                movePointControlPointId = MoveControlPointId.Control1;
                                worldControl = curvePoint.position + curvePoint.Control1;
                            }
                            
                            controlPlanePosition = GetPointOnPlaneY( curvePoint.position, spline.GetTransform().up, worldControl );
                            controlPlaneOffset = worldControl - controlPlanePosition;
                        }

                        if( movePointControlPointId == MoveControlPointId.Control1 )
                        {
                            curvePoint.Control1 = curvePoint.Control1 + pointMovement;
                        }
                        else
                        {
                            curvePoint.Control2 = curvePoint.Control2 + pointMovement;
                        }
                        spline.SetPoint( movePointCurvePointIndex, curvePoint );
                    }
                    else
                    {
                        for( int i = 0; i < pointSelection.Count; ++i )
                        {
                            int index = pointSelection[i];
                            CurvePoint point = spline.GetPoint( index );
                            point.position = point.position + pointMovement;
                            spline.SetPoint( index, point );
                        }
                    }
                }

                if( guiEvent.type == EventType.MouseUp )
                {
                    movePointCurvePointIndex = -1;
                }
            }

            if( Moving )
            {
                useEvent = true;
            }

            if( MovingControlPoint )
            {
                CurvePoint curvePoint = spline.GetPoint( movePointCurvePointIndex );
                DrawControlPointMovementGuides( spline, curvePoint.position, curvePoint.Control1 + curvePoint.position, curvePoint.Control2 + curvePoint.position );
            }
        }

        // stop points disappearing into infinity!
        bool IsSafeToProjectFromPlane( Camera camera, Transform transform )
        {
            return Vector3.Dot(camera.transform.forward, transform.up) < 0.95f;
        }
        bool TwoDimentionalMode( Camera camera, Transform transform )
        {
            return Vector3.Dot(camera.transform.up, transform.up) < 0.95f;
        }

        static Vector3 GetPointOnPlaneY(Vector3 planePosition, Vector3 planeNormal, Vector3 point)
        {
            return MathHelper.LinePlaneIntersection( point, planeNormal, planePosition, planeNormal );
        }

        float DepthScale(Vector3 point, Vector3 cameraPosition)
        {
            IEditableSpline spline = target as IEditableSpline;
            return Vector3.Distance( spline.GetTransform().position, cameraPosition ) / Vector3.Distance( point, cameraPosition );
        }

        void DrawWireDisk(Vector3 point, Vector3 normal, float radius, Vector3 cameraPosition)
        {
            Handles.DrawWireDisc( point, normal, radius * DepthScale( point, cameraPosition ) );
        }
    }
}
