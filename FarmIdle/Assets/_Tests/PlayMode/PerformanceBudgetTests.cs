using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // El presupuesto de rendimiento que se puede vigilar sin un celular: memoria que se reserva por cuadro mientras se juega (el
    // recolector de basura es lo que da tirones en un celular de gama media), nada de Instantiate durante el juego y un solo material
    // para todo lo que se repite en el mundo (pocos draw calls). Los FPS reales hay que medirlos en el aparato.
    //
    // La memoria se mide con el contador del Profiler "GC Allocated In Frame". En el Editor ese contador nunca baja de unos 300 B por
    // cuadro AUNQUE no haya ningún script del juego corriendo (es el propio editor y el corredor de pruebas), así que lo que se vigila es
    // lo que el juego SUMA por encima de ese piso: parado no suma nada y jugando suma poco (una cadena corta cuando cambia un cartel).
    public class PerformanceBudgetTests
    {
        // Lo máximo que puede sumar el juego por encima del piso del editor, en bytes por cuadro, en promedio.
        private const long IdleBudgetBytesPerFrame = 32;
        private const long PlayingBudgetBytesPerFrame = 160;

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

        private static T[] All<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        }

        // Todos los objetos que existen (activos o guardados en un pool, también los de DontDestroyOnLoad): sube solo si alguien hace
        // Instantiate durante el juego.
        private static int CountObjects()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
        }

        // Reserva de memoria por cuadro, en bytes, durante 'seconds' de tiempo real. 'step' corre cada cuadro (mover al jugador...).
        private static IEnumerator MeasureAllocations(float seconds, Action step, List<long> perFrame)
        {
            ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            Assert.IsTrue(recorder.Valid, "No existe el contador 'GC Allocated In Frame' del Profiler.");

            yield return null;
            float end = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < end)
            {
                step?.Invoke();
                yield return null;
                perFrame.Add(recorder.LastValue);
            }

            recorder.Dispose();
        }

        private static long Median(List<long> frames)
        {
            return frames.OrderBy(v => v).ElementAt(frames.Count / 2);
        }

        private static string Summary(List<long> frames)
        {
            var sorted = frames.OrderBy(v => v).ToList();
            return frames.Count + " cuadros, " + (frames.Sum() / Math.Max(1, frames.Count)) + " B/cuadro de media, mediana " + Median(frames) + " B, p95 "
                + sorted[(int)(sorted.Count * 0.95f)] + " B, máximo " + sorted[sorted.Count - 1] + " B";
        }

        [UnityTest]
        public IEnumerator Allocations_TheGameAddsNothingWhenIdle_AndLittleWhilePlaying_AndNeverInstantiates()
        {
            // 1) El piso del editor, sin el juego.
            var floor = new List<long>();
            yield return MeasureAllocations(2f, null, floor);

            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();

            // Con el build activo en Android el Bootstrapper pide 60 FPS, y en el editor un tope de FPS hace que el propio editor reserve ~100 KB
            // por cuadro (medido con una sonda sin el juego): eso taparía lo que sí es del juego. Se mide sin tope (TearDown lo restaura).
            Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(3f);   // calentamiento: primeras cargas y compilaciones

            // 2) Parado, sin que pase nada.
            var idle = new List<long>();
            yield return MeasureAllocations(4f, null, idle);

            // 3) Jugando: un recorrido sin parar por las nueve parcelas (maduran rápido: hay mucha cosecha), y al pad de venta con la mochila llena.
            GameObject player = GameObject.FindWithTag("Player");
            var controller = player.GetComponent<CharacterController>();
            PlotView[] plots = All<PlotField>().First(field => field.gameObject.activeInHierarchy && Mathf.Abs(field.transform.position.x) < 0.1f && Mathf.Abs(field.transform.position.z - 7f) < 0.1f).Plots.ToArray();
            SellZone sellZone = Object.FindAnyObjectByType<SellZone>();
            int objectsBefore = CountObjects();

            Vector3[] route =
            {
                plots[0].transform.position, plots[1].transform.position, plots[2].transform.position, plots[5].transform.position, plots[4].transform.position,
                plots[3].transform.position, plots[6].transform.position, plots[7].transform.position, plots[8].transform.position
            };
            int target = 0;
            bool selling = false;

            void Step()
            {
                Vector3 goal = selling ? sellZone.transform.position : route[target];
                Vector3 toGoal = goal - player.transform.position;
                toGoal.y = 0f;

                if (toGoal.magnitude < 0.2f)
                {
                    if (selling)
                    {
                        if (BackpackManager.Instance.Backpack.Count == 0) selling = false;
                    }
                    else
                    {
                        target = (target + 1) % route.Length;
                        for (int i = 0; i < plots.Length; i++) plots[i].Tick(2f);
                        IReadOnlyInventory bag = BackpackManager.Instance.Backpack;
                        if (bag.Count >= bag.Capacity - 1) selling = true;
                    }
                }

                controller.Move(Vector3.ClampMagnitude(toGoal, 5f * Time.deltaTime));
            }

            var playing = new List<long>();
            yield return MeasureAllocations(25f, Step, playing);

            long sold = EconomyManager.Instance.SoldUnits;
            long floorMedian = Median(floor);
            long idleMedian = Median(idle);
            long idleExtra = idle.Sum() / idle.Count - floorMedian;
            long playingExtra = playing.Sum() / playing.Count - idleMedian;

            Debug.Log("[FarmIdle] Memoria por cuadro, piso del editor sin el juego: " + Summary(floor));
            Debug.Log("[FarmIdle] Memoria por cuadro, juego parado: " + Summary(idle) + " (suma " + idleExtra + " B sobre el piso)");
            Debug.Log("[FarmIdle] Memoria por cuadro, jugando (vendió " + sold + " productos): " + Summary(playing) + " (suma " + playingExtra + " B sobre el juego parado)");

            Assert.Greater(sold, 5, "El recorrido tiene que haber cosechado y vendido de verdad.");
            Assert.AreEqual(objectsBefore, CountObjects(), "Jugar (sembrar, cosechar, vender) no crea objetos nuevos: todo sale del pool.");
            Assert.LessOrEqual(idleMedian - floorMedian, IdleBudgetBytesPerFrame, "Parado, el juego no tiene que reservar memoria por cuadro.\n" + Summary(idle));
            Assert.LessOrEqual(playingExtra, PlayingBudgetBytesPerFrame, "Jugando, el juego reserva demasiado por cuadro.\n" + Summary(playing));
        }

        // ---------- Un solo material para lo que se repite ----------

        private static void AddMaterials(GameObject root, HashSet<Material> into)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<TMP_Text>() != null) continue;   // los carteles de texto usan el material de su fuente
                if (renderer is TrailRenderer) continue;                   // la estela del Turbo es transparente y aparece solo con el buff

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null) into.Add(material);
                }
            }
        }

        [UnityTest]
        public IEnumerator EverythingThatRepeats_UsesTheSinglePaletteMaterial_AndTheWholeWorldUsesFew()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            // Lo que se repite o crece en número (plantas, productos, monedas, pads, zonas) sale del PoolManager o del mapa: una paleta.
            var repeated = new HashSet<Material>();
            foreach (PlotView plot in All<PlotView>())
            {
                AddMaterials(plot.Crop.PlantPrefab, repeated);
                AddMaterials(plot.Crop.ProductPrefab, repeated);
                AddMaterials(plot.gameObject, repeated);
            }

            foreach (PurchasePad pad in All<PurchasePad>()) AddMaterials(pad.gameObject, repeated);
            foreach (DwellPad pad in All<DwellPad>()) AddMaterials(pad.gameObject, repeated);
            foreach (PerkPad pad in All<PerkPad>()) AddMaterials(pad.gameObject, repeated);
            AddMaterials(Object.FindAnyObjectByType<SellFeedback>().CoinPrefab, repeated);

            string repeatedNames = string.Join(", ", repeated.Select(m => m.name));
            Assert.AreEqual(1, repeated.Count, "Plantas, productos, monedas y pads usan una sola paleta. Materiales: " + repeatedNames);
            Assert.AreEqual("M_Palette", repeated.First().name);

            // El mundo entero: la paleta más el suelo, el jugador y su nariz (arte provisorio de la Fase 1) y las marcas del piso.
            var world = new HashSet<Material>();
            foreach (MeshRenderer renderer in All<MeshRenderer>())
            {
                if (renderer.GetComponent<TMP_Text>() != null) continue;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.name != "M_PoolProbe") world.Add(material);   // el cubo de prueba del pool es de debug
                }
            }

            string worldNames = string.Join(", ", world.Select(m => m.name));
            Debug.Log("[FarmIdle] Materiales del mundo (" + world.Count + "): " + worldNames);
            Assert.LessOrEqual(world.Count, 6, "Pocos materiales en todo el mundo. Materiales: " + worldNames);
        }
    }
}
