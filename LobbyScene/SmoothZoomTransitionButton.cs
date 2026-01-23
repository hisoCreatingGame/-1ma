using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement; // シーン遷移に必須
using System.Collections;

public class SmoothZoomTransitionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("遷移設定")]
    [SerializeField] private string nextSceneName = "GameScene"; // 移動先のシーン名
    [SerializeField] private float waitTime = 1.0f; // クリック後の待機時間

    [Header("ズーム設定")]
    [SerializeField] private float zoomScale = 1.2f;   // ズーム倍率
    [SerializeField] private float duration = 0.2f;    // 変化にかかる時間
    [SerializeField] private float startDelay = 0.0f;  // ズーム開始までの遅延

    private Vector3 defaultScale;
    private Coroutine zoomCoroutine;
    private bool isTransitioning = false; // 連打防止用フラグ

    void Start()
    {
        defaultScale = transform.localScale;

        // 透明部分の無視設定（画像のRead/Write Enabledを忘れずに！）
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.alphaHitTestMinimumThreshold = 0.1f;
        }
    }

    // カーソルが乗った時
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isTransitioning) return; // 遷移中は反応させない

        if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
        zoomCoroutine = StartCoroutine(ScaleTo(defaultScale * zoomScale, startDelay));
    }

    // カーソルが離れた時
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isTransitioning) return; // 遷移中は反応させない

        if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
        zoomCoroutine = StartCoroutine(ScaleTo(defaultScale, 0f));
    }

    // クリックされた時
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isTransitioning) return; // 既に押されていたら何もしない

        // 遷移処理を開始
        StartCoroutine(WaitAndLoad());
    }

    // 1秒待ってロードするコルーチン
    IEnumerator WaitAndLoad()
    {
        isTransitioning = true; // ボタンを無効化（連打防止）

        // ここにクリック音再生などを入れてもOK

        // 指定時間待つ（1秒）
        yield return new WaitForSeconds(waitTime);

        // シーンをロード
        SceneManager.LoadScene(nextSceneName);
    }

    // 滑らかにサイズを変えるコルーチン
    IEnumerator ScaleTo(Vector3 targetScale, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        Vector3 startScale = transform.localScale;
        float time = 0;

        while (time < duration)
        {
            transform.localScale = Vector3.Lerp(startScale, targetScale, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        transform.localScale = targetScale;
    }
}