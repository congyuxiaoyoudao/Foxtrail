using UnityEngine;

namespace Hiking.Journey
{
    /// <summary>
    /// 生成水平放置的3D环形地图（XZ平面）
    /// </summary>
    public static class Ring3DMeshBuilder
    {
        /// <summary>
        /// 创建3D环形地面patch（在XZ平面）
        /// </summary>
        public static Mesh CreateGroundPatch(float angleWidth, float innerRadius, float outerRadius, float thickness, string name, float startAngle = 0)
        {
            int segments = Mathf.Max(4, Mathf.CeilToInt(angleWidth));

            // 顶面和底面各需要顶点
            int ringsPerSurface = 2;
            int verticesPerRing = segments + 1;
            int topVertices = verticesPerRing * ringsPerSurface;
            int bottomVertices = topVertices;

            // 侧边顶点
            int innerSideVertices = verticesPerRing * 2;
            int outerSideVertices = verticesPerRing * 2;

            int totalVertices = topVertices + bottomVertices + innerSideVertices + outerSideVertices;

            var vertices = new Vector3[totalVertices];
            var normals = new Vector3[totalVertices];
            var uv = new Vector2[totalVertices];
            var triangles = new int[(segments * 2 * 6) + (segments * 6 * 2)];

            int vertIndex = 0;
            int triIndex = 0;

            // 1. 顶面 (y = thickness/2)
            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + angleWidth * i / segments;
                Vector3 pos = PointOnRing(angle, 0); // XZ平面上的点

                // 内圈顶点
                vertices[vertIndex] = pos * innerRadius + Vector3.up * thickness * 0.5f;
                normals[vertIndex] = Vector3.up;
                uv[vertIndex] = new Vector2((float)i / segments, 0);
                vertIndex++;

                // 外圈顶点
                vertices[vertIndex] = pos * outerRadius + Vector3.up * thickness * 0.5f;
                normals[vertIndex] = Vector3.up;
                uv[vertIndex] = new Vector2((float)i / segments, 1);
                vertIndex++;
            }

            // 顶面三角形
            for (int i = 0; i < segments; i++)
            {
                int baseV = i * 2;
                triangles[triIndex++] = baseV;
                triangles[triIndex++] = baseV + 2;
                triangles[triIndex++] = baseV + 1;

                triangles[triIndex++] = baseV + 1;
                triangles[triIndex++] = baseV + 2;
                triangles[triIndex++] = baseV + 3;
            }

            int bottomStart = vertIndex;

            // 2. 底面 (y = -thickness/2)
            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + angleWidth * i / segments;
                Vector3 pos = PointOnRing(angle, 0);

                vertices[vertIndex] = pos * innerRadius + Vector3.down * thickness * 0.5f;
                normals[vertIndex] = Vector3.down;
                uv[vertIndex] = new Vector2((float)i / segments, 0);
                vertIndex++;

                vertices[vertIndex] = pos * outerRadius + Vector3.down * thickness * 0.5f;
                normals[vertIndex] = Vector3.down;
                uv[vertIndex] = new Vector2((float)i / segments, 1);
                vertIndex++;
            }

            // 底面三角形
            for (int i = 0; i < segments; i++)
            {
                int baseV = bottomStart + i * 2;
                triangles[triIndex++] = baseV;
                triangles[triIndex++] = baseV + 1;
                triangles[triIndex++] = baseV + 2;

                triangles[triIndex++] = baseV + 1;
                triangles[triIndex++] = baseV + 3;
                triangles[triIndex++] = baseV + 2;
            }

            int innerSideStart = vertIndex;

            // 3. 内侧面
            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + angleWidth * i / segments;
                Vector3 pos = PointOnRing(angle, 0);
                Vector3 normal = -pos; // 指向环内部

                vertices[vertIndex] = pos * innerRadius + Vector3.up * thickness * 0.5f;
                normals[vertIndex] = normal;
                uv[vertIndex] = new Vector2((float)i / segments, 1);
                vertIndex++;

                vertices[vertIndex] = pos * innerRadius + Vector3.down * thickness * 0.5f;
                normals[vertIndex] = normal;
                uv[vertIndex] = new Vector2((float)i / segments, 0);
                vertIndex++;
            }

            // 内侧面三角形
            for (int i = 0; i < segments; i++)
            {
                int baseV = innerSideStart + i * 2;
                triangles[triIndex++] = baseV;
                triangles[triIndex++] = baseV + 1;
                triangles[triIndex++] = baseV + 2;

                triangles[triIndex++] = baseV + 1;
                triangles[triIndex++] = baseV + 3;
                triangles[triIndex++] = baseV + 2;
            }

            int outerSideStart = vertIndex;

            // 4. 外侧面
            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + angleWidth * i / segments;
                Vector3 pos = PointOnRing(angle, 0);
                Vector3 normal = pos; // 指向环外部

                vertices[vertIndex] = pos * outerRadius + Vector3.up * thickness * 0.5f;
                normals[vertIndex] = normal;
                uv[vertIndex] = new Vector2((float)i / segments, 1);
                vertIndex++;

                vertices[vertIndex] = pos * outerRadius + Vector3.down * thickness * 0.5f;
                normals[vertIndex] = normal;
                uv[vertIndex] = new Vector2((float)i / segments, 0);
                vertIndex++;
            }

            // 外侧面三角形
            for (int i = 0; i < segments; i++)
            {
                int baseV = outerSideStart + i * 2;
                triangles[triIndex++] = baseV;
                triangles[triIndex++] = baseV + 2;
                triangles[triIndex++] = baseV + 1;

                triangles[triIndex++] = baseV + 1;
                triangles[triIndex++] = baseV + 2;
                triangles[triIndex++] = baseV + 3;
            }

            var mesh = new Mesh
            {
                name = name,
                vertices = vertices,
                normals = normals,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 创建3D环形路径
        /// </summary>
        public static Mesh CreatePathRing(float angleWidth, float innerRadius, float outerRadius, float height, string name, float startAngle = 0)
        {
            return CreateGroundPatch(angleWidth, innerRadius, outerRadius, height, name, startAngle);
        }

        /// <summary>
        /// 在XZ平面上的圆环点（Y作为高度轴），逆时针方向
        /// </summary>
        public static Vector3 PointOnRing(float angle, float height)
        {
            float radians = angle * Mathf.Deg2Rad;
            // 使用负角度实现逆时针：X = -Sin, Z = Cos
            return new Vector3(-Mathf.Sin(radians), height, Mathf.Cos(radians));
        }
    }
}
