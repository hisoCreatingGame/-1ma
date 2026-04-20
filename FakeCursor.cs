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

        _myCanvas = GetComponent<Canvas>();
        if (_myCanvas == null)
        {
            _myCanvas = gameObject.AddComponent<Canvas>();
        }
        _myCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _myCanvas.sortingOrder = sortingOrder;

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
        // カーソル位置の更新
        if (cursor != null)
        {
            cursor.position = Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
                cursor.localScale = Vector3.one * 0.85f;

            if (Input.GetMouseButtonUp(0))
                cursor.localScale = Vector3.one;
        }

        if (_myCanvas != null && _myCanvas.sortingOrder != sortingOrder)
        {
            _myCanvas.sortingOrder = sortingOrder;
        }
    }

    IEnumerator TrailRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(trailInterval); // ポーズ中でも動く待機

        while (true)
        {
            CreateTrail();
            yield return wait;
        }
    }

    void CreateTrail()
    {
        // ★修正: カーソル自体が非表示なら軌跡も作らない
        if (cursor != null && !cursor.gameObject.activeInHierarchy) return;
        
        if (trailPrefab == null || trailParent == null) return;

        Image trail = Instantiate(trailPrefab, trailParent);
        trail.rectTransform.position = cursor.position;
        trail.raycastTarget = false;

        StartCoroutine(FadeOut(trail));
    }

    IEnumerator FadeOut(Image img)
    {
        float t = 0f;
        Color c = img.color;
        float startAlpha = c.a > 0 ? c.a : 0.5f;

        while (t < trailLifeTime)
        {
            // ★修正: Time.deltaTime だとTimeScale=0の時に止まってしまうため、
            // unscaledDeltaTime (実時間) を使用する
            t += Time.unscaledDeltaTime;

            c.a = Mathf.Lerp(startAlpha, 0f, t / trailLifeTime);
            img.color = c;
            yield return null;
        }

        Destroy(img.gameObject);
    }

    public void SetScale(float scale)
    {
        if(cursor != null) cursor.localScale = Vector3.one * scale;
    }

    public void SetVisible(bool visible)
    {
        if(cursor != null) cursor.gameObject.SetActive(visible);
    }
}