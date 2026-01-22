using UnityEngine;
using System.Collections;
using UnityEngine.UI; // Required when Using UI elements.

public class ButtonImage : MonoBehaviour
{
    public Image theButton;

    // Use this for initialization
    void Start()
    {
        theButton.alphaHitTestMinimumThreshold = 0.5f;
    }
}