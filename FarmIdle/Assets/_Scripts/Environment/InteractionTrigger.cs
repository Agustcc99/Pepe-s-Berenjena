using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Relé entre la física y los IInteractable: detecta con el collider trigger de este objeto a los actores que
    // se paran encima (jugador, ayudantes) y, mientras estén ahí y puedan actuar, llama a Interact del
    // IInteractable que vive en el mismo objeto. Así parcelas, zonas de venta y de desbloqueo no tocan la física.
    //
    // - Solo conoce IActor e IInteractable (Core): no sabe nada del jugador ni de sus componentes.
    // - Resuelve al actor una vez por entrada (OnTriggerEnter), nunca por frame; con nadie encima el Update no hace nada.
    // - Supone un collider por actor. Si el collider del actor se destruye o se apaga sin que llegue OnTriggerExit,
    //   el actor se olvida solo.
    [DisallowMultipleComponent]
    public sealed class InteractionTrigger : MonoBehaviour
    {
        private struct Occupant
        {
            public Collider Collider;
            public IActor Actor;
        }

        private readonly List<Occupant> _occupants = new List<Occupant>(2);
        private IInteractable _target;
        private IInteractableExit _exitListener;   // opcional: el mismo objeto, si quiere saber cuándo el actor se va

        // Cuántos actores hay encima ahora (para pruebas y depuración).
        public int ActorCount => _occupants.Count;

        private void Awake()
        {
            _target = GetComponent<IInteractable>();
            if (_target == null)
            {
                Debug.LogError("[FarmIdle] InteractionTrigger '" + name + "': falta un IInteractable (por ejemplo PlotView) en este objeto.", this);
                return;
            }

            _exitListener = _target as IInteractableExit;

            Collider trigger = GetComponent<Collider>();
            if (trigger == null || !trigger.isTrigger)
            {
                Debug.LogError("[FarmIdle] InteractionTrigger '" + name + "': hace falta un Collider con Is Trigger marcado.", this);
            }
        }

        private void OnDisable()
        {
            for (int i = _occupants.Count - 1; i >= 0; i--)
            {
                NotifyLeft(_occupants[i].Actor);
            }

            _occupants.Clear();
        }

        private void NotifyLeft(IActor actor)
        {
            if (_exitListener != null) _exitListener.ActorLeft(actor);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_target == null) return;

            IActor actor = other.GetComponentInParent<IActor>();
            if (actor == null) return;

            for (int i = 0; i < _occupants.Count; i++)
            {
                if (ReferenceEquals(_occupants[i].Actor, actor)) return;
            }

            _occupants.Add(new Occupant { Collider = other, Actor = actor });
        }

        private void OnTriggerExit(Collider other)
        {
            for (int i = _occupants.Count - 1; i >= 0; i--)
            {
                if (_occupants[i].Collider != other) continue;

                IActor actor = _occupants[i].Actor;
                _occupants.RemoveAt(i);
                NotifyLeft(actor);
            }
        }

        private void Update()
        {
            for (int i = _occupants.Count - 1; i >= 0; i--)
            {
                Occupant occupant = _occupants[i];

                if (occupant.Collider == null || !occupant.Collider.enabled || !occupant.Collider.gameObject.activeInHierarchy)
                {
                    _occupants.RemoveAt(i);
                    NotifyLeft(occupant.Actor);
                    continue;
                }

                if (occupant.Actor.CanInteract) _target.Interact(occupant.Actor);
            }
        }
    }
}
