namespace FarmIdle
{
    // Regla de la vibración que no necesita un celular para probarse. Es C# puro, sin UnityEngine: el reloj se lo pasa HapticsManager
    // (tiempo sin escala). Dos pulsos no pueden estar más cerca que 'minInterval' (vender diez unidades seguidas no es un zumbido), salvo
    // que el nuevo sea MÁS FUERTE que el anterior: subir de nivel justo después de una venta tiene que sentirse.
    public sealed class PulseThrottle
    {
        private readonly double _minInterval;
        private bool _any;
        private double _until;
        private int _lastStrength;

        public PulseThrottle(double minIntervalSeconds)
        {
            _minInterval = minIntervalSeconds > 0d ? minIntervalSeconds : 0d;
        }

        // true si ese pulso puede darse ahora; si puede, anota el disparo.
        public bool TryPass(double now, int strength)
        {
            if (_any && now < _until && strength <= _lastStrength) return false;

            _any = true;
            _until = now + _minInterval;
            _lastStrength = strength;
            return true;
        }

        public void Reset()
        {
            _any = false;
        }
    }
}
