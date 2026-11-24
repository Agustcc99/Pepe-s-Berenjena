using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // F-21: las decoraciones tienen vida. Cada tipo de movimiento hace lo suyo y no se va de su lugar.
    public class DecorationMotionTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
        }

        private DecorationMotion Create(DecorationMotionKind kind, Vector3 axis, float amount, float speed, float bob)
        {
            _go = new GameObject("Motion");
            _go.transform.position = new Vector3(0f, 1f, 0f);
            _go.SetActive(false);
            var motion = _go.AddComponent<DecorationMotion>();
            motion.Configure(kind, axis, amount, speed, bob, 0f);
            _go.SetActive(true);
            return motion;
        }

        [UnityTest]
        public IEnumerator Spin_TurnsAndSway_RocksAroundTheRestPose()
        {
            Create(DecorationMotionKind.Spin, Vector3.forward, 90f, 0f, 0f);
            Quaternion first = _go.transform.localRotation;
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(Quaternion.Angle(first, _go.transform.localRotation), 1f, "El molino gira.");

            Object.Destroy(_go);
            yield return null;

            Create(DecorationMotionKind.Sway, Vector3.forward, 3f, 1f, 0f);
            float most = 0f;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                most = Mathf.Max(most, Quaternion.Angle(Quaternion.identity, _go.transform.localRotation));
            }

            Assert.LessOrEqual(most, 3.01f, "Se mece hasta su amplitud y no más.");
        }

        [UnityTest]
        public IEnumerator Orbit_StaysAtItsRadius_AndHop_NeverGoesBelowTheGround()
        {
            Create(DecorationMotionKind.Orbit, Vector3.up, 0.8f, 2f, 0.1f);
            Vector3 center = _go.transform.localPosition;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                Vector3 flat = _go.transform.localPosition - center;
                Assert.AreEqual(0.8f, new Vector2(flat.x, flat.z).magnitude, 1e-3f, "Da vueltas a su radio.");
                Assert.LessOrEqual(Mathf.Abs(flat.y), 0.1001f);
            }

            Object.Destroy(_go);
            yield return null;

            Create(DecorationMotionKind.Hop, Vector3.up, 0.2f, 2f, 0f);
            float baseY = _go.transform.localPosition.y;
            float highest = baseY;
            float until = Time.time + 0.7f;   // por tiempo, no por cuadros: en batch los cuadros pasan casi sin tiempo
            while (Time.time < until)
            {
                yield return null;
                float y = _go.transform.localPosition.y;
                Assert.GreaterOrEqual(y, baseY - 1e-4f, "Salta hacia arriba, no se hunde.");
                highest = Mathf.Max(highest, y);
            }

            Assert.Greater(highest, baseY + 0.01f, "Salta.");
        }

        [UnityTest]
        public IEnumerator Pulse_ShrinksOnlyTheChosenAxes()
        {
            Create(DecorationMotionKind.Pulse, Vector3.right, 0.8f, 3f, 0f);
            float minX = 1f, minY = 1f;
            float until = Time.time + 0.7f;   // más de dos ciclos completos
            while (Time.time < until)
            {
                yield return null;
                minX = Mathf.Min(minX, _go.transform.localScale.x);
                minY = Mathf.Min(minY, _go.transform.localScale.y);
            }

            Assert.Less(minX, 0.7f, "El ancho aletea.");
            Assert.AreEqual(1f, minY, 1e-4f, "El alto no cambia.");
        }
    }
}
