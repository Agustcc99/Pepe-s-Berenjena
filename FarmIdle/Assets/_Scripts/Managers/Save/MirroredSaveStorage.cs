using System;

namespace FarmIdle
{
    // Un almacén de clave/valor SÍNCRONO (en la web, localStorage del navegador). Detrás de una interfaz para probar el espejo sin navegador.
    public interface IKeyValueStore
    {
        bool TryGet(string key, out string value);
        bool TrySet(string key, string value);
        void Remove(string key);
    }

    // Guarda la partida principal en DOS lugares: el almacén de siempre (en la web, IndexedDB, cuya escritura es asíncrona) y un espejo
    // síncrono (localStorage). Existe para el riesgo de la web: si el jugador cierra la pestaña de golpe, el navegador puede cortar la
    // escritura a IndexedDB a la mitad, pero la de localStorage ya terminó cuando la función volvió. Al leer, gana la copia MÁS NUEVA
    // (por la fecha del archivo): si IndexedDB perdió los últimos segundos, el espejo los devuelve.
    //   - Solo se espeja la partida principal (el respaldo 'save.bak' es para archivos dañados, no para cierres a medias).
    //   - Un espejo que falla (localStorage bloqueado o lleno, modo privado) no rompe nada: se sigue con el almacén de siempre.
    //   - Fuera de la web no se usa: el almacén de archivos escribe de forma síncrona y segura.
    public sealed class MirroredSaveStorage : ISaveStorage
    {
        public const string MirrorKey = "farmidle.save.main";

        private readonly ISaveStorage _inner;
        private readonly IKeyValueStore _mirror;

        public MirroredSaveStorage(ISaveStorage inner, IKeyValueStore mirror)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
        }

        public bool TryRead(string slot, out string content)
        {
            bool hasInner = _inner.TryRead(slot, out string innerText);
            if (slot != SaveFile.MainSlot)
            {
                content = innerText;
                return hasInner;
            }

            bool hasMirror = _mirror.TryGet(MirrorKey, out string mirrorText) && !string.IsNullOrEmpty(mirrorText);

            if (hasInner && hasMirror)
            {
                // Gana el más nuevo; si el espejo no se puede leer como partida, se queda el de siempre.
                content = SaveSerializer.SavedAtOf(mirrorText) > SaveSerializer.SavedAtOf(innerText) ? mirrorText : innerText;
                return true;
            }

            if (hasMirror && SaveSerializer.SavedAtOf(mirrorText) > DateTime.MinValue)
            {
                content = mirrorText;   // IndexedDB no tenía nada (o se perdió) y el espejo sí
                return true;
            }

            content = innerText;
            return hasInner;
        }

        public bool TryWrite(string slot, string content)
        {
            bool written = _inner.TryWrite(slot, content);

            // El espejo se escribe aunque el almacén falle: es una segunda oportunidad de no perder la partida.
            if (slot == SaveFile.MainSlot) _mirror.TrySet(MirrorKey, content ?? string.Empty);
            return written;
        }

        public bool Delete(string slot)
        {
            if (slot == SaveFile.MainSlot) _mirror.Remove(MirrorKey);
            return _inner.Delete(slot);
        }

        public void Flush()
        {
            _inner.Flush();
        }
    }
}
