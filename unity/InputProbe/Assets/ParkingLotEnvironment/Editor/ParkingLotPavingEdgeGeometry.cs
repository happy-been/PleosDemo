using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParkingLotEnvironment.Editor
{
    /// <summary>
    /// Builds the exposed vertical perimeter faces ("skirts") for flat paving pads such as sidewalks.
    ///
    /// The pads themselves stay single flat quads; without skirts the ground behind them is visible through
    /// the open vertical edge between the pad top and the asphalt. This helper emits ONLY vertical faces,
    /// one strip per exposed edge interval, spanning from <c>bottom</c> up to the owning surface's top.
    /// There are no top/bottom caps and footprints are never widened.
    ///
    /// Pure geometry: no scene, asset or Editor API dependencies.
    /// </summary>
    public static class ParkingLotPavingEdgeGeometry
    {
        /// <summary>A flat paving pad: axis-aligned footprint (Rect.x = world X, Rect.y = world Z) at a world-space top height.</summary>
        public readonly struct Surface
        {
            public readonly Rect footprint;
            public readonly float top;

            public Surface(Rect footprint, float top)
            {
                this.footprint = footprint;
                this.top = top;
            }
        }

        public const string MeshName = "Neighborhood_paving_edge_faces";

        // Heights / positions closer than this are considered equal (touching pads, equal tops, merged cut points).
        private const double Tolerance = 0.0001;

        // How far we step outward / inward from an edge to find out which footprints lie on which side of it.
        // Must be larger than Tolerance so footprints touching within tolerance are still caught, yet tiny enough
        // not to reach any unrelated geometry.
        private const double ProbeDistance = 0.0002;

        // Double-precision copy of a validated surface (Rect floats convert exactly).
        private readonly struct Box
        {
            public readonly double minX, maxX, minZ, maxZ, top;

            public Box(Rect r, float top)
            {
                minX = r.xMin;
                maxX = r.xMax;
                minZ = r.yMin;
                maxZ = r.yMax;
                this.top = top;
            }

            // Strict interior test; boundary points are outside.
            public bool Contains(double x, double z)
            {
                return x > minX && x < maxX && z > minZ && z < maxZ;
            }
        }

        /// <summary>
        /// Creates one mesh containing every exposed vertical edge face of <paramref name="surfaces"/>.
        /// Normals point outward from the owning rectangle; triangles use Unity's clockwise front-face winding to match.
        /// UVs are world metres: u = distance along the edge (increasing to the viewer's right), v = world height.
        /// Input is not modified.
        /// </summary>
        public static Mesh Create(IReadOnlyList<Surface> surfaces, float bottom = -0.005f)
        {
            if (surfaces == null)
                throw new ArgumentNullException(nameof(surfaces));
            if (float.IsNaN(bottom) || float.IsInfinity(bottom))
                throw new ArgumentException("bottom must be finite.", nameof(bottom));

            var boxes = new List<Box>(surfaces.Count);
            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface s = surfaces[i];
                Rect r = s.footprint;
                if (!IsFinite(r.x) || !IsFinite(r.y) || !IsFinite(r.width) || !IsFinite(r.height))
                    throw new ArgumentException($"Surface {i} has a non-finite footprint.", nameof(surfaces));
                if (!(r.width > 0f) || !(r.height > 0f))
                    throw new ArgumentException($"Surface {i} has an empty or negative footprint.", nameof(surfaces));
                if (!IsFinite(s.top))
                    throw new ArgumentException($"Surface {i} has a non-finite top.", nameof(surfaces));
                if (!(s.top > bottom))
                    throw new ArgumentException($"Surface {i} top ({s.top}) must be above bottom ({bottom}).", nameof(surfaces));
                boxes.Add(new Box(r, s.top));
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < boxes.Count; i++)
            {
                // Four sides: (runs along X?, outward sign on the perpendicular axis).
                AppendEdge(boxes, i, true, -1, bottom, vertices, normals, uvs, triangles); // -Z side
                AppendEdge(boxes, i, true, +1, bottom, vertices, normals, uvs, triangles); // +Z side
                AppendEdge(boxes, i, false, -1, bottom, vertices, normals, uvs, triangles); // -X side
                AppendEdge(boxes, i, false, +1, bottom, vertices, normals, uvs, triangles); // +X side
            }

            var mesh = new Mesh { name = MeshName };
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (vertices.Count > 0)
                mesh.RecalculateTangents();
            return mesh;
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        /// <summary>
        /// Emits the exposed faces of one side of surface <paramref name="index"/>.
        /// The edge is split at every other footprint's end points that fall on it, each piece is classified
        /// by probing just outside / just inside its midpoint, and consecutive pieces that expose the same
        /// height band are merged back into a single quad (no internal seams, no needless subdivision).
        /// </summary>
        private static void AppendEdge(
            List<Box> boxes, int index, bool alongX, int outSign, float bottom,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            Box s = boxes[index];
            double fixedCoord = alongX ? (outSign < 0 ? s.minZ : s.maxZ) : (outSign < 0 ? s.minX : s.maxX);
            double a = alongX ? s.minX : s.minZ;
            double b = alongX ? s.maxX : s.maxZ;

            // Only footprints reaching the edge line (within probe range) and overlapping the edge span matter.
            var relevant = new List<int>();
            var cuts = new List<double> { a, b };
            for (int j = 0; j < boxes.Count; j++)
            {
                if (j == index)
                    continue;
                Box o = boxes[j];
                double oa0 = alongX ? o.minX : o.minZ;
                double oa1 = alongX ? o.maxX : o.maxZ;
                double op0 = alongX ? o.minZ : o.minX;
                double op1 = alongX ? o.maxZ : o.maxX;
                if (op0 > fixedCoord + ProbeDistance || op1 < fixedCoord - ProbeDistance)
                    continue;
                if (oa1 <= a + Tolerance || oa0 >= b - Tolerance)
                    continue; // only touches at a corner (or not at all): can never hide part of this edge
                relevant.Add(j);
                if (oa0 > a + Tolerance && oa0 < b - Tolerance) cuts.Add(oa0);
                if (oa1 > a + Tolerance && oa1 < b - Tolerance) cuts.Add(oa1);
            }

            // Sort and drop cut points closer than Tolerance (avoids sliver pieces); keep exact end points.
            cuts.Sort();
            var pts = new List<double>(cuts.Count);
            foreach (double c in cuts)
            {
                if (pts.Count == 0 || c - pts[pts.Count - 1] > Tolerance)
                    pts.Add(c);
            }
            if (pts.Count == 1)
                pts.Add(b);
            else
                pts[pts.Count - 1] = b;

            // Walk the pieces, merging neighbours that expose the same lower bound.
            bool runActive = false;
            double runStart = 0, runEnd = 0, runLow = 0;
            for (int k = 0; k + 1 < pts.Count; k++)
            {
                double p0 = pts[k], p1 = pts[k + 1];
                bool visible = TryGetVisibleLow(boxes, index, relevant, alongX, outSign, fixedCoord,
                    0.5 * (p0 + p1), bottom, out double low);

                if (visible && runActive && Math.Abs(low - runLow) <= Tolerance)
                {
                    runEnd = p1;
                    continue;
                }
                if (runActive)
                    AppendQuad(s, alongX, outSign, fixedCoord, runStart, runEnd, runLow, vertices, normals, uvs, triangles);
                runActive = visible;
                if (visible)
                {
                    runStart = p0;
                    runEnd = p1;
                    runLow = low;
                }
            }
            if (runActive)
                AppendQuad(s, alongX, outSign, fixedCoord, runStart, runEnd, runLow, vertices, normals, uvs, triangles);
        }

        /// <summary>
        /// Decides which part of the edge piece (centred on <paramref name="mid"/>) is exposed.
        /// Every hiding footprint hides the face from the bottom up to its own top, so the exposed band is
        /// [max(bottom, highest hiding top), own top]. Returns false when nothing (more than Tolerance) is exposed.
        /// </summary>
        private static bool TryGetVisibleLow(
            List<Box> boxes, int index, List<int> relevant, bool alongX, int outSign, double fixedCoord,
            double mid, float bottom, out double low)
        {
            Box s = boxes[index];
            double outPerp = fixedCoord + outSign * ProbeDistance;
            double inPerp = fixedCoord - outSign * ProbeDistance;
            double hiddenTo = double.NegativeInfinity;

            foreach (int j in relevant)
            {
                Box o = boxes[j];
                bool outsideIn = alongX ? o.Contains(mid, outPerp) : o.Contains(outPerp, mid);
                bool insideIn = alongX ? o.Contains(mid, inPerp) : o.Contains(inPerp, mid);

                if (outsideIn)
                {
                    // Either the edge lies strictly inside o (both probes inside), or o is a neighbour across the
                    // edge (only the outside probe inside). In both cases o covers the face up to o.top; a higher
                    // or equal o hides it entirely, a lower o leaves the band above o.top.
                    hiddenTo = Math.Max(hiddenTo, o.top);
                }
                else if (insideIn)
                {
                    // o overlaps this surface and ends flush with this edge line: both would emit coincident faces.
                    // Keep the taller one; on equal tops (within tolerance) keep the lower index.
                    bool oWins = o.top > s.top + Tolerance || (Math.Abs(o.top - s.top) <= Tolerance && j < index);
                    if (oWins)
                        hiddenTo = Math.Max(hiddenTo, o.top);
                }
            }

            low = Math.Max((double)bottom, hiddenTo);
            if (s.top - low <= Tolerance)
                return false;
            if (low - bottom <= Tolerance)
                low = bottom;
            return true;
        }

        /// <summary>
        /// Appends one outward-facing quad covering [from, to] along the edge and [low, s.top] in height.
        /// Vertices are not shared so every face keeps its own flat normal and world-metre UVs.
        /// </summary>
        private static void AppendQuad(
            Box s, bool alongX, int outSign, double fixedCoord, double from, double to, double low,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            // With Unity's clockwise-front winding, quad (bottom-start, top-start, top-end, bottom-end) faces
            // cross(up, travelDirection). That equals the outward normal when travelling +X on the -Z side,
            // -X on the +Z side, +Z on the +X side and -Z on the -X side.
            bool ascending = alongX ? outSign < 0 : outSign > 0;
            double start = ascending ? from : to;
            double end = ascending ? to : from;
            float yLow = (float)low;
            float yTop = (float)s.top;

            Vector3 normal = alongX ? new Vector3(0f, 0f, outSign) : new Vector3(outSign, 0f, 0f);
            // u grows along the travel direction, i.e. to the right as seen from outside (texture is never mirrored).
            float uStart = (float)(ascending ? start : -start);
            float uEnd = (float)(ascending ? end : -end);

            int baseIndex = vertices.Count;
            vertices.Add(MakePoint(alongX, fixedCoord, start, yLow));
            vertices.Add(MakePoint(alongX, fixedCoord, start, yTop));
            vertices.Add(MakePoint(alongX, fixedCoord, end, yTop));
            vertices.Add(MakePoint(alongX, fixedCoord, end, yLow));
            for (int n = 0; n < 4; n++)
                normals.Add(normal);
            uvs.Add(new Vector2(uStart, yLow));
            uvs.Add(new Vector2(uStart, yTop));
            uvs.Add(new Vector2(uEnd, yTop));
            uvs.Add(new Vector2(uEnd, yLow));

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        private static Vector3 MakePoint(bool alongX, double fixedCoord, double along, float y)
        {
            return alongX
                ? new Vector3((float)along, y, (float)fixedCoord)
                : new Vector3((float)fixedCoord, y, (float)along);
        }
    }
}
