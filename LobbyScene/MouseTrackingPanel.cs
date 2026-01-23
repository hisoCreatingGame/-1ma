using UnityEngine;
using UnityEngine.UI;

public class MouseTrackingPanel : MonoBehaviour
{
    [SerializeField] private Image targetPanel; // 変更したいパネルのImage
    [SerializeField] private Sprite[] backgroundImages; // 切り替える画像のリスト

    void Update()
    {
        if (backgroundImages.Length == 0) return;

        // マウスのX座標の画面に対する割合（0.0 〜 1.0）を取得
        float mouseRatio = Input.mousePosition.x / Screen.width;

        // 割合を配列のインデックスに変換（例：0~1を 0~5 に変換）
        // Clampを使って範囲外のエラーを防ぎます
        int index = Mathf.FloorToInt(mouseRatio * backgroundImages.Length);
        index = Mathf.Clamp(index, 0, backgroundImages.Length - 1);

        // 画像を適用
        targetPanel.sprite = backgroundImages[index];
    }
}