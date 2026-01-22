using UnityEngine;
using UnityEngine.EventSystems;

public class MahjongTile : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int TileId { get; private set; }
    public int OwnerSeatIndex { get; private set; } // NetworkIdから座席番号(int)に変更

    private MahjongPlayer _ownerPlayer;
    private float _zDistanceToCamera;

    // ダブルクリック判定用
    private float _lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.4f;

    private void Awake() // FusionのSpawned()の代わりにAwakeを使用
    {
        if (GetComponent<Collider>() == null)
        {
            // 物理演算対策のためTriggerにする
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
        }
        else
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }

    // NetworkId ではなく SeatIndex を受け取るように変更
    public void Initialize(int id, int ownerSeatIndex)
    {
        TileId = id;
        OwnerSeatIndex = ownerSeatIndex;
        _ownerPlayer = null; // キャッシュクリア
    }

    private MahjongPlayer GetOwnerPlayer()
    {
        if (_ownerPlayer == null)
        {
            // GameManager経由で座席番号からプレイヤーを探す
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

    // --- クリック処理 ---
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;

        // ダブルクリック判定
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
        // HasInputAuthority の代わりに IsHuman をチェック（人間のみ操作可能）
        if (player != null && player.IsHuman)
        {
            // IDではなくインスタンス自身を渡す
            player.RequestDiscard(this);
        }
    }

    // --- ドラッグ処理 ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        var player = GetOwnerPlayer();
        if (player == null || !player.IsHuman) return; // 人間以外はドラッグ不可
        if (Camera.main == null) return;

        _zDistanceToCamera = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
        
        // ドラッグ開始通知
        player.StartDraggingTile(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        var player = GetOwnerPlayer();
        if (player == null || !player.IsHuman) return;

        // マウスのワールドX座標だけを計算してPlayerに渡す
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

    private Vector3 GetMouseWorldPos(Vector2 screenPos)
    {
        if (Camera.main == null) return Vector3.zero;
        Vector3 mousePoint = new Vector3(screenPos.x, screenPos.y, _zDistanceToCamera);
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}