using UnityEngine;

namespace FantasticSplines
{
    public enum NodeType
    {
        Point, // control points ignored
        Free, // free moving control points
        Aligned, // control points are aligned but can be different magnitudes
        Mirrored // control points are aligned and equal in magnitude
    }

    [System.Serializable]
    public struct SplineNode
    {
        public Vector3 position;

        // relative to position
        [SerializeField]
        Vector3 control1;
        [SerializeField]
        Vector3 control2;

        int lastChangedControl;

        [SerializeField]
        NodeType nodeType;

        public Vector3 Control1Position => position + control1;
        public Vector3 Control2Position => position + control2;

        // return the NodeType that the node can be while still retaining it's shape
        public static NodeType GetNodeTypeFromControls(SplineNode node)
        {
            if( node.nodeType == NodeType.Point )
            {
                return NodeType.Point;
            }

            if( Mathf.Approximately( node.control1.sqrMagnitude, 0 ) && Mathf.Approximately( node.control2.sqrMagnitude, 0 ) )
            {
                return NodeType.Point;
            }

            if( node.control1 == -node.control2 )
            {
                return NodeType.Mirrored;
            }

            if( node.control1.normalized == node.control2.normalized )
            {
                return NodeType.Aligned;
            }

            return NodeType.Free;
        }

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

        public Vector3 Control1
        {
            get
            {
                if( nodeType == NodeType.Point )
                {
                    return Vector3.zero;
                }

                return control1;
            }
            set
            {
                control1 = value;
                control2 = ConstrainControlPoint( control1, control2, nodeType );
                lastChangedControl = 1;
            }
        }

        public Vector3 Control2
        {
            get
            {
                if( nodeType == NodeType.Point )
                {
                    return Vector3.zero;
                }

                return control2;
            }
            set
            {
                control2 = value;
                control1 = ConstrainControlPoint( control2, control1, nodeType );
                lastChangedControl = 2;
            }
        }

        public NodeType NodeType
        {
            get
            {
                return nodeType;
            }
        }

        public void SetNodeType(NodeType type)
        {
            nodeType = type;

            if( lastChangedControl == 2 )
            {
                control1 = ConstrainControlPoint( control2, control1, type );
            }
            else
            {
                control2 = ConstrainControlPoint( control1, control2, type );
            }
        }

        public SplineNode(Vector3 position)
        {
            nodeType = NodeType.Point;
            this.position = position;
            control1 = control2 = Vector3.zero;
            lastChangedControl = 0;
        }

        public SplineNode(Vector3 position, Vector3 control1, Vector3 control2)
        {
            this.position = position;
            this.control1 = control1;
            this.control2 = control2;
            lastChangedControl = 0;
            nodeType = NodeType.Free;
            nodeType = GetNodeTypeFromControls( this );
        }

        public SplineNode(Vector3 position, Vector3 control1, Vector3 control2, NodeType type)
        {
            this.position = position;
            this.control1 = control1;
            this.control2 = control2;
            nodeType = type;
            lastChangedControl = 0;
        }

        public SplineNode(SplineNode other)
        {
            position = other.position;
            control1 = other.control1;
            control2 = other.control2;
            nodeType = other.nodeType;
            lastChangedControl = other.lastChangedControl;
        }

        public SplineNode Transform(Transform transform)
        {
            SplineNode result = this;
            result.position = transform.TransformPoint( result.position );
            result.control1 = transform.TransformVector( result.control1 );
            result.control2 = transform.TransformVector( result.control2 );
            return result;
        }

        public SplineNode InverseTransform(Transform transform)
        {
            SplineNode result = this;
            result.position = transform.InverseTransformPoint( result.position );
            result.control1 = transform.InverseTransformVector( result.control1 );
            result.control2 = transform.InverseTransformVector( result.control2 );
            return result;
        }

        public override string ToString()
        {
            return string.Format( "[{0}:{1}]", position, nodeType );
        }
    }
}