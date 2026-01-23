using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FakeCursor : MonoBehaviour
{
    public static FakeCursor Instance;

    [Header("Main Cursor")]
    [SerializeField] RectTransform cursor;

    [Header("Trail")]
    [SerializeField] Image trailPrefab;
    [SerializeField] Transform trailParent;
    [SerializeField] float trailInterval = 0.03f;
    [SerializeField] float trailLifeTime = 0.3f;

    [Header("Rendering")]
    [Tooltip("描画順序。他のCanvasより大きな値にしてください")]
    [SerializeField] int sortingOrder = 30000;

    private Canvas _myCanvas;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // --- 修正箇所 1: 独自のCanvasを設定して最前面に表示する ---
        _myCanvas = GetComponent<Canvas>();
        if (_myCanvas == null)
        {
            _myCanvas = gameObject.AddComponent<Canvas>();
        }
        _myCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _myCanvas.sortingOrder = sortingOrder;

        // --- 修正箇所 2: カーソル画像がクリックを邪魔しないようにする ---
        if (cursor != null)
        {
            Image cursorImg = cursor.GetComponent<Image>();
            if (cursorImg != null)
            {
                cursorImg.raycastTarget = false;
            }
        }
    }

    void Start()
    {
        Cursor.visible = false;
        StartCoroutine(TrailRoutine());
    }

    void Update()
    {
        // ScreenSpaceOverlayなのでマウス座標をそのまま代入でOK
        cursor.position = Input.mousePosition;

        // CanvasのSortingOrderが他から変更されないよう監視（念のため）
        if (_myCanvas != null && _myCanvas.sortingOrder != sortingOrder)
        {
            _myCanvas.sortingOrder = sortingOrder;
        }

        // クリック演出（任意）
        if (Input.GetMouseButtonDown(0))
            cursor.localScale = Vector3.one * 0.85f;

        if (Input.GetMouseButtonUp(0))
            cursor.localScale = Vector3.one;
    }

    IEnumerator TrailRoutine()
    {
        while (true)
        {
            CreateTrail();
            yield return new WaitForSeconds(trailInterval);
        }
    }

    void CreateTrail()
    {
        if (trailPrefab == null || trailParent == null) return;

        Image trail = Instantiate(trailPrefab, trailParent);
        trail.rectTransform.position = cursor.position;
        
        // --- 修正箇所 3: 軌跡もクリックを邪魔しないようにする ---
        trail.raycastTarget = false;

        StartCoroutine(FadeOut(trail));
    }

    IEnumerator FadeOut(Image img)
    {
        float t = 0f;
        Color c = img.color;
        // 初期アルファ値を保持するか、0.5f決め打ちかはプレハブ設定に合わせるならimg.color.aを使う
        float startAlpha = c.a > 0 ? c.a : 0.5f;

        while (t < trailLifeTime)
        {
            t += Time.deltaTime;
            // 徐々に透明に
            c.a = Mathf.Lerp(startAlpha, 0f, t / trailLifeTime);
            img.color = c;
            yield return null;
        }

        Destroy(img.gameObject);
    }

    // 外部からサイズ変更
    public void SetScale(float scale)
    {
        if(cursor != null) cursor.localScale = Vector3.one * scale;
    }

    // 外部から表示ON/OFF
    public void SetVisible(bool visible)
    {
        if(cursor != null) cursor.gameObject.SetActive(visible);
    }
}