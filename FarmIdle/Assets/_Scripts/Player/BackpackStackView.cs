using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // La pila visible de la mochila: pone los productos en la espalda del granjero, uno sobre otro, en el mismo orden
    // que el Inventory (el último en entrar queda arriba, que es el primero en salir).
    // No decide nada: el inventario ya tiene el valor final desde que entra el producto y esta vista solo lo refleja.
    //   - Cada cambio del Inventory se reconcilia con la pila: lo que sobra arriba vuelve al pool y lo que falta sale
    //     del pool. Así aguanta cualquier mezcla de cultivos y que la mochila se vacíe de arriba hacia abajo.
    //   - Cuando el jugador avisa de dónde vino el producto (ProductReceived), ese producto "vuela" desde ahí hasta su
    //     lugar en la pila. Es una corrutina con tiempo sin escala, igual que el resto de las animaciones cosméticas:
    //     no depende de que el juego esté corriendo y no cambia ningún valor.
    //   - Al revés, cuando el jugador avisa que el producto de arriba sale hacia la zona de venta (ProductLeaving, justo
    //     antes de sacarlo del inventario), la pila lo SEPARA: deja de contarlo entre sus productos, suelta su lugar y lo
    //     vuela hasta el destino; recién al llegar vuelve al pool. Así la pila sigue igual al inventario en todo momento
    //     y el vuelo de salida no la retiene. Si el inventario pierde un producto sin ese aviso (otra vía), el de arriba
    //     vuelve al pool en el acto, como antes.
    // Los productos salen del PoolManager. Al activarse declara el Prewarm por prefab: la mochila entera en su MÁXIMA
    // capacidad (lo máximo que puede haber de un mismo producto en la pila, aunque hoy la mochila sea más chica: las mejoras
    // de capacidad la agrandan después) más 'departingHeadroom' (los que pueden estar volando hacia la venta mientras la pila
    // vuelve a llenarse), así que no hay Instantiate durante el juego. Ojo: Prewarm no es aditivo; si algún día hay más de una
    // pila (ayudantes), la cuenta tiene que ser la del nivel entero.
    // Cuando la mochila crece (CapacityChanged) la pila acompaña: conserva todo lo que lleva y, si la columna ya no entra en la
    // altura máxima, apretuja los productos (alto efectivo menor) para que no se pase de la cabeza del granjero.
    // Con una mochila de más de 'singleColumnCapacity' lugares (los perks de nivel la llevan hasta 30) la pila pasa a DOS columnas
    // lado a lado: los productos se reparten uno en cada columna, así la altura necesaria es la mitad y casi no hace falta apretujar.
    public sealed class BackpackStackView : MonoBehaviour
    {
        // Un producto de la pila. 'Instance' es null si el cultivo no tiene prefab de producto (error de
        // configuración que se avisa una vez): la mochila igual lo cuenta.
        private sealed class StackItem
        {
            public CropData Crop;
            public GameObject Instance;
            public Vector3 SlotPosition;      // posición local al ancla
            public Quaternion SlotRotation;   // rotación local al ancla
            public Coroutine Flight;
            public Coroutine Settle;   // rebote de aterrizaje
        }

        [SerializeField, Tooltip("El jugador: avisa de dónde viene cada producto que recibe.")]
        private PlayerActor actor;

        [SerializeField, Tooltip("Dónde empieza la pila: un punto en la espalda del granjero. La pila sube desde acá, por el eje Y del ancla.")]
        private Transform anchor;

        [SerializeField, Tooltip("Los cultivos que pueden terminar en la mochila: de acá sale el Prewarm de los productos.")]
        private CropData[] crops;

        [SerializeField, Min(0.05f), Tooltip("Altura que ocupa cada producto en la pila.")]
        private float itemHeight = 0.26f;

        [SerializeField, Min(0.5f), Tooltip("Altura máxima de la pila entera. Con una mochila más grande los productos se apretujan para no pasar de acá (hasta 13 productos de 0,26 entran sin apretujar).")]
        private float maxStackHeight = 3.4f;

        [SerializeField, Min(1), Tooltip("Hasta cuántos lugares de mochila la pila es una sola columna. Con más pasa a dos columnas lado a lado (13 de 0,26 llenan la altura máxima).")]
        private int singleColumnCapacity = 13;

        [SerializeField, Min(0.05f), Tooltip("Separación entre las dos columnas cuando la pila las usa (en el eje X del ancla, a lo ancho de la espalda).")]
        private float columnSpacing = 0.5f;

        [SerializeField, Min(0.05f), Tooltip("Cuánto tarda el producto en volar de la parcela a la pila.")]
        private float flightSeconds = 0.35f;

        [SerializeField, Min(0f), Tooltip("Cuánto sube el producto a mitad de camino en su vuelo.")]
        private float flightArc = 0.9f;

        [SerializeField, Min(0), Tooltip("Cuántos productos pueden estar volando hacia la zona de venta a la vez, además de los de la pila (ritmo de venta x duración del vuelo, con margen). Sale del Prewarm.")]
        private int departingHeadroom = 6;

        [SerializeField, Min(0f), Tooltip("Cuánto sube el producto a mitad de camino cuando sale hacia la venta.")]
        private float departureArc = 1.1f;

        [SerializeField, Min(0.05f), Tooltip("Duración del rebote al aterrizar en la pila.")]
        private float settleSeconds = 0.2f;

        [SerializeField, Range(0f, 0.6f), Tooltip("Cuánto se estira/achata el producto al aterrizar.")]
        private float settleSquash = 0.25f;

        [SerializeField, Min(0.05f), Tooltip("J-08: duración del rebote de la pila ENTERA cuando la mochila se llena.")]
        private float fullBounceSeconds = 0.5f;

        [SerializeField, Range(0f, 0.6f), Tooltip("Cuánto se estira/achata la pila entera al llenarse.")]
        private float fullBounceSquash = 0.35f;

        // Un producto que ya salió de la pila y vuela hacia la zona de venta. Es dueño de su GameObject hasta que llega.
        private sealed class Departure
        {
            public GameObject Instance;
            public Coroutine Flight;
        }

        private readonly List<StackItem> _items = new List<StackItem>();
        private readonly List<Departure> _departing = new List<Departure>();
        private IReadOnlyInventory _inventory;
        private int _maxCapacity;
        private float _slotHeight;   // alto efectivo de cada lugar con la capacidad actual
        private int _columns = 1;    // 1 o 2, según la capacidad actual
        private bool _missingPrefabLogged;
        private Coroutine _fullBounce;

        // Para pruebas y depuración.
        public bool IsFullBouncing => _fullBounce != null;
        public int ItemCount => _items.Count;
        public int DepartingCount => _departing.Count;
        public GameObject DepartingObject(int index) { return _departing[index].Instance; }
        public int DepartingHeadroom => departingHeadroom;
        public Transform Anchor => anchor;
        public float ItemHeight => _slotHeight > 0f ? _slotHeight : itemHeight;
        public int Columns => _columns;
        public float ColumnSpacing => columnSpacing;
        public GameObject ItemObject(int index) { return _items[index].Instance; }
        public bool IsFlying(int index) { return _items[index].Flight != null; }

        private void OnEnable()
        {
            BackpackManager backpacks = BackpackManager.Instance;
            if (backpacks == null)
            {
                Debug.LogError("[FarmIdle] BackpackStackView: no hay BackpackManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            if (anchor == null)
            {
                Debug.LogError("[FarmIdle] BackpackStackView: falta el ancla de la espalda.", this);
                return;
            }

            _inventory = backpacks.Backpack;
            _maxCapacity = Mathf.Max(backpacks.MaxCapacity, _inventory.Capacity);
            _columns = ColumnsFor(_inventory.Capacity);
            _slotHeight = SlotHeightFor(_inventory.Capacity);
            _inventory.Changed += OnInventoryChanged;
            _inventory.CapacityChanged += OnCapacityChanged;
            if (actor != null)
            {
                actor.ProductReceived += OnProductReceived;
                actor.ProductLeaving += OnProductLeaving;
            }

            PrewarmProducts();
            Sync();   // por si se reactiva con la mochila ya cargada
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory.CapacityChanged -= OnCapacityChanged;
                _inventory = null;
            }

            if (actor != null)
            {
                actor.ProductReceived -= OnProductReceived;
                actor.ProductLeaving -= OnProductLeaving;
            }

            // Al desactivarse Unity ya detuvo el rebote de la pila: el ancla vuelve a su tamaño.
            _fullBounce = null;
            if (anchor != null) anchor.localScale = Vector3.one;

            // Los productos viven en el pool, no en esta escena: tienen que volver antes de que la escena se descargue.
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                ReleaseItem(i);
            }

            // Lo mismo con los que iban volando a la venta: al desactivarse Unity ya detuvo sus corrutinas.
            for (int i = _departing.Count - 1; i >= 0; i--)
            {
                ReleaseDeparture(_departing[i]);
            }
        }

        private void OnInventoryChanged(InventoryChange change)
        {
            Sync();

            // J-08: la cosecha que llena la mochila hace rebotar la pila entera (una corrutina por llenado, no por cuadro).
            if (change.Added && !change.IsReplacement && _inventory.IsFull)
            {
                if (_fullBounce != null) StopCoroutine(_fullBounce);
                _fullBounce = StartCoroutine(FullBounce());
            }
        }

        // Squash & stretch del ancla (y con ella de toda la pila), con tiempo sin escala. Es cosmético.
        private IEnumerator FullBounce()
        {
            float elapsed = 0f;
            while (elapsed < fullBounceSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                anchor.localScale = SquashStretch.Evaluate(elapsed / fullBounceSeconds, fullBounceSquash);
                yield return null;
            }

            anchor.localScale = Vector3.one;
            _fullBounce = null;
        }

        // La mochila creció (mejora de capacidad). Lo que lleva no cambia, así que no se toca ningún producto: solo se
        // reacomoda la altura de los lugares si la columna más alta ya no entra en 'maxStackHeight'.
        private void OnCapacityChanged(int capacity)
        {
            if (capacity > _maxCapacity)
            {
                // Más grande de lo previsto en el Prewarm (no pasa con los valores normales): se declara de nuevo.
                _maxCapacity = capacity;
                PrewarmProducts();
            }

            int columns = ColumnsFor(capacity);
            float height = SlotHeightFor(capacity);
            if (columns == _columns && Mathf.Approximately(height, _slotHeight)) return;

            _columns = columns;
            _slotHeight = height;
            for (int i = 0; i < _items.Count; i++)
            {
                StackItem item = _items[i];
                item.SlotPosition = SlotPositionFor(i);

                // Los que están volando hacia su lugar lo recalculan solos en cada paso.
                if (item.Instance != null && item.Flight == null) PlaceInSlot(item);
            }
        }

        private int ColumnsFor(int capacity)
        {
            return capacity > singleColumnCapacity ? 2 : 1;
        }

        // El alto de cada lugar: lo que entra en la altura máxima con las filas que hacen falta (la mitad de la capacidad con dos columnas).
        private float SlotHeightFor(int capacity)
        {
            int columns = ColumnsFor(capacity);
            int rows = Mathf.Max(1, (capacity + columns - 1) / columns);
            return Mathf.Min(itemHeight, maxStackHeight / rows);
        }

        // Con una columna el producto i va en la fila i; con dos se reparten (par a la izquierda, impar a la derecha) y la fila es i / 2.
        private Vector3 SlotPositionFor(int index)
        {
            int row = index / _columns;
            float x = _columns == 2 ? ((index % 2) == 0 ? -columnSpacing * 0.5f : columnSpacing * 0.5f) : 0f;
            return new Vector3(x, (row + 0.5f) * _slotHeight, 0f);
        }

        // El inventario avisa primero y el jugador después: el producto que acaba de entrar es el de arriba de la pila.
        private void OnProductReceived(CropData crop, Vector3 from)
        {
            if (_items.Count == 0) return;

            StackItem top = _items[_items.Count - 1];
            if (top.Crop != crop || top.Instance == null || top.Flight != null) return;

            top.Flight = StartCoroutine(Fly(top, from));
        }

        // El producto de arriba sale hacia 'to' y tarda 'seconds'. Todavía está en el inventario (el aviso es previo a
        // sacarlo): se lo saca de la pila y se lo vuela. Cuando llegue el cambio del inventario, la pila ya coincide.
        private void OnProductLeaving(CropData crop, Vector3 to, float seconds)
        {
            if (_items.Count == 0) return;

            StackItem top = _items[_items.Count - 1];
            if (top.Crop != crop) return;   // no debería pasar: la pila es igual al inventario; que Sync lo resuelva

            _items.RemoveAt(_items.Count - 1);
            StopSettle(top);

            if (top.Flight != null)
            {
                StopCoroutine(top.Flight);   // si todavía venía volando desde la parcela, sigue desde donde está
                top.Flight = null;
            }

            // Sin prefab de producto (error de configuración ya avisado) no hay nada que volar.
            if (top.Instance == null) return;

            var departure = new Departure { Instance = top.Instance };
            _departing.Add(departure);
            departure.Flight = StartCoroutine(FlyOut(departure, to, seconds));
        }

        // Deja la pila igual al contenido de la mochila. La mochila solo cambia por arriba (LIFO), así que lo de abajo
        // que ya coincide se queda como está.
        private void Sync()
        {
            IReadOnlyList<CropData> content = _inventory.Items;

            int keep = 0;
            while (keep < _items.Count && keep < content.Count && _items[keep].Crop == content[keep])
            {
                keep++;
            }

            for (int i = _items.Count - 1; i >= keep; i--)
            {
                ReleaseItem(i);
            }

            for (int i = keep; i < content.Count; i++)
            {
                AddItem(content[i], i);
            }
        }

        private void AddItem(CropData crop, int index)
        {
            var item = new StackItem
            {
                Crop = crop,
                SlotPosition = SlotPositionFor(index),
                // Se alternan de a 90 grados, con una pizca de variación fija, para que no parezca una columna de reglas.
                SlotRotation = Quaternion.Euler(0f, (index % 2) * 90f + ((index * 37) % 11 - 5), 0f)
            };

            GameObject prefab = crop != null ? crop.ProductPrefab : null;
            PoolManager pools = PoolManager.Instance;
            if (prefab != null && pools != null)
            {
                item.Instance = pools.Get(prefab, anchor.position, anchor.rotation, anchor);
                PlaceInSlot(item);
            }
            else if (!_missingPrefabLogged)
            {
                _missingPrefabLogged = true;
                Debug.LogError("[FarmIdle] BackpackStackView: el cultivo '" + (crop != null ? crop.name : "?") + "' no tiene prefab de producto (o falta el PoolManager): la mochila lo cuenta pero no se ve en la pila.", this);
            }

            _items.Add(item);
        }

        private void ReleaseItem(int index)
        {
            StackItem item = _items[index];
            _items.RemoveAt(index);
            StopSettle(item);

            if (item.Flight != null)
            {
                StopCoroutine(item.Flight);
                item.Flight = null;
            }

            // Sin PoolManager (el juego se está cerrando) el producto se destruye junto con su escena.
            if (item.Instance != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(item.Instance);
            }
        }

        private static void PlaceInSlot(StackItem item)
        {
            Transform t = item.Instance.transform;
            t.localPosition = item.SlotPosition;
            t.localRotation = item.SlotRotation;
            t.localScale = Vector3.one;   // el vuelo lo achica al salir: el objeto reciclado vuelve a tamaño completo
        }

        // El producto sale de 'from' (el mundo), sube en arco y aterriza en su lugar de la pila. El destino se vuelve
        // a calcular en cada paso porque la espalda se mueve con el jugador.
        private IEnumerator Fly(StackItem item, Vector3 from)
        {
            Transform t = item.Instance.transform;
            float elapsed = 0f;

            while (true)
            {
                float k = Mathf.Clamp01(elapsed / flightSeconds);
                float eased = k * k * (3f - 2f * k);   // arranca y frena suave

                Vector3 position = Vector3.Lerp(from, anchor.TransformPoint(item.SlotPosition), eased);
                position.y += Mathf.Sin(k * Mathf.PI) * flightArc;
                t.position = position;
                t.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, eased);

                if (elapsed >= flightSeconds) break;

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            PlaceInSlot(item);
            item.Flight = null;
            item.Settle = StartCoroutine(Settle(item));
        }

        // Rebote al aterrizar en la pila (J-03): squash & stretch corto, tiempo sin escala. Es cosmético; si el producto
        // sale o vuelve al pool antes de terminar, quien lo saca corta la corrutina.
        private IEnumerator Settle(StackItem item)
        {
            Transform t = item.Instance.transform;
            float elapsed = 0f;

            while (elapsed < settleSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                t.localScale = SquashStretch.Evaluate(elapsed / settleSeconds, settleSquash);
                yield return null;
            }

            t.localScale = Vector3.one;
            item.Settle = null;
        }

        private void StopSettle(StackItem item)
        {
            if (item.Settle == null) return;

            StopCoroutine(item.Settle);
            item.Settle = null;
        }

        // El producto sale de donde está (la pila, o a mitad de camino si todavía venía de la parcela), sube en arco hasta
        // 'to' achicándose y vuelve al pool al llegar. Tiempo sin escala, como el resto de los vuelos: es cosmético.
        private IEnumerator FlyOut(Departure departure, Vector3 to, float seconds)
        {
            Transform t = departure.Instance.transform;
            Vector3 from = t.position;
            Vector3 startScale = t.localScale;
            float duration = Mathf.Max(seconds, 0.01f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float k = Mathf.Clamp01(elapsed / duration);
                float eased = k * k * (3f - 2f * k);

                Vector3 position = Vector3.Lerp(from, to, eased);
                position.y += Mathf.Sin(k * Mathf.PI) * departureArc;
                t.position = position;
                t.localScale = startScale * Mathf.Lerp(1f, 0.5f, eased);

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            departure.Flight = null;
            ReleaseDeparture(departure);
        }

        private void ReleaseDeparture(Departure departure)
        {
            _departing.Remove(departure);

            if (departure.Flight != null)
            {
                StopCoroutine(departure.Flight);
                departure.Flight = null;
            }

            // Sin PoolManager (el juego se está cerrando) el producto se destruye junto con su escena.
            if (departure.Instance != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(departure.Instance);
            }

            departure.Instance = null;
        }

        private void PrewarmProducts()
        {
            PoolManager pools = PoolManager.Instance;
            if (pools == null)
            {
                Debug.LogError("[FarmIdle] BackpackStackView: no hay PoolManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            var prefabs = new HashSet<GameObject>();
            if (crops != null)
            {
                foreach (CropData crop in crops)
                {
                    if (crop == null || crop.ProductPrefab == null)
                    {
                        Debug.LogError("[FarmIdle] BackpackStackView: hay un cultivo sin prefab de producto en la lista (" + (crop != null ? crop.name : "vacío") + ").", this);
                        continue;
                    }

                    prefabs.Add(crop.ProductPrefab);
                }
            }

            foreach (GameObject prefab in prefabs)
            {
                pools.Prewarm(prefab, _maxCapacity + departingHeadroom);
            }
        }
    }
}
