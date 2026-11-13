using System;

namespace FarmIdle
{
    // "No hay anuncios": el servicio de un build final que no tiene ningún SDK (por ejemplo, Android sin el plugin de AdMob). Nunca está
    // listo y, si igual se le pide, avisa NoFill en el acto: los pads de video muestran "Sin anuncio" y no se regala nada. Es C# puro,
    // sin UnityEngine. Reemplaza al MockAdService (que SOLO existe en el Editor y en los builds de desarrollo: es el que simula que el
    // video se completó).
    public sealed class NoAdService : IAdService
    {
        public string Name => "Sin anuncios";

        public bool IsReady(string placement)
        {
            return false;
        }

        public void ShowRewarded(string placement, Action<AdResult> onFinished)
        {
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));

            onFinished(AdResult.NoFill);
        }

        public void Tick(float unscaledDeltaSeconds)
        {
        }
    }
}
