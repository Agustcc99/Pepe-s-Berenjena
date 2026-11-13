namespace FarmIdle
{
    // Algo con lo que un actor interactúa por proximidad, sin botones: parcela, zona de venta, zona de desbloqueo.
    // Quien detecta al actor (InteractionTrigger) llama a Interact en cada frame mientras el actor está encima y
    // puede actuar (IActor.CanInteract). Cada objeto decide qué hace y cada cuánto: la parcela, por ejemplo,
    // siembra una vez y después no hace nada hasta que vuelve a quedar vacía.
    public interface IInteractable
    {
        void Interact(IActor actor);
    }
}
