using System.Runtime.InteropServices;

namespace FarmIdle
{
    // Puente con la página web (Assets/Plugins/WebGL/FarmIdleWeb.jslib). Solo existe en un build de WebGL; en el Editor y
    // en los demás destinos todo es un no-op, así el resto del código no necesita #if.
    //   - SyncFileSystem: Unity WebGL guarda Application.persistentDataPath en un sistema de archivos EN MEMORIA
    //     (IDBFS) que no llega a IndexedDB sola (la sincronización automática viene apagada de fábrica). Después de cada
    //     guardado hay que pedirla, o al recargar la página la partida no está.
    //   - RegisterPageHide: avisa cuando la pestaña se oculta o se cierra, que en la web es el único "OnApplicationQuit"
    //     confiable. Llama a SaveManager.OnWebPageHidden por SendMessage.
    //   - WebLocalStore: localStorage del navegador, SÍNCRONO. Lo usa MirroredSaveStorage como segunda copia de la partida (la escritura a
    //     IndexedDB es asíncrona y se puede perder si la pestaña se cierra de golpe).
    public static class WebPersistence
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void FI_SyncFileSystem();
        [DllImport("__Internal")] private static extern void FI_RegisterPageHide(string gameObjectName, string methodName);
        [DllImport("__Internal")] private static extern int FI_LocalSet(string key, string value);
        [DllImport("__Internal")] private static extern System.IntPtr FI_LocalGet(string key);
        [DllImport("__Internal")] private static extern void FI_LocalRemove(string key);
#endif

        // localStorage (síncrono). Fuera de la web no hay nada: TrySet / TryGet devuelven false.
        public static bool LocalSet(string key, string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return FI_LocalSet(key, value) != 0;
#else
            return false;
#endif
        }

        public static bool LocalGet(string key, out string value)
        {
            value = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            System.IntPtr pointer = FI_LocalGet(key);
            if (pointer == System.IntPtr.Zero) return false;

            value = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(pointer);
            return value != null;
#else
            return false;
#endif
        }

        public static void LocalRemove(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FI_LocalRemove(key);
#endif
        }

        public static void SyncFileSystem()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FI_SyncFileSystem();
#endif
        }

        public static void RegisterPageHide(string gameObjectName, string methodName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FI_RegisterPageHide(gameObjectName, methodName);
#endif
        }
    }

    // localStorage como IKeyValueStore. En el Editor y fuera de la web siempre falla en silencio (el espejo no se usa ahí).
    public sealed class WebLocalStore : IKeyValueStore
    {
        public bool TryGet(string key, out string value)
        {
            return WebPersistence.LocalGet(key, out value);
        }

        public bool TrySet(string key, string value)
        {
            return WebPersistence.LocalSet(key, value);
        }

        public void Remove(string key)
        {
            WebPersistence.LocalRemove(key);
        }
    }
}
