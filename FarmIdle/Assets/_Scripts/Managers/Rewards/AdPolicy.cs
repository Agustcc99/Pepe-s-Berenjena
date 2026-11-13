namespace FarmIdle
{
    // ¿Se ofrecen videos con recompensa en este build? En la web, durante el Basic Launch de CrazyGames no hay anuncios (ni ingresos): los
    // pads de video y el "Duplicar" del regreso se apagan del todo, en vez de mostrar "Sin anuncio". Al pasar a Full Launch se construye con
    // FARMIDLE_WEB_ADS=on (ReleaseBuilder define FARMIDLE_WEB_ADS). Android y el Editor siempre los ofrecen.
    public static class AdPolicy
    {
        public static bool RewardedAdsOffered
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR && !FARMIDLE_WEB_ADS
                return Offered(true, false);
#else
                return Offered(false, false);
#endif
            }
        }

        public static bool Offered(bool webBuild, bool webAdsEnabled)
        {
            return !webBuild || webAdsEnabled;
        }
    }
}
