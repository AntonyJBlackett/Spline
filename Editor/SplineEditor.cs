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

        public override void OnInspectorGUI()
        {
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

            GUILayout.Label( "Edit Mode: " + editMode.ToString() );
            GUILayout.Label( "Add Point Mode: " + addPointMode.ToString() );
            GUILayout.Label( "Moving Point: " + moving.ToString() );

            DrawDefaultInspector();
        }

        float rightClickStart;
        float rightClickTime = 0.2f;
        bool useEvent = false;
        void OnSceneGUI()
        {
            useEvent = false;
            Event guiEvent = Event.current;
            Spline spline = target as Spline;

            if( guiEvent.type == EventType.MouseEnterWindow )
            {
                SceneView.currentDrawingSceneView.Focus();
            }

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

            if( editMode != SplineEditMode.None
                && guiEvent.type == EventType.KeyDown
                && guiEvent.keyCode == KeyCode.Escape )
            {
                guiEvent.Use();
                ResetEditMode();
            }
            
            if( pointSelection.Count > 0
                && guiEvent.type == EventType.KeyDown
                && (guiEvent.keyCode == KeyCode.Delete || guiEvent.keyCode == KeyCode.Backspace ) )
            {
                Undo.RecordObject( target, "Delete Points" );
                for( int i = 0; i < pointSelection.Count; ++i )
                {
                    spline.RemovePoint( pointSelection[i] );
                }
                ClearPointSelection();
                EditorUtility.SetDirty( target );
            }

            if( pointSelection.Count > 0 )
            {
                editMode = SplineEditMode.MovePoint;
            }

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

            DrawSpline( spline );
            if( guiEvent.isMouse )
            {
                SceneView.currentDrawingSceneView.Repaint();
            }
            
            // intercept unity object drag select
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
            ClearPointSelection();
            editMode = SplineEditMode.None;
            planeOffset = Vector3.zero;
            controlPlanePosition = Vector3.zero;
            addPointState = AddPointState.PointPosition;
            EditorUtility.SetDirty( target );
        }

        void DrawSplinePoints(Spline spline)
        {
            for( int i = 0; i < spline.PointCount; ++i )
            {
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
                    Handles.DrawDottedLine( planePosition, hitDown.point, 2 );
                }
            }
        }

        void DrawSplineSelectionControlPoint(Spline spline)
        {
            for( int i = 0; i < spline.PointCount; ++i )
            {
                CurvePoint point = spline.GetPoint( i );
                if( point.PointType == PointType.Point )
                {
                    continue;
                }

                Vector3 control1 = point.position + point.Control1;
                Vector3 control2 = point.position + point.Control2;
                Handles.color = Color.grey;

                Handles.SphereHandleCap( 0, control1, spline.transform.rotation, handleCapSize, EventType.Repaint );
                Handles.SphereHandleCap( 0, control2, spline.transform.rotation, handleCapSize, EventType.Repaint );

                Handles.DrawLine( point.position, control1 );
                Handles.DrawLine( point.position, control2 );
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
            DrawSplineSelectionControlPoint( spline );
            DrawSplinePoints( spline );
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

        bool DoClickSelection(Spline spline, Event guiEvent)
        {
            bool clearSelection = !guiEvent.shift && !guiEvent.command;
            bool hasSelection = pointSelection.Count > 0;

            for( int i = 0; i < spline.PointCount; ++i )
            {
                if( pointSelection.Contains( i ) )
                {
                    Handles.color = Color.green;
                }
                else
                {
                    Handles.color = Color.white;
                }

                Vector3 point = spline.GetPointPosition( i );
                if( IsMouseOverPoint( Camera.current, point, guiEvent.mousePosition ) )
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
                    if( IsMouseOverPoint( Camera.current, spline.GetPointPosition( i ), guiEvent.mousePosition ) )
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
                Vector3 newControlPointPosition = GetPointPlacement( camera, guiEvent.mousePosition, addPointPosition, transform.up, ref controlPlanePosition, ref controlPlaneOffset, guiEvent.shift && canShift, guiEvent.command );
            
                Vector3 relativeControlPoint = newControlPointPosition - addPointPosition;
                Vector3 relativeControlPlanePoint = controlPlanePosition - addPointPosition;

                Vector3 control1 = addPointPosition + relativeControlPoint;
                Vector3 controlPlane1 = addPointPosition + relativeControlPlanePoint;
                Vector3 control2 = addPointPosition - relativeControlPoint;
                Vector3 controlPlane2 = addPointPosition - relativeControlPlanePoint;
                
                Handles.color = Color.green;
                // draw control placement GUI
                Handles.SphereHandleCap( 0, control1, Quaternion.identity, handleCapSize*0.5f, guiEvent.type );
                Handles.SphereHandleCap( 0, control2, Quaternion.identity, handleCapSize*0.5f, guiEvent.type );

                DrawWireDisk( control1, transform.up, diskRadius*0.5f, camera.transform.position );
                DrawWireDisk( control2, transform.up, diskRadius*0.5f, camera.transform.position );

                Handles.DrawDottedLine( addPointPosition, control1, 2 );
                Handles.DrawDottedLine( addPointPosition, control2, 2 );

                Handles.color = Color.yellow;
                DrawWireDisk( controlPlane1, transform.up, diskRadius*0.5f, camera.transform.position );
                DrawWireDisk( controlPlane2, transform.up, diskRadius*0.5f, camera.transform.position );

                Handles.DrawDottedLine( addPointPosition, controlPlane1, 2 );
                Handles.DrawDottedLine( addPointPosition, controlPlane2, 2 );

                Handles.DrawDottedLine( control1, controlPlane1, 2 );
                Handles.DrawDottedLine( control2, controlPlane2, 2 );
            
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
                        spline.AddPoint( new CurvePoint( addPointPosition, newControlPointPosition - addPointPosition, PointType.Mirrored ) );
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

                    Debug.Log( "newSegmentLength " + newSegmentLength );
                    Debug.Log( "segmentLength " + segmentLength );
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

        bool moving { get { return movementControlPointIndex != -1; } }
        int movementControlPointIndex = -1;
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
                    Vector3 point = spline.GetPointPosition( index );
                    if( IsMouseOverPoint( Camera.current, point, guiEvent.mousePosition ) )
                    {
                        planePosition = GetPointOnPlaneY( spline.transform.position, spline.transform.up, point );
                        planeOffset = point - planePosition;
                        movementControlPointIndex = index;
                        break;
                    }
                }
            }
            else if( moving && (guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp) && guiEvent.button == 0 )
            {
                Vector3 pointMovement = Vector3.zero;

                // move the point
                {
                    int index = movementControlPointIndex;
                    Vector3 point = spline.GetPointPosition( index );
                    Vector3 newPoint = point;
                    Vector2 screenDelta = TransformMouseDeltaToScreenDelta( guiEvent.delta );
                    Ray mouseRay = MousePositionToRay( Camera.current, guiEvent.mousePosition );
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

                    for( int i = 0; i < pointSelection.Count; ++i )
                    {
                        int index = pointSelection[i];
                        Vector3 point = spline.GetPointPosition( index );
                        Vector3 newPoint = point + pointMovement;
                        spline.SetPoint( index, newPoint );
                    }
                }


                if( guiEvent.type == EventType.MouseUp )
                {
                    movementControlPointIndex = -1;
                }
            }

            if( moving )
            {
                useEvent = true;
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
