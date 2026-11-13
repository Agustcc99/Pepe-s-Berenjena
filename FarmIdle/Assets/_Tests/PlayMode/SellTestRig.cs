using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La zona de venta armada por código sobre un FieldTestWorld (y, si hace falta, un BackpackRig): el EconomyManager y
    // un pad con su trigger, el SellZone y el InteractionTrigger, igual que los arma el builder. Con 'withFeedback' suma
    // también el SellFeedback (moneda "molde" con AwakeCounter y un texto 3D de TextMeshPro).
    // El pad se arma desactivado y se enciende al final: el Awake y el OnEnable ya ven todas sus referencias.
    internal sealed class SellRig
    {
        public EconomyManager Economy { get; private set; }
        public GameObject Pad { get; private set; }
        public SellZone Zone { get; private set; }
        public SellFeedback Feedback { get; private set; }
        public Transform Target { get; private set; }
        public GameObject CoinTemplate { get; private set; }
        public GameConfig Config { get; private set; }

        public float Interval { get; private set; }
        public float FlightSeconds { get; private set; }

        public Vector3 PadPosition => Pad.transform.position;

        // Pone precios a los cultivos de un BackpackRig (el cultivo i paga prices[i]).
        public static void SetPrices(BackpackRig backpack, params int[] prices)
        {
            for (int i = 0; i < backpack.Crops.Length; i++)
            {
                TestReflection.Set(backpack.Crops[i], "sellPrice", prices[i]);
            }
        }

        // 'config' es el GameConfig que ya usa el BackpackRig (así hay uno solo); se le fijan los valores de venta.
        public static SellRig AddTo(FieldTestWorld world, GameConfig config, Vector3 padPosition, float intervalSeconds, float flightSeconds, bool withFeedback)
        {
            var rig = new SellRig { Config = config, Interval = intervalSeconds, FlightSeconds = flightSeconds };

            TestReflection.Set(config, "sellIntervalSeconds", intervalSeconds);
            TestReflection.Set(config, "sellFlightSeconds", flightSeconds);

            var economyObject = new GameObject("EconomyManager");
            economyObject.transform.SetParent(world.Root.transform, false);
            rig.Economy = economyObject.AddComponent<EconomyManager>();
            rig.Economy.Initialize(config);

            rig.Pad = new GameObject("SellPad");
            rig.Pad.SetActive(false);
            rig.Pad.transform.SetParent(world.Root.transform, false);
            rig.Pad.transform.position = padPosition;

            // Mismo trigger que el del builder: 2,6 de lado y la altura del jugador.
            var trigger = rig.Pad.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(2.6f, 2f, 2.6f);

            rig.Zone = rig.Pad.AddComponent<SellZone>();
            rig.Pad.AddComponent<InteractionTrigger>();

            rig.Target = new GameObject("Target").transform;
            rig.Target.SetParent(rig.Pad.transform, false);
            rig.Target.localPosition = new Vector3(0f, 0.64f, 0f);

            TestReflection.Set(rig.Zone, "config", config);
            TestReflection.Set(rig.Zone, "target", rig.Target);

            if (withFeedback)
            {
                rig.CoinTemplate = BackpackRig.CreateProductTemplate("Coin-template");

                var popupObject = new GameObject("Popup", typeof(TextMeshPro));
                popupObject.transform.SetParent(rig.Pad.transform, false);
                popupObject.transform.localPosition = new Vector3(0f, 2.3f, 0f);

                var visual = new GameObject("Visual").transform;
                visual.SetParent(rig.Pad.transform, false);

                rig.Feedback = rig.Pad.AddComponent<SellFeedback>();
                TestReflection.Set(rig.Feedback, "zone", rig.Zone);
                TestReflection.Set(rig.Feedback, "coinPrefab", rig.CoinTemplate);
                TestReflection.Set(rig.Feedback, "popupLabel", popupObject.GetComponent<TextMeshPro>());
                TestReflection.Set(rig.Feedback, "padVisual", visual);
            }

            rig.Pad.SetActive(true);
            return rig;
        }

        // El pad cuelga del mundo y se destruye con él; solo la moneda molde queda aparte.
        public void Destroy()
        {
            if (Pad != null) Object.DestroyImmediate(Pad);
            if (CoinTemplate != null) Object.DestroyImmediate(CoinTemplate);
        }
    }
}
