using System;
using System.Collections.Generic;
using UnityEngine;

namespace Foost {
namespace Utils {

public class LineRenderer
{
    //public static readonly Shader debugShader = Shader.Find("Battlehub/RTGizmos/Handles");
    public static readonly Shader debugShader = Shader.Find("Battlehub/RTHandles/VertexColor");

    private bool _updateOnly;
    private Mesh _mesh;
    private Material _material;
    private int[] _meshIndices;
    public List<Vector3> vertices = new List<Vector3>();
    public List<Color> colors = new List<Color>();

    public LineRenderer(bool updateOnly)
    {
        _updateOnly = updateOnly;
        _mesh = new Mesh();
        _mesh.hideFlags = HideFlags.HideAndDontSave;
        _mesh.MarkDynamic();
        _material = new Material(debugShader);
    }

    public void CheckUpdate()
    {
        if (_updateOnly && Time.inFixedTimeStep) {
            throw new System.Exception("Started debug line renderer from fixed time step");
        }
    }

    public void AddLine(Vector3 from, Vector3 to, Color fromColor, Color toColor)
    {
        vertices.Add(from);
        vertices.Add(to);
        colors.Add(fromColor);
        colors.Add(toColor);
    }

    public void AddLine(Vector3 from, Vector3 to, Color color)
    {
        AddLine(from, to, color, color);
    }

    public void DrawFrame()
    {
        CheckUpdate();

        int vcount = vertices.Count;
        if (vcount == 0) {
            return;
        }

        if ((_meshIndices != null) && (_meshIndices.Length != vcount)) {
            _meshIndices = null;
        }

        bool indicesChanged = (_meshIndices == null);
        if (indicesChanged) {
            _meshIndices = new int[vcount];
            for (int i = 0; i < vcount; ++i) {
                _meshIndices[i] = i;
            }
            _mesh.indexFormat = (vcount >= 65536) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.Clear();
        }

        _mesh.SetVertices(vertices);
        _mesh.SetColors(colors);
        if (indicesChanged) {
            _mesh.SetIndices(_meshIndices, MeshTopology.Lines, 0);
        }
        _mesh.RecalculateBounds();

        Graphics.DrawMesh(_mesh, Matrix4x4.identity, _material, 0, null, 0, null, false, false);

        vertices.Clear();
        colors.Clear();
    }
}

public static class DebugDraw
{
    private static LineRenderer _renderer = new LineRenderer(true);


    public static void DrawFrame()
    {
        _renderer.DrawFrame();
    }

    public static LineRenderer StartRender()
    {
        _renderer.CheckUpdate();
        return _renderer;
    }

    public static void StartLines(out List<Vector3> vertices, out List<Color> colors)
    {
        _renderer.CheckUpdate();
        vertices = _renderer.vertices;
        colors = _renderer.colors;
    }

    public static void Line(Vector3 from, Vector3 to, Color fromColor, Color toColor)
    {
        _renderer.CheckUpdate();
        _renderer.AddLine(from, to, fromColor, toColor);
    }

    public static void Line(Vector3 from, Vector3 to, Color color)
    {
        _renderer.CheckUpdate();
        _renderer.AddLine(from, to, color);
    }

    public static void Lines(Vector3[] positions, Color color, int start = 0, int count = -1, bool drawZero = false, bool looped = false)
    {
        if ((positions == null) || (count == 0)) {
            return;
        }
        _renderer.CheckUpdate();

        if (start < 0) {
            start = positions.Length + start;
        }
        if (start >= positions.Length) {
            return;
        }
        if (count < 0) {
            count = positions.Length - start;
        }
        if (start < 0) {
            count += start;
            start = 0;
        }
        if (start + count > positions.Length) {
            count = positions.Length - start;
        }
        if (count <= 1) {
            return;
        }

        Vector3 prevPos = positions[start];
        Vector3 firstPos = prevPos;
        while (count > 1) {
            --count;
            Vector3 nextPos = positions[++start];
            if (drawZero || (nextPos != Vector3.zero)) {
                if ((! drawZero) && (firstPos == Vector3.zero)) {
                    firstPos = nextPos;
                }
                if (drawZero || (prevPos != Vector3.zero)) {
                    _renderer.AddLine(prevPos, nextPos, color);
                }
                prevPos = nextPos;
            }
        }

        if (looped && (drawZero || (firstPos != Vector3.zero))) {
            _renderer.AddLine(prevPos, firstPos, color);
        }
    }

    public static void Transform(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, float size)
    {
        _renderer.CheckUpdate();
        _renderer.AddLine(p, p + right * size, Color.red);
        _renderer.AddLine(p, p + up * size, Color.green);
        _renderer.AddLine(p, p + forward * size, Color.blue);
    }

    public static void Transform(Transform t, float size)
    {
        Transform(t.position, t.right, t.up, t.forward, size);
    }

    public static void Cross(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, float size)
    {
        _renderer.CheckUpdate();
        _renderer.AddLine(p - right * size, p + right * size, color);
        _renderer.AddLine(p - up * size, p + up * size, color);
        _renderer.AddLine(p - forward * size, p + forward * size, color);
    }

    public static void Cross(Transform t, Color color, float size)
    {
        Cross(t.position, t.right, t.up, t.forward, color, size);
    }

    public static void Cross(Vector3 p, Color color, float size)
    {
        Cross(p, Vector3.right, Vector3.up, Vector3.forward, color, size);
    }

    public static void Sphere(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, float radius, int segments = 16, int rings = 10)
    {
        _renderer.CheckUpdate();
        segments = Math.Max(3, segments);
        rings = Math.Max(3, rings);
        right *= radius;
        up *= radius;
        forward *= radius;

        for (int i = 0; i < rings; ++i) {
            float a0 = Mathf.PI * i / rings;
            float a1 = Mathf.PI * (i + 1) / rings;
            float y0 = Mathf.Cos(a0);
            float y1 = Mathf.Cos(a1);
            float xz0 = Mathf.Sin(a0);
            float xz1 = Mathf.Sin(a1);

            for (int j = 0; j < segments; ++j) {
                float b0 = 2.0f * Mathf.PI * j / segments;
                float x0 = Mathf.Cos(b0);
                float z0 = Mathf.Sin(b0);

                Vector3 p0 = p + (right * xz0 * x0) + (up * y0) + (forward * xz0 * z0);
                Vector3 p01 = p + (right * xz1 * x0) + (up * y1) + (forward * xz1 * z0);
                _renderer.AddLine(p0, p01, color);

                if (i != 0) {
                    float b1 = 2.0f * Mathf.PI * (j + 1) / segments;
                    float x1 = Mathf.Cos(b1);
                    float z1 = Mathf.Sin(b1);

                    Vector3 p10 = p + (right * xz0 * x1) + (up * y0) + (forward * xz0 * z1);
                    _renderer.AddLine(p0, p10, color);
                }
            }
        }
    }

    public static void Sphere(Transform t, Color color, float radius, int segments = 16, int rings = 10)
    {
        Sphere(t.position, t.right, t.up, t.forward, color, radius * t.lossyScale.x, segments, rings);
    }

    public static void Sphere(Vector3 p, Color color, float radius, int segments = 16, int rings = 10)
    {
        Sphere(p, Vector3.right, Vector3.up, Vector3.forward, color, radius, segments, rings);
    }

    public static void Cone(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, int direction, float length, float radius, int segments = 16)
    {
        _renderer.CheckUpdate();
        if (direction == 0) {
            Vector3 temp = right;
            right = -up;
            up = temp;
        }
        else if (direction == 1) {
            //up is up
        }
        else {
            Vector3 temp = forward;
            forward = -up;
            up = temp;
        }
        right *= radius;
        up *= length;
        forward *= radius;

        Vector3 top = p + up;
        for (int i = 0; i < segments; ++i) {
            float a0 = 2.0f * Mathf.PI * i / segments;
            float x0 = Mathf.Cos(a0);
            float z0 = Mathf.Sin(a0);

            Vector3 p0 = p + (right * x0) + (forward * z0);
            _renderer.AddLine(top, p0, color);

            float a1 = 2.0f * Mathf.PI * (i + 1) / segments;
            float x1 = Mathf.Cos(a1);
            float z1 = Mathf.Sin(a1);

            Vector3 p1 = p + (right * x1) + (forward * z1);
            _renderer.AddLine(p0, p1, color);
        }
    }

    public static void Cone(Transform t, Color color, int direction, float length, float radius, int segments = 16)
    {
        Cone(t.position, t.TransformVector(Vector3.right), t.TransformVector(Vector3.up), t.TransformVector(Vector3.forward), color, direction, length, radius, segments);
    }

    public static void Cone(Vector3 p, Color color, int direction, float length, float radius, int segments = 16)
    {
        Cone(p, Vector3.right, Vector3.up, Vector3.forward, color, direction, length, radius, segments);
    }

    public static void Capsule(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, int direction, float length, float radius, int segments = 16, int rings = 10)
    {
        _renderer.CheckUpdate();
        Vector3 dir;
        if (direction == 0) {
            dir = right;
            right = -up;
            up = dir;
        }
        else if (direction == 1) {
            dir = up;
        }
        else {
            dir = forward;
            forward = -up;
            up = dir;
        }
        right *= radius;
        up *= radius;
        forward *= radius;

        float halfLen = Mathf.Max(0.5f * length, radius);
        dir *= halfLen - radius;

        segments = Math.Max(3, segments);
        rings = Math.Max(4, rings & ~1);

        for (int i = 0; i < rings; ++i) {
            bool midRing = i == rings / 2;
            float a0 = Mathf.PI * i / rings;
            float a1 = Mathf.PI * (i + 1) / rings;
            float y0 = Mathf.Cos(a0);
            float y1 = Mathf.Cos(a1);
            float xz0 = Mathf.Sin(a0);
            float xz1 = Mathf.Sin(a1);

            float topSign = (i < rings / 2) ? 1.0f : -1.0f;

            for (int j = 0; j < segments; ++j) {
                float b0 = 2.0f * Mathf.PI * j / segments;
                float x0 = Mathf.Cos(b0);
                float z0 = Mathf.Sin(b0);

                Vector3 p0mid = p + (right * xz0 * x0) + (up * y0) + (forward * xz0 * z0);
                if (midRing) {
                    _renderer.AddLine(p0mid + dir, p0mid - dir, color);
                }
                Vector3 p0 = p0mid + topSign * dir;
                Vector3 p01 = p + (right * xz1 * x0) + (up * y1) + (forward * xz1 * z0) + topSign * dir;
                _renderer.AddLine(p0, p01, color);

                if (i != 0) {
                    float b1 = 2.0f * Mathf.PI * (j + 1) / segments;
                    float x1 = Mathf.Cos(b1);
                    float z1 = Mathf.Sin(b1);

                    Vector3 p10 = p + (right * xz0 * x1) + (up * y0) + (forward * xz0 * z1);
                    if (midRing) {
                        _renderer.AddLine(p0mid + dir, p10 + dir, color);
                    }
                    p10 += topSign * dir;
                    _renderer.AddLine(p0, p10, color);
                }
            }
        }
    }

    public static void Capsule(Transform t, Color color, int direction, float length, float radius, int segments = 16, int rings = 10)
    {
        Capsule(t.position, t.TransformVector(Vector3.right), t.TransformVector(Vector3.up), t.TransformVector(Vector3.forward), color, direction, length, radius, segments, rings);
    }

    public static void Box(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, Vector3 size)
    {
        _renderer.CheckUpdate();
        Vector3 dirpp = 0.5f * (right * size.x + forward * size.z);
        Vector3 dirpn = 0.5f * (right * size.x - forward * size.z);
        Vector3 p0 = p + 0.5f * up * size.y;

        _renderer.AddLine(p0 + dirpp, p0 + dirpn, color);
        _renderer.AddLine(p0 + dirpn, p0 - dirpp, color);
        _renderer.AddLine(p0 - dirpp, p0 - dirpn, color);
        _renderer.AddLine(p0 - dirpn, p0 + dirpp, color);

        if (size.y <= Mathf.Epsilon) {
            return;
        }

        Vector3 p1 = p - 0.5f * up * size.y;
        _renderer.AddLine(p1 + dirpp, p1 + dirpn, color);
        _renderer.AddLine(p1 + dirpn, p1 - dirpp, color);
        _renderer.AddLine(p1 - dirpp, p1 - dirpn, color);
        _renderer.AddLine(p1 - dirpn, p1 + dirpp, color);

        _renderer.AddLine(p0 + dirpp, p1 + dirpp, color);
        _renderer.AddLine(p0 + dirpn, p1 + dirpn, color);
        _renderer.AddLine(p0 - dirpp, p1 - dirpp, color);
        _renderer.AddLine(p0 - dirpn, p1 - dirpn, color);
    }

    public static void Box(Transform t, Color color, Vector3 size)
    {
        Box(t.position, t.TransformVector(Vector3.right), t.TransformVector(Vector3.up), t.TransformVector(Vector3.forward), color, size);
    }

    public static void Box(Vector3 p, Color color, Vector3 size)
    {
        Box(p, Vector3.right, Vector3.up, Vector3.forward, color, size);
    }
}

public class PolyRenderer
{
    public static readonly Shader debugShader = Shader.Find("Battlehub/RTHandles/Shape");

    private bool _updateOnly;
    private Mesh _mesh;
    private Material _material;
    private int[] _meshIndices;
    public List<Vector3> vertices = new List<Vector3>();
    public List<Vector3> normals = new List<Vector3>();
    public List<Color> colors = new List<Color>();

    public PolyRenderer(bool updateOnly, bool ztest)
    {
        _updateOnly = updateOnly;
        _mesh = new Mesh();
        _mesh.hideFlags = HideFlags.HideAndDontSave;
        _mesh.MarkDynamic();
        _material = new Material(debugShader);
        _material.color = Color.white;
        _material.SetFloat("_ZTest", ztest ? 4.0f : 0.0f);
    }

    public void CheckUpdate()
    {
        if (_updateOnly && Time.inFixedTimeStep) {
            throw new System.Exception("Started debug poly renderer from fixed time step");
        }
    }

    public void AddTri(Vector3 a, Vector3 b, Vector3 c, Color acol, Color bcol, Color ccol)
    {
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        colors.Add(acol);
        colors.Add(bcol);
        colors.Add(ccol);

        Vector3 n = Vector3.Cross((b - a).normalized, (c - a).normalized);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
    }

    public void AddTriCCW(Vector3 a, Vector3 b, Vector3 c, Color acol, Color bcol, Color ccol)
    {
        vertices.Add(c);
        vertices.Add(b);
        vertices.Add(a);
        colors.Add(ccol);
        colors.Add(bcol);
        colors.Add(acol);

        Vector3 n = Vector3.Cross((b - c).normalized, (a - c).normalized);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
    }

    public void AddTri(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        AddTri(a, b, c, color, color, color);
    }

    public void AddTriCCW(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        AddTriCCW(a, b, c, color, color, color);
    }

    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color acol, Color bcol, Color ccol, Color dcol)
    {
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        colors.Add(acol);
        colors.Add(bcol);
        colors.Add(ccol);

        vertices.Add(a);
        vertices.Add(c);
        vertices.Add(d);
        colors.Add(acol);
        colors.Add(ccol);
        colors.Add(dcol);

        Vector3 n = Vector3.Cross((b - a).normalized, (c - a).normalized);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
    }

    public void AddQuadCCW(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color acol, Color bcol, Color ccol, Color dcol)
    {
        vertices.Add(d);
        vertices.Add(c);
        vertices.Add(a);
        colors.Add(dcol);
        colors.Add(ccol);
        colors.Add(acol);

        vertices.Add(a);
        vertices.Add(c);
        vertices.Add(b);
        colors.Add(acol);
        colors.Add(ccol);
        colors.Add(bcol);

        Vector3 n = Vector3.Cross((b - c).normalized, (a - c).normalized);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
        normals.Add(n);
    }

    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        AddQuad(a, b, c, d, color, color, color, color);
    }

    public void AddQuadCCW(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        AddQuadCCW(a, b, c, d, color, color, color, color);
    }

    public void DrawFrame()
    {
        CheckUpdate();

        int vcount = vertices.Count;
        if (vcount == 0) {
            return;
        }

        if ((_meshIndices != null) && (_meshIndices.Length != vcount)) {
            _meshIndices = null;
        }

        bool indicesChanged = (_meshIndices == null);
        if (indicesChanged) {
            _meshIndices = new int[vcount];
            for (int i = 0; i < vcount; ++i) {
                _meshIndices[i] = i;
            }
            _mesh.indexFormat = (vcount >= 65536) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.Clear();
        }

        _mesh.SetVertices(vertices);
        _mesh.SetColors(colors);
        _mesh.SetNormals(normals);
        if (indicesChanged) {
            _mesh.SetIndices(_meshIndices, MeshTopology.Triangles, 0);
        }
        _mesh.RecalculateBounds();

        Graphics.DrawMesh(_mesh, Matrix4x4.identity, _material, 0, null, 0, null, false, false);

        vertices.Clear();
        colors.Clear();
        normals.Clear();
    }
}

// Default are:
// - z-test enabled ; disable and re-enable it if drawing see-through solids
// - vertex order is CW
public static class SolidDraw
{
    private static PolyRenderer _ztestRenderer = new PolyRenderer(true, true);
    private static PolyRenderer _noztestRenderer = new PolyRenderer(true, false);
    private static PolyRenderer _renderer = _ztestRenderer;

    public static void DrawFrame()
    {
        _ztestRenderer.DrawFrame();
        _noztestRenderer.DrawFrame();
    }

    public static void EnableZTest(bool enable)
    {
        _renderer = enable ? _ztestRenderer : _noztestRenderer;
    }

    public static PolyRenderer StartRender()
    {
        _renderer.CheckUpdate();
        return _renderer;
    }

    public static void StartTris(out List<Vector3> vertices, out List<Color> colors)
    {
        _renderer.CheckUpdate();
        vertices = _renderer.vertices;
        colors = _renderer.colors;
    }

    public static void Tri(Vector3 a, Vector3 b, Vector3 c, Color acol, Color bcol, Color ccol)
    {
        _renderer.CheckUpdate();
        _renderer.AddTri(a, b, c, acol, bcol, ccol);
    }

    public static void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        _renderer.CheckUpdate();
        _renderer.AddTri(a, b, c, color);
    }

    public static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color acol, Color bcol, Color ccol, Color dcol)
    {
        _renderer.CheckUpdate();
        _renderer.AddQuad(a, b, c, d, acol, bcol, ccol, dcol);
    }

    public static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        _renderer.CheckUpdate();
        _renderer.AddQuad(a, b, c, d, color);
    }

    public static void Tri(Vector3 a, Vector3 b, Vector3 c, Color acol, Color bcol, Color ccol, bool ccw)
    {
        _renderer.CheckUpdate();
        if (ccw) {
            _renderer.AddTriCCW(a, b, c, acol, bcol, ccol);
        }
        else {
            _renderer.AddTri(a, b, c, acol, bcol, ccol);
        }
    }

    public static void Tri(Vector3 a, Vector3 b, Vector3 c, Color color, bool ccw)
    {
        _renderer.CheckUpdate();
        if (ccw) {
            _renderer.AddTriCCW(a, b, c, color);
        }
        else {
            _renderer.AddTri(a, b, c, color);
        }
    }

    public static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color acol, Color bcol, Color ccol, Color dcol, bool ccw)
    {
        _renderer.CheckUpdate();
        if (ccw) {
            _renderer.AddQuadCCW(a, b, c, d, acol, bcol, ccol, dcol);
        }
        else {
            _renderer.AddQuad(a, b, c, d, acol, bcol, ccol, dcol);
        }
    }

    public static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, bool ccw)
    {
        _renderer.CheckUpdate();
        if (ccw) {
            _renderer.AddQuadCCW(a, b, c, d, color);
        }
        else {
            _renderer.AddQuad(a, b, c, d, color);
        }
    }

    public static void Sphere(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, float radius, int segments = 16, int rings = 10)
    {
        _renderer.CheckUpdate();
        segments = Math.Max(3, segments);
        rings = Math.Max(3, rings);
        right *= radius;
        up *= radius;
        forward *= radius;

        for (int i = 0; i < rings; ++i) {
            float a0 = Mathf.PI * i / rings;
            float a1 = Mathf.PI * (i + 1) / rings;
            float y0 = Mathf.Cos(a0);
            float y1 = Mathf.Cos(a1);
            float xz0 = Mathf.Sin(a0);
            float xz1 = Mathf.Sin(a1);

            for (int j = 0; j < segments; ++j) {
                float b0 = 2.0f * Mathf.PI * j / segments;
                float x0 = Mathf.Cos(b0);
                float z0 = Mathf.Sin(b0);

                Vector3 p00 = p + (right * xz0 * x0) + (up * y0) + (forward * xz0 * z0);
                Vector3 p01 = p + (right * xz1 * x0) + (up * y1) + (forward * xz1 * z0);

                float b1 = 2.0f * Mathf.PI * (j + 1) / segments;
                float x1 = Mathf.Cos(b1);
                float z1 = Mathf.Sin(b1);
                Vector3 p10 = p + (right * xz0 * x1) + (up * y0) + (forward * xz0 * z1);
                Vector3 p11 = p + (right * xz1 * x1) + (up * y1) + (forward * xz1 * z1);

                if (i == 0) {
                    _renderer.AddTriCCW(p00, p01, p11, color);
                }
                else if (i == rings - 1) {
                    _renderer.AddTriCCW(p00, p01, p10, color);
                }
                else {
                    _renderer.AddQuadCCW(p00, p01, p11, p10, color);
                }
            }
        }
    }

    public static void Sphere(Transform t, Color color, float radius, int segments = 16, int rings = 10)
    {
        Sphere(t.position, t.right, t.up, t.forward, color, radius * t.lossyScale.x, segments, rings);
    }

    public static void Sphere(Vector3 p, Color color, float radius, int segments = 16, int rings = 10)
    {
        Sphere(p, Vector3.right, Vector3.up, Vector3.forward, color, radius, segments, rings);
    }

    public static void Cone(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, int direction, float length, float radius, int segments = 16)
    {
        _renderer.CheckUpdate();
        if (direction == 0) {
            Vector3 temp = right;
            right = -up;
            up = temp;
        }
        else if (direction == 1) {
            //up is up
        }
        else {
            Vector3 temp = forward;
            forward = -up;
            up = temp;
        }
        right *= radius;
        up *= length;
        forward *= radius;

        Vector3 top = p + up;
        for (int i = 0; i < segments; ++i) {
            float a0 = 2.0f * Mathf.PI * i / segments;
            float x0 = Mathf.Cos(a0);
            float z0 = Mathf.Sin(a0);
            Vector3 p0 = p + (right * x0) + (forward * z0);

            float a1 = 2.0f * Mathf.PI * (i + 1) / segments;
            float x1 = Mathf.Cos(a1);
            float z1 = Mathf.Sin(a1);
            Vector3 p1 = p + (right * x1) + (forward * z1);

            _renderer.AddTriCCW(top, p0, p1, color);
            _renderer.AddTri(p, p0, p1, color);
        }
    }

    public static void Cone(Transform t, Color color, int direction, float length, float radius, int segments = 16)
    {
        Cone(t.position, t.TransformVector(Vector3.right), t.TransformVector(Vector3.up), t.TransformVector(Vector3.forward), color, direction, length, radius, segments);
    }

    public static void Cone(Vector3 p, Color color, int direction, float length, float radius, int segments = 16)
    {
        Cone(p, Vector3.right, Vector3.up, Vector3.forward, color, direction, length, radius, segments);
    }

    public static void Capsule(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, int direction, float length, float radius, int segments = 16, int rings = 10)
    {
        _renderer.CheckUpdate();
        Vector3 dir;
        if (direction == 0) {
            dir = right;
            right = -up;
            up = dir;
        }
        else if (direction == 1) {
            dir = up;
        }
        else {
            dir = forward;
            forward = -up;
            up = dir;
        }
        right *= radius;
        up *= radius;
        forward *= radius;

        float halfLen = Mathf.Max(0.5f * length, radius);
        dir *= halfLen - radius;

        segments = Math.Max(3, segments);
        rings = Math.Max(4, rings & ~1);

        for (int i = 0; i < rings; ++i) {
            bool midRing = i == rings / 2;
            float a0 = Mathf.PI * i / rings;
            float a1 = Mathf.PI * (i + 1) / rings;
            float y0 = Mathf.Cos(a0);
            float y1 = Mathf.Cos(a1);
            float xz0 = Mathf.Sin(a0);
            float xz1 = Mathf.Sin(a1);

            float topSign = (i < rings / 2) ? 1.0f : -1.0f;

            for (int j = 0; j < segments; ++j) {
                float b0 = 2.0f * Mathf.PI * j / segments;
                float x0 = Mathf.Cos(b0);
                float z0 = Mathf.Sin(b0);

                Vector3 p00mid = p + (right * xz0 * x0) + (up * y0) + (forward * xz0 * z0);
                Vector3 p00 = p00mid + topSign * dir;
                Vector3 p01 = p + (right * xz1 * x0) + (up * y1) + (forward * xz1 * z0) + topSign * dir;

                float b1 = 2.0f * Mathf.PI * (j + 1) / segments;
                float x1 = Mathf.Cos(b1);
                float z1 = Mathf.Sin(b1);

                Vector3 p10mid = p + (right * xz0 * x1) + (up * y0) + (forward * xz0 * z1);
                Vector3 p10 = p10mid + topSign * dir;
                Vector3 p11 = p + (right * xz1 * x1) + (up * y1) + (forward * xz1 * z1) + topSign * dir;

                if (i == 0) {
                    _renderer.AddTriCCW(p00, p01, p11, color);
                }
                else if (i == rings - 1) {
                    _renderer.AddTriCCW(p00, p01, p10, color);
                }
                else {
                    if (midRing) {
                        _renderer.AddQuadCCW(p00mid + dir, p00mid - dir, p10mid - dir, p10mid + dir, color);
                    }
                    _renderer.AddQuadCCW(p00, p01, p11, p10, color);
                }
            }
        }
    }

    public static void Capsule(Transform t, Color color, int direction, float length, float radius, int segments = 16, int rings = 10)
    {
        Capsule(t.position, t.TransformVector(Vector3.right), t.TransformVector(Vector3.up), t.TransformVector(Vector3.forward), color, direction, length, radius, segments, rings);
    }

    public static void Box(Vector3 p, Vector3 right, Vector3 up, Vector3 forward, Color color, Vector3 size)
    {
        _renderer.CheckUpdate();
        Vector3 dirpp = 0.5f * (right * size.x + forward * size.z);
        Vector3 dirpn = 0.5f * (right * size.x - forward * size.z);
        Vector3 p0 = p + 0.5f * up * size.y;
        _renderer.AddQuadCCW(p0 - dirpp, p0 + dirpn, p0 + dirpp, p0 - dirpn, color);

        if (size.y <= Mathf.Epsilon) {
            return;
        }

        Vector3 p1 = p - 0.5f * up * size.y;
        _renderer.AddQuad(p1 - dirpp, p1 + dirpn, p1 + dirpp, p1 - dirpn, color);

        _renderer.AddQuadCCW(p1 - dirpp, p1 + dirpn, p0 + dirpn, p0 - dirpp, color);
        _renderer.AddQuadCCW(p1 + dirpn, p1 + dirpp, p0 + dirpp, p0 + dirpn, color);
        _renderer.AddQuadCCW(p1 + dirpp, p1 - dirpn, p0 - dirpn, p0 + dirpp, color);
        _renderer.AddQuadCCW(p1 - dirpn, p1 - dirpp, p0 - dirpp, p0 - dirpn, color);
    }

    public static void Box(Transform t, Color color, Vector3 size)
    {
        Box(t.position, t.TransformVector(Vector3.right), t.TransformVector(Vector3.up), t.TransformVector(Vector3.forward), color, size);
    }

    public static void Box(Vector3 p, Color color, Vector3 size)
    {
        Box(p, Vector3.right, Vector3.up, Vector3.forward, color, size);
    }
}

} // namespace Foost.Utils
} // namespace Foost
