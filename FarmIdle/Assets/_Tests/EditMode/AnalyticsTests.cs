using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    // Analítica (N-01): el catálogo de eventos respeta lo que piden los SDK y el formato del log es el esperado.
    public class AnalyticsTests
    {
        private static string[] AllNames()
        {
            return typeof(AnalyticsEvents)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue())
                .ToArray();
        }

        [Test]
        public void EveryName_IsLowercaseAscii_WithUnderscores_UpTo40Chars()
        {
            foreach (string name in AllNames())
            {
                Assert.IsTrue(Regex.IsMatch(name, "^[a-z][a-z0-9_]{0,39}$"), "'" + name + "' no sirve como nombre de evento o parámetro.");
            }
        }

        [Test]
        public void NoNameIsRepeated()
        {
            string[] names = AllNames();
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void TheCatalog_HasTheMinimumEventsOfTheRoadmap()
        {
            string[] expected =
            {
                "tutorial_paso", "sesion_inicio", "sesion_fin", "nivel_subido", "pad_comprado", "rewarded_ofrecido",
                "rewarded_aceptado", "rewarded_completado", "offline_reclamado", "fertilizante_usado", "turbo_usado",
                "prestigio", "mision_completada", "abandono"
            };
            CollectionAssert.IsSubsetOf(expected, AllNames());
        }

        [Test]
        public void TheConsoleFormat_ShowsTheEventAndItsParameter()
        {
            Assert.AreEqual("[FarmIdle][Analytics] turbo_usado", ConsoleAnalytics.Format("turbo_usado", null, null));
            Assert.AreEqual("[FarmIdle][Analytics] pad_comprado id=zone-2", ConsoleAnalytics.Format("pad_comprado", "id", "zone-2"));
        }

        [Test]
        public void TheConsoleService_WritesOneLogLinePerEvent()
        {
            var service = new ConsoleAnalytics();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Log, "[FarmIdle][Analytics] sesion_inicio");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Log, "[FarmIdle][Analytics] nivel_subido nivel=3");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Log, "[FarmIdle][Analytics] rewarded_ofrecido lugar=turbo");

            service.Log("sesion_inicio");
            service.Log("nivel_subido", "nivel", 3L);
            service.Log("rewarded_ofrecido", "lugar", "turbo");
        }

        [Test]
        public void TheNullService_AcceptsEveryCall_AndDoesNothing()
        {
            IAnalytics service = new NullAnalytics();
            Assert.AreEqual("Null", service.Name);
            Assert.DoesNotThrow(() =>
            {
                service.Log("sesion_inicio");
                service.Log("pad_comprado", "id", "zone-a");
                service.Log("nivel_subido", "nivel", 2L);
                service.Log("pad_comprado", null, (string)null);
            });
        }
    }
}
