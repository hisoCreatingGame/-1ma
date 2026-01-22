using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class StartButtonEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Scale Settings")]
    [SerializeField] float hoverScale = 1.1f;
    [SerializeField] float clickScale = 0.95f;
    [SerializeField] float animSpeed = 10f;

    Vector3 defaultScale;
    Coroutine scaleCoroutine;

    void Awake()
    {
        defaultScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartScaleAnim(defaultScale * hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartScaleAnim(defaultScale);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        StartScaleAnim(defaultScale * clickScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StartScaleAnim(defaultScale * hoverScale);
    }

    void StartScaleAnim(Vector3 target)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleAnim(target));
    }

    IEnumerator ScaleAnim(Vector3 target)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                target,
                Time.deltaTime * animSpeed
            );
            yield return null;
        }
        transform.localScale = target;
    }
}
