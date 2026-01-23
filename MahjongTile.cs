using UnityEngine;
using UnityEngine.EventSystems;

public class MahjongTile : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int TileId { get; private set; }
    public int OwnerSeatIndex { get; private set; } 

    private MahjongPlayer _ownerPlayer;
    private float _zDistanceToCamera;

    private float _lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.4f;

    // ★追加: 操作可能フラグ
    private bool _isInteractable = true;

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

    public void Initialize(int id, int ownerSeatIndex)
    {
        TileId = id;
        OwnerSeatIndex = ownerSeatIndex;
        _ownerPlayer = null; 
        _isInteractable = true; // 初期化時は操作可能
        SetDarkened(false);
    }

    // ★追加: 牌を暗くして操作不能にするメソッド
    public void SetDarkened(bool isDarkened)
    {
        _isInteractable = !isDarkened;
        
        // 色を変更（暗くする＝グレー、通常＝白）
        Color targetColor = isDarkened ? Color.gray : Color.white;
        
        // 子要素のレンダラーも含めて色を変える
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
        if (!_isInteractable) return; // ★操作不可なら無視
        if (eventData.dragging) return;

        if (Time.time - _lastClickTime <= DOUBLE_CLICK_THRESHOLD)
        {
            DiscardThisTile();
            _lastClickTime = 0f;
        }
        else
        {
            _lastClickTime = Time.time;
        }
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
        if (!_isInteractable) return; // ★操作不可なら無視
        
        var player = GetOwnerPlayer();
        if (player == null || !player.IsHuman) return; 
        if (Camera.main == null) return;

        _zDistanceToCamera = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
        
        player.StartDraggingTile(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isInteractable) return; // ★操作不可なら無視

        var player = GetOwnerPlayer();
        if (player == null || !player.IsHuman) return;

        Vector3 mouseWorldPos = GetMouseWorldPos(eventData.position);
        player.OnTileDragging(this, mouseWorldPos.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // EndDragはBeginDragが呼ばれていれば呼ばれるため、ここでのチェックは必須ではないが念のため
        var player = GetOwnerPlayer();
        if (player != null && player.IsHuman)
        {
            player.StopDraggingTile(this);
        }
    }

    private Vector3 GetMouseWorldPos(Vector2 screenPos)
    {
        if (Camera.main == null) return Vector3.zero;
        Vector3 mousePoint = new Vector3(screenPos.x, screenPos.y, _zDistanceToCamera);
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable) return; // ★操作不可ならホバーもしない

        var player = GetOwnerPlayer();
        if (player != null && player.IsHuman)
        {
            player.OnTileHoverEnter(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Exitは常に通す（ホバー解除漏れを防ぐため）
        var player = GetOwnerPlayer();
        if (player != null && player.IsHuman)
        {
            player.OnTileHoverExit(this);
        }
    }
}