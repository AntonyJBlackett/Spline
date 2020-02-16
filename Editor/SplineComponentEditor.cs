using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

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

    [CustomEditor( typeof( SplineComponent ) )]
    public class SplineComponentEditor : Editor
    {
        SplineEditMode editMode = SplineEditMode.None;
        SplineAddPointMode addPointMode = SplineAddPointMode.Append;
        float diskRadius = 0.2f;
        float handleCapSize = 0.1f;
        Vector3 planePosition;
        Vector3 planeOffset;

        void OnEnable()
        {
            ResetEditMode();
        }

        public override void OnInspectorGUI()
        {
            if( GUILayout.Button( "Append Point" ) )
            {
                ResetEditMode();
                editMode = SplineEditMode.AddPoint;
                addPointMode = SplineAddPointMode.Append;
            }

            if( GUILayout.Button( "Insert Point" ) )
            {
                ResetEditMode();
                editMode = SplineEditMode.AddPoint;
                addPointMode = SplineAddPointMode.Insert;
            }

            GUILayout.Label( "Edit Mode: " + editMode.ToString() );
            GUILayout.Label( "Add Point Mode: " + addPointMode.ToString() );
            GUILayout.Label( "Moving Point: " + moving.ToString() );
        }

        bool useEvent = false;
        void OnSceneGUI()
        {
            useEvent = false;
            Event guiEvent = Event.current;
            SplineComponent spline = target as SplineComponent;

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
                        DoInsertPoint( spline, guiEvent );
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

        Vector2 TransformMouseDeltaToScreenDelta(Vector2 mouseDelta)
        {
            mouseDelta.y = -mouseDelta.y;
            return mouseDelta * EditorGUIUtility.pixelsPerPoint;
        }

        Vector3 TransformMousePositionToScreenPoint(Camera camera, Vector3 mousePosition)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            mousePosition.y = camera.pixelHeight - mousePosition.y * pixelsPerPoint;
            mousePosition.x *= pixelsPerPoint;
            return mousePosition;
        }

        Ray MousePositionToRay(Camera camera, Vector3 mousePosition)
        {
            return camera.ScreenPointToRay( TransformMousePositionToScreenPoint( camera, mousePosition ) );
        }

        void ResetEditMode()
        {
            editMode = SplineEditMode.None;
            planeOffset = Vector3.zero;
        }

        void DrawSplinePoints(SplineComponent spline)
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

                Vector3 point = spline.GetPoint( i );
                Handles.SphereHandleCap( 0, point, spline.transform.rotation, handleCapSize, EventType.Repaint );
            }
            Handles.color = Color.white;
        }

        void DrawSplinePlaneProjectionLines(SplineComponent spline)
        {
            for( int i = 0; i < spline.PointCount; ++i )
            {
                Vector3 point = spline.GetPoint( i );
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.transform, point );
                Handles.DrawDottedLine( pointOnPlane, point, 2 );
            }
        }

        void DrawSplineSplineLines(SplineComponent spline)
        {
            for( int i = 0; i < spline.PointCount; ++i )
            {
                Vector3 point = spline.GetPoint( i );
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.transform, point );
                if( i < spline.PointCount - 1 )
                {
                    Vector3 nextPoint = spline.GetPoint( i + 1 );
                    Handles.DrawLine( nextPoint, point );
                }
            }
        }

        void DrawSplinePlaneProjectedSplineLines(SplineComponent spline)
        {
            for( int i = 0; i < spline.PointCount; ++i )
            {
                Vector3 point = spline.GetPoint( i );
                Vector3 pointOnPlane = GetPointOnPlaneY( spline.transform, point );
                if( i < spline.PointCount - 1 )
                {
                    Vector3 nextPoint = spline.GetPoint( i + 1 );
                    Vector3 nextPointOnPlane = GetPointOnPlaneY( spline.transform, nextPoint );
                    Handles.DrawDottedLine( pointOnPlane, nextPointOnPlane, 2 );
                }
            }
        }

        void DrawSplineSelectionDisks( SplineComponent spline)
        {
            ValidatePointSelection( spline );
            Transform transform = spline.transform;
            for( int i = 0; i < pointSelection.Count; ++i )
            {
                int index = pointSelection[i];

                Vector3 point = spline.GetPoint( index );
                Vector3 planePoint = GetPointOnPlaneY( transform, point );
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

        void DrawSpline(SplineComponent spline)
        {
            DrawSplinePoints( spline );
            DrawSplineSplineLines( spline );
            DrawSplinePlaneProjectionLines( spline );
            DrawSplinePlaneProjectedSplineLines( spline );
            DrawSplineSelectionDisks( spline );
        }

        List<int> pointSelection = new List<int>();
        void SelectionAddPoint(SplineComponent spline, int index)
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

        void ValidatePointSelection(SplineComponent spline)
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

        bool DoClickSelection(SplineComponent spline, Event guiEvent)
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

                Vector3 point = spline.GetPoint( i );
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
        bool DoDragSelection(SplineComponent spline, Event guiEvent)
        {
            bool clearSelection = false;
            if( spline.PointCount > 0 && !dragSelectActive && guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && guiEvent.shift )
            {
                mouseDragSelectionStart = guiEvent.mousePosition;
                dragSelectActive = true;

                for( int i = 0; i < spline.PointCount; ++i )
                {
                    if( IsMouseOverPoint( Camera.current, spline.GetPoint( i ), guiEvent.mousePosition ) )
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
                    Vector3 point = spline.GetPoint( i );
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

        void DoPointSelection(SplineComponent spline, Event guiEvent)
        {
            bool clearClick = DoClickSelection( spline, guiEvent );
            bool clearDrag = DoDragSelection( spline, guiEvent );
            bool clearSelection = clearClick && clearDrag;
            
            if( clearSelection )
            {
                ClearPointSelection();
            }
        }

        void DoAppendPoint(SplineComponent spline, Event guiEvent)
        {
            Selection.activeGameObject = spline.gameObject;
            Transform handleTransform = spline.transform;
            Transform transform = spline.transform;
            Camera camera = Camera.current;

            Ray mouseRay = MousePositionToRay( camera, guiEvent.mousePosition );
            Vector3 mouseWorldPosition = MathHelper.LinePlaneIntersection( mouseRay, transform.position + planeOffset, transform.up );

            Vector3 connectingPoint = spline.GetPoint( spline.PointCount - 1 );
            Vector3 connectingPlanePoint = GetPointOnPlaneY( transform, connectingPoint );

            if( guiEvent.command )
            {
                RaycastHit hit;
                if( Physics.Raycast( mouseRay, out hit ) )
                {
                    mouseWorldPosition = hit.point;
                    planePosition = MathHelper.LinePlaneIntersection( mouseWorldPosition, transform.up, transform.position, transform.up );
                    planeOffset = mouseWorldPosition - planePosition;
                }
            }
            else if( guiEvent.shift )
            {
                Vector3 verticalPlaneNormal = Vector3.Cross( transform.up, Vector3.Cross( transform.up, mouseRay.direction ) );
                mouseWorldPosition = MathHelper.LinePlaneIntersection( mouseRay, planePosition, verticalPlaneNormal );
                planeOffset = planeOffset + transform.up * Vector3.Dot( transform.up, mouseWorldPosition - (planePosition + planeOffset) );
            }
            else
            {
                planePosition = mouseWorldPosition - planeOffset;
            }

            Vector3 newPointPosition = planePosition + planeOffset;

            // new point
            Handles.color = Color.yellow;
            Handles.SphereHandleCap( 0, newPointPosition, transform.rotation, handleCapSize, guiEvent.type );
            DrawWireDisk( newPointPosition, transform.up, diskRadius, camera.transform.position );
            Handles.DrawDottedLine( connectingPoint, newPointPosition, 5 );
            DrawWireDisk( planePosition, transform.up, diskRadius, camera.transform.position );
            Handles.DrawDottedLine( connectingPlanePoint, planePosition, 2 );
            Handles.DrawDottedLine( planePosition, newPointPosition, 2 );

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
                Undo.RecordObject( target, "Add Point" );
                spline.AddPoint( newPointPosition, Space.World );
                EditorUtility.SetDirty( spline );
                guiEvent.Use();
            }
        }

        void DoInsertPoint(SplineComponent spline, Event guiEvent)
        {
            Selection.activeGameObject = spline.gameObject;

            Transform handleTransform = spline.transform;
            Transform transform = spline.transform;
            Camera camera = Camera.current;

            Ray mouseRay = MousePositionToRay( camera, guiEvent.mousePosition );
            Vector3 mouseWorldPosition = MathHelper.LinePlaneIntersection( mouseRay, transform.position + planeOffset, transform.up );


            List<int> connectingPoints = new List<int>();
            Vector3 newPointPosition = HandleUtility.ClosestPointToPolyLine( spline.GetPoints().ToArray() );
            for( int i = 1; i < spline.PointCount; ++i )
            {
                Vector3 direction = (newPointPosition - spline.GetPoint( i-1 )).normalized;
                Vector3 direction2 = (newPointPosition - spline.GetPoint( i )).normalized;
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

            int insertIndex = spline.PointCount;
            if( connectingPoints.Count != 0 )
            {
                insertIndex = connectingPoints[connectingPoints.Count - 1];
            }

            if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
            {
                Undo.RecordObject( target, "Insert Point" );
                spline.InsertPoint( insertIndex, newPointPosition, Space.World );
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
        void DoMovePoint(SplineComponent spline, Event guiEvent)
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
                    Vector3 point = spline.GetPoint( index );
                    if( IsMouseOverPoint( Camera.current, point, guiEvent.mousePosition ) )
                    {
                        planePosition = GetPointOnPlaneY( spline.transform, point );
                        planeOffset = point - planePosition;
                        movementControlPointIndex = index;
                        break;
                    }
                }
            }
            else if( moving && (guiEvent.type == EventType.MouseDrag || guiEvent.type == EventType.MouseUp) && guiEvent.button == 0 )
            {
                Vector3 pointMovement = Vector3.zero;

                // control point
                {
                    int index = movementControlPointIndex;
                    Vector3 point = spline.GetPoint( index );
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

                    planePosition = GetPointOnPlaneY( transform, point );
                    planeOffset = point - planePosition;
                    pointMovement = newPoint - point;
                }

                if( pointMovement.magnitude > 0 )
                {
                    Undo.RecordObject( target, "Move Points" );

                    for( int i = 0; i < pointSelection.Count; ++i )
                    {
                        int index = pointSelection[i];
                        Vector3 point = spline.GetPoint( index );
                        Vector3 newPoint = point + pointMovement;
                        spline.SetPoint( index, newPoint, Space.World );
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

        Vector3 GetPointOnPlaneY(Transform transform, Vector3 point)
        {
            return MathHelper.LinePlaneIntersection( point, transform.up, transform.position, transform.up );
        }

        float DepthScale(Vector3 point, Vector3 cameraPosition)
        {
            SplineComponent spline = target as SplineComponent;
            return Vector3.Distance( spline.transform.position, cameraPosition ) / Vector3.Distance( point, cameraPosition );
        }

        void DrawWireDisk(Vector3 point, Vector3 normal, float radius, Vector3 cameraPosition)
        {
            Handles.DrawWireDisc( point, normal, radius * DepthScale( point, cameraPosition ) );
        }
    }
}
