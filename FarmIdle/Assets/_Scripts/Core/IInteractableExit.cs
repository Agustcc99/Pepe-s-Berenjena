namespace FarmIdle
{
    // Opcional: un IInteractable que quiere saber cuándo un actor deja de estar encima. Lo usan las zonas que llevan una
    // cuenta mientras el actor se queda parado (el pago de una compra "gotea" de a poco): al irse, la cuenta de esa
    // visita se corta. La parcela y la zona de venta no lo necesitan y no lo implementan.
    // Lo avisa quien detecta al actor (InteractionTrigger) cuando sale del trigger, cuando su collider desaparece o
    // cuando la zona se apaga.
    public interface IInteractableExit
    {
        void ActorLeft(IActor actor);
    }
}
