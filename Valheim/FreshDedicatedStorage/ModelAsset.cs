using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Mike.Valheim.SmallStorageChest
{
    internal static class ModelAsset
    {
        // This is the fixed collision and snap footprint used by the existing placed boxes.
        internal static readonly Vector3 BoxSize = new Vector3(0.55f, 0.40f, 0.55f);
        // The visible chest deliberately overfills that footprint so rows look like a compact
        // storage wall without changing the established placement grid.
        internal static readonly Vector3 BoxVisualSize = new Vector3(0.70f, 0.52f, 0.70f);
        internal static readonly Vector3 ColumnSize = new Vector3(0.75f, 1.20f, 0.75f);

        private static readonly Dictionary<string, Color[]> Colors = new Dictionary<string, Color[]>
        {
            ["dedicated_storage_box"] = new[] { new Color(.30f, .15f, .07f) },
            ["hugins_reliquary_column"] = new[] { new Color(.358f, .329f, .250f) }
        };

        internal static Material Material(string model, Material source)
        {
            if (!source || !source.shader) throw new InvalidOperationException("Base chest shader is unavailable.");
            var colors = Colors[model];
            var texture = new Texture2D(colors.Length, 1, TextureFormat.RGBA32, false, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int index = 0; index < colors.Length; index++) texture.SetPixel(index, 0, colors[index].gamma);
            texture.Apply();
            var material = new Material(source.shader) { name = model + "_material", shaderKeywords = Array.Empty<string>() };
            material.mainTexture = texture;
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .12f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return material;
        }

        internal static Mesh Mesh(string model, Vector3 size)
        {
            var positions = new List<Vector3>();
            var texcoords = new List<Vector2>();
            var normals = new List<Vector3>();
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var meshNormals = new List<Vector3>();
            var triangles = new List<int>();
            foreach (string line in System.Text.Encoding.UTF8.GetString(Read(model + ".obj")).Split('\n'))
            {
                string[] part = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (part.Length == 0) continue;
                if (part[0] == "v") positions.Add(new Vector3(-F(part[1]), F(part[2]), F(part[3])));
                else if (part[0] == "vt") texcoords.Add(new Vector2(F(part[1]), F(part[2])));
                else if (part[0] == "vn") normals.Add(new Vector3(-F(part[1]), F(part[2]), F(part[3])));
                else if (part[0] == "f")
                {
                    int first = vertices.Count;
                    for (int index = 1; index < part.Length; index++)
                    {
                        string[] face = part[index].Split('/');
                        vertices.Add(positions[int.Parse(face[0], CultureInfo.InvariantCulture) - 1]);
                        uv.Add(texcoords[int.Parse(face[1], CultureInfo.InvariantCulture) - 1]);
                        meshNormals.Add(normals[int.Parse(face[2], CultureInfo.InvariantCulture) - 1]);
                    }
                    for (int index = 1; index < part.Length - 2; index++)
                    {
                        triangles.Add(first); triangles.Add(first + index + 1); triangles.Add(first + index);
                    }
                }
            }
            if (vertices.Count == 0 || triangles.Count == 0 || vertices.Count > 65535)
                throw new InvalidDataException(model + " has invalid runtime geometry.");
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(vertex);
            var scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            for (int index = 0; index < vertices.Count; index++)
            {
                vertices[index] = Vector3.Scale(vertices[index] - origin, scale);
                Vector3 normal = meshNormals[index];
                meshNormals[index] = new Vector3(normal.x / scale.x, normal.y / scale.y, normal.z / scale.z).normalized;
            }
            var mesh = new Mesh { name = model + "_mesh" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetNormals(meshNormals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        private static byte[] Read(string name)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mike.Valheim.SmallStorageChest.Assets." + name))
            using (var output = new MemoryStream())
            {
                if (input == null) throw new FileNotFoundException(name);
                input.CopyTo(output);
                return output.ToArray();
            }
        }
        private static float F(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
