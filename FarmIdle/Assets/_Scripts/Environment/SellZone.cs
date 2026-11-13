using System;
using UnityEngine;

namespace FarmIdle
{
    // La zona de venta: un pad en el suelo donde el jugador (o un ayudante) descarga la mochila. Es un IInteractable,
    // así que no sabe quién la pisa: solo ve un IActor y descubre lo que sabe hacer con 'actor is IBackpackDispenser'.
    // El que detecta al actor es el InteractionTrigger del mismo objeto, que llama a Interact en cada frame mientras el
    // actor esté encima y pueda actuar (el jugador, solo en Playing: en pausa y con un anuncio no vende).
    //
    // Cada 'sellIntervalSeconds' vende una unidad, de arriba hacia abajo (LIFO):
    //   1) le pide el producto de arriba al actor (IBackpackDispenser),
    //   2) el EconomyManager paga el precio final y lo suma a la billetera, en el mismo instante,
    //   3) avisa con Sold, para que lo cosmético (la moneda y el "+N" de SellFeedback) aparezca al llegar el producto.
    // Con la mochila vacía no hace nada y no escribe en la consola (esto corre en cada frame).
    // El ritmo usa el tiempo del juego (Time.time, que no avanza con timeScale en 0) a través de un SalePacer.
    public sealed class SellZone : MonoBehaviour, IInteractable, IGuidePoint
    {
        // IGuidePoint: con la mochila llena, la flecha del borde de la pantalla (UI) apunta al pad de venta.
        public Vector3 Position => transform.position;
        public int GuidePriority => GuidePoints.SellPriority;

        public bool WantsGuide
        {
            get
            {
                BackpackManager backpack = BackpackManager.Instance;
                return backpack != null && backpack.Backpack != null && backpack.Backpack.IsFull;
            }
        }

        private void OnEnable()
        {
            GuidePoints.Register(this);
        }

        private void OnDisable()
        {
            GuidePoints.Unregister(this);
        }

        [SerializeField, Tooltip("Intervalo entre ventas y duración del vuelo del producto (GameConfig).")]
        private GameConfig config;

        [SerializeField, Tooltip("Punto del mundo hacia donde vuelan los productos vendidos. Sin asignar, el origen de la zona.")]
        private Transform target;

        private SalePacer _pacer;
        private bool _configErrorLogged;

        // Se dispara después de cada venta (la billetera ya tiene las monedas): qué cultivo, cuánto pagó y en cuántos
        // segundos llega el producto a la zona. Lo escucha SellFeedback; el resultado no depende de nadie.
        public event Action<CropData, long, float> Sold;

        // Para pruebas y depuración.
        public Transform Target => target != null ? target : transform;

        private void Awake()
        {
            if (config == null) return;   // se avisa en la primera interacción: una zona mal armada no vende

            _pacer = new SalePacer(config.SellIntervalSeconds);
        }

        // IInteractable
        public void Interact(IActor actor)
        {
            if (!IsConfigured()) return;
            if (!(actor is IBackpackDispenser dispenser)) return;

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null) return;

            float now = Time.time;
            if (!_pacer.IsReady(now)) return;

            // Si el actor no tiene nada que entregar, no se anota la venta: lo primero que entre sale sin esperar turno.
            float flightSeconds = config.SellFlightSeconds;
            if (!dispenser.TryDispense(Target.position, flightSeconds, out CropData crop)) return;

            _pacer.Commit(now);
            long coins = economy.SellProduct(crop);
            Sold?.Invoke(crop, coins, flightSeconds);
        }

        // Una zona mal armada (sin GameConfig) no vende y avisa una sola vez, en vez de un error por frame.
        private bool IsConfigured()
        {
            if (_pacer != null) return true;

            if (config != null)
            {
                _pacer = new SalePacer(config.SellIntervalSeconds);   // por si se le asignó la config después del Awake
                return true;
            }

            if (!_configErrorLogged)
            {
                _configErrorLogged = true;
                Debug.LogError("[FarmIdle] La zona de venta '" + name + "' no puede vender: falta el GameConfig.", this);
            }

            return false;
        }
    }
}
