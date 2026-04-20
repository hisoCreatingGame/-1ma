using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections; // 追加

public class MahjongTile : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int TileId { get; private set; }
    public int OwnerSeatIndex { get; private set; } 

    private MahjongPlayer _ownerPlayer;
    private float _zDistanceToCamera;

    private float _lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.4f;

    private bool _isInteractable = true;

    // ★追加: アニメーション中かどうか
    public bool IsAnimating { get; private set; } = false;

    // ★追加: 発光エフェクト用のTrailRenderer
    private TrailRenderer _trail;


    public void Initialize(int id, int ownerSeatIndex)
    {
        TileId = id;
        OwnerSeatIndex = ownerSeatIndex;
        // _ownerPlayer = null; // 必要であれば
        // SetDarkened(false);  // 必要であれば
        
        // ★初期化時にTrailがあれば無効化しておく
        if (_trail != null) _trail.enabled = false;
    }

    // ★追加: 外部（GameManager）から光の軌跡をONにするメソッド
    public void EnableTrailEffect(bool enable)
    {
        if (enable)
        {
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();

            // 光る軌跡の設定（ゴールド風）
            _trail.time = 0.5f; // 残存時間
            _trail.startWidth = 1.0f; 
            _trail.endWidth = 0.0f;
            _trail.material = new Material(Shader.Find("Sprites/Default")); 
            _trail.startColor = new Color(1f, 0.9f, 0.4f, 0.8f); // 金色っぽい光
            _trail.endColor = new Color(1f, 0.5f, 0f, 0f);
            
            _trail.Clear();
            _trail.enabled = true;
        }
        else
        {
            if (_trail != null) _trail.enabled = false;
        }
    }

    private void Awake() 
    {
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
        }
        else
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }


    // ★追加: ツモ時の落下＆発光アニメーション
    public void PlayTsumoDropAnimation(Vector3 targetPos)
    {
        if (IsAnimating) return;
        StartCoroutine(DropRoutine(targetPos));
    }

    private IEnumerator DropRoutine(Vector3 targetPos)
    {
        IsAnimating = true;
        _isInteractable = false; // 落下中は操作不可

        // 1. TrailRenderer（光る軌跡）のセットアップ
        if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
        
        _trail.time = 0.3f; // 軌跡が残る時間
        _trail.startWidth = 1.0f; // 牌の幅くらい
        _trail.endWidth = 0.0f;
        _trail.material = new Material(Shader.Find("Sprites/Default")); // 発光しやすいシェーダー
        _trail.startColor = new Color(1f, 1f, 0.5f, 0.8f); // 薄い黄色（光）
        _trail.endColor = new Color(1f, 0.8f, 0f, 0f);
        _trail.enabled = true;
        _trail.Clear();

        // 2. 開始位置の設定（上空）
        float dropHeight = 15.0f; // 落ちてくる高さ
        transform.position = targetPos + Vector3.up * dropHeight;
        
        // 3. 落下アニメーション（重力加速風）
        float duration = 0.4f; // 落下にかかる時間
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // イージング（EaseInQuad: 徐々に加速）
            float easeT = t * t; 

            transform.position = Vector3.Lerp(startPos, targetPos, easeT);
            
            // 落下に合わせて回転も少し戻す（出現時は少し傾けておくなど）
            // ここではシンプルに位置のみ動かします
            
            yield return null;
        }

        // 4. 着地後の後始末
        transform.position = targetPos;
        yield return new WaitForSeconds(0.1f); // 少し余韻
        
        if (_trail != null) _trail.enabled = false; // 軌跡をオフ
        
        IsAnimating = false;
        _isInteractable = true; // 操作可能に戻す
    }

    public void SetDarkened(bool isDarkened)
    {
        _isInteractable = !isDarkened;
        Color targetColor = isDarkened ? Color.gray : Color.white;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.material.color = targetColor;
        }
        if (TryGetComponent<Renderer>(out var selfR))
        {
            selfR.material.color = targetColor;
        }
    }

    private MahjongPlayer GetOwnerPlayer()
    {
        if (_ownerPlayer == null)
        {
            if (MahjongGameManager.Instance != null && MahjongGameManager.Instance.connectedPlayers != null)
            {
                foreach (var p in MahjongGameManager.Instance.connectedPlayers)
                {
                    if (p.Seat == OwnerSeatIndex)
                    {
                        _ownerPlayer = p;
                        break;
                    }
                }
            }
        }
        return _ownerPlayer;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isInteractable) return; 
        if (eventData.dragging) return;

        DiscardThisTile();

        /*
        if (Time.time - _lastClickTime <= DOUBLE_CLICK_THRESHOLD)
        {
            DiscardThisTile();
            _lastClickTime = 0f;
        }
        else
        {
            _lastClickTime = Time.time;
        }
        */
    }

    private void DiscardThisTile()
    {
        var player = GetOwnerPlayer();
        if (player != null && player.IsHuman)
        {
            player.RequestDiscard(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_isInteractable) return; 
        
        var player = GetOwnerPlayer();
        if (player == null || !player.IsHuman) return; 
        if (Camera.main == null) return;

        _zDistanceToCamera = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
        
        player.StartDraggingTile(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isInteractable) return; 

        var player = GetOwnerPlayer();
        if (player == null || !player.IsHuman) return;

        Vector3 mouseWorldPos = GetMouseWorldPos(eventData.position);
        player.OnTileDragging(this, mouseWorldPos.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var player = GetOwnerPlayer();
        if (player != null && player.IsHuman)
        {
            player.StopDraggingTile(this);
        }
    }

    // MahjongTile.cs 180行目付近を修正
private Vector3 GetMouseWorldPos(Vector2 screenPos)
{
    if (Camera.main == null) return Vector3.zero;

    // カメラから牌がある平面（XZ平面）へのレイキャスト
    Ray ray = Camera.main.ScreenPointToRay(screenPos);
    // 牌の現在の高さ(transform.position.y)で水平な面を定義
    Plane tilePlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
    
    if (tilePlane.Raycast(ray, out float distance))
    {
        return ray.GetPoint(distance);
    }
    
    return Vector3.zero;
}

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable) return; 

        var player = GetOwnerPlayer();
        if (player != null && player.IsHuman)
        {
            player.OnTileHoverEnter(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var player = GetOwnerPlayer();
        if (player != null && player.IsHuman)
        {
            player.OnTileHoverExit(this);
        }
    }
}