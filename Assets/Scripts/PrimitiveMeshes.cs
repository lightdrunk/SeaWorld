using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 共享的程序化图元网格。手工构建，全程不创建临时 GameObject，杜绝层级泄漏。
/// </summary>
public static class PrimitiveMeshes
{
    private static Mesh _unitSphere;

    /// <summary>半径 0.5 的单位球（经纬网格）。</summary>
    public static Mesh UnitSphere
    {
        get
        {
            if (_unitSphere == null)
            {
                _unitSphere = BuildUnitSphere();
            }
            return _unitSphere;
        }
    }

    private static Mesh BuildUnitSphere()
    {
        const int lonSeg = 16;
        const int latSeg = 12;

        Vector3[] verts = new Vector3[(lonSeg + 1) * (latSeg + 1)];
        Vector2[] uvs = new Vector2[verts.Length];
        int vi = 0;
        for (int lat = 0; lat <= latSeg; lat++)
        {
            float theta = lat / (float)latSeg * Mathf.PI;
            float sinT = Mathf.Sin(theta);
            float cosT = Mathf.Cos(theta);
            for (int lon = 0; lon <= lonSeg; lon++)
            {
                float phi = lon / (float)lonSeg * Mathf.PI * 2f;
                verts[vi] = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi)) * 0.5f;
                uvs[vi] = new Vector2(lon / (float)lonSeg, lat / (float)latSeg);
                vi++;
            }
        }

        int[] tris = new int[lonSeg * latSeg * 6];
        int ti = 0;
        for (int lat = 0; lat < latSeg; lat++)
        {
            for (int lon = 0; lon < lonSeg; lon++)
            {
                int a = lat * (lonSeg + 1) + lon;
                int b = a + lonSeg + 1;
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = a + 1;
                tris[ti++] = a + 1; tris[ti++] = b; tris[ti++] = b + 1;
            }
        }

        Mesh m = new Mesh { name = "UnitSphere" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = verts;
        m.uv = uvs;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}