using System.Collections.Generic;

namespace FarmIdle
{
    // Los slots en memoria: no persisten nada. Sirve para las pruebas (que no tocan la partida real del dispositivo) y para
    // simular fallos: 'FailWrites' hace que toda escritura falle y 'FailReads' que toda lectura falle.
    public sealed class MemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> _slots = new Dictionary<string, string>();

        public bool FailWrites { get; set; }
        public bool FailReads { get; set; }
        public int WriteCount { get; private set; }
        public int FlushCount { get; private set; }

        public IReadOnlyDictionary<string, string> Slots => _slots;

        public bool Has(string slot)
        {
            return _slots.ContainsKey(slot);
        }

        // Para armar una situación de prueba (un archivo dañado, uno viejo...) sin pasar por el contador de escrituras.
        public void Put(string slot, string content)
        {
            _slots[slot] = content;
        }

        public bool TryRead(string slot, out string content)
        {
            if (FailReads)
            {
                content = null;
                return false;
            }

            return _slots.TryGetValue(slot, out content);
        }

        public bool TryWrite(string slot, string content)
        {
            if (FailWrites) return false;

            _slots[slot] = content;
            WriteCount++;
            return true;
        }

        public bool Delete(string slot)
        {
            _slots.Remove(slot);
            return true;
        }

        public void Flush()
        {
            FlushCount++;
        }
    }
}
