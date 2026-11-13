using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace FarmIdle
{
    // Contadores de un pool, para depurar y para los tests.
    public readonly struct PoolStats
    {
        public readonly int Created;    // veces que se hizo Instantiate en total
        public readonly int Active;     // instancias en uso
        public readonly int Inactive;   // instancias libres, listas para entregar
        public readonly int Misses;     // instancias creadas fuera del Prewarm: el pool se quedó corto

        public PoolStats(int created, int active, int inactive, int misses)
        {
            Created = created;
            Active = active;
            Inactive = inactive;
            Misses = misses;
        }
    }

    // Pools de GameObjects sobre UnityEngine.Pool. La regla del juego es no hacer Instantiate ni Destroy mientras
    // se juega: cada sistema declara lo que va a necesitar con Prewarm (al arrancar), pide con Get y devuelve con Release.
    //
    // - Las instancias libres están desactivadas y cuelgan de un contenedor propio de cada pool.
    // - Un objeto pedido sin padre queda en ese contenedor (que vive en el objeto persistente de servicios): hay que
    //   devolverlo con Release. Si se le da un padre de la escena, tiene que volver al pool antes de descargar esa escena.
    // - Si se pide más de lo que hay (Misses), el pool crea una instancia nueva y avisa por consola: señal para subir el Prewarm.
    public sealed class PoolManager : MonoBehaviour
    {
        // Instancias libres que se conservan como máximo; las que sobren al devolverlas se destruyen.
        public const int DefaultMaxSize = 256;

        public static PoolManager Instance { get; private set; }

        private sealed class Pool
        {
            public GameObject Prefab;
            public Transform Container;
            public ObjectPool<GameObject> Inner;
            public int Created;
            public int Misses;
            public bool Prewarming;
            public bool MissWarned;
        }

        private readonly Dictionary<GameObject, Pool> _poolsByPrefab = new Dictionary<GameObject, Pool>();
        private readonly Dictionary<GameObject, Pool> _poolByInstance = new Dictionary<GameObject, Pool>();
        private readonly HashSet<GameObject> _active = new HashSet<GameObject>();
        private readonly List<GameObject> _scratch = new List<GameObject>();

        // Lo llama el Bootstrapper; hasta entonces Instance es null.
        public void Initialize()
        {
            Instance = this;
        }

        // Deja el pool del prefab con 'count' instancias creadas (y desactivadas). Se puede repetir sin problema:
        // solo crea las que falten. 'maxSize' solo cuenta la primera vez que se crea el pool de ese prefab.
        public void Prewarm(GameObject prefab, int count, int maxSize = DefaultMaxSize)
        {
            if (prefab == null)
            {
                Debug.LogError("[FarmIdle] PoolManager.Prewarm: el prefab es null.");
                return;
            }
            if (count <= 0) return;

            Pool pool = GetOrCreatePool(prefab, Mathf.Max(count, maxSize), false);

            // Se piden hasta tener 'count' en total (las libres se reutilizan, las que falten se crean) y se devuelven.
            int toTake = count - pool.Inner.CountActive;
            pool.Prewarming = true;
            for (int i = 0; i < toTake; i++)
            {
                _scratch.Add(pool.Inner.Get());
            }
            pool.Prewarming = false;

            for (int i = 0; i < _scratch.Count; i++)
            {
                pool.Inner.Release(_scratch[i]);
            }
            _scratch.Clear();

            Debug.Log("[FarmIdle] Pool '" + prefab.name + "' listo: " + pool.Inner.CountAll + " instancias.");
        }

        public GameObject Get(GameObject prefab)
        {
            return Get(prefab, Vector3.zero, Quaternion.identity);
        }

        // Entrega una instancia activa, ya colocada. No hace Instantiate si hay una libre.
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[FarmIdle] PoolManager.Get: el prefab es null.");
                return null;
            }

            Pool pool = GetOrCreatePool(prefab, DefaultMaxSize, true);
            GameObject instance = pool.Inner.Get();

            // Primero se coloca y después se activa, para que OnEnable ya vea la posición final.
            Transform t = instance.transform;
            t.SetParent(parent != null ? parent : pool.Container, false);
            t.SetPositionAndRotation(position, rotation);

            _active.Add(instance);
            instance.SetActive(true);
            return instance;
        }

        // Devuelve la instancia al pool: se desactiva y vuelve al contenedor.
        public void Release(GameObject instance)
        {
            if (instance == null) return;

            if (!_poolByInstance.TryGetValue(instance, out Pool pool))
            {
                Debug.LogWarning("[FarmIdle] Release de un objeto que no viene de ningún pool (se destruye): " + instance.name, instance);
                Destroy(instance);
                return;
            }

            if (!_active.Remove(instance))
            {
                Debug.LogWarning("[FarmIdle] Release de un objeto que ya estaba libre en el pool: " + instance.name, instance);
                return;
            }

            pool.Inner.Release(instance);
        }

        public bool TryGetStats(GameObject prefab, out PoolStats stats)
        {
            if (prefab != null && _poolsByPrefab.TryGetValue(prefab, out Pool pool))
            {
                stats = new PoolStats(pool.Created, pool.Inner.CountActive, pool.Inner.CountInactive, pool.Misses);
                return true;
            }

            stats = default;
            return false;
        }

        private Pool GetOrCreatePool(GameObject prefab, int maxSize, bool warnIfNew)
        {
            if (_poolsByPrefab.TryGetValue(prefab, out Pool existing)) return existing;

            if (warnIfNew)
            {
                Debug.LogWarning("[FarmIdle] Get sin Prewarm previo de '" + prefab.name + "': se crea el pool ahora (Instantiate durante el juego).");
            }

            var pool = new Pool { Prefab = prefab };

            pool.Container = new GameObject("Pool " + prefab.name).transform;
            pool.Container.SetParent(transform, false);

            // collectionCheck en false: los Release duplicados ya los detecta el set de activos, en cualquier build.
            pool.Inner = new ObjectPool<GameObject>(
                createFunc: () => CreateInstance(pool),
                actionOnGet: null,
                actionOnRelease: instance => ParkInstance(pool, instance),
                actionOnDestroy: DestroyInstance,
                collectionCheck: false,
                defaultCapacity: Mathf.Min(maxSize, 16),
                maxSize: maxSize);

            _poolsByPrefab.Add(prefab, pool);
            return pool;
        }

        // Único lugar donde se hace Instantiate.
        private GameObject CreateInstance(Pool pool)
        {
            GameObject instance = Instantiate(pool.Prefab, pool.Container);
            instance.name = pool.Prefab.name;
            instance.SetActive(false);

            pool.Created++;
            _poolByInstance.Add(instance, pool);

            if (!pool.Prewarming)
            {
                pool.Misses++;
                if (!pool.MissWarned)
                {
                    pool.MissWarned = true;
                    Debug.LogWarning("[FarmIdle] El pool '" + pool.Prefab.name + "' creó una instancia fuera del Prewarm (ya van " + pool.Created + "). Conviene subir el Prewarm.");
                }
            }

            return instance;
        }

        private static void ParkInstance(Pool pool, GameObject instance)
        {
            instance.SetActive(false);
            instance.transform.SetParent(pool.Container, false);
        }

        // Solo ocurre si se devuelven más instancias que maxSize.
        private void DestroyInstance(GameObject instance)
        {
            _poolByInstance.Remove(instance);
            Destroy(instance);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
