namespace FarmIdle
{
    // El ritmo de la venta: deja pasar una unidad cada 'interval' segundos y nada más. Es C# puro, sin UnityEngine: el
    // reloj se lo pasa quien lo usa (la zona de venta le da el tiempo del juego, que no corre en pausa ni con un anuncio).
    // Se separa en dos pasos a propósito: IsReady pregunta y Commit anota la venta. Con la mochila vacía la zona
    // pregunta pero no anota, así que el primer producto que entre se vende al instante en vez de esperar un turno.
    public sealed class SalePacer
    {
        private readonly float _interval;
        private float _nextSaleAt;   // desde cuándo se puede vender la próxima unidad

        public SalePacer(float interval)
        {
            _interval = interval > 0f ? interval : 0f;
        }

        public float Interval => _interval;

        // true si ya toca vender (la primera vez, siempre).
        public bool IsReady(float now)
        {
            return now >= _nextSaleAt;
        }

        // Anota una venta hecha en 'now': la próxima toca 'interval' después. Si se vendió justo a tiempo el ritmo sigue
        // parejo (no se acumula el retraso de un frame); si hubo una pausa larga, el ritmo arranca de nuevo desde ahora.
        public void Commit(float now)
        {
            _nextSaleAt = now - _nextSaleAt > _interval ? now + _interval : _nextSaleAt + _interval;
        }

        // Vuelve a "recién arrancado" (la primera venta toca al instante).
        public void Reset()
        {
            _nextSaleAt = 0f;
        }
    }
}
