using UnityEngine;
using UnityEngine.Serialization;


// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand


namespace FantasticSplines
{
    public enum NodeType
    {
        Point, // control points ignored
        Free, // free moving control points
        Aligned, // control points are aligned but can be different magnitudes
        Mirrored, // control points are aligned and equal in magnitude
        Automatic // automatically calculates positions of the control points to make a smooth curve.
    }

    [System.Serializable]
    public struct SplineNode
    {
        // position of the node in spline space
        public Vector3 position;

        // The length the tangent will be when set to the automatic node type
        // This is achieved by constraining the controls based on other nodes in the spline
        // Currently this only works at edit time.
        public float automaticTangentLength;

        // 'in control' point position in spline node space (spline node local space).
        [SerializeField]
        [FormerlySerializedAs( "control1" )]
        Vector3 localInControlPoint;

        // 'out control' point position in spline node space (spline node local space).
        [SerializeField]
        [FormerlySerializedAs( "control2" )]
        Vector3 localOutControlPoint;

        // the control that was most recently edited by the user
        // 0 == none
        // 1 == localInControlPoint
        // 1 == localOutControlPoint
        int lastChangedControl;

        // defines the behaviour of the controls this node has, point, mirrored, etc
        [SerializeField]
        [FormerlySerializedAsAttribute( "pointType" )]
        NodeType nodeType;

        // A dirty float comparison function
        bool FloatEqualEnough( float x, float y )
        {
            return Mathf.Abs(x-y) <= float.Epsilon*10;
        }

        // Returns true if the compare node has all the same values, or close enough (float comparison)
        public bool EqualTo( SplineNode compare )
        {
            return FloatEqualEnough( position.x, compare.position.x )
                && FloatEqualEnough( position.y, compare.position.y)
                && FloatEqualEnough( position.z, compare.position.z)
                && FloatEqualEnough( automaticTangentLength, compare.automaticTangentLength)
                && FloatEqualEnough( localInControlPoint.x, compare.localInControlPoint.x )
                && FloatEqualEnough( localInControlPoint.y, compare.localInControlPoint.y )
                && FloatEqualEnough( localInControlPoint.z, compare.localInControlPoint.z )
                && FloatEqualEnough( localOutControlPoint.x, compare.localOutControlPoint.x )
                && FloatEqualEnough( localOutControlPoint.y, compare.localOutControlPoint.y )
                && FloatEqualEnough( localOutControlPoint.z, compare.localOutControlPoint.z );
        }

        // Returns true if the compare not is not EqualTo()
        public bool IsDifferentFrom( SplineNode compare )
        {
            return !EqualTo( compare );
        }

        // The spline space position of the in control point
        public Vector3 InControlPoint
        {
            get
            {
                return localInControlPoint + position;
            }
            set
            {
                LocalInControlPoint = value - position;
            }
        }
        // The spline space position of the out control point
        public Vector3 OutControlPoint
        {
            get
            {
                return localOutControlPoint + position;
            }
            set
            {
                LocalOutControlPoint = value - position;
            }
        }

        // return the NodeType that the node can be while still retaining it's shape
        public static NodeType GetNodeTypeFromControls(SplineNode node)
        {
            if( node.nodeType == NodeType.Point )
            {
                return NodeType.Point;
            }

            if( node.nodeType == NodeType.Automatic )
            {
                return NodeType.Automatic;
            }

            if( Mathf.Approximately( node.localInControlPoint.sqrMagnitude, 0 ) && Mathf.Approximately( node.localOutControlPoint.sqrMagnitude, 0 ) )
            {
                return NodeType.Point;
            }

            if( node.localInControlPoint == -node.localOutControlPoint )
            {
                return NodeType.Mirrored;
            }

            if( node.localInControlPoint.normalized == node.localOutControlPoint.normalized )
            {
                return NodeType.Aligned;
            }

            return NodeType.Free;
        }

        // Returns a constrained control point position for 'constrain' based off the 'master' control point and the node type
        public static Vector3 ConstrainControlPoint(Vector3 master, Vector3 constrain, NodeType type)
        {
            switch( type )
            {
                case NodeType.Aligned:
                    if( master.magnitude > 0 )
                    {
                        constrain = -master.normalized * constrain.magnitude;
                    }
                    break;
                case NodeType.Mirrored:
                    constrain = -master;
                    break;
            }

            return constrain;
        }

        // Gets and sets the in control point position relative to node position
        // in spline space and contrains the other control point based on node type
        public Vector3 LocalInControlPoint
        {
            get
            {
                if( nodeType == NodeType.Point )
                {
                    return Vector3.zero;
                }

                return localInControlPoint;
            }
            set
            {
                localInControlPoint = value;
                localOutControlPoint = ConstrainControlPoint( localInControlPoint, localOutControlPoint, nodeType );
                lastChangedControl = 1;
            }
        }

        // Gets and sets the out control point position relative to node position
        // in spline space and contrains the other control point based on node type
        public Vector3 LocalOutControlPoint
        {
            get
            {
                if( nodeType == NodeType.Point )
                {
                    return Vector3.zero;
                }

                return localOutControlPoint;
            }
            set
            {
                localOutControlPoint = value;
                localInControlPoint = ConstrainControlPoint( localOutControlPoint, localInControlPoint, nodeType );
                lastChangedControl = 2;
            }
        }

        // Gets the node type
        public NodeType NodeType
        {
            get
            {
                return nodeType;
            }
        }

        // Sets the node type and contrains the controls
        public void SetNodeType(NodeType type)
        {
            nodeType = type;

            if( lastChangedControl == 2 )
            {
                localInControlPoint = ConstrainControlPoint( localOutControlPoint, localInControlPoint, type );
            }
            else
            {
                localOutControlPoint = ConstrainControlPoint( localInControlPoint, localOutControlPoint, type );
            }
        }

        // Constructors
        // Constructs a spline node from a spline space position and automatic tangent length
        // Note that this node is a point type until the spline editor 'automatic' tool is used on it
        public SplineNode(Vector3 position, float automaticTangentLength )
        {
            nodeType = NodeType.Point;
            this.position = position;
            localInControlPoint = localOutControlPoint = Vector3.zero;
            lastChangedControl = 0;
            this.automaticTangentLength = automaticTangentLength;
        }

        // Constructs a spline node from a spline space position and two node relative control points
        public SplineNode(Vector3 position, Vector3 control1, Vector3 control2, float automaticTangentLength )
        {
            this.position = position;
            this.localInControlPoint = control1;
            this.localOutControlPoint = control2;
            lastChangedControl = 0;
            this.automaticTangentLength = automaticTangentLength;
            nodeType = NodeType.Free;
            nodeType = GetNodeTypeFromControls( this );
        }

        // Constructs a spline node from a spline space position and two node relative control points
        public SplineNode(Vector3 position, Vector3 control1, Vector3 control2, NodeType type, float automaticTangentLength )
        {
            this.position = position;
            nodeType = type;
            this.localInControlPoint = control1;
            this.localOutControlPoint = control2;
            this.automaticTangentLength = automaticTangentLength;
            lastChangedControl = 0;
        }

        // Constructs a spline node from another spline node
        public SplineNode(SplineNode other, float automaticTangentLength = 0.3f )
        {
            position = other.position;
            nodeType = other.nodeType;
            localInControlPoint = other.localInControlPoint;
            localOutControlPoint = other.localOutControlPoint;
            this.automaticTangentLength = automaticTangentLength;
            lastChangedControl = other.lastChangedControl;
        }

        // Transforms a Spline node with a transform
        public SplineNode Transform(Transform transform)
        {
            SplineNode result = this;
            result.position = transform.TransformPoint( result.position );
            result.localInControlPoint = transform.TransformVector( result.localInControlPoint );
            result.localOutControlPoint = transform.TransformVector( result.localOutControlPoint );
            return result;
        }

        // Inverse transforms a Spline node with a transform
        public SplineNode InverseTransform(Transform transform)
        {
            SplineNode result = this;
            result.position = transform.InverseTransformPoint( result.position );
            result.localInControlPoint = transform.InverseTransformVector( result.localInControlPoint );
            result.localOutControlPoint = transform.InverseTransformVector( result.localOutControlPoint );
            return result;
        }

        // Prints the nodes position and nodeType
        public override string ToString()
        {
            return string.Format( "[{0}:{1}]", position, nodeType );
        }
    }
}