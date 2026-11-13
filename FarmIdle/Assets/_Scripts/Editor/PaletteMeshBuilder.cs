using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Arma mallas low poly de un solo material: cada triángulo apunta con su UV al centro de una celda de la
    // paleta (Palette), igual que los modelos de Kenney con su colormap. Las normales son planas (cada cara con las
    // suyas), que es lo que da el aspecto facetado.
    // Solo formas convexas (caja, cono, esfera de 20 caras): así el sentido de las caras se corrige solo,
    // mirando que la normal apunte hacia afuera.
    internal sealed class PaletteMeshBuilder
    {
        // Esfera de 20 caras (icosaedro): 12 vértices y 20 triángulos.
        private static readonly int[] IcosahedronFaces =
        {
            0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
            3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
        };

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        // Caja rotada alrededor de su centro.
        internal void AddBox(Vector3 center, Vector3 size, Quaternion rotation, int color)
        {
            Vector3 half = size * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                // El bit 0 de i es x, el bit 1 es y y el bit 2 es z (0 = lado negativo, 1 = positivo).
                var local = new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
                corners[i] = center + rotation * local;
            }

            // Las 6 caras como cuadriláteros de esquinas consecutivas.
            int[][] faces =
            {
                new[] { 0, 2, 6, 4 },   // -x
                new[] { 1, 3, 7, 5 },   // +x
                new[] { 0, 1, 5, 4 },   // -y
                new[] { 2, 3, 7, 6 },   // +y
                new[] { 0, 1, 3, 2 },   // -z
                new[] { 4, 5, 7, 6 }    // +z
            };

            foreach (int[] face in faces)
            {
                AddTriangle(corners[face[0]], corners[face[1]], corners[face[2]], center, color);
                AddTriangle(corners[face[0]], corners[face[2]], corners[face[3]], center, color);
            }
        }

        // Caja parada sobre 'basePoint' (su centro de abajo) e inclinada alrededor de ese punto: tallos, hojas, brotes.
        internal void AddStanding(Vector3 basePoint, Vector3 size, Quaternion rotation, int color)
        {
            Vector3 center = basePoint + rotation * new Vector3(0f, size.y * 0.5f, 0f);
            AddBox(center, size, rotation, color);
        }

        // Cono (o pirámide si 'sides' es chico) con la base en 'baseCenter' y la punta en 'apex'. Sirve apuntando para cualquier lado.
        internal void AddCone(Vector3 baseCenter, float baseRadius, Vector3 apex, int sides, int color)
        {
            if (sides < 3) throw new ArgumentOutOfRangeException(nameof(sides));

            Vector3 axis = apex - baseCenter;
            Quaternion toAxis = Quaternion.FromToRotation(Vector3.up, axis.normalized);

            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                ring[i] = baseCenter + toAxis * (new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * baseRadius);
            }

            Vector3 inside = baseCenter + axis * 0.3f;
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % sides];
                AddTriangle(a, b, apex, inside, color);         // lado
                AddTriangle(a, b, baseCenter, inside, color);   // tapa
            }
        }

        // Cilindro de 'sides' lados con el eje a lo largo de Y (antes de rotarlo), centrado en 'center' y con 'height' de
        // alto. Sirve para monedas y discos. Es convexo, así que el sentido de las caras se corrige solo.
        internal void AddCylinder(Vector3 center, float radius, float height, int sides, Quaternion rotation, int color)
        {
            if (sides < 3) throw new ArgumentOutOfRangeException(nameof(sides));

            var top = new Vector3[sides];
            var bottom = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                var ring = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                top[i] = center + rotation * (ring + new Vector3(0f, height * 0.5f, 0f));
                bottom[i] = center + rotation * (ring - new Vector3(0f, height * 0.5f, 0f));
            }

            Vector3 topCenter = center + rotation * new Vector3(0f, height * 0.5f, 0f);
            Vector3 bottomCenter = center + rotation * new Vector3(0f, -height * 0.5f, 0f);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                AddTriangle(bottom[i], bottom[next], top[next], center, color);   // lado (dos triángulos por lado)
                AddTriangle(bottom[i], top[next], top[i], center, color);
                AddTriangle(top[i], top[next], topCenter, center, color);         // tapas
                AddTriangle(bottom[i], bottom[next], bottomCenter, center, color);
            }
        }

        // Esfera low poly (icosaedro), por ejemplo los tomates.
        internal void AddSphere(Vector3 center, float radius, int color)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] unit =
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f)
            };

            for (int i = 0; i < unit.Length; i++)
            {
                unit[i] = center + unit[i].normalized * radius;
            }

            // Un error de tipeo en la lista de caras dejaría agujeros: en un icosaedro todas las aristas miden lo mismo.
            float edge = (unit[0] - unit[11]).magnitude;
            for (int i = 0; i < IcosahedronFaces.Length; i += 3)
            {
                Vector3 a = unit[IcosahedronFaces[i]];
                Vector3 b = unit[IcosahedronFaces[i + 1]];
                Vector3 c = unit[IcosahedronFaces[i + 2]];

                if (Mathf.Abs((a - b).magnitude - edge) > 1e-4f || Mathf.Abs((b - c).magnitude - edge) > 1e-4f || Mathf.Abs((c - a).magnitude - edge) > 1e-4f)
                {
                    throw new InvalidOperationException("La lista de caras del icosaedro está mal (cara " + i / 3 + ").");
                }

                AddTriangle(a, b, c, center, color);
            }
        }

        // Un triángulo de un modelo ya hecho (los de Kenney): conserva sus normales y su sentido de caras, y solo cambia
        // el color por una celda de la paleta. No sirve para formas armadas a mano: ahí las caras se corrigen solas.
        internal void AddModelTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normalA, Vector3 normalB, Vector3 normalC, int color)
        {
            Vector2 uv = Palette.UvOf(color);

            int start = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _normals.Add(normalA);
            _normals.Add(normalB);
            _normals.Add(normalC);
            _uvs.Add(uv);
            _uvs.Add(uv);
            _uvs.Add(uv);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
        }

        internal Mesh ToMesh(string meshName)
        {
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Un triángulo con vértices propios (normal plana). 'inside' es un punto dentro de la forma convexa: si la
        // normal apunta hacia él, el triángulo estaba al revés y se da vuelta. En Unity la cara frontal es la que
        // se ve en sentido horario, y su normal es Cross(b - a, c - a).
        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, int color)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 centroid = (a + b + c) / 3f;

            if (Vector3.Dot(normal, centroid - inside) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            normal.Normalize();
            Vector2 uv = Palette.UvOf(color);

            int start = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _normals.Add(normal);
            _normals.Add(normal);
            _normals.Add(normal);
            _uvs.Add(uv);
            _uvs.Add(uv);
            _uvs.Add(uv);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
        }
    }
}
