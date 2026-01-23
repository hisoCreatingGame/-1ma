using UnityEngine;

public class CameraZoomController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 20f; // ズームの限界（寄り）
    [SerializeField] private float maxZoom = 60f; // ズームの限界（引き）

    // ボタンから呼び出す用：ズームイン（寄る）
    public void ZoomIn()
    {
        float newSize = mainCamera.fieldOfView - zoomSpeed;
        mainCamera.fieldOfView = Mathf.Clamp(newSize, minZoom, maxZoom);
    }

    // ボタンから呼び出す用：ズームアウト（引く）
    public void ZoomOut()
    {
        float newSize = mainCamera.fieldOfView + zoomSpeed;
        mainCamera.fieldOfView = Mathf.Clamp(newSize, minZoom, maxZoom);
    }
}