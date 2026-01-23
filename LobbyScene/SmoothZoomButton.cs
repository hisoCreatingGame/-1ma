using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class SmoothZoomButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("テキスト設定")]
    [SerializeField] private TMP_Text outputText;
    [SerializeField, TextArea] private string message;

    [Header("ズーム設定")]
    [SerializeField] private float zoomScale = 1.2f;   // 目標の倍率
    [SerializeField] private float duration = 0.2f;    // 変化にかかる時間（秒）
    [SerializeField] private float startDelay = 0.0f;  // 開始するまでの遅延（秒）

    private Vector3 defaultScale;
    
    // ズーム用のコルーチン
    private Coroutine currentZoomCoroutine; 
    
    // ★追加: メッセージ表示用のコルーチン（ズーム用とは分ける必要があります）
    private Coroutine _messageCoroutine;

    void Start()
    {
        defaultScale = transform.localScale;

        // 透過部分の判定除外設定
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.alphaHitTestMinimumThreshold = 0.1f;
        }
    }

    // --- ズーム処理 (OnPointerEnter / OnPointerExit) ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentZoomCoroutine != null) StopCoroutine(currentZoomCoroutine);
        currentZoomCoroutine = StartCoroutine(ScaleTo(defaultScale * zoomScale, startDelay));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentZoomCoroutine != null) StopCoroutine(currentZoomCoroutine);
        currentZoomCoroutine = StartCoroutine(ScaleTo(defaultScale, 0f));
    }

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

    // --- クリック時のテキスト処理 (OnPointerClick) ---

    // ★修正箇所: クリック時に2秒間だけテキストを表示する
    public void OnPointerClick(PointerEventData eventData)
    {
        if (outputText != null)
        {
            // 連打対策: 既にメッセージ表示のタイマーが動いていたらリセットする
            if (_messageCoroutine != null)
            {
                StopCoroutine(_messageCoroutine);
            }

            // 新しいメッセージ表示コルーチンを開始
            _messageCoroutine = StartCoroutine(ShowMessageRoutine());
        }
    }

    // ★追加: テキストを表示して2秒後に消すコルーチン
    private IEnumerator ShowMessageRoutine()
    {
        // 1. テキストを表示
        outputText.text = message;

        // 2. 2秒待つ
        yield return new WaitForSeconds(2.0f);

        // 3. テキストを消す
        outputText.text = "";
        
        _messageCoroutine = null;
    }
}