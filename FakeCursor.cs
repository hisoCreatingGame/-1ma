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

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        Cursor.visible = false;
        StartCoroutine(TrailRoutine());
    }
    void Update()
    {
        cursor.position = Input.mousePosition;

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
        Image trail = Instantiate(trailPrefab, trailParent);
        trail.rectTransform.position = cursor.position;
        StartCoroutine(FadeOut(trail));
    }

    IEnumerator FadeOut(Image img)
    {
        float t = 0f;
        Color c = img.color;

        while (t < trailLifeTime)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0.5f, 0f, t / trailLifeTime);
            img.color = c;
            yield return null;
        }

        Destroy(img.gameObject);
    }

    // 外部からサイズ変更
    public void SetScale(float scale)
    {
        cursor.localScale = Vector3.one * scale;
    }

    // 外部から表示ON/OFF
    public void SetVisible(bool visible)
    {
        cursor.gameObject.SetActive(visible);
    }
}
