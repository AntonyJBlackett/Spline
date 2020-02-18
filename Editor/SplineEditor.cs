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
        Insert
    }

    [CustomEditor( typeof( Spline ) )]
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

        void SetSelectionPointType( Spline spline, PointType type)
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

        Bounds GetSelectionBounds( Spline spline )
        {
            if( pointSelection.Count == 0 )
            {
                return GetBounds( spline );
            }

            Bounds bounds = new Bounds( spline.GetPointPosition(pointSelection[0]), Vector3.one * 0.1f );
            for( int i = 1; i < pointSelection.Count; ++i )
            {
                bounds.Encapsulate( spline.GetPointPosition(pointSelection[i]) );
            }

            if( bounds.size.magnitude < 1 )
            {
                bounds.size = bounds.size.normalized * 1;
            }
            
            return bounds;
        }

        Bounds GetBounds( Spline spline )
        {
            Bounds bounds = new Bounds( spline.GetPointPosition(0), Vector3.zero );
            for( int i = 1; i < spline.PointCount; ++i )
            {
                bounds.Encapsulate( spline.GetPointPosition(i) );
            }
            return bounds;
        }

        public void FrameCamera( Spline spline )
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

        void FlattenSelection(Spline spline, PointType type)
        {
            Undo.RecordObject( target, "Flatten Selection" );
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];
                CurvePoint point = spline.GetPoint( index );

                point.position = GetPointOnPlaneY( spline.transform.position, spline.transform.up, point.position );
                point.Control1 = GetPointOnPlaneY( spline.transform.position, spline.transform.up, point.Control1 + point.position ) - point.position;
                point.Control2 = GetPointOnPlaneY( spline.transform.position, spline.transform.up, point.Control2 + point.position ) - point.position;

                spline.SetPoint( index, point );
            }
            EditorUtility.SetDirty( target );
        }

        void SmoothSelection( Spline spline, PointType type )
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
                    beforeIndex = spline.PointCount-1;
                }
                before = spline.GetPoint( beforeIndex );

                int afterIndex = (index + 1) % spline.PointCount;
                after = spline.GetPoint( afterIndex );

                Vector3 direction = after.position - before.position;
                float dist1 = Mathf.Min( (point.position - before.position).magnitude, direction.magnitude );
                float dist2 = Mathf.Min( (point.position - after.position).magnitude, direction.magnitude );
                
                point.SetPointType( PointType.Aligned );
                point.Control1 = -direction.normalized * dist1 * 0.4f;
                point.Control2 = direction.normalized * dist2 * 0.4f;

                if( !spline.Loop )
                {
                    if( index == 0 || index == spline.PointCount - 1 )
                    {
                        point.SetPointType( PointType.Point );
                    }
                }

                spline.SetPoint( index, point );
            }
            EditorUtility.SetDirty( target );
        }

        public void DeleteSelectedPoints( Spline spline )
        {
            int removePoints = pointSelection.Count;
            for( int i = 0; i < removePoints; ++i )
            {
                RemovePoint( spline, pointSelection[0] );
            }
            ClearPointSelection();
            EditorUtility.SetDirty( target );
        }

        void PasteCurve( Spline spline )
        {
            DeleteSelectedPoints( spline );
            int oldPointCount = spline.PointCount;
            for( int i = 0; i < clipboard.Count; i++ )
            {
                spline.AddPoint( clipboard[i] );
            }

            pointSelection.Clear();
            for( int i = oldPointCount; i < spline.PointCount; i++ )
            {
                pointSelection.Add( i );
            }
        }

        public void CopyCurve( Spline spline )
        {
            clipboard.Clear();
            for( int i = 0; i < pointSelection.Count; i++ )
            {
                int index = pointSelection[i];
                clipboard.Add( spline.GetPoint(index) );
            }
        }

        public override void OnInspectorGUI()
        {
            Spline spline = target as Spline;

            GUILayout.Label( "Edit Modes" );
            if( editMode == SplineEditMode.AddPoint && addPointMode == SplineAddPointMode.Append )
            {
                if( GUILayout.Button( "Cancel Append Point" ) )
                {
                    ResetEditMode();
                }
            }
            else if( GUILayout.Button( "Append Point" ) )
            {
                ResetEditMode();
                editMode = SplineEditMode.AddPoint;
                addPointMode = SplineAddPointMode.Append;
            }

            if( editMode == SplineEditMode.AddPoint && addPointMode == SplineAddPointMode.Insert )
            {
                if( GUILayout.Button( "Cancel Insert Point" ) )
                {
                    ResetEditMode();
                }
            }
            else if( GUILayout.Button( "Insert Point" ) )
            {
                ResetEditMode();
                editMode = SplineEditMode.AddPoint;
                addPointMode = SplineAddPointMode.Insert;
            }
            EditorGUI.BeginChangeCheck();
            bool loop = GUILayout.Toggle( spline.Loop, "Loop" );
            if( EditorGUI.EndChangeCheck() )
            {
                Undo.RecordObject( target, "Loop Toggle" );
                spline.Loop = loop;
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
            if( GUILayout.Button( "Test Fast Bezier" ) )
            {
                UnityFastBezier.Program.Main();
                Debug.Log( "-------------------" );
                FastBezier.Program.Main();
            }

            GUILayout.Space( 10 );
            GUILayout.Label( "Debug" );
            GUILayout.Label( "Edit Mode: " + editMode.ToString() );
            GUILayout.Label( "Add Point Mode: " + addPointMode.ToString() );
            GUILayout.Label( "Moving Point: " + moving.ToString() );

            DrawDefaultInspector();
        }

        float rightClickStart;
        float rightClickTime = 0.2f;
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

        void RightClickCancel( Event guiEvent )
        {
            if( guiEvent.button == 1 && guiEvent.type == EventType.MouseDown )
            {
                rightClickStart = Time.unscaledTime;
            }
            if( guiEvent.button == 1 && guiEvent.type == EventType.MouseDrag )
            {
                // cancel right click
                rightClickStart = 0;
            }
            if( guiEvent.button == 1 && guiEvent.type == EventType.MouseUp )
            {
                if( Time.unscaledTime - rightClickStart < rightClickTime )
                {
                    ResetEditMode();
                }
            }
        }

        void KeyboardInputs( Spline spline, Event guiEvent )
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
                    for( int i = 0; i < spline.PointCount; ++i )
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

        void RemovePoint( Spline spline, int index )
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
                Tools.current = Tool.Custom;
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
            Spline spline = target as Spline;

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
                    if( addPointMode == SplineAddPointMode.Append )
                    {
                        DoAppendPoint( spline, guiEvent );
                    }
                    else
                    {
                        if( spline.PointCount < 2 )
                        {
                            DoAppendPoint( spline, guiEvent );
                        }
                        else
                        {
                            DoInsertPoint( spline, guiEvent );
                        }
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

        void DrawSplinePoints(Spline spline)
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

                Vector3 point = spline.GetPointPosition( i );
                Handles.SphereHandleCap( 0, point, spline.transform.rotation, handleCapSize, EventType.Repaint );
            }
            Handles.color = Color.white;
        }

        void DrawSplinePlaneProjectionLines(Spline spline)
        {
            for( int i = 0; i < spline.PointCount; ++i )
            {
                Vector3 point = spline.GetPointPosition( i );
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.transform.position, spline.transform.up, point );
                Handles.DrawDottedLine( pointOnPlane, point, 2 );
            }
        }

        void DrawSplineLines(Spline spline)
        {
            for( int i = 0; i < spline.PointCount; ++i )
            {
                Vector3 point = spline.GetPointPosition( i );
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.transform.position, spline.transform.up, point );
                if( i < spline.PointCount - 1 )
                {
                    Vector3 nextPoint = spline.GetPointPosition( i + 1 );
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

        void DrawBezierSplineLines(Spline spline)
        {
            for( int i = 1; i < spline.PointCount; ++i )
            {
                DrawBezierSegment( spline.GetPoint( i-1 ), spline.GetPoint( i ) );
            }
        
            if( spline.Loop && spline.PointCount > 1)
            {
                DrawBezierSegment( spline.GetPoint( spline.PointCount-1 ), spline.GetPoint( 0 ) );
            }
        }

        void DrawBezierPlaneProjectedSplineLines(Spline spline)
        {
            for( int i = 1; i < spline.PointCount; ++i )
            {
                DrawBezierSegmentOnPlane( spline.transform.position, spline.transform.up, spline.GetPoint( i-1 ), spline.GetPoint( i ) );
            }
        
            if( spline.Loop && spline.PointCount > 1)
            {
                DrawBezierSegmentOnPlane( spline.transform.position, spline.transform.up, spline.GetPoint( spline.PointCount-1 ), spline.GetPoint( 0 ) );
            }
        }

        void DrawSplinePlaneProjectedSplineLines(Spline spline)
        {
            for( int i = 0; i < spline.PointCount; ++i )
            {
                Vector3 point = spline.GetPointPosition( i );
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.transform.position, spline.transform.up, point );
                if( i < spline.PointCount - 1 )
                {
                    Vector3 nextPoint = spline.GetPointPosition( i + 1 );
                    Vector3 nextPointOnPlane = GetPointOnPlaneY( spline.transform.position, spline.transform.up, nextPoint );
                    Handles.DrawDottedLine( pointOnPlane, nextPointOnPlane, 2 );
                }
            }
        }

        void DrawSplineSelectionDisks( Spline spline)
        {
            ValidatePointSelection( spline );
            Transform transform = spline.transform;
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];

                Vector3 point = spline.GetPointPosition( index );
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

        void DrawControlPoints( Spline spline, int index )
        {
            CurvePoint point = spline.GetPoint( index );
            if( point.PointType == PointType.Point )
            {
                return;
            }

            Vector3 control1 = point.position + point.Control1;
            Vector3 control2 = point.position + point.Control2;
            Handles.color = Color.grey;

            if( index > 0 || spline.Loop )
            {
                Handles.SphereHandleCap( 0, control1, Quaternion.identity, handleCapSize, EventType.Repaint );
                Handles.DrawLine( point.position, control1 );
            }

            if( index < spline.PointCount-1 || spline.Loop )
            {
                Handles.SphereHandleCap( 0, control2, Quaternion.identity, handleCapSize, EventType.Repaint );
                Handles.DrawLine( point.position, control2 );
            }
        }

        void DrawSplineSelectionControlPoint(Spline spline)
        {
            if( pointSelection.Count == 0 )
            {
                for( int i = 0; i < spline.PointCount; ++i )
                {
                    DrawControlPoints( spline, i );
                }
            }
            else
            {
                for( int i = 0; i < pointSelection.Count; ++i )
                {
                    DrawControlPoints( spline, pointSelection[i] );
                }
            }
        }

        void DrawSpline(Spline spline)
        {
            //DrawSplineLines( spline );
            DrawBezierSplineLines( spline );
            DrawSplinePlaneProjectionLines( spline );
            //DrawSplinePlaneProjectedSplineLines( spline );
            DrawBezierPlaneProjectedSplineLines( spline );
            DrawSplineSelectionDisks( spline );

            DrawSplinePoints( spline );
            DrawSplineSelectionControlPoint( spline );
        }

        List<int> pointSelection = new List<int>();
        void SelectionAddPoint(Spline spline, int index)
        {
            ValidatePointSelection( spline );
            if( !spline.IsIndexInRange( index ) )
            {
                return;
            }

            if( pointSelection.Contains( index ) )
            {
                return;
            }
            pointSelection.Add( index );
        }

        void ValidatePointSelection(Spline spline)
        {
            for( int i = pointSelection.Count - 1; i >= 0; i-- )
            {
                if( !spline.IsIndexInRange( pointSelection[i] ) )
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

        List<int> GetPointIndiciesSelectedFirst( Spline spline )
        {
            List<int> pointIndicies = new List<int>();
            for( int i = 0; i < spline.PointCount; ++i )
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

        bool DoClickSelection(Spline spline, Event guiEvent)
        {
            bool clearSelection = !guiEvent.shift && !guiEvent.command;
            bool hasSelection = pointSelection.Count > 0;

            List<int> sortedPoints = GetPointIndiciesSelectedFirst( spline );
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
                
                CurvePoint curvePoint = spline.GetPoint( i );

                bool overControl1 = IsMouseOverPoint( Camera.current, curvePoint.position + curvePoint.Control1, guiEvent.mousePosition );
                bool overControl2 = IsMouseOverPoint( Camera.current, curvePoint.position + curvePoint.Control2, guiEvent.mousePosition );

                bool control1Interactable = spline.Loop || i > 0;
                bool control2Interactable = spline.Loop || i < spline.PointCount-1;

                bool control1Detected = overControl1 && control1Interactable;
                bool control2Detected = overControl2 && control2Interactable;

                if( curvePoint.PointType != PointType.Point &&
                    ( control1Detected || control2Detected ) )
                {
                    useEvent = true;
                    if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
                    {
                        clearSelection = false;
                        if( pointSelection.Count == 0 )
                        {
                            SelectionAddPoint( spline, i );
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
                        if( pointSelection.Contains( i ) && guiEvent.command )
                        {
                            pointSelection.Remove( i );
                        }
                        else if( !pointSelection.Contains( i ) )
                        {
                            if( !guiEvent.shift && !guiEvent.command )
                            {
                                ClearPointSelection();
                            }
                            SelectionAddPoint( spline, i );
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
        bool DoDragSelection(Spline spline, Event guiEvent)
        {
            bool clearSelection = false;
            if( spline.PointCount > 0 && !dragSelectActive && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && guiEvent.shift )
            {
                mouseDragSelectionStart = guiEvent.mousePosition;
                dragSelectActive = true;

                for( int i = 0; i < spline.PointCount; ++i )
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
                for( int i = 0; i < spline.PointCount; ++i )
                {
                    Vector3 point = spline.GetPointPosition( i );
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

        void DoPointSelection(Spline spline, Event guiEvent)
        {
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

        void DrawControlPointMovementGuides( Spline spline, Vector3 point, Vector3 control1, Vector3 control2)
        {
            Camera camera = Camera.current;
            Transform transform = spline.transform;

            Vector3 controlPlane1 = GetPointOnPlaneY( point, transform.up, control1 );
            Vector3 controlPlane2 = GetPointOnPlaneY( point, transform.up, control2 );
            
            Handles.color = Color.green;
            // draw control placement GUI
            Handles.SphereHandleCap( 0, control1, Quaternion.identity, handleCapSize*0.5f, EventType.Repaint );
            Handles.SphereHandleCap( 0, control2, Quaternion.identity, handleCapSize*0.5f, EventType.Repaint );

            DrawWireDisk( control1, transform.up, diskRadius*0.5f, camera.transform.position );
            DrawWireDisk( control2, transform.up, diskRadius*0.5f, camera.transform.position );

            Handles.DrawDottedLine( point, control1, 2 );
            Handles.DrawDottedLine( point, control2, 2 );

            Handles.color = Color.yellow;
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
        void DoAppendPoint(Spline spline, Event guiEvent)
        {
            if( !guiEvent.shift )
            {
                canShift = true;
            }

            Selection.activeGameObject = spline.gameObject;
            Transform handleTransform = spline.transform;
            Transform transform = spline.transform;
            Camera camera = Camera.current;
            
            Vector3 connectingPoint = spline.GetPointPosition( spline.PointCount - 1 );
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
                    if( controlPosition.magnitude < 0.01f )
                    {
                        spline.AddPoint( new CurvePoint( addPointPosition ) );
                    }
                    else
                    {
                        Vector3 control = newControlPointPosition - addPointPosition;
                        spline.AddPoint( new CurvePoint( addPointPosition, -control, control, PointType.Mirrored ) );
                    }

                    EditorUtility.SetDirty( spline );
                    guiEvent.Use();
                }
            }
            
            // draw common new point GUI
            Handles.color = Color.yellow;
            Handles.DrawDottedLine( connectingPoint, addPointPosition, 5 );
            Handles.SphereHandleCap( 0, addPointPosition, Quaternion.identity, handleCapSize, guiEvent.type );

            Handles.color = Color.white;
        }

        void DoInsertPoint(Spline spline, Event guiEvent)
        {
            Selection.activeGameObject = spline.gameObject;

            Transform handleTransform = spline.transform;
            Transform transform = spline.transform;
            Camera camera = Camera.current;

            Ray mouseRay = MousePositionToRay( camera, guiEvent.mousePosition );
            Vector3 mouseWorldPosition = MathHelper.LinePlaneIntersection( mouseRay, transform.position + planeOffset, transform.up );

            List<int> connectingPoints = new List<int>();
            List<Vector3> points = spline.GetPoints();
            List<int> curveSegmentIndicies = spline.GetSegmentsForPoints();
            Vector3 newPointPosition = HandleUtility.ClosestPointToPolyLine( points.ToArray() );
            for( int i = 1; i < points.Count; ++i )
            {
                Vector3 direction = (newPointPosition - points[ i-1 ]).normalized;
                Vector3 direction2 = (newPointPosition - points[ i ]).normalized;
                if( Vector3.Dot(-direction, direction2) > 0.99f )
                {
                    connectingPoints.Add( i - 1 );
                    connectingPoints.Add( i );
                }
            }

            planePosition = MathHelper.LinePlaneIntersection( newPointPosition, transform.up, transform.position, transform.up );
            planeOffset = mouseWorldPosition - planePosition;

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
                int segmentIndex = spline.PointCount-1;
                float t = 0;
                if( connectingPoints.Count != 0 )
                {
                    segmentIndex = curveSegmentIndicies[ connectingPoints[0] ];
                    
                    float newSegmentLength = 0;
                    float segmentLength = 0;
                    for( int i = 0; i < points.Count-1; ++i )
                    {
                        if( curveSegmentIndicies[i] < segmentIndex )
                        {
                            continue;
                        }
                        if( curveSegmentIndicies[i] > segmentIndex )
                        {
                            break;
                        }

                        Vector3 point1 = points[i];
                        Vector3 point2 = points[i+1];

                        float resolutionLength = Vector3.Distance( point1, point2 );
                        segmentLength += resolutionLength;

                        if( i < connectingPoints[0] )
                        {
                            newSegmentLength += resolutionLength;
                        }
                        if( i == connectingPoints[0] )
                        {
                            newSegmentLength += Vector3.Distance( point1, newPointPosition );
                        }
                    }

                    t = newSegmentLength / segmentLength;
                }

                Undo.RecordObject( target, "Insert Point" );
                spline.InsertPoint( segmentIndex, t );
                EditorUtility.SetDirty( spline );
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

        bool moving { get { return movePointCurvePointIndex != -1; } }
        bool movingControlPoint { get { return movePointCurvePointIndex != -1 && (movePointControlPointId == 1 ||  movePointControlPointId == 2); } }
        int movePointCurvePointIndex = -1;
        int movePointControlPointId = 0;
        void DoMovePoint(Spline spline, Event guiEvent)
        {
            Transform transform = spline.transform;

            if( !moving )
            {
                DoPointSelection( spline, guiEvent );
            }

            if( !moving && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
            {
                for( int i = 0; i < pointSelection.Count; ++i )
                {
                    int index = pointSelection[i];
                    CurvePoint point = spline.GetPoint( index );
                    // control points
                    Vector3 worldControl1 = point.Control1 + point.position;
                    Vector3 worldControl2 = point.Control2 + point.position;

                    
                    bool overControl1 = IsMouseOverPoint( Camera.current, worldControl1, guiEvent.mousePosition );
                    bool overControl2 = IsMouseOverPoint( Camera.current, worldControl2, guiEvent.mousePosition );

                    bool control1Interactable = spline.Loop || index > 0;
                    bool control2Interactable = spline.Loop || index < spline.PointCount-1;

                    bool control1Detected = overControl1 && control1Interactable;
                    bool control2Detected = overControl2 && control2Interactable;

                    if( point.PointType != PointType.Point && control1Detected )
                    {
                        controlPlanePosition = GetPointOnPlaneY( point.position, spline.transform.up, worldControl1 );
                        controlPlaneOffset = worldControl1 - controlPlanePosition;

                        movePointCurvePointIndex = index;
                        movePointControlPointId = 1;
                    }
                    else if( point.PointType != PointType.Point && control2Detected )
                    {
                        controlPlanePosition = GetPointOnPlaneY( point.position, spline.transform.up, worldControl2 );
                        controlPlaneOffset = worldControl2 - controlPlanePosition;

                        movePointCurvePointIndex = index;
                        movePointControlPointId = 2;
                    }
                    // actual points
                    else if( IsMouseOverPoint( Camera.current, point.position, guiEvent.mousePosition ) )
                    {
                        movePointCurvePointIndex = index;
                        movePointControlPointId = 0;
                    }

                    if( moving )
                    {
                        planePosition = GetPointOnPlaneY( spline.transform.position, spline.transform.up, point.position );
                        planeOffset = point.position - planePosition;
                        break;
                    }
                }
            }
            else if( moving && (guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp) && guiEvent.button == 0 )
            {
                Vector3 pointMovement = Vector3.zero;
                Vector2 screenDelta = TransformMouseDeltaToScreenDelta( guiEvent.delta );
                Ray mouseRay = MousePositionToRay( Camera.current, guiEvent.mousePosition );

                if( movingControlPoint )
                { 
                     // move the selected control point
                    CurvePoint curvePoint = spline.GetPoint( movePointCurvePointIndex );
                    Vector3 point = curvePoint.position + curvePoint.Control1;

                    if( movePointControlPointId == 2 )
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
                    Vector3 point = spline.GetPointPosition( index );
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

                if( pointMovement.magnitude > 0 )
                {
                    Undo.RecordObject( target, "Move Points" );

                    if( movingControlPoint )
                    {
                        CurvePoint curvePoint = spline.GetPoint( movePointCurvePointIndex );
                        if( movePointControlPointId == 1 )
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
                            Vector3 point = spline.GetPointPosition( index );
                            Vector3 newPoint = point + pointMovement;
                            spline.SetPointPosition( index, newPoint );
                        }
                    }
                }

                if( guiEvent.type == EventType.MouseUp )
                {
                    movePointCurvePointIndex = -1;
                }
            }

            if( moving )
            {
                useEvent = true;
            }

            if( movingControlPoint )
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
            Spline spline = target as Spline;
            return Vector3.Distance( spline.transform.position, cameraPosition ) / Vector3.Distance( point, cameraPosition );
        }

        void DrawWireDisk(Vector3 point, Vector3 normal, float radius, Vector3 cameraPosition)
        {
            Handles.DrawWireDisc( point, normal, radius * DepthScale( point, cameraPosition ) );
        }
    }
}
