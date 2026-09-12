using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-assignable bundle for the FallPod / StageStart pod descent UI hierarchy.
/// </summary>
[Serializable]
public struct PodSequenceVisuals
{
    public RectTransform panelRoot;
    public RectTransform fallPodOj;
    public RectTransform podGroundOj;
    public Image podImage;
    public Image podFireImage;
    public Image podC2Image;
    public RectTransform podShadowRect;
    public Image podC1Image;
    public Image podGroundImage;
    public RectTransform podUnitRect;
    public Image podUnitImage;
    public RectTransform podUnitShadowRect;
    public Image podUnitShadowImage;
    public RectTransform fallPodWaitOj;

    public readonly bool IsValidForSequence()
    {
        return panelRoot != null
               && fallPodOj != null
               && podGroundOj != null
               && podImage != null
               && podShadowRect != null
               && podUnitRect != null
               && podUnitImage != null;
    }
}
