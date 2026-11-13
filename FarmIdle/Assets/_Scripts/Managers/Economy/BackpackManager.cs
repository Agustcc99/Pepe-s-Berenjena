using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Dueño de la mochila del jugador: tiene el Inventory y es la única puerta para meter o sacar productos.
    // Es el mismo esquema de EconomyManager con la billetera: la UI (DebugPanel) y el gameplay (la pila visible del
    // jugador) no se ven entre sí, así que se encuentran acá. Ven la mochila como IReadOnlyInventory (contenido +
    // evento) y los cambios se piden por TryAdd / TryPop.
    // Es la mochila del jugador local: un ayudante tendría su propio Inventory dentro de su IBackpackOwner.
    // La venta (Fase 5) saca de acá de arriba hacia abajo; las mejoras de capacidad (Fase 6) la agrandan por
    // SetCapacityBonus, y la partida guardada (Fase 7, ISaveable "backpack") lleva el contenido: los ids de cultivo de abajo hacia
    // arriba. La capacidad NO se guarda: la reconstruye el progreso (mejoras y perks), que se restaura antes.
    public sealed class BackpackManager : MonoBehaviour, ISaveable
    {
        public static BackpackManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class BackpackSave
        {
            public int version = SaveVersion;
            public string[] crops = new string[0];
        }

        private GameConfig _config;
        private Inventory _inventory;
        private int _baseCapacity;

        // Vista de solo lectura: contenido, capacidad y evento Changed.
        public IReadOnlyInventory Backpack => _inventory;

        // El jugador quiso cosechar con la mochila llena (se avisa en cada intento: quien lo escucha, el audio, limita la frecuencia).
        public event Action FullRejected;

        // Cuando la mochila no tiene lugar, TryAdd devuelve false y avisa FullRejected.
        public bool TryAddOrReject(CropData crop)
        {
            if (_inventory.TryAdd(crop)) return true;

            if (_inventory.IsFull) FullRejected?.Invoke();
            return false;
        }

        // La capacidad más grande que puede llegar a tener (la base más las mejoras al máximo): la pila visible declara
        // su Prewarm con este número al cargar el nivel, así crecer la mochila nunca obliga a hacer Instantiate.
        public int MaxCapacity { get; private set; }

        // Lo llama el Bootstrapper; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config;
            _baseCapacity = config.BackpackCapacity;
            MaxCapacity = Mathf.Max(_baseCapacity, config.MaxBackpackCapacity);
            _inventory = new Inventory(_baseCapacity);
            _inventory.Changed += OnInventoryChanged;
            Instance = this;
        }

        // ---------- Partida guardada ----------

        public string SaveId => "backpack";

        public event Action SaveRequested;

        private void OnInventoryChanged(InventoryChange change)
        {
            SaveRequested?.Invoke();
        }

        public string Capture()
        {
            var save = new BackpackSave { crops = new string[_inventory.Count] };
            for (int i = 0; i < save.crops.Length; i++) save.crops[i] = _inventory.Items[i].Id;
            return JsonUtility.ToJson(save);
        }

        // Reemplaza el contenido. Los cultivos que ya no existen se saltean y lo que no entra en la capacidad actual se
        // descarta (avisando): la capacidad ya creció si el progreso se restauró primero. Con null o vacío queda vacía.
        public void Restore(string json)
        {
            var crops = new List<CropData>();

            BackpackSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<BackpackSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            int unknown = 0;
            if (save != null && save.crops != null)
            {
                foreach (string id in save.crops)
                {
                    CropData crop = _config.FindCrop(id);
                    if (crop != null) crops.Add(crop);
                    else unknown++;
                }
            }

            int kept = _inventory.ReplaceContents(crops);

            if (unknown > 0 || kept < crops.Count)
            {
                Debug.LogWarning("[FarmIdle] Partida: mochila restaurada con " + kept + " productos (" + unknown + " de cultivos que ya no existen, " + (crops.Count - kept) + " que no entraban).");
            }
        }

        // Devuelve false (sin tocar nada) si la mochila está llena.
        public bool TryAdd(CropData crop)
        {
            return _inventory.TryAdd(crop);
        }

        // Saca el producto de arriba (LIFO). Devuelve false si está vacía.
        public bool TryPop(out CropData crop)
        {
            return _inventory.TryPop(out crop);
        }

        // F-18: saca hasta 'amount' unidades de ese cultivo, estén donde estén en la pila. Devuelve cuántas sacó.
        public int TryTake(CropData crop, int amount)
        {
            return _inventory.TryTake(crop, amount);
        }

        // Fija cuántos lugares extra tiene la mochila sobre su capacidad base (lo que dan las mejoras). Solo crece, sin
        // perder lo que lleva. Pasa por acá y no por el Inventory para que la capacidad tenga un único dueño.
        // Con 'allowShrink' (solo al restaurar o borrar la partida) la capacidad también puede bajar.
        public void SetCapacityBonus(int bonus, bool allowShrink = false)
        {
            int capacity = _baseCapacity + Mathf.Max(0, bonus);

            if (capacity > MaxCapacity)
            {
                // Más de lo previsto en el Prewarm: se acepta, pero la pila visible va a tener que crear instancias.
                Debug.LogWarning("[FarmIdle] BackpackManager: la capacidad (" + capacity + ") supera la máxima prevista (" + MaxCapacity + "); la pila va a hacer Instantiate.");
                MaxCapacity = capacity;
            }

            if (allowShrink) _inventory.SetCapacity(capacity);
            else _inventory.TryGrowTo(capacity);
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            if (Instance == this) Instance = null;
        }
    }
}
