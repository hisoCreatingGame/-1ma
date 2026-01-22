using UnityEngine;

public class Hai_object : MonoBehaviour
{
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            rb.isKinematic = false;
        }
    }
}