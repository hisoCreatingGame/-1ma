using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class ZoomAndClickText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("設定")]
    [SerializeField] private TMP_Text outputText;
    [SerializeField, TextArea] private string message;
    [SerializeField] private float zoomScale = 1.2f;

    private Vector3 defaultScale;
    private Coroutine _currentCoroutine; // 現在実行中のコルーチンを保存する変数

    void Start()
    {
        defaultScale = transform.localScale;

        // 透明部分のクリック透過設定
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.alphaHitTestMinimumThreshold = 0.3f;
        }
    }

    // ホバー時の処理
    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = defaultScale * zoomScale;
    }

    // ホバー終了時の処理
    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = defaultScale;
    }

    // ★修正箇所: インターフェースの実装は必ず void にする必要があります
    public void OnPointerClick(PointerEventData eventData)
    {
        if (outputText != null)
        {
            // もし既にメッセージ表示のタイマーが動いていたら、一度止める（連打対策）
            // これをしないと、連打したときに文字が点滅したり早く消えたりしてしまいます
            if (_currentCoroutine != null)
            {
                StopCoroutine(_currentCoroutine);
            }

            // コルーチン（遅延処理）を開始する
            _currentCoroutine = StartCoroutine(ShowMessageRoutine());
        }
    }

    // ★修正箇所: 実際の遅延処理を行うコルーチンを別のメソッドとして定義
    private IEnumerator ShowMessageRoutine()
    {
        // 1. テキストを表示
        outputText.text = message;

        // 2. 2秒待つ
        yield return new WaitForSeconds(2.0f);

        // 3. テキストを消す
        outputText.text = "";
        
        // 4. 完了したので変数をクリア
        _currentCoroutine = null;
    }
}