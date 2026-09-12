using UnityEngine;

/// <summary>
/// シーン開始時に <see cref="Game03PodManager.TryBeginStageStartSequence"/> を起動する。
/// Game03BgmManager の Start（BGM 再生）より後で実行されるよう Script Execution Order を遅めにしておくこと。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03StageStartPresentation : MonoBehaviour
{
    [SerializeField] private Game03PodManager podManager;
    [SerializeField] private bool playOnStart = true;

    private void Start()
    {
        if (podManager == null)
        {
            podManager = GetComponent<Game03PodManager>();
        }

        if (playOnStart && podManager != null)
        {
            podManager.TryBeginStageStartSequence();
        }
    }
}
