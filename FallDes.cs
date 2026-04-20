using UnityEngine;

public class FallDes : MonoBehaviour
{
    public float destroyY = 0f;

    void Update()
    {
        if (transform.position.y <= destroyY)
        {
            Debug.Log("Destroy!");
            Destroy(gameObject);
        }
    }
}
