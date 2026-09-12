using UnityEngine;

/// <summary>
/// menu02_scene の EffectOj 配下 BuzzEffect03 を、
/// シーン開始から常時再生（終了条件なし）で駆動する。
/// </summary>
[DisallowMultipleComponent]
public sealed class Menu02EffectOjAutoRunner : MonoBehaviour
{
    [SerializeField] private WorkMovieBuzzEffect03Controller buzzEffect03Controller;

    public static void EnsureSceneController()
    {
        // menu02_scene では事前アタッチ運用に統一するため、
        // 自動探索/自動追加は行わない。
    }

    private void Awake()
    {
        EnsurePlaying();
    }

    private void OnEnable()
    {
        EnsurePlaying();
    }

    private void Update()
    {
        if (buzzEffect03Controller == null)
        {
            return;
        }

        buzzEffect03Controller.ManualTick(Time.deltaTime);
    }

    private void EnsurePlaying()
    {
        if (buzzEffect03Controller == null)
        {
            return;
        }

        if (!buzzEffect03Controller.enabled)
        {
            buzzEffect03Controller.enabled = true;
        }

        buzzEffect03Controller.SetBuzzState(true, false);
    }
}
