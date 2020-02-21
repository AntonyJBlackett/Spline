using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace FantasticSplines
{
	[System.Serializable]
	internal struct CurveSegment
	{
		public PointType startPointType; 
		public PointType endPointType;

		public Bezier3 bezier
		{
			get => _bezier;
			set { _bezier = value; _tdMap.Initialise(_bezier); }
		}
		
		[System.NonSerialized] private TDMap _tdMap;
		[SerializeField] public Bezier3 _bezier;

		public float Length => tdMap.Length;
		public float GetT(float d) => tdMap.GetT(d);
		public float GetDistance(float t) => tdMap.GetDistance(t);
		public Vector3 GetPosition(float t) => bezier.GetPos(t);
		public Vector3 GetDirection(float t) => bezier.GetTangent(t);
		public Vector3 GetPositionAtDistance(float d) => bezier.GetPos(GetT(d));
		public Vector3 GetDirectionAtDistance(float d) => bezier.GetTangent(GetT(d));
		
		private TDMap tdMap
		{
			get
			{
				if (!_tdMap.IsValid()) { _tdMap.Initialise(_bezier); }
				return _tdMap;
			}
		}

		public CurveSegment(Bezier3 bezier, PointType from, PointType to)
		{
			this._bezier = bezier;
			this.startPointType = from;
			this.endPointType = to;
			_tdMap = new TDMap(bezier);
		}

		public CurveSegment ReInitialise(Bezier3 bezier, PointType from, PointType to)
		{
			this.startPointType = from;
			this.endPointType = to;
			this.bezier = bezier;
			return this;
		}

		public CurveSegment WithRightCurvePoint(CurvePoint point)
		{
			Bezier3 bez = bezier;
			bez.C = point.Control1Position;
			bez.D = point.position;
			bezier = bez;
			endPointType = point.PointType;
			return this;
		}
		public CurveSegment WithLeftCurvePoint(CurvePoint point)
		{
			Bezier3 bez = bezier;
			bez.B = point.Control2Position;
			bez.A = point.position;
			bezier = bez;
			startPointType = point.PointType;
			return this;
		}
	}
	
	[System.Serializable]
	public class MCCurve : ISpline
	{
		[SerializeField]
		private List<CurveSegment> segments = new List<CurveSegment>();

		[SerializeField]
		public bool loop = false;

		public bool Loop
		{
			get => loop;
			set => loop = value;
		}

		public float Length
		{
			get { return segments.Sum(s => s.Length); }
		}
		public float InverseLength => 1f / Length;

		public int SegmentCount => segments.Count;

		public int PointCount
		{
			get
			{
				// Special case - if we have a single 0-length segment. Then we only have 1 point.
				if (SegmentCount == 1 && Mathf.Approximately(Length, 0f))
				{
					return 1;
				}
				if (loop || SegmentCount == 0)
				{
					return SegmentCount;
				}
				return SegmentCount + 1;
			}
		}

		public SegmentPosition GetSegmentAtT(float t)
		{
			return GetSegmentAtDistance(t * Length);
		}
		public SegmentPosition GetSegmentAtDistance(float distance)
		{
			int sIndex = 0;
			while (distance > segments[sIndex].Length)
			{
				distance -= segments[sIndex].Length;
			}

			float finalT = segments[sIndex].GetT(distance);
			return new SegmentPosition(sIndex, distance);
		}
		
		float LoopNormalisedT(float normalisedT)
		{
			if( loop )
			{
				return Mathf.Repeat( normalisedT, 1 );
			}

			return Mathf.Clamp01( normalisedT );
		}
		float LoopDistance(float distance)
		{
			if( loop )
			{
				return Mathf.Repeat( distance, Length );
			}

			return Mathf.Clamp( distance, 0f, Length );
		}
		
		public float GetSpeed(float t)
		{
			// not really mathematically correct hey 
			return GetDirection(t).magnitude;
		}

		public Vector3 GetDirection(float t)
		{
			var pos = GetSegmentAtT(t);
			return segments[pos.index].GetDirection(pos.segmentT);
		}

		public Vector3 GetPoint(float t)
		{
			var pos = GetSegmentAtT(t);
			return segments[pos.index].GetPosition(pos.segmentT);
		}

		public float GetLength(float fromT = 0, float toT = 1)
		{
			return (toT - fromT) * Length;
		}

		public float GetT(float length)
		{
			return length * InverseLength;
		}

		public float GetClosestT(Vector3 point)
		{
			return GetT(GetClosestSegmentPointer(point));
		}

		public float GetClosestT(Ray ray)
		{
			return GetT(GetClosestSegmentPointer(ray));
		}

		public Vector3 GetClosestPoint(Vector3 point)
		{
			var segPos= GetClosestSegmentPointer(point);
			return segments[segPos.index].GetPosition(segPos.segmentT);
		}

		public Vector3 GetClosestPoint(Ray ray)
		{
			var segPos= GetClosestSegmentPointer(ray);
			return segments[segPos.index].GetPosition(segPos.segmentT);
		}

		public float Step(float t, float worldDistance)
		{
			return LoopNormalisedT(t + (worldDistance * InverseLength));
		}

		public CurvePoint GetPoint(int index)
		{
			// Special case for first node in a non-looping spline
			if (!loop && index <= 0)
			{
				Vector3 P = segments[index].bezier.start;
				Vector3 rightControl = segments[index].bezier.startControl;
				PointType type = segments[index].startPointType;
				
				Vector3 leftControl = CurvePoint.ConstrainControlPoint(P, rightControl, type);
				
				return new CurvePoint(P, leftControl, rightControl, type);
			}
			else if (!loop && index >= SegmentCount)
			{
				int lastIndex = SegmentCount - 1;
				Vector3 P = segments[lastIndex].bezier.end;
				Vector3 leftControl = segments[lastIndex].bezier.endControl;
				PointType type = segments[lastIndex].endPointType;
				Vector3 rightControl = CurvePoint.ConstrainControlPoint(P, leftControl, type);
				
				return new CurvePoint(P, leftControl, rightControl, type);
			}
			else
			{
				int prevIndex = MathHelper.WrapIndex(index-1, SegmentCount);
				Vector3 P = segments[index].bezier.start;
				Vector3 leftControl = segments[prevIndex].bezier.endControl;
				Vector3 rightControl = segments[index].bezier.startControl;
				return new CurvePoint(P, leftControl, rightControl, segments[index].startPointType);	
			}
		}

		public void SetPoint(int index, CurvePoint point)
		{
			if( index < 0 || index > PointCount )
			{
				return;
			}

			if (loop || index > 0)
			{
				int prevIndex = MathHelper.WrapIndex(index - 1, PointCount);
				segments[prevIndex] = segments[prevIndex].WithRightCurvePoint(point);
			}

			if (loop || index < segments.Count)
			{
				index = MathHelper.WrapIndex(index, PointCount);
				segments[index] = segments[index].WithLeftCurvePoint(point);
			}
		}

		// inserts a point on the curve without changing its shape
		public void InsertPoint(float t)
		{
			SegmentPosition seg = GetSegmentAtT(t);
			Bezier3 A, B;
			PointType start = segments[seg.index].startPointType;
			PointType end = segments[seg.index].endPointType;
			segments[seg.index].bezier.SplitAt(seg.segmentT, out A, out B);
			segments.Insert(seg.index, new CurveSegment(A, start, PointType.Aligned));
			segments[seg.index + 1].ReInitialise(B, PointType.Aligned, end);
		}
		
		// appends a point to the end of the curve at position
		public void AddPoint(CurvePoint point)
		{
			if (PointCount == 0)
			{
				Bezier3 zeroBezier = new Bezier3(point.position, point.position, point.position, point.position);
				CurveSegment newSegment = new CurveSegment(zeroBezier, point.PointType, point.PointType);
				segments.Add(newSegment);
			}
			else
			{
				CurveSegment lastSeg = segments[segments.Count - 1];
				Bezier3 newBez = new Bezier3(lastSeg.bezier.end, lastSeg.bezier.endControl, point.Control1, point.position);
				CurveSegment newSegment = new CurveSegment(newBez, lastSeg.endPointType, point.PointType);
				segments.Add(newSegment);
			}
		}
		
		// adds the given CurvePoint at index in the list of CurvePoint in curve
		public void AddPointAt(int index, CurvePoint point)
		{
			if (segments.Count == 0)
			{
				AddPoint(point);
				return;
			}

			Bezier3 currSeg = segments[index].bezier;
			Vector3 reverseControlPos = currSeg.A - currSeg.startControl;
			// make a new segment from 'point' to currSeg
			Bezier3 newBezier = new Bezier3(point.position, point.Control1Position, reverseControlPos, currSeg.A);
			CurveSegment newSeg = new CurveSegment(newBezier, point.PointType, segments[index].startPointType);
			segments.Insert(index, newSeg);
			
			// now fixup the previous segment
			if (loop || index > 0)
			{
				int prevIndex = MathHelper.WrapIndex(index - 1, PointCount);
				segments[prevIndex] = segments[prevIndex].WithRightCurvePoint(point);
			}
		}
		
		// removes a Curve Point at index
		public void RemovePoint(int index)
		{
			if (index < 0 || index > segments.Count)
			{
				return;
			}
			if (segments.Count == 1 || (index == 0 && !loop))
			{
				// remove first segment, no fixup needed
				segments.RemoveAt(index);
			}
			else if (!loop && index == segments.Count)
			{
				// remove last segment, no fixup needed
				segments.RemoveAt(segments.Count-1);
			}
			else
			{
				CurvePoint b = GetPoint(index + 1);
				segments.RemoveAt(index);
				int prevIndex = MathHelper.WrapIndex(index - 1, PointCount);
				segments[prevIndex] = segments[prevIndex].WithRightCurvePoint(b);
			}
		}


		public List<CurvePoint> GetPoints()
		{
			List<CurvePoint> points = new List<CurvePoint>(PointCount);
			for (int i = 0; i < PointCount; ++i)
			{
				points.Add(GetPoint(i));
			}

			return points;
		}

		public List<Vector3> GetPoints(float worldSpacing, bool includeEndPoint = true, bool includeSplinePoints = false)
		{
			List<Vector3> points = new List<Vector3>();
			for (float t = 0; t < Length; t += worldSpacing)
			{
				points.Add(GetPoint(t));
			}

			if (includeEndPoint)
			{
				points.Add(GetPoint(1f));
			}
			return points;
		}

		public float GetDistance(float splineT)
		{
			return splineT * Length;
		}

		public float GetT(SegmentPosition position)
		{
			return GetDistance(position) * InverseLength;
		}

		public float GetDistance(SegmentPosition position)
		{
			float distance = 0f;
			for (int i = 0; i < position.index; ++i)
			{
				distance += segments[i].Length;
			}

			return distance + segments[position.index].GetDistance(position.segmentT);
		}
		
        public SegmentPosition GetClosestSegmentPointer(Vector3 point, float paramThreshold = 0.000001f)
        {
	        float minDistSq = float.MaxValue;
            SegmentPosition bestSeg = new SegmentPosition(0, 0f);
            for (int i = 0; i < SegmentCount; i++)
            {
                Bezier3 curve = segments[i].bezier;
                float curveClosestParam = curve.GetClosestT(point, paramThreshold);

                Vector3 curvePos = curve.GetPos(curveClosestParam);
                float distSq = (curvePos - point).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestSeg = new SegmentPosition(i, curveClosestParam);
                }
            }

            return bestSeg;
        }

        public SegmentPosition GetClosestSegmentPointer(Ray ray, float paramThreshold = 0.000001f)
        {
            float minDistSqWorld = float.MaxValue;
            float minDistSqProjected = float.MaxValue;
            SegmentPosition bestSeg = new SegmentPosition(0, 0f);
            bool foundPointInFront = false;
            for (int i = 0; i < segments.Count; i++)
            {
                Bezier3 curve = segments[i].bezier;
                Bezier3 projected = Bezier3.ProjectToPlane( curve, ray.origin, ray.direction );

                float curveClosestParam = projected.GetClosestT(ray.origin, paramThreshold);

                Vector3 projectedPos = projected.GetPos(curveClosestParam);
                Vector3 pos = curve.GetPos(curveClosestParam);

                bool infront = Vector3.Dot( ray.direction, pos - ray.origin ) >= 0;
                if( infront || !foundPointInFront )
                {
                    if( !foundPointInFront )
                    {
                        minDistSqWorld = float.MaxValue;
                        minDistSqProjected = float.MaxValue;
                        foundPointInFront = true;
                    }

                    float distSqProjected = (projectedPos - ray.origin).sqrMagnitude;
                    float distSqWorld = (pos - ray.origin).sqrMagnitude;
                    if( 
                        ( distSqProjected < minDistSqProjected )
                        || ( Mathf.Abs( distSqProjected - minDistSqProjected ) < float.Epsilon && distSqWorld < minDistSqWorld )  
                    )
                    {
                        minDistSqProjected = distSqProjected;
                        minDistSqWorld = distSqWorld;
                        bestSeg = new SegmentPosition(i, curveClosestParam);
                    }
                }
            }

            return bestSeg;
        }

        public Bezier3 GetBezierForSegment(int index)
        {
	        return segments[index].bezier;
        }
	}
}