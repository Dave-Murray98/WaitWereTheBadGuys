using UnityEngine;
using Infohazard.HyperNav;

public class SimpleWormFollow : MonoBehaviour
{
    [SerializeField] private SplineNavAgent navAgent;
    [SerializeField] private Transform target;

    [SerializeField] private float speed = 1f;

    [SerializeField] Rigidbody rb;


    private Vector3 moveDirection;

    private void Start()
    {
        if (navAgent == null)
        {
            navAgent = GetComponent<SplineNavAgent>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    // Update is called once per frame
    private void Update()
    {
        if (navAgent != null)
        {
            //handle movement direction
            navAgent.Destination = target.position;
            moveDirection = navAgent.DesiredVelocity;
            Debug.Log("Desired Velocity: " + moveDirection);

            // Rotate to face movement direction
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }

        }


    }

    private void FixedUpdate()
    {
        if (rb != null)
            rb.linearVelocity = speed * Time.deltaTime * moveDirection;
    }
}
