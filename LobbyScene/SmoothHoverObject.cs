using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class SmoothHoverObject : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI設定")]
    [SerializeField] private TMP_Text infoText; // 右下のテキスト欄

    [Header("ズーム設定")]
    [SerializeField] private float zoomScale = 1.2f;   // ズーム倍率
    [SerializeField] private float duration = 0.2f;    // 変化にかかる時間（滑らかさ）
    [SerializeField] private float startDelay = 0.0f;  // ズーム開始までの遅延時間

    private Vector3 defaultScale;
    private Coroutine currentCoroutine;
    
    // ★追加: 表示名を保持する変数
    private string _displayName;

    void Start()
    {
        defaultScale = transform.localScale;
        
        // 初期状態ではオブジェクト名をセットしておく
        if (string.IsNullOrEmpty(_displayName))
        {
            _displayName = this.gameObject.name;
        }

        // 透明部分を無視する設定
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.alphaHitTestMinimumThreshold = 0.1f;
        }
    }

    // ★追加: 外部から表示名（ホバー時のテキスト）を変更するメソッド
    public void SetDisplayName(string newName)
    {
        _displayName = newName;
    }

    // カーソルが乗った時
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(ScaleTo(defaultScale * zoomScale, startDelay));

        // ★変更: _displayName を表示する
        if (infoText != null)
        {
            infoText.text = _displayName;
        }
    }

    // カーソルが離れた時
    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(ScaleTo(defaultScale, 0f));

        if (infoText != null)
        {
            infoText.text = "";
        }
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
}