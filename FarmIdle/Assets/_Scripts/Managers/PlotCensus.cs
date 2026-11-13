using System;
using UnityEngine;

namespace FarmIdle
{
    // Censo de parcelas: cuántas hay en cada estado. Es el punto de encuentro entre el gameplay (Environment, que
    // anota cada cambio) y la UI (que solo lee los conteos y escucha Changed). Como Environment y UI no se pueden
    // ver entre sí, se hablan a través de este servicio de Managers, igual que el HUD y la billetera.
    public sealed class PlotCensus : MonoBehaviour
    {
        public static PlotCensus Instance { get; private set; }

        public int Empty { get; private set; }
        public int Growing { get; private set; }
        public int Ready { get; private set; }

        public int Total => Empty + Growing + Ready;

        // Se dispara después de cada cambio de los conteos.
        public event Action Changed;

        // Lo llama el Bootstrapper; hasta entonces Instance es null.
        public void Initialize()
        {
            Instance = this;
        }

        // Cada parcela se anota con el estado en que está, avisa cada cambio y se da de baja al desactivarse.
        public void Register(PlotState state)
        {
            Add(state, 1);
            Changed?.Invoke();
        }

        public void Unregister(PlotState state)
        {
            Add(state, -1);
            Changed?.Invoke();
        }

        public void Move(PlotState from, PlotState to)
        {
            if (from == to) return;

            Add(from, -1);
            Add(to, 1);
            Changed?.Invoke();
        }

        private void Add(PlotState state, int delta)
        {
            switch (state)
            {
                case PlotState.Empty: Empty = Apply(Empty, delta, state); break;
                case PlotState.Growing: Growing = Apply(Growing, delta, state); break;
                case PlotState.Ready: Ready = Apply(Ready, delta, state); break;
            }
        }

        // Nunca baja de 0: si pasara, hay una parcela que se dio de baja sin haberse anotado.
        private static int Apply(int current, int delta, PlotState state)
        {
            int next = current + delta;
            if (next >= 0) return next;

            Debug.LogWarning("[FarmIdle] PlotCensus: el conteo de parcelas '" + state + "' quedaría negativo; se deja en 0.");
            return 0;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
