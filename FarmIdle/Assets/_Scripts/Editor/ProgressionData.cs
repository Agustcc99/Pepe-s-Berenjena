using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Los datos de la progresión: las tablas de diseño de las zonas y las mejoras (con sus costos), la creación de sus ScriptableObjects
    // y el enganche al GameConfig. Viene de la Fase 6 (AR-05). Las mallas y la escena de estas piezas son de ZoneBuilder y PlazaBuilder.
    // Es una biblioteca: Phase2SceneBuilder.BuildAll llama a CreateAssets. Los datos que ya existen NO se pisan (si ajustás un costo a
    // mano en un asset, se respeta) y el GameConfig solo recibe las listas si todavía las tiene vacías.
    internal static class ProgressionData
    {
        private const string ZonesFolder = "Assets/ScriptableObjects/Zones";
        private const string UpgradesFolder = "Assets/ScriptableObjects/Upgrades";

        // ---------- Tablas de diseño ----------

        // Las zonas: dónde está su campo, dónde su portón y qué cultivo tiene cada fila (de la más cercana al jugador a la más lejana).
        // Los costos salen de la calibración de B-02 (Docs/ECONOMIA_V1.md): el bot de balance reparte las 23 compras en ~4 h 20 min de
        // jugador real (una compra cada 2-4 min al principio, cada 15-25 min al final). Una zona nueva siempre trae más parcelas; la
        // dorada trae además un cultivo mejor. Para recalcular la tabla: EconomyCalibration (test explícito del bot).
        internal sealed class ZoneSpec
        {
            public string File, Id, Name;
            public long Cost;
            public Vector3 Center, Gate;
            public string[] Rows;
            public int Season;   // F-24: desde qué temporada se puede comprar (0 = desde el principio)
        }

        internal static readonly ZoneSpec[] Zones =
        {
            new ZoneSpec
            {
                File = "Zone_East", Id = "zone_east", Name = "Huerta del Este", Cost = 410,
                Center = new Vector3(11f, 0f, 7f), Gate = new Vector3(5.5f, 0f, 7f),
                Rows = new[] { "Carrot", "Tomato", "Tomato" }
            },
            new ZoneSpec
            {
                File = "Zone_West", Id = "zone_west", Name = "Huerto Dorado", Cost = 3400,
                Center = new Vector3(-11f, 0f, 7f), Gate = new Vector3(-5.5f, 0f, 7f),
                Rows = new[] { "Pumpkin", "Pumpkin", "Pumpkin" }
            },

            // F-02: la segunda fila de campos, al norte. Cada portón queda en un campo ya abierto (el del medio en el inicial, los
            // de los costados en el del medio), así se llega siempre pasando por una zona abierta.
            new ZoneSpec
            {
                File = "Zone_North", Id = "zone_north", Name = "Huerta del Norte", Cost = 16000,
                Center = new Vector3(0f, 0f, 19f), Gate = new Vector3(0f, 0f, 13f),
                Rows = new[] { "Corn", "Corn", "Eggplant" }
            },
            new ZoneSpec
            {
                File = "Zone_NorthWest", Id = "zone_northwest", Name = "Chacra del Noroeste", Cost = 36000,
                Center = new Vector3(-11f, 0f, 19f), Gate = new Vector3(-5.5f, 0f, 19f),
                Rows = new[] { "Eggplant", "Watermelon", "Watermelon" }
            },
            new ZoneSpec
            {
                File = "Zone_NorthEast", Id = "zone_northeast", Name = "Quinta del Nordeste", Cost = 83000,
                Center = new Vector3(11f, 0f, 19f), Gate = new Vector3(5.5f, 0f, 19f),
                Rows = new[] { "Watermelon", "Sunflower", "Sunflower" }
            },

            // F-24: la tercera fila, al norte, una granja nueva por temporada (la primera aparece al empezar la temporada 1). Cada
            // temporada hay una más para comprar antes de poder cerrarla. Todas de girasol (el mejor cultivo); los costos son
            // PROVISORIOS (B-05): la venta ya rinde x1,25 por temporada y la tienda de semillas suma monedas de arranque. El portón
            // del primero queda en el campo del medio de la fila anterior (hay que abrirlo antes), y los de los costados, en el primero.
            new ZoneSpec
            {
                File = "Zone_Dawn", Id = "zone_dawn", Name = "Campo del Alba", Cost = 150000, Season = 1,
                Center = new Vector3(0f, 0f, 31f), Gate = new Vector3(0f, 0f, 25f),
                Rows = new[] { "Sunflower", "Sunflower", "Sunflower" }
            },
            new ZoneSpec
            {
                File = "Zone_Oak", Id = "zone_oak", Name = "Quinta del Roble", Cost = 260000, Season = 2,
                Center = new Vector3(-11f, 0f, 31f), Gate = new Vector3(-5.5f, 0f, 31f),
                Rows = new[] { "Sunflower", "Sunflower", "Sunflower" }
            },
            new ZoneSpec
            {
                File = "Zone_Estate", Id = "zone_estate", Name = "Estancia Dorada", Cost = 400000, Season = 3,
                Center = new Vector3(11f, 0f, 31f), Gate = new Vector3(5.5f, 0f, 31f),
                Rows = new[] { "Sunflower", "Sunflower", "Sunflower" }
            },
        };

        // Las mejoras, de izquierda a derecha en la plaza. 'Value' es el efecto TOTAL del nivel (lugares extra o multiplicador).
        // Los multiplicadores de precio son 1,33 / 1,67 / 2: con los precios base 3, 6, 12 y 24 del juego dan siempre monedas
        // enteras exactas (4 / 5 / 6 para el trigo...), así el +33 % se nota igual en todos los cultivos.
        internal sealed class UpgradeSpec
        {
            public string File, Id, Name, Short;
            public UpgradeKind Kind;
            public (long cost, float value)[] Levels;
            public int Accent;      // color del aro del pad (celda de la paleta)
            public float X, Z;      // posición en la plaza (sectores Atributos y Almacén)
        }

        internal static readonly UpgradeSpec[] Upgrades =
        {
            new UpgradeSpec
            {
                File = "Upgrade_Capacity", Id = "upgrade_capacity", Name = "Mochila", Short = "Moch", Kind = UpgradeKind.BackpackCapacity,
                Levels = new (long, float)[] { (950, 4f), (2000, 7f), (11000, 10f) }, Accent = Palette.CarrotOrange, X = PlazaBuilder.WestNearX, Z = PlazaBuilder.AttributeRow2Z
            },
            new UpgradeSpec
            {
                File = "Upgrade_Speed", Id = "upgrade_speed", Name = "Velocidad", Short = "Vel", Kind = UpgradeKind.MoveSpeed,
                Levels = new (long, float)[] { (600, 1.10f), (1800, 1.20f), (6600, 1.30f) }, Accent = Palette.Water, X = PlazaBuilder.WestFarX, Z = PlazaBuilder.AttributeRow2Z
            },
            new UpgradeSpec
            {
                File = "Upgrade_Growth", Id = "upgrade_growth", Name = "Crecimiento", Short = "Crec", Kind = UpgradeKind.GrowthSpeed,
                Levels = new (long, float)[] { (50, 1.25f), (900, 1.5f), (2500, 2f), (23000, 2.25f), (60000, 2.5f), (112000, 3f) }, Accent = Palette.LeafLight, X = PlazaBuilder.WestFarX, Z = PlazaBuilder.AttributeRow1Z
            },
            new UpgradeSpec
            {
                File = "Upgrade_Price", Id = "upgrade_price", Name = "Precio", Short = "Prec", Kind = UpgradeKind.SellPrice,
                Levels = new (long, float)[] { (290, 1.33f), (1300, 1.67f), (8600, 2f), (29000, 2.33f), (69000, 2.67f), (135000, 3f) }, Accent = Palette.WheatGold, X = PlazaBuilder.WestNearX, Z = PlazaBuilder.AttributeRow1Z
            },
            // B-01 (tanda 2): el "Almacén" amplía el tope del progreso offline de 2 h a 3 h y 4 h. 'Value' son los SEGUNDOS EXTRA. Va al
            // segundo renglón de la plaza, sin mover los otros cuatro pads. Costos PROVISORIOS (compra de fin de juego, tras todo lo demás).
            new UpgradeSpec
            {
                File = "Upgrade_Offline", Id = "upgrade_offline", Name = "Almacén", Short = "Alm", Kind = UpgradeKind.OfflineCap,
                Levels = new (long, float)[] { (140000, 3600f), (146000, 7200f) }, Accent = Palette.TomatoRed, X = PlazaBuilder.StoreX, Z = PlazaBuilder.StoreRowZ
            },
        };

        internal static string ZonePath(ZoneSpec zone) { return ZonesFolder + "/" + zone.File + ".asset"; }
        internal static string UpgradePath(UpgradeSpec upgrade) { return UpgradesFolder + "/" + upgrade.File + ".asset"; }

        // ---------- Balance ----------

        // Lleva las zonas y las mejoras que ya existen a las tablas de arriba (costos y efectos). Es idempotente. No se llama desde
        // BuildAll a propósito: BuildAll respeta lo que se haya ajustado a mano; esto es el "aplicar la tabla" explícito.
        internal static void ApplyBalance()
        {
            foreach (ZoneSpec spec in Zones)
            {
                var zone = AssetDatabase.LoadAssetAtPath<ZoneData>(ZonePath(spec));
                if (zone == null) continue;

                var serialized = new SerializedObject(zone);
                serialized.FindProperty("unlockCost").longValue = spec.Cost;
                serialized.FindProperty("requiredSeason").intValue = spec.Season;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(zone);
            }

            foreach (UpgradeSpec spec in Upgrades)
            {
                var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(UpgradePath(spec));
                if (upgrade == null) continue;

                var serialized = new SerializedObject(upgrade);
                SerializedProperty levels = serialized.FindProperty("levels");
                levels.arraySize = spec.Levels.Length;
                for (int i = 0; i < spec.Levels.Length; i++)
                {
                    SerializedProperty level = levels.GetArrayElementAtIndex(i);
                    level.FindPropertyRelative("cost").longValue = spec.Levels[i].cost;
                    level.FindPropertyRelative("value").floatValue = spec.Levels[i].value;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(upgrade);
            }
        }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(ZonesFolder);
            Phase1SceneBuilder.EnsureFolder(UpgradesFolder);

            foreach (ZoneSpec zone in Zones) CreateZoneData(zone);
            foreach (UpgradeSpec upgrade in Upgrades) CreateUpgradeData(upgrade);

            LinkConfig();
            AssetDatabase.SaveAssets();
        }

        private static void CreateZoneData(ZoneSpec spec)
        {
            string path = ZonePath(spec);
            if (AssetDatabase.LoadAssetAtPath<ZoneData>(path) != null) return;   // respeta lo que se haya ajustado a mano

            var zone = ScriptableObject.CreateInstance<ZoneData>();
            var serialized = new SerializedObject(zone);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.Name;
            serialized.FindProperty("unlockCost").longValue = spec.Cost;
            serialized.FindProperty("requiredSeason").intValue = spec.Season;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(zone, path);
        }

        private static void CreateUpgradeData(UpgradeSpec spec)
        {
            string path = UpgradePath(spec);
            if (AssetDatabase.LoadAssetAtPath<UpgradeData>(path) != null) return;   // respeta lo que se haya ajustado a mano

            var upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            var serialized = new SerializedObject(upgrade);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.Name;
            serialized.FindProperty("shortLabel").stringValue = spec.Short;
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;

            SerializedProperty levels = serialized.FindProperty("levels");
            levels.arraySize = spec.Levels.Length;
            for (int i = 0; i < spec.Levels.Length; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("cost").longValue = spec.Levels[i].cost;
                level.FindPropertyRelative("value").floatValue = spec.Levels[i].value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(upgrade, path);
        }

        // El GameConfig recibe las mejoras solo si todavía no tiene lista (así una lista armada a mano no se pisa) y las zonas que
        // le falten se suman al final; se vuelve a guardar para que los valores de compra queden escritos en el asset.
        private static void LinkConfig()
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);

            var zones = new List<Object>();
            foreach (ZoneSpec zone in Zones) zones.Add(Phase2SceneBuilder.LoadRequired<ZoneData>(ZonePath(zone)));
            Phase7SceneBuilder.AppendMissing(serialized, "zones", zones);   // zonas nuevas (F-02) entran a un config que ya tenía la lista

            var upgrades = new List<Object>();
            foreach (UpgradeSpec upgrade in Upgrades) upgrades.Add(Phase2SceneBuilder.LoadRequired<UpgradeData>(UpgradePath(upgrade)));
            FillIfEmpty(serialized, "upgrades", upgrades);
            Phase7SceneBuilder.AppendMissing(serialized, "upgrades", upgrades);   // mejoras nuevas (B-01) entran a un config que ya tenía la lista

            foreach (string field in new[] { "purchaseSeconds", "purchaseStartDelaySeconds" })
            {
                if (serialized.FindProperty(field) == null) throw new InvalidOperationException("GameConfig no tiene el campo '" + field + "'.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void FillIfEmpty(SerializedObject serialized, string field, IReadOnlyList<Object> values)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray) throw new InvalidOperationException("GameConfig no tiene la lista '" + field + "'.");
            if (property.arraySize > 0) return;

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
