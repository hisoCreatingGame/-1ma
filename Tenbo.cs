using UnityEngine;

public class Tenbo : MonoBehaviour
{
    public GameObject[] prefabs;   // 5種類
    public int spawnCount = 5;
    public float force = 8f;
    public float spread = 1.5f;

    Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SpawnObjects();
        }
    }

    void SpawnObjects()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            // カメラ下（画面外）
            Vector3 screenPos = new Vector3(
                Random.Range(0, Screen.width),
                -50f,
                cam.nearClipPlane + 1f
            );

            Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);

            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // カメラが向いている方向 ＋ 少しばらつき
                Vector3 dir =
                    cam.transform.forward +
                    cam.transform.right * Random.Range(-spread, spread) +
                    cam.transform.up * Random.Range(-spread * 0.5f, spread * 0.5f);

                rb.AddForce(dir.normalized * force, ForceMode.Impulse);
            }
        }
    }
}
