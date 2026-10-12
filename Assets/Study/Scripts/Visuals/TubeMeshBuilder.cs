using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Study.Visuals
{
    // Builds thin tube meshes along polylines. Tubes instead of 1-pixel
    // LineRenderers because those shimmer and alias badly in a headset.
    // Several polylines can go into one mesh (one draw call per style group).
    //   UV.x = distance along the line in metres (used for dashes)
    //   vertex alpha = per-point fade (used for trail tails)
    public class TubeMeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
        }

        // alphas: optional, one per point (defaults to 1).
        public void AddPolyline(IList<Vector3> points, float radius, int sides, bool closed, IList<float> alphas = null)
        {
            int count = points.Count;
            if (count < 2) return;
            int rings = closed ? count + 1 : count; // closed loops repeat the first ring so UVs run continuously
            int firstVertex = vertices.Count;

            // Parallel-transport frames keep the tube from twisting along curves.
            Vector3 previousTangent = Tangent(points, 0, closed);
            Vector3 normal = AnyPerpendicular(previousTangent);
            float distance = 0;

            for (int ring = 0; ring < rings; ring++)
            {
                int i = ring % count;
                Vector3 tangent = Tangent(points, i, closed);
                normal = Vector3.ProjectOnPlane(normal, tangent);
                if (normal.sqrMagnitude < 1e-12f) normal = AnyPerpendicular(tangent);
                normal.Normalize();
                Vector3 binormal = Vector3.Cross(tangent, normal);
                if (ring > 0) distance += Vector3.Distance(points[(ring - 1) % count], points[i]);
                float alpha = alphas != null ? alphas[i] : 1f;

                for (int s = 0; s <= sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / sides;
                    Vector3 direction = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                    vertices.Add(points[i] + direction * radius);
                    normals.Add(direction);
                    uvs.Add(new Vector2(distance, s / (float)sides));
                    colors.Add(new Color(1, 1, 1, alpha));
                }
                previousTangent = tangent;
            }

            int stride = sides + 1;
            for (int ring = 0; ring < rings - 1; ring++)
            for (int s = 0; s < sides; s++)
            {
                int a = firstVertex + ring * stride + s;
                int b = a + stride;
                // Wound so faces point outward (Unity front faces are clockwise).
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
            }
        }

        public void AddSegment(Vector3 from, Vector3 to, float radius, int sides)
        {
            AddPolyline(new[] { from, to }, radius, sides, false);
        }

        // Writes the accumulated geometry into mesh (created if null) and returns it.
        public Mesh ToMesh(Mesh mesh = null)
        {
            if (mesh == null) mesh = new Mesh { name = "Tube", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 Tangent(IList<Vector3> points, int i, bool closed)
        {
            int n = points.Count;
            Vector3 previous = closed ? points[(i - 1 + n) % n] : points[Mathf.Max(i - 1, 0)];
            Vector3 next = closed ? points[(i + 1) % n] : points[Mathf.Min(i + 1, n - 1)];
            return (next - previous).normalized;
        }

        static Vector3 AnyPerpendicular(Vector3 v)
        {
            Vector3 other = Mathf.Abs(v.y) < 0.9f ? Vector3.up : Vector3.right;
            return Vector3.Cross(v, other).normalized;
        }

        // A UV sphere, for the shell and the vector's tip marker.
        public static Mesh Sphere(float radius, int longitudeSegments, int latitudeSegments)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float theta = lat * Mathf.PI / latitudeSegments;
                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float phi = lon * 2f * Mathf.PI / longitudeSegments;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    verts.Add(n * radius);
                    norms.Add(n);
                }
            }
            int stride = longitudeSegments + 1;
            for (int lat = 0; lat < latitudeSegments; lat++)
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int a = lat * stride + lon;
                int b = a + stride;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
            }
            var mesh = new Mesh { name = "Sphere", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
