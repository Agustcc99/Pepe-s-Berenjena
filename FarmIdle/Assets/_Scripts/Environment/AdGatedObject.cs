using UnityEngine;

namespace FarmIdle
{
    // Se pone en los pads de video: si este build no ofrece anuncios con recompensa (AdPolicy), el pad entero se apaga desde el arranque.
    public sealed class AdGatedObject : MonoBehaviour
    {
        private void Awake()
        {
            if (!AdPolicy.RewardedAdsOffered) gameObject.SetActive(false);
        }
    }
}
