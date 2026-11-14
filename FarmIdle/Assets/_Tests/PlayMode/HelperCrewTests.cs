using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // F-19: los ayudantes que se ven. Son adorno: aparecen y desaparecen con el nivel de HelperManager (al comprar, al cargar la
    // partida y al empezar una temporada nueva), usan la paleta única, no tienen colliders, no crean objetos y su circuito no pisa
    // nada del mapa.
    public class HelperCrewTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return GameBoot.CleanUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return GameBoot.CleanUp();
        }

        private static HelperCrew Crew()
        {
            HelperCrew[] crews = Object.FindObjectsByType<HelperCrew>(FindObjectsInactive.Include);
            Assert.AreEqual(1, crews.Length, "La escena tiene un solo HelperCrew.");
            return crews[0];
        }

        private static int Shown(HelperCrew crew)
        {
            int shown = 0;
            for (int i = 0; i < crew.Count; i++)
            {
                if (!crew.WalkerAt(i).activeSelf) continue;

                Assert.AreEqual(shown, i, "Se prenden en orden: el peón " + i + " está prendido y uno anterior no.");
                shown++;
            }

            return shown;
        }

        private static void BuyAll()
        {
            HelperManager helpers = HelperManager.Instance;
            while (!helpers.IsMaxed)
            {
                EconomyManager.Instance.AddCoins(helpers.NextCost);
                Assert.IsTrue(helpers.TryBuyNext());
            }
        }

        [UnityTest]
        public IEnumerator TheScene_HasOneHiddenWalkerPerHelperLevel_WithThePaletteMaterial_AndNoColliders()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            HelperCrew crew = Crew();
            Assert.AreEqual(HelperManager.Instance.Total, crew.Count, "Un peón por nivel de ayudante.");
            Assert.AreEqual(0, Shown(crew), "Sin ayudantes comprados no se ve ninguno.");

            for (int i = 0; i < crew.Count; i++)
            {
                GameObject walker = crew.WalkerAt(i);
                Assert.IsNotNull(walker.GetComponent<HelperWalker>(), walker.name + " sin HelperWalker.");
                Assert.AreEqual(0, walker.GetComponentsInChildren<Collider>(true).Length, walker.name + " es adorno: sin colliders.");

                Renderer[] renderers = walker.GetComponentsInChildren<Renderer>(true);
                Assert.Greater(renderers.Length, 0);
                foreach (Renderer renderer in renderers)
                {
                    Assert.AreEqual(1, renderer.sharedMaterials.Length, renderer.name);
                    Assert.AreEqual("M_Palette", renderer.sharedMaterial.name, walker.name + "/" + renderer.name + " usa la paleta única.");
                }
            }
        }

        [UnityTest]
        public IEnumerator Walkers_FollowTheHelperLevel_OnBuy_OnSeasonReset_AndOnLoad()
        {
            var storage = new MemorySaveStorage();
            yield return GameBoot.BootToReady(storage);
            yield return null;

            HelperCrew crew = Crew();
            HelperManager helpers = HelperManager.Instance;

            for (int level = 1; level <= helpers.Total; level++)
            {
                EconomyManager.Instance.AddCoins(helpers.NextCost);
                Assert.IsTrue(helpers.TryBuyNext());
                Assert.AreEqual(level, Shown(crew), "Cada compra suma un peón a la vista.");
            }

            helpers.ResetSeason();
            Assert.AreEqual(0, Shown(crew), "La temporada nueva se lleva a los ayudantes, y a los peones con ellos.");

            EconomyManager.Instance.AddCoins(helpers.NextCost);
            Assert.IsTrue(helpers.TryBuyNext());
            EconomyManager.Instance.AddCoins(helpers.NextCost);
            Assert.IsTrue(helpers.TryBuyNext());
            Assert.AreEqual(2, Shown(crew));
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));

            // Se cierra el juego y se vuelve a abrir con el MISMO almacenamiento.
            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.AreEqual(2, HelperManager.Instance.Level);
            Assert.AreEqual(2, Shown(Crew()), "Al cargar la partida vuelven los peones del nivel guardado.");
        }

        [UnityTest]
        public IEnumerator EveryWalker_GoesRoundItsCircuit_LoadingAndUnloading_WithoutSteppingOnAnything_OrCreatingObjects()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();
            yield return null;

            int objectsBefore = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
            BuyAll();
            yield return null;

            HelperCrew crew = Crew();
            Assert.AreEqual(crew.Count, Shown(crew));

            // Los puestos donde deja la carga, para comprobar que descarga pegado a uno de los dos.
            Vector3 truck = Object.FindAnyObjectByType<SellTruck>().transform.position;
            Vector3 stand = GameObject.Find("SellStand").transform.position;
            var hits = new Collider[8];
            const float step = 0.05f;

            for (int i = 0; i < crew.Count; i++)
            {
                var walker = crew.WalkerAt(i).GetComponent<HelperWalker>();
                Vector3 loadPoint = walker.RoutePoint(0);
                Vector3 unloadPoint = walker.RoutePoint(walker.UnloadIndex);
                Assert.Less(Mathf.Min(Vector3.Distance(unloadPoint, truck), Vector3.Distance(unloadPoint, stand)), 2.2f, walker.name + " descarga junto al camión o al puesto.");

                int loads = 0, unloads = 0;
                bool carrying = walker.Carrying;
                Assert.IsFalse(carrying, walker.name + " aparece con las manos vacías.");
                var problems = new List<string>();

                for (int tick = 0; tick < 900 && problems.Count == 0; tick++)   // 45 s de juego: más de dos vueltas
                {
                    walker.Tick(step);
                    Vector3 position = walker.transform.position;

                    if (Mathf.Abs(position.y) > 0.001f) problems.Add("se despega del piso en " + position);

                    // Nada del mapa en su camino: ni triggers (parcelas, pads, portones) ni colisiones (vallas, jugador).
                    int count = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.6f, 0.3f, hits, ~0, QueryTriggerInteraction.Collide);
                    if (count > 0) problems.Add("pisa '" + hits[0].name + "' en " + position);

                    if (walker.Carrying == carrying) continue;

                    carrying = walker.Carrying;
                    if (carrying)
                    {
                        loads++;
                        if (Vector3.Distance(position, loadPoint) > 0.01f) problems.Add("carga lejos del campo, en " + position);
                    }
                    else
                    {
                        unloads++;
                        if (Vector3.Distance(position, unloadPoint) > 0.01f) problems.Add("descarga lejos de su puesto, en " + position);
                    }
                }

                Assert.IsEmpty(problems, walker.name + ": " + string.Join("; ", problems));
                Assert.GreaterOrEqual(loads, 2, walker.name + " carga en cada vuelta.");
                Assert.GreaterOrEqual(unloads, 2, walker.name + " descarga en cada vuelta.");
            }

            Assert.AreEqual(objectsBefore, Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length, "Los peones ya estaban en la escena: comprar y caminar no crea objetos.");

            // Al irse (temporada nueva) quedan como los armó el builder: sin la pila a la vista.
            HelperManager.Instance.ResetSeason();
            Assert.AreEqual(0, Shown(crew));
            for (int i = 0; i < crew.Count; i++)
            {
                Assert.AreEqual(Vector3.zero, crew.WalkerAt(i).transform.Find("Body/Pile").localScale);
            }
        }
    }
}
