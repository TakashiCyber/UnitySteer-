// ----------------------------------------------------------------------------
//
// Written by Ricardo J. Mendez http://www.arges-systems.com/ based on 
// OpenSteer's PolylinePathway.
//
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publfish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
//
// ----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnitySteer
{
    /// <summary>
    /// Represents a Pathway created from a list of Vector3s
    /// </summary>
    public class Vector3Pathway : IPathway
    {
        private IList<float> _lengths;
        private float _totalPathLength;

        public float Radius { get; set; }

        public Vector3Pathway()
        {
            _lengths = new List<float>(10);
            Normals = new List<Vector3>(10);
        }


        /// <summary>
        /// Constructs a Vector3Pathway given an array of points and a path radius
        /// </summary>
        /// <param name="path">
        /// List of Vector3 to be used for the path in world space <see cref="Vector3" />
        /// </param>
        /// <param name="radius">
        /// Radius to use for the connections <see cref="System.Single" />
        /// </param>
        /// <remarks>
        /// The current implementation assumes that all pathways will
        /// have the same radius.
        /// </remarks>
        public Vector3Pathway(IList<Vector3> path, float radius)
        {
            Initialize(path, radius);
        }

        /// <summary>
        /// List of calculated normals between points.
        /// </summary>
        protected IList<Vector3> Normals { get; private set; }

        public IList<Vector3> Path { get; private set; }


        public Vector3 FirstPoint
        {
            get { return Path.FirstOrDefault(); }
        }

        public Vector3 LastPoint
        {
            get { return Path.LastOrDefault(); }
        }

        public float TotalPathLength
        {
            get { return _totalPathLength; }
        }

        public int SegmentCount
        {
            get { return Path.Count; }
        }


        /// <summary>
        /// Constructs the Pathway from a list of Vector3
        /// </summary>
        /// <param name="path">
        /// A list of Vector3 defining the path points in world space<see cref="Vector3" />
        /// </param>
        /// <param name="radius">
        /// Radius to use for the connections<see cref="System.Single" />
        /// </param>
        public virtual void Initialize(IList<Vector3> path, float radius)
        {
            Path = new List<Vector3>();
            Radius = radius;

            // set data members, allocate arrays
            var pointCount = path.Count;
            _totalPathLength = 0;

            _lengths = new List<float>(pointCount);
            Normals = new List<Vector3>(pointCount);

            // loop over all points
            for (var i = 0; i < pointCount; i++)
            {
                AddPoint(path[i]);
            }
        }

        private void AddPoint(Vector3 point)
        {
            if (Path.Count > 0)
            {
                // compute the segment length
                var normal = point - Path.Last();
                var length = normal.magnitude;
                _lengths.Add(length);
                Normals.Add(normal/length);
                // keep running total of segment lengths
                _totalPathLength += length;
            }
            else
            {
                Normals.Add(Vector3.zero);
                _lengths.Add(0);
            }
            Path.Add(point);
        }

        /// <summary>
        /// Given an arbitrary point ("A"), returns the nearest point ("P") on
        /// this path.  Also returns, via output arguments, the path tangent at
        /// P and a measure of how far A is outside the Pathway's "tube".  Note
        /// that a negative distance indicates A is inside the Pathway.
        /// </summary>
        /// <param name="point">Reference point.</param>
        /// <param name="pathRelative">Structure indicating the relative path position.</param>
        /// <returns>The closest point to the received reference point.</returns>
        public virtual Vector3 MapPointToPath(Vector3 point, ref PathRelativePosition pathRelative)
        {
            var minDistance = float.MaxValue;
            var onPath = Vector3.zero;

            pathRelative.segmentIndex = -1;
            // loop over all segments, find the one nearest to the given point
            for (var i = 1; i < Path.Count; i++)
            {
                var segmentLength = _lengths[i];
                var segmentNormal = Normals[i];
                var chosenPoint = Vector3.zero;
                var d = OpenSteerUtility.PointToSegmentDistance(point, Path[i - 1], Path[i],
                    segmentNormal, segmentLength,
                    ref chosenPoint);
                if (d < minDistance)
                {
                    minDistance = d;
                    onPath = chosenPoint;
                    pathRelative.tangent = segmentNormal;
                    pathRelative.segmentIndex = i;
                }
            }

            // measure how far original point is outside the Pathway's "tube"
            pathRelative.outside = (onPath - point).magnitude - Radius;

            // return point on path
            return onPath;
        }


        /// <summary>
        /// Maps the reference point to a distance along the path.
        /// </summary>
        /// <param name="point">Reference point.</param>
        /// <returns>The distance along the path for the point.</returns>
        public virtual float MapPointToPathDistance(Vector3 point)
        {
            if (Path.Count < 2)
                return 0;

            var minDistance = float.MaxValue;
            float segmentLengthTotal = 0;
            float pathDistance = 0;

            for (var i = 1; i < Path.Count; i++)
            {
                var segmentProjection = 0f;
                var segmentLength = _lengths[i];
                var segmentNormal = Normals[i];
                var d = OpenSteerUtility.PointToSegmentDistance(point, Path[i - 1], Path[i],
                    segmentNormal, segmentLength,
                    ref segmentProjection);
                if (d < minDistance)
                {
                    minDistance = d;
                    pathDistance = segmentLengthTotal + segmentProjection;
                }
                segmentLengthTotal += segmentLength;
            }

            // return distance along path of onPath point
            return pathDistance;
        }


        /// <summary>
        /// Given a distance along the path, convert it to a specific point on the path.
        /// </summary>
        /// <param name="pathDistance">Path distance to calculate corresponding point for.</param>
        /// <returns>The corresponding path point to the path distance.</returns>
        public virtual Vector3 MapPathDistanceToPoint(float pathDistance)
        {
            // clip or wrap given path distance according to cyclic flag
            var remaining = pathDistance;
            if (pathDistance < 0)
                return Path.First();
            if (pathDistance >= _totalPathLength)
                return Path.Last();

            // step through segments, subtracting off segment lengths until
            // locating the segment that contains the original pathDistance.
            // Interpolate along that segment to find 3d point value to return.
            var result = Vector3.zero;
            for (var i = 1; i < Path.Count; i++)
            {
                var segmentLength = _lengths[i];
                if (segmentLength < remaining)
                {
                    remaining -= segmentLength;
                }
                else
                {
                    var ratio = remaining/segmentLength;
                    result = Vector3.Lerp(Path[i - 1], Path[i], ratio);
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// Determines whether the received point is inside the path.
        /// </summary>
        /// <param name="point">Point to evaluate.</param>
        /// <returns><c>true</c> if the point is inside the path; otherwise, <c>false</c>.</returns>
        public bool IsInsidePath(Vector3 point)
        {
            var tStruct = new PathRelativePosition();

            MapPointToPath(point, ref tStruct);
            return tStruct.outside < 0;
        }

        /// <summary>
        /// Calculates how far outside the path is the reference point.
        /// </summary>
        /// <param name="point">Reference point.</param>
        /// <returns>How far outside the path is the reference point.</returns>
        public float HowFarOutsidePath(Vector3 point)
        {
            var tStruct = new PathRelativePosition();

            MapPointToPath(point, ref tStruct);
            return tStruct.outside;
        }


        public void DrawGizmos()
        {
            for (var i = 0; i < Path.Count - 1; i++)
            {
                Debug.DrawLine(Path[i], Path[i + 1], Color.green);
            }
        }
    }
}