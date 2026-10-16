using UnityEngine;

public class MinimapCamera : MonoBehaviour
{
    [SerializeField] private Transform target; // player
    [SerializeField] private float followSpeed = 10.0f;

    // Update is called once per frame
    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, target.position, followSpeed * Time.deltaTime);
    }
}
