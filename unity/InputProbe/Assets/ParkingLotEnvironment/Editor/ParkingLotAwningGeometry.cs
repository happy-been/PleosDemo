using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParkingLotEnvironment.Editor
{
    // Pure geometry for the shop awning, in shop local space. Single-sided; the shader is expected to use Cull Off.
    // Submesh 0 = coloured stripes (even panels + side triangles), submesh 1 = cream stripes (odd panels).
    // UV0 is in physical metres. Front-facing winding follows Unity's clockwise rule, i.e. (p1-p0)x(p2-p0) is the front normal.
    public static class ParkingLotAwningGeometry
    {
        const int PanelCount = 55;
        const float PanelWidth = 0.2f;
        const int TopSegU = 1, TopSegT = 8, ValSegU = 1, ValSegV = 2;
        const float HalfWidth = 5.5f, WallZ = -4.5f, Depth = 1.31f;
        const float AttachY = 2.958f, FrontY = 2.508f, Sag = 0.035f;
        const float ValanceBottomY = 2.28f, ValanceHeight = 0.22f, ValanceWave = 0.008f;

        static readonly float SlopeLength = Mathf.Sqrt(Depth * Depth + 0.45f * 0.45f);

        public static Mesh Create()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>[] { new List<int>(), new List<int>() };

            for (int p = 0; p < PanelCount; p++)
            {
                float x0 = -HalfWidth + p*PanelWidth;
                List<int> sub = tris[p % 2]; // even index -> coloured (0), odd -> cream (1)

                // Top cloth: i runs along +X, j runs wall -> front. Welded within the panel only.
                AddGrid(verts, uvs, sub, TopSegU, TopSegT,
                    (u, t) => TopPoint(u, t, x0),
                    (u, t) => new Vector2(x0 + u*PanelWidth, SlopeLength * t));

                // Valance: i runs along +X, j runs top -> bottom. Separate vertices keep the fold crease.
                AddGrid(verts, uvs, sub, ValSegU, ValSegV,
                    (u, v) => ValancePoint(x0 + u*PanelWidth, v),
                    (u, v) => new Vector2(x0 + u*PanelWidth, SlopeLength + ValanceHeight * v));
            }

            AddSide(verts, uvs, tris[0], HalfWidth);
            AddSide(verts, uvs, tris[0], -HalfWidth);

            var mesh = new Mesh { name = "Shop_awning_fabric_candidate" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tris[0], 0);
            mesh.SetTriangles(tris[1], 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents(); // needs normals and UV0
            mesh.RecalculateBounds();
            return mesh;
        }

        // u is local to the panel (0..1) for the sag; t: 0 = wall, 1 = front edge.
        static Vector3 TopPoint(float u, float t, float panelX0)
        {
            float x=panelX0+u*PanelWidth;
            // The unchanged metal arms are at x=-5.4,0,+5.4. Sag between them, never through them.
            float span=Mathf.Sin(Mathf.PI*Mathf.Clamp01(Mathf.Abs(x)/5.4f));
            float y = Mathf.Lerp(AttachY, FrontY, t) - Sag * span * Mathf.Sin(Mathf.PI * t);
            return new Vector3(x, y, WallZ - Depth * t);
        }

        // v: 0 = top hem at the front edge, 1 = bottom hem. Normal faces -Z.
        static Vector3 ValancePoint(float x, float v)
        {
            float wave = ValanceWave * Mathf.Sin(2f * Mathf.PI * (x + HalfWidth));
            float bottomY = ValanceBottomY - wave;
            return new Vector3(x, Mathf.Lerp(FrontY, bottomY, v), WallZ - Depth + wave * v);
        }

        // Grid with (nu+1)*(nv+1) vertices and 2*nu*nv triangles.
        // Tris are (i,j),(i+1,j),(i,j+1) and (i+1,j),(i+1,j+1),(i,j+1): normal = dU x dV.
        // Top: +X x (wall->front) = up. Valance: +X x (down) = -Z.
        static void AddGrid(List<Vector3> verts, List<Vector2> uvs, List<int> tris, int nu, int nv,
            Func<float, float, Vector3> pos, Func<float, float, Vector2> uv)
        {
            int start = verts.Count;
            for (int j = 0; j <= nv; j++)
            {
                for (int i = 0; i <= nu; i++)
                {
                    float u = i / (float)nu, v = j / (float)nv;
                    verts.Add(pos(u, v));
                    uvs.Add(uv(u, v));
                }
            }

            int stride = nu + 1;
            for (int j = 0; j < nv; j++)
            {
                for (int i = 0; i < nu; i++)
                {
                    int a = start + j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
        }

        // Triangle attach -> front edge -> rear, facing outward (+X for x > 0, -X for x < 0).
        static void AddSide(List<Vector3> verts, List<Vector2> uvs, List<int> tris, float x)
        {
            int s = verts.Count;
            var attach = new Vector3(x, AttachY, WallZ);
            var edge = new Vector3(x, FrontY, WallZ - Depth);
            var rear = new Vector3(x, FrontY, WallZ);
            verts.Add(attach); verts.Add(edge); verts.Add(rear);
            foreach (var v in new[] { attach, edge, rear })
                uvs.Add(new Vector2(-v.z, v.y));

            // (edge-attach)x(rear-attach) points -X, so reverse the order for +X.
            if (x > 0f) { tris.Add(s); tris.Add(s + 2); tris.Add(s + 1); }
            else { tris.Add(s); tris.Add(s + 1); tris.Add(s + 2); }
        }
    }
}
