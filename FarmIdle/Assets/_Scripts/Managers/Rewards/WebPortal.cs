using System.Runtime.InteropServices;

namespace FarmIdle
{
    // Puente con el portal web que aloja el juego (hoy CrazyGames; ROADMAP D-03). Solo existe en un build de WebGL; en el Editor y en los
    // demás destinos todo es un no-op, así el resto del código no necesita #if. El lado JS es window.FarmIdlePortal, que define el
    // adaptador del portal (WebPortals/crazygames-adapter.js) y vive en Assets/Plugins/WebGL/FarmIdleWeb.jslib. Sin adaptador tampoco pasa nada.
    //   - SetGameplay: "empezó / se cortó el juego" para el portal (gameplayStart / gameplayStop del SDK de CrazyGames).
    //   - ReadLocale: idioma que informa el portal ("es-AR", "en-US"), o null si no hay portal o todavía no lo dijo.
    public static class WebPortal
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void FI_PortalGameplay(int playing);
        [DllImport("__Internal")] private static extern System.IntPtr FI_PortalLocale();
#endif

        public static void SetGameplay(bool playing)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FI_PortalGameplay(playing ? 1 : 0);
#endif
        }

        public static string ReadLocale()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            System.IntPtr pointer = FI_PortalLocale();
            return pointer == System.IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);
#else
            return null;
#endif
        }
    }
}
