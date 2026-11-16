using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // La plaza de pads: dónde va cada sector y cada pad (las medidas que leen también los builders de perks, fertilizante, Turbo y la
    // guía), el recinto con su avenida, sus vallas con colisión, sus puertas y sus carteles, y los pads de mejora con su dibujo.
    // Viene de la Fase 6 (AR-05). Los datos de las mejoras son de ProgressionData; lo que comparten todos los pads, de PadBuilder.
    // Es una biblioteca: Phase2SceneBuilder.BuildAll llama a CreateAssets (las mallas se rehacen en cada corrida) y la escena, a Build.
    internal static class PlazaBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Environment";

        // La plaza de pads, en cinco sectores con su valla, su puerta y su cartel, unidos por una avenida central (x = 0) que arranca en
        // la entrada, al sur del pad de venta (z = -3,5):
        //   oeste: Videos (z -10,2 a -16,4) y Atributos (-16,4 a -27)
        //   este:  Fertilizante (-10,2 a -16,4), Puntos (-16,4 a -22) y Almacén (-22 a -27)
        // Cada sector deja un pasillo libre al norte (la puerta da a él) y los pads al fondo: entrar no obliga a pisar un pad (playtest Q-01).
        // Las vallas tienen colisión, así que se llega a cada sector solo por su puerta.
        internal const float PlazaNorthZ = -10.2f;   // la valla norte, con la entrada
        internal const float PlazaSouthZ = -27f;
        internal const float PlazaHalfWidth = 11.9f;   // F-17: una tercera columna (x ±10,3, pads de 2,6) a cada lado, con la valla pasando el borde del pad
        internal const float AvenueHalfWidth = 1.2f;   // la entrada tiene el mismo ancho que la avenida
        internal const float DoorWidth = 1.8f;
        internal const float DividerZ = -16.4f;        // el divisor entre las dos bandas (oeste y este)
        internal const float EastDividerZ = -22f;      // el divisor entre Puntos y Almacén

        // Dónde va cada pad. Dos columnas por sector (a 3,6 de paso, con pads de 2,6) y el Almacén, con un solo pad, en el medio.
        internal const float WestFarX = -6.7f;
        internal const float WestNearX = -3.1f;
        internal const float EastNearX = 3.1f;
        internal const float EastFarX = 6.7f;
        internal const float StoreX = EastNearX;       // Almacén (columna de la avenida) y, a su lado, el pad de decoraciones (F-17)
        internal const float DecorX = EastFarX;
        internal const float SkinX = -10.3f;           // tercera columna del sector Atributos (oeste): skins del jugador (F-17 tanda 3)
        internal const float HelperX = 10.3f;         // tercera columna del sector Almacén: ayudantes
        // Cada fila deja ~0,8 hasta el divisor de más al sur: la barra de pago de un pad cuelga 1,65 hacia la cámara.
        internal const float VideoRowZ = -13.9f;       // Videos y Fertilizante
        internal const float AttributeRow1Z = -20.6f;  // Atributos: Crecimiento y Precio
        internal const float AttributeRow2Z = -24.4f;  // Atributos: Velocidad y Mochila
        internal const float PointsRowZ = -19.8f;      // Puntos (perks)
        internal const float StoreRowZ = -25.0f;       // Almacén
        private const string PlazaMeshPath = MeshesFolder + "/SM_PlazaDecor.asset";

        private sealed class SectorSpec
        {
            public string Key;                 // clave de la tabla de textos del cartel
            public bool West;
            public float NorthZ, SouthZ, DoorZ;
            public float FarX;                 // la columna de pads más lejana de la avenida: hasta ahí llega el camino
            public int Accent;
        }

        private static readonly SectorSpec[] Sectors =
        {
            new SectorSpec { Key = "plaza.videos", West = true, NorthZ = PlazaNorthZ, SouthZ = DividerZ, DoorZ = -11.4f, FarX = WestFarX, Accent = Palette.Water },
            new SectorSpec { Key = "plaza.attributes", West = true, NorthZ = DividerZ, SouthZ = PlazaSouthZ, DoorZ = -17.8f, FarX = SkinX, Accent = Palette.WheatGold },
            new SectorSpec { Key = "plaza.fertilizer", West = false, NorthZ = PlazaNorthZ, SouthZ = DividerZ, DoorZ = -11.4f, FarX = EastFarX, Accent = Palette.LeafLight },
            new SectorSpec { Key = "plaza.points", West = false, NorthZ = DividerZ, SouthZ = EastDividerZ, DoorZ = -17.45f, FarX = EastFarX, Accent = Palette.TomatoRed },
            new SectorSpec { Key = "plaza.storage", West = false, NorthZ = EastDividerZ, SouthZ = PlazaSouthZ, DoorZ = -23.05f, FarX = HelperX, Accent = Palette.CarrotOrange },
        };

        private static string PadMeshPath(ProgressionData.UpgradeSpec upgrade) { return MeshesFolder + "/SM_UpgradePad_" + upgrade.Kind + ".asset"; }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);

            foreach (ProgressionData.UpgradeSpec upgrade in ProgressionData.Upgrades)
            {
                Phase3SceneBuilder.SaveMesh(BuildUpgradePad(upgrade).ToMesh("SM_UpgradePad_" + upgrade.Kind), PadMeshPath(upgrade));
            }

            Phase3SceneBuilder.SaveMesh(BuildPlazaDecor().ToMesh("SM_PlazaDecor"), PlazaMeshPath);
        }

        // ---------- Mallas ----------

        // Plataforma baja como la del pad de venta, con un aro del color de la mejora y un dibujo en el medio que dice qué es.
        private static PaletteMeshBuilder BuildUpgradePad(ProgressionData.UpgradeSpec upgrade)
        {
            var mesh = new PaletteMeshBuilder();
            const float frame = 0.12f;
            const float padSize = PadBuilder.PadSize;
            const float padTop = PadBuilder.PadTop;

            mesh.AddBox(new Vector3(0f, frame * 0.5f, 0f), new Vector3(padSize, frame, padSize), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, padTop * 0.5f, 0f), new Vector3(padSize - 0.3f, padTop, padSize - 0.3f), Quaternion.identity, Palette.Cream);
            mesh.AddCylinder(new Vector3(0f, padTop + 0.01f, 0f), 0.98f, 0.02f, 16, Quaternion.identity, upgrade.Accent);
            mesh.AddCylinder(new Vector3(0f, padTop + 0.02f, 0f), 0.84f, 0.02f, 16, Quaternion.identity, Palette.Cream);

            float y = padTop + 0.03f;
            switch (upgrade.Kind)
            {
                case UpgradeKind.GrowthSpeed:
                    // Un brote: tallo y dos hojas.
                    mesh.AddStanding(new Vector3(0f, y, 0f), new Vector3(0.09f, 0.5f, 0.09f), Quaternion.identity, Palette.LeafDark);
                    mesh.AddBox(new Vector3(0.26f, y + 0.55f, 0f), new Vector3(0.5f, 0.05f, 0.24f), Quaternion.Euler(0f, 0f, 28f), Palette.LeafLight);
                    mesh.AddBox(new Vector3(-0.26f, y + 0.47f, 0f), new Vector3(0.5f, 0.05f, 0.24f), Quaternion.Euler(0f, 0f, -28f), Palette.LeafLight);
                    break;

                case UpgradeKind.BackpackCapacity:
                    // Un saco atado.
                    mesh.AddBox(new Vector3(0f, y + 0.28f, 0f), new Vector3(0.8f, 0.56f, 0.58f), Quaternion.identity, Palette.WheatStraw);
                    mesh.AddBox(new Vector3(0f, y + 0.66f, 0f), new Vector3(0.46f, 0.2f, 0.4f), Quaternion.identity, Palette.WheatStraw);
                    mesh.AddBox(new Vector3(0f, y + 0.57f, 0f), new Vector3(0.52f, 0.05f, 0.46f), Quaternion.identity, Palette.Wood);
                    break;

                case UpgradeKind.MoveSpeed:
                    // Dos flechas hacia adelante.
                    for (int i = 0; i < 2; i++)
                    {
                        float z = -0.3f + i * 0.5f;
                        mesh.AddBox(new Vector3(0.22f, y + 0.05f, z), new Vector3(0.62f, 0.1f, 0.16f), Quaternion.Euler(0f, 45f, 0f), Palette.Water);
                        mesh.AddBox(new Vector3(-0.22f, y + 0.05f, z), new Vector3(0.62f, 0.1f, 0.16f), Quaternion.Euler(0f, -45f, 0f), Palette.Water);
                    }
                    break;

                case UpgradeKind.SellPrice:
                    // Una pila de monedas.
                    mesh.AddCylinder(new Vector3(0f, y + 0.08f, 0f), 0.46f, 0.16f, 12, Quaternion.identity, Palette.WheatStraw);
                    mesh.AddCylinder(new Vector3(0f, y + 0.24f, 0f), 0.46f, 0.16f, 12, Quaternion.identity, Palette.WheatGold);
                    mesh.AddCylinder(new Vector3(0f, y + 0.4f, 0f), 0.46f, 0.16f, 12, Quaternion.identity, Palette.WheatStraw);
                    mesh.AddCylinder(new Vector3(0f, y + 0.485f, 0f), 0.34f, 0.04f, 12, Quaternion.identity, Palette.WheatGold);
                    break;

                case UpgradeKind.OfflineCap:
                    // Un granero: cuerpo rojo, techo a dos aguas de madera y una puerta clara.
                    mesh.AddBox(new Vector3(0f, y + 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.7f), Quaternion.identity, Palette.TomatoRed);
                    mesh.AddBox(new Vector3(0.27f, y + 0.74f, 0f), new Vector3(0.62f, 0.06f, 0.82f), Quaternion.Euler(0f, 0f, -32f), Palette.Wood);
                    mesh.AddBox(new Vector3(-0.27f, y + 0.74f, 0f), new Vector3(0.62f, 0.06f, 0.82f), Quaternion.Euler(0f, 0f, 32f), Palette.Wood);
                    mesh.AddBox(new Vector3(0f, y + 0.22f, 0.36f), new Vector3(0.34f, 0.44f, 0.04f), Quaternion.identity, Palette.Cream);
                    break;
            }

            return mesh;
        }

        // El recinto de la plaza, en coordenadas del mundo: piso de piedra, un caminito de tierra desde el pad de venta hasta la entrada,
        // la valla (con el hueco de la entrada al norte, flanqueado por dos postes altos con un travesaño y un cartel) y nada más:
        // los pads son objetos aparte.
        private static PaletteMeshBuilder BuildPlazaDecor()
        {
            var mesh = new PaletteMeshBuilder();
            float centerZ = (PlazaNorthZ + PlazaSouthZ) * 0.5f;
            float depth = PlazaNorthZ - PlazaSouthZ;

            mesh.AddBox(new Vector3(0f, 0.025f, centerZ), new Vector3(PlazaHalfWidth * 2f, 0.05f, depth), Quaternion.identity, Palette.Stone);

            // La avenida de tierra: del borde sur del pad de venta (z = -5) hasta el fondo de la plaza.
            const float pathNorthZ = -5.2f;
            // (Por encima del piso de piedra, que llega a 0,05: si no, la tierra queda tapada.)
            mesh.AddBox(new Vector3(0f, 0.055f, (pathNorthZ + PlazaSouthZ) * 0.5f), new Vector3(AvenueHalfWidth * 2f, 0.02f, pathNorthZ - PlazaSouthZ), Quaternion.identity, Palette.SoilLight);

            // Una ruta de tierra desde la avenida hasta el fondo de cada sector, por su puerta.
            foreach (SectorSpec sector in Sectors)
            {
                float side = sector.West ? -1f : 1f;
                float from = side * AvenueHalfWidth;
                mesh.AddBox(new Vector3((from + sector.FarX) * 0.5f, 0.055f, sector.DoorZ), new Vector3(Mathf.Abs(sector.FarX - from), 0.02f, 1.6f), Quaternion.identity, Palette.SoilLight);
            }

            // Valla: postes cada ~2 y dos travesaños.
            foreach ((Vector3 from, Vector3 to) wall in PlazaWalls()) AddFenceRun(mesh, wall.from, wall.to);

            // El cartel de cada sector: dos postes bajos y un tablón oscuro con una franja del color del sector (el texto lo pone
            // PlazaSignLabel). Va en el pasillo del norte del sector, detrás de sus pads, y bajo, para no tapar los del sector de al lado.
            foreach (SectorSpec sector in Sectors)
            {
                float centerX = SignCenterX(sector);
                float z = SignZ(sector);
                foreach (float x in new[] { centerX - 1.7f, centerX + 1.7f })
                {
                    mesh.AddStanding(new Vector3(x, 0f, z), new Vector3(0.16f, 1.3f, 0.16f), Quaternion.identity, Palette.Wood);
                }

                mesh.AddBox(new Vector3(centerX, 0.95f, z), new Vector3(3.6f, 0.7f, 0.1f), Quaternion.identity, Palette.SoilDark);
                mesh.AddBox(new Vector3(centerX, 1.34f, z), new Vector3(3.6f, 0.14f, 0.12f), Quaternion.identity, sector.Accent);
            }

            // La entrada: dos postes altos, un travesaño arriba y un cartel de madera con una moneda dorada.
            foreach (float x in new[] { -AvenueHalfWidth, AvenueHalfWidth })
            {
                mesh.AddStanding(new Vector3(x, 0f, PlazaNorthZ), new Vector3(0.22f, 2.2f, 0.22f), Quaternion.identity, Palette.Wood);
            }

            mesh.AddBox(new Vector3(0f, 2.1f, PlazaNorthZ), new Vector3(AvenueHalfWidth * 2f + 0.3f, 0.2f, 0.2f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, 1.6f, PlazaNorthZ), new Vector3(1.5f, 0.8f, 0.08f), Quaternion.identity, Palette.Cream);
            Quaternion faceCamera = Quaternion.Euler(90f, 0f, 0f);
            mesh.AddCylinder(new Vector3(0f, 1.6f, PlazaNorthZ - 0.05f), 0.28f, 0.04f, 14, faceCamera, Palette.WheatStraw);
            mesh.AddCylinder(new Vector3(0f, 1.6f, PlazaNorthZ - 0.07f), 0.21f, 0.04f, 14, faceCamera, Palette.WheatGold);
            return mesh;
        }

        // Los tramos de valla (de un punto a otro, en el mundo): sirven para dibujarla y para ponerle colisión. El perímetro, los
        // divisores entre sectores y las dos vallas de la avenida, que dejan una puerta por sector.
        private static List<(Vector3 from, Vector3 to)> PlazaWalls()
        {
            var walls = new List<(Vector3 from, Vector3 to)>();
            float w = PlazaHalfWidth;
            float a = AvenueHalfWidth;

            walls.Add((new Vector3(-w, 0f, PlazaNorthZ), new Vector3(-a, 0f, PlazaNorthZ)));   // norte, con la entrada en la avenida
            walls.Add((new Vector3(a, 0f, PlazaNorthZ), new Vector3(w, 0f, PlazaNorthZ)));
            walls.Add((new Vector3(-w, 0f, PlazaNorthZ), new Vector3(-w, 0f, PlazaSouthZ)));
            walls.Add((new Vector3(w, 0f, PlazaNorthZ), new Vector3(w, 0f, PlazaSouthZ)));
            walls.Add((new Vector3(-w, 0f, PlazaSouthZ), new Vector3(w, 0f, PlazaSouthZ)));

            walls.Add((new Vector3(-w, 0f, DividerZ), new Vector3(-a, 0f, DividerZ)));
            walls.Add((new Vector3(a, 0f, DividerZ), new Vector3(w, 0f, DividerZ)));
            walls.Add((new Vector3(a, 0f, EastDividerZ), new Vector3(w, 0f, EastDividerZ)));

            AddAvenueWall(walls, -a, true);
            AddAvenueWall(walls, a, false);
            return walls;
        }

        // Una valla de la avenida (x = +-AvenueHalfWidth) de norte a sur, con un hueco por cada puerta de ese lado.
        private static void AddAvenueWall(List<(Vector3 from, Vector3 to)> walls, float x, bool west)
        {
            var doors = new List<float>();
            foreach (SectorSpec sector in Sectors)
            {
                if (sector.West == west) doors.Add(sector.DoorZ);
            }

            doors.Sort();
            doors.Reverse();   // de norte (z mayor) a sur

            float cursor = PlazaNorthZ;
            foreach (float door in doors)
            {
                float top = door + DoorWidth * 0.5f;
                if (cursor - top > 0.01f) walls.Add((new Vector3(x, 0f, cursor), new Vector3(x, 0f, top)));
                cursor = door - DoorWidth * 0.5f;
            }

            walls.Add((new Vector3(x, 0f, cursor), new Vector3(x, 0f, PlazaSouthZ)));
        }

        private static void AddFenceRun(PaletteMeshBuilder mesh, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            int segments = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
            Quaternion along = Quaternion.LookRotation(delta.normalized, Vector3.up);

            for (int i = 0; i <= segments; i++)
            {
                mesh.AddStanding(Vector3.Lerp(from, to, i / (float)segments), new Vector3(0.14f, 0.95f, 0.14f), Quaternion.identity, Palette.Wood);
            }

            Vector3 middle = (from + to) * 0.5f;
            mesh.AddBox(middle + Vector3.up * 0.4f, new Vector3(0.07f, 0.07f, length), along, Palette.Wood);
            mesh.AddBox(middle + Vector3.up * 0.75f, new Vector3(0.07f, 0.07f, length), along, Palette.Wood);
        }

        private static float SignCenterX(SectorSpec sector)
        {
            return (sector.West ? -1f : 1f) * (AvenueHalfWidth + PlazaHalfWidth) * 0.5f;
        }

        private static float SignZ(SectorSpec sector)
        {
            return sector.NorthZ - 0.35f;
        }

        // ---------- Escena ----------

        // Agrega a la escena abierta la plaza de mejoras: sus pads, el recinto, la colisión de las vallas y los carteles.
        // Los assets se recargan por ruta justo antes de usarlos.
        internal static void Build(Transform parent)
        {
            var plaza = new GameObject("UpgradePlaza");
            plaza.transform.SetParent(parent, false);
            foreach (ProgressionData.UpgradeSpec upgrade in ProgressionData.Upgrades) BuildUpgradePadObject(plaza.transform, upgrade);

            var decor = PadBuilder.CreateMeshChild(plaza.transform, "PlazaDecor", Phase2SceneBuilder.LoadRequired<Mesh>(PlazaMeshPath),
                Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath), Vector3.zero);
            decor.position = Vector3.zero;   // la malla ya viene en coordenadas del mundo

            BuildPlazaWallColliders(plaza.transform);
            BuildPlazaSignLabels(plaza.transform);
        }

        private static void BuildUpgradePadObject(Transform parent, ProgressionData.UpgradeSpec spec)
        {
            var upgradeData = Phase2SceneBuilder.LoadRequired<UpgradeData>(ProgressionData.UpgradePath(spec));
            var mesh = Phase2SceneBuilder.LoadRequired<Mesh>(PadMeshPath(spec));

            var pad = PadBuilder.BuildPadObject<UpgradePad>(parent, spec.File.Replace("Upgrade_", "UpgradePad_"), new Vector3(spec.X, 0f, spec.Z), mesh, PadBuilder.PadSize, out Transform visual);
            EditorUi.Wire(pad, ("upgrade", upgradeData));
            PadBuilder.WirePadCommon(pad, visual);
        }

        // La colisión de la valla: una caja fina por tramo, de la altura de la valla. Sin esto se cruzaría la valla de un paso.
        private static void BuildPlazaWallColliders(Transform parent)
        {
            var root = new GameObject("PlazaWalls");
            root.transform.SetParent(parent, false);

            foreach ((Vector3 from, Vector3 to) wall in PlazaWalls())
            {
                Vector3 delta = wall.to - wall.from;
                var segment = new GameObject("Wall");
                segment.transform.SetParent(root.transform, false);
                segment.transform.position = (wall.from + wall.to) * 0.5f + Vector3.up * 0.6f;

                var box = segment.AddComponent<BoxCollider>();
                box.size = Mathf.Abs(delta.x) > Mathf.Abs(delta.z) ? new Vector3(Mathf.Abs(delta.x) + 0.2f, 1.2f, 0.2f) : new Vector3(0.2f, 1.2f, Mathf.Abs(delta.z) + 0.2f);
            }
        }

        // Los textos de los carteles de los sectores (claves plaza.* de la tabla). Cuelgan del tablón que dibuja BuildPlazaDecor.
        private static void BuildPlazaSignLabels(Transform parent)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new InvalidOperationException("No hay fuente por defecto de TextMeshPro. Ejecutá FarmIdle > Fase 2 > Importar recursos de TextMeshPro.");
            }

            foreach (SectorSpec sector in Sectors)
            {
                var go = new GameObject("SignLabel_" + sector.Key.Substring("plaza.".Length), typeof(TextMeshPro));
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(SignCenterX(sector), 0.95f, SignZ(sector) - 0.1f);

                var label = go.GetComponent<TextMeshPro>();
                label.font = font;
                label.text = Loc.Get(sector.Key, GameLanguage.Spanish);
                label.fontSize = 5.5f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                PadBuilder.ApplyWorldLabelStyle(label);   // U-12: borde negro y amarillo claro
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.rectTransform.sizeDelta = new Vector2(3.4f, 0.7f);

                var sign = go.AddComponent<PlazaSignLabel>();
                var serialized = new SerializedObject(sign);
                serialized.FindProperty("key").stringValue = sector.Key;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
