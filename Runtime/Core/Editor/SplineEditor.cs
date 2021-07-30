﻿using System.Collections.Generic;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines


#if UNITY_EDITOR

    public enum GridSpace
    {
        Node,
        Spline,
        Global
    }
    {
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
    }

        Vector3 planePosition;

        const float diskRadiusScalar = 2f;
        {
            return SplineHandleUtility.GetNodeHandleSize( position );
        }
        {
            return SplineHandleUtility.GetNodeHandleSize( position ) * 0.5f * diskRadiusScalar;
        }
        {
            return SplineHandleUtility.GetNodeHandleSize( position ) * controlPointRadiusScalar;
        }
        {
            return SplineHandleUtility.GetNodeHandleSize( position ) * 0.5f * controlPointRadiusScalar * diskRadiusScalar;
        }
            {
                if (nodeSelection[i] < insertIndex)
                {
                    insertIndex = nodeSelection[i];
                }
            }

            DeleteNodes( spline, nodeSelection );
                insertIndex++;
            }
            }
            {
                buttonText = mode.ToString();
            }
                doRepaint = true;

        static bool DoToggleButton( bool value, string buttonText)
            return DoToggleButton( value, buttonText, value );
        }
        static bool DoToggleButton( bool value, string buttonText, bool activated )
            return DoToggleButton( value, buttonText, activated, Color.green, Color.white * 0.7f );
        }
        static bool DoToggleButton(bool value, string buttonText, bool activated, Color activeColor, Color inactiveColor )
            GUIStyle style = new GUIStyle( EditorStyles.miniButton );
        {
            for( int i = 0; i < selected.Count; ++i )
            {
                if( spline.GetNode(selected[i]).NodeType == nodeType )
                {
                    return true;
                }
            }

            return false;
        }

            GUIStyle selected = new GUIStyle( EditorStyles.miniButton );
            selected.active.textColor = selected.hover.textColor = selected.normal.textColor = Color.green;
            GUIStyle normal = EditorStyles.miniButton;
            EditorGUI.BeginChangeCheck();
            EditorActive = DoToggleButton( EditorActive, "Spline Tool", EditorActive, Color.green, Color.red );
            foldoutSplineEditor = EditorGUILayout.Foldout( foldoutSplineEditor, "Spline Editor" );
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
            if( foldoutSplineEditor )
                    doRepaint = true;

                    GUILayout.Label( "Automatic Tangent Length" );
                    nodeAutomaticTangentLength = EditorGUILayout.Slider( nodeAutomaticTangentLengthBefore, 0, 1 );
                    bool change = !Mathf.Approximately( nodeAutomaticTangentLengthBefore, nodeAutomaticTangentLength );
                    if( change )
                    {
                        Undo.RecordObjects( spline.GetTransform().gameObject.GetComponents<Component>(), "Set Automatic Tangent Length" );
                        SplineEditorTools.SetNodesAutomaticTangentLegnth( spline, nodeSelection, nodeAutomaticTangentLength );
                    }
                }
                EditorGUILayout.EndHorizontal();
                    EditorApplication.QueuePlayerLoopUpdate();
                    EditorApplication.QueuePlayerLoopUpdate();
                    EditorApplication.QueuePlayerLoopUpdate();
                    EditorApplication.QueuePlayerLoopUpdate();
                    EditorApplication.QueuePlayerLoopUpdate();
                    {
                        GridSpace = GridSpace.Global;
                        EditorApplication.QueuePlayerLoopUpdate();
                    }
                    {
                        GridSpace = GridSpace.Spline;
                        EditorApplication.QueuePlayerLoopUpdate();
                    }
                    {
                        GridSpace = GridSpace.Node;
                        EditorApplication.QueuePlayerLoopUpdate();
                    }
                    {
                        float scalar = EditorGUILayout.FloatField( "Uniform Grid Scale", SnapToGridScale.x );
                        Vector3 grid = Vector3.one * scalar;
                        SnapToGridScale = grid;
                    }
                    {
                    }
                    {
                        EditorApplication.QueuePlayerLoopUpdate();
                    }
                    {
                        EditorUtility.SetDirty( spline.GetComponent() );
                    }
        {
        }
        public static bool SnapToGridXActive
            get
            {
                return SnapToGridX && !MoveVerticalModifier( Event.current );
            }
        }
        public static bool SnapToGridYActive
            get
            {
                return SnapToGridY && MoveVerticalModifier( Event.current );
            }
        }
        public static bool SnapToGridZActive
            get
            {
                return SnapToGridZ && !MoveVerticalModifier( Event.current );
            }
        }
            useEvent = false;

        {

            //hack click deselect node.
            // maintain as selected active object if we click when we have a node selected
            if( guiEvent.type == EventType.MouseDown && guiEvent.button == 0 )
                            edited = true;
                            edited = true;
                {
                    SplineEditorTools.AutomaticSmooth( spline );
                }



            // hacks: intercept unity object drag select
            Vector3 handlePosition = Camera.current.transform.position + Camera.current.transform.forward * 10;
        }
        {
            // draw things
            if( guiEvent.type == EventType.Repaint )
        }

        Color horizontalGridColour = new Color( .7f, .7f, 1, .2f );
        void DrawGrid( IEditableSpline spline )
            Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix(spline);

            Vector3 origin = gridMatrix.MultiplyPoint( Vector3.zero );
            Vector3 forward = gridMatrix.MultiplyVector( Vector3.forward );
            Vector3 right = gridMatrix.MultiplyVector( Vector3.right );

            DrawGrid( origin, forward, right, 1, 10 );
            DrawGrid( origin, forward, right, 1, 10 );

                Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix( spline );

            Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix( spline );
                Matrix4x4 gridMatrix = GetSnapGridLocalToWorldMatrix( spline );
                Vector3 pointOnPlane = MathsUtils.ProjectPointOnPlane( gridMatrix.MultiplyPoint( Vector3.zero ), gridMatrix.MultiplyVector( Vector3.up ), point );
                    Vector3 nextPointOnPlane = MathsUtils.ProjectPointOnPlane( gridMatrix.MultiplyPoint( Vector3.zero ), gridMatrix.MultiplyVector( Vector3.up ), nextPoint );
                useEvent = true;
                        return;
                            }
                        return;
                    {
                        if( !moveNodeState.HasMoved && nodeSelection.Contains( result.nodeIndex ) && !isSelectionEvent )
                        {
                            ClearNodeSelection( spline );
                            SelectionAddNode( spline, result.nodeIndex );
                            selectionChanged = true;
                            clearSelection = false;
                    }
                if( snapToGrid )
                {
                    worldPosition = SnapWorldPointToGrid( worldPosition, GetSnapGridLocalToWorldMatrix( spline, origin ) );
                    planeOffset = worldPosition - planePoint;
                }
            }

            float halfGridScale = SnapToHalfGrid ? 0.5f : 1;
            Vector3 gridUp = GetSnapGridUp( spline );

                // draw point placement GUI
                Handles.color = Color.yellow;

                DrawWireDisk( addNodePosition, gridUp, GetNodeDiscSize( addNodePosition ), camera.transform.position );

            Handles.color = Color.yellow;
            {
                // draw beziers
                int nodeCount = spline.GetNodeCount();

                    // addNodeMode == SplineAddNodeMode.Append
                    SplineNode node1 = spline.GetNode( nodeCount - 1 );
                        newNodeDirection = newNode.position - node2.position;
                        node2.LocalInControlPoint = newNodeDirection.normalized * 0.1f * newNodeDirection.magnitude;
                            newNode.automaticTangentLength = node1.automaticTangentLength;

            // draw new point being added
            Handles.SphereHandleCap( 0, addNodePosition, Quaternion.identity, GetNodeHandleSize( addNodePosition ), guiEvent.type );
            Vector3 gridUp = GetSnapGridUp( spline );

            // new point
            Handles.color = Color.yellow;
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
            }
            {
                Plane,
                Verticle
            }
            {
                Moving = false;
            }

        void InitialiseMoveNodeState( IEditableSpline spline, int nodeIndex, MoveControlPointId selectedType, MoveNodeState.MovingModes currentMovingMode )
        {

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
            Matrix4x4 matrix = Matrix4x4.TRS( customOrigin, transform.rotation, transform.lossyScale );
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
            }
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
        }
            {
                {
                    moveNodeState = new MoveNodeState();
                    useEvent = true;
                    return;
                }
            }
            {
            }
                        EditorUtility.SetDirty( spline.GetComponent() );
                            EditorUtility.SetDirty( spline.GetComponent() );
                    if( !moveNodeState.HasMoved )
                    {
                        DoNodeSelection( spline, guiEvent );
                    }
                    useEvent = true;

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

#endregion
        {
        }

        public static void AutomaticSmooth(IEditableSpline spline)
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

            Smooth( spline, automaticNodes );

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

        public static void Smooth(IEditableSpline spline, List<int> nodeIndicies)
            if( spline.GetNodeCount() < 2 )
            {
                return;
            }
            {
                // special case
                SmoothLoopedStartAndEndPoints( spline );
                return;
            }
                {
                    SmoothStartPoint( spline );
                }
                {
                    SmoothEndPoint( spline );
                }
                {
                }
            }

        static void SmoothPoint( IEditableSpline spline, int index )
        {
            if (spline.GetNodeCount() < 2)
            {
                return;
            }

            SplineNode node = spline.GetNode( index );
                Vector3 offset = spline.GetNode( spline.LoopIndex(index - 1) ).position - node.position;
            {

            if (node.NodeType != NodeType.Automatic)
            {
                node.SetNodeType(NodeType.Aligned);
            }
            {
                spline.SetNode( index, node );
                EditorUtility.SetDirty( spline.GetComponent() );
            }

        static void SmoothStartPoint( IEditableSpline spline )
        {
            if (spline.GetNodeCount() < 2)
            {
                return;
            }

            SplineNode startNode = spline.GetNode(0);
            SplineNode nextNode = spline.GetNode(1);

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
            SplineNode endNode = spline.GetNode(spline.GetNodeCount()-1);

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

        public static void DeleteDuplicateNodes(IEditableSpline spline, List<int> nodeIndicies)
            {
                int ai = i;
                int bi = nodeCount - i - 1;

                SplineNode a = spline.GetNode( ai );
                SplineNode b = spline.GetNode( bi );

                a.SetLocalControls( a.LocalOutControlPoint, a.LocalInControlPoint );
                b.SetLocalControls( b.LocalOutControlPoint, b.LocalInControlPoint );

                spline.SetNode( ai, b );
                spline.SetNode( bi, a );
            }
#endif