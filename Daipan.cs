using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Daipan : MonoBehaviour
{
    public Animator animator;

    // 爆発設定
    public float minExplosionForce = 200f;
    public float maxExplosionForce = 1000f;
    public float explosionRadius = 5f;
    public float upwardsModifier = 1f;
    public LayerMask affectedLayer;
    public Transform explosionCenter;

    // チャージ設定
    public float maxChargeTime = 2f;
    private float chargeTime = 0f;
    private bool isCharging = false;

    // ゲージ
    public Slider chargeSlider;

    void Start()
    {
        if (chargeSlider != null)
            chargeSlider.value = 0f;
    }

    void Update()
    {
        // Space 押し始め
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isCharging = true;
            chargeTime = 0f;
            UpdateGauge();
        }

        // 押している間
        if (isCharging)
        {
            chargeTime += Time.deltaTime;
            chargeTime = Mathf.Min(chargeTime, maxChargeTime);
            UpdateGauge();
        }

        // 離した瞬間
        if (Input.GetKeyUp(KeyCode.Space))
        {
            isCharging = false;
            animator.SetTrigger("isDaipan");
        }

        // Rキーでリスタート
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    void UpdateGauge()
    {
        if (chargeSlider != null)
        {
            chargeSlider.value = chargeTime / maxChargeTime;
        }
    }

    // Animation Event
    public void OnAnimationEnd()
    {
        Explode();
        ResetGauge();
    }

    void ResetGauge()
    {
        chargeTime = 0f;
        if (chargeSlider != null)
            chargeSlider.value = 0f;
    }

    void Explode()
    {
        float powerRatio = chargeTime / maxChargeTime;
        float explosionForce = Mathf.Lerp(
            minExplosionForce,
            maxExplosionForce,
            powerRatio
        );

        Collider[] colliders = Physics.OverlapSphere(
            explosionCenter.position,
            explosionRadius,
            affectedLayer
        );

        foreach (Collider hit in colliders)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(
                    explosionForce,
                    explosionCenter.position,
                    explosionRadius,
                    upwardsModifier,
                    ForceMode.Impulse
                );
            }
        }
    }
}
