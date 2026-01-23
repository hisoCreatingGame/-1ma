using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 重要
using TMPro;

public class HoverObject : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("設定")]
    [SerializeField] public TMP_Text infoText; // 右下のテキスト欄をここにアサイン
    [SerializeField] private float zoomScale = 1.2f; // ズーム倍率
    
    private Vector3 defaultScale;

    void Start()
    {
        defaultScale = transform.localScale;
    }

    // カーソルが乗った時
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 1. 画像を少しズーム
        transform.localScale = defaultScale * zoomScale;

        // 2. オブジェクトの名前をテキスト欄に表示
        if (infoText != null)
        {
            infoText.text = this.gameObject.name;
        }
    }

    // カーソルが離れた時
    public void OnPointerExit(PointerEventData eventData)
    {
        // サイズを元に戻す
        transform.localScale = defaultScale;

        // テキストを消す（必要であれば）
        if (infoText != null)
        {
            infoText.text = "";
        }
    }
}