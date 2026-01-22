using UnityEngine;
using UnityEngine.UI;

public class AlphaHitTestFix : MonoBehaviour
{
    void Awake()
    {
        var img = GetComponent<Image>();
        img.alphaHitTestMinimumThreshold = 0.2f;
    }
}
