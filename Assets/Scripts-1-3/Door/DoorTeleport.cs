using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DoorTeleport : MonoBehaviour
{
    [SerializeField] private Transform destination;
    [SerializeField] private UnityEvent onArrival = new UnityEvent();

    public void Teleport()
    {
        if (!isActiveAndEnabled || destination == null)
            return;

        DoorTransition transition = DoorTransition.Active;
        if (transition == null)
        {
            Debug.LogWarning("Add DoorTransition to the player.", this);
            return;
        }

        transition.Teleport(destination, onArrival);
    }
}
