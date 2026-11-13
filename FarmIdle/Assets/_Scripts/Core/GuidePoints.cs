using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Un lugar del mundo al que conviene ir AHORA, pasada la guía de los primeros minutos: el pad de venta con la mochila llena, un
    // pad de compra que ya se puede pagar. Lo implementa un componente de Environment; la UI lo lee para la flecha del borde de la
    // pantalla. Así el entorno y la UI no se conocen (misma idea que ITutorialTarget).
    public interface IGuidePoint
    {
        Vector3 Position { get; }

        // Cuál gana si hay varios: el más alto. Entre iguales, el más cercano.
        int GuidePriority { get; }

        // true mientras conviene ir. Se consulta un par de veces por segundo: tiene que ser barato y sin asignaciones.
        bool WantsGuide { get; }
    }

    // El punto de encuentro de esos lugares: se anotan al activarse y se borran al apagarse. Es una lista estática y no un servicio
    // porque no tiene estado propio ni nada que guardar (ver DECISIONES.md).
    public static class GuidePoints
    {
        public const int BuyPriority = 1;
        public const int SellPriority = 2;

        private static readonly List<IGuidePoint> Points = new List<IGuidePoint>(64);

        public static int Count => Points.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Points.Clear();
        }

        public static void Register(IGuidePoint point)
        {
            if (point != null && !Points.Contains(point)) Points.Add(point);
        }

        public static void Unregister(IGuidePoint point)
        {
            Points.Remove(point);
        }

        // El lugar al que conviene ir desde 'from' (null si ninguno quiere): mayor prioridad y, entre iguales, el más cercano en el suelo.
        public static IGuidePoint Best(Vector3 from)
        {
            IGuidePoint best = null;
            int bestPriority = int.MinValue;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Points.Count; i++)
            {
                IGuidePoint point = Points[i];
                if (!point.WantsGuide) continue;

                int priority = point.GuidePriority;
                if (priority < bestPriority) continue;

                Vector3 position = point.Position;
                float dx = position.x - from.x;
                float dz = position.z - from.z;
                float distance = dx * dx + dz * dz;
                if (priority == bestPriority && distance >= bestDistance) continue;

                best = point;
                bestPriority = priority;
                bestDistance = distance;
            }

            return best;
        }
    }
}
