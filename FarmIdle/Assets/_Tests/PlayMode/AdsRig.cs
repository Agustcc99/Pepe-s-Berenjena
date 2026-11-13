using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // Los servicios de la Fase 8 armados por código sobre un FieldTestWorld (que ya trae el GameManager en Playing): economía, buffs,
    // fertilizante y AdsManager con un MockAdService. Sirve para probar las reglas de los anuncios (enfriamientos, corte por tiempo,
    // resultados repetidos) sin arrancar el juego entero. Los números son chicos a propósito para no esperar:
    //   - monedas iniciales 1000, fertilizante $100, stock máximo 2;
    //   - enfriamiento mínimo entre anuncios 1,0 s y corte por tiempo 0,8 s;
    //   - 'Turbo': buff x3 por 2,0 s con enfriamiento de 1,5 s; 'Fert': +1 fertilizante con enfriamiento de 1,5 s.
    internal sealed class AdsRig
    {
        public GameConfig Config { get; private set; }
        public RewardData Turbo { get; private set; }
        public RewardData Fert { get; private set; }
        public MockAdService Mock { get; private set; }
        public EconomyManager Economy { get; private set; }
        public BuffManager Buffs { get; private set; }
        public FertilizerManager Fertilizer { get; private set; }
        public AdsManager Ads { get; private set; }

        private GameObject _services;

        public static AdsRig Create(FieldTestWorld world, IAdService service = null)
        {
            var rig = new AdsRig();

            rig.Turbo = TestReflection.CreateReward("turbo", RewardKind.MoveSpeedBuff, 3f, 2f, 1.5f);
            rig.Fert = TestReflection.CreateReward("fert", RewardKind.Fertilizer, 1f, 0f, 1.5f);

            rig.Config = ScriptableObject.CreateInstance<GameConfig>();
            TestReflection.Set(rig.Config, "startingCoins", 1000);
            TestReflection.Set(rig.Config, "rewards", new[] { rig.Turbo, rig.Fert });
            TestReflection.Set(rig.Config, "fertilizerPrice", 100);
            TestReflection.Set(rig.Config, "fertilizerMaxStock", 2);
            TestReflection.Set(rig.Config, "fertilizerBuyMaxStock", 2);
            TestReflection.Set(rig.Config, "fertilizerBuyCooldownSeconds", 0f);
            TestReflection.Set(rig.Config, "adMinIntervalSeconds", 1f);
            TestReflection.Set(rig.Config, "adTimeoutSeconds", 0.8f);
            TestReflection.Set(rig.Config, "maxFinalMoveSpeed", 11f);

            rig.Mock = new MockAdService { DurationSeconds = 0.2f };

            rig._services = new GameObject("AdsRigServices");
            rig._services.transform.SetParent(world.Root.transform, false);

            rig.Economy = rig.Add<EconomyManager>();
            rig.Economy.Initialize(rig.Config);

            rig.Buffs = rig.Add<BuffManager>();
            rig.Buffs.Initialize(rig.Config);

            rig.Fertilizer = rig.Add<FertilizerManager>();
            rig.Fertilizer.Initialize(rig.Config);

            rig.Ads = rig.Add<AdsManager>();
            rig.Ads.Initialize(rig.Config, service ?? rig.Mock);
            return rig;
        }

        public void Destroy()
        {
            if (_services != null) Object.DestroyImmediate(_services);
            Object.DestroyImmediate(Config);
            Object.DestroyImmediate(Turbo);
            Object.DestroyImmediate(Fert);
        }

        private T Add<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(_services.transform, false);
            return go.AddComponent<T>();
        }
    }
}
