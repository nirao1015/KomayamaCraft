using System;
using DG.Tweening;
using UnityEngine;

namespace DialogueScene
{
    /// <summary>
    /// 会話シーンの背景 Image 用揺れ（1 ショット / 継続）。CSV の fx_shake* から駆動。
    /// 揺れ前に拡大し、終了時は位置・スケールを必ず基準値へ戻す（揺れ tween は途中で Kill しない）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueBackgroundShakeEffect : MonoBehaviour
    {
        private const int TweenIdScale = 71001;

        [Header("拡大（隙間防止）")]
        [SerializeField] private float shakeScaleMultiplier = 1.06f;
        [SerializeField] private float scaleTweenSeconds = 0.1f;

        [Header("Hit（1 ショット・被弾）")]
        [SerializeField] private float hitDurationSeconds = 0.35f;
        [SerializeField] private float hitStrengthPixels = 28f;
        [SerializeField] private int hitVibrato = 20;

        [Header("Descent（継続・降下 G）")]
        [SerializeField] private float descentStrengthPixels = 14f;
        [SerializeField] private float descentFrequencyHz = 2.4f;
        [SerializeField] private float descentSettleSeconds = 0.18f;

        private RectTransform _target;
        private Vector2 _baseAnchoredPosition;
        private Vector3 _baseLocalScale = Vector3.one;

        private int _effectGeneration;
        private bool _continuousActive;
        private bool _hitShakeActive;
        private bool _restoreAfterHitCompletes;
        private bool _restoreAfterContinuousStop;

        public void Bind(RectTransform backgroundRect)
        {
            _target = backgroundRect;
            ShutdownImmediate();
            CaptureRestPose();
        }

        /// <summary>
        /// シーン終了・スキップ時用。新規 tween を作らず、揺れ・拡大を即停止して基準姿勢へ戻す。
        /// </summary>
        public void ShutdownImmediate()
        {
            _effectGeneration++;
            _continuousActive = false;
            _hitShakeActive = false;
            _restoreAfterHitCompletes = false;
            _restoreAfterContinuousStop = false;

            if (_target == null)
            {
                return;
            }

            _target.DOKill();
            DOTween.Kill(_target);
            ApplyRestPose();
        }

        public void PlayOneShot(string preset)
        {
            if (_target == null)
            {
                return;
            }

            string key = string.IsNullOrWhiteSpace(preset) ? "hit" : preset.Trim().ToLowerInvariant();
            if (key != "hit")
            {
                Debug.LogWarning($"[DialogueBackgroundShakeEffect] Unknown shake preset '{preset}', using hit.");
            }

            if (_hitShakeActive)
            {
                _restoreAfterHitCompletes = true;
                return;
            }

            EndContinuousOffsetOnly();
            BeginEffectSession();
            _restoreAfterContinuousStop = false;

            RunScaleUpThen(RunHitShake);
        }

        public void StartContinuous(string preset)
        {
            if (_target == null)
            {
                return;
            }

            string key = string.IsNullOrWhiteSpace(preset) ? "descent" : preset.Trim().ToLowerInvariant();
            if (key != "descent")
            {
                Debug.LogWarning($"[DialogueBackgroundShakeEffect] Unknown continuous preset '{preset}', using descent.");
            }

            if (_hitShakeActive)
            {
                _restoreAfterHitCompletes = true;
                _restoreAfterContinuousStop = true;
                return;
            }

            BeginEffectSession();
            _restoreAfterContinuousStop = false;

            RunScaleUpThen(() => _continuousActive = true);
        }

        public void StopAll()
        {
            if (_target == null)
            {
                return;
            }

            if (_hitShakeActive)
            {
                _continuousActive = false;
                _restoreAfterHitCompletes = true;
                _restoreAfterContinuousStop = true;
                return;
            }

            if (_continuousActive)
            {
                _continuousActive = false;
                _restoreAfterContinuousStop = true;
                BeginRestoreFromContinuous();
                return;
            }

            BeginRestoreImmediate();
        }

        private void EndContinuousOffsetOnly()
        {
            _continuousActive = false;
            if (_target != null)
            {
                _target.anchoredPosition = _baseAnchoredPosition;
            }
        }

        private void BeginEffectSession()
        {
            _effectGeneration++;
            _restoreAfterHitCompletes = false;
            KillScaleTweensOnly();
            ApplyRestPose();
        }

        private void RunScaleUpThen(Action onScaledUp)
        {
            int gen = _effectGeneration;
            Vector3 scaled = _baseLocalScale * Mathf.Max(1f, shakeScaleMultiplier);
            _target
                .DOScale(scaled, Mathf.Max(0.02f, scaleTweenSeconds))
                .SetId(TweenIdScale)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (gen != _effectGeneration || _target == null)
                    {
                        return;
                    }

                    onScaledUp?.Invoke();
                });
        }

        private void RunHitShake()
        {
            if (_target == null)
            {
                return;
            }

            int gen = _effectGeneration;
            _hitShakeActive = true;
            Vector2 strength = new Vector2(hitStrengthPixels, hitStrengthPixels * 0.85f);
            _target
                .DOShakeAnchorPos(
                    Mathf.Max(0.05f, hitDurationSeconds),
                    strength,
                    Mathf.Max(1, hitVibrato),
                    90f,
                    false,
                    true)
                .SetUpdate(true)
                .OnComplete(() => OnHitShakeComplete(gen));
        }

        private void OnHitShakeComplete(int generation)
        {
            if (_target == null || generation != _effectGeneration || !isActiveAndEnabled)
            {
                return;
            }

            _hitShakeActive = false;
            _restoreAfterHitCompletes = false;
            BeginRestoreImmediate();
        }

        private void BeginRestoreFromContinuous()
        {
            int gen = _effectGeneration;
            _target
                .DOAnchorPos(_baseAnchoredPosition, Mathf.Max(0.02f, descentSettleSeconds))
                .SetId(TweenIdScale)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (gen != _effectGeneration || _target == null)
                    {
                        return;
                    }

                    BeginRestoreImmediate();
                });
        }

        private void BeginRestoreImmediate()
        {
            if (_target == null || !isActiveAndEnabled)
            {
                return;
            }

            int gen = _effectGeneration;
            KillScaleTweensOnly();

            float duration = Mathf.Max(0.02f, scaleTweenSeconds);
            Sequence seq = DOTween.Sequence()
                .SetId(TweenIdScale)
                .SetUpdate(true);
            seq.Join(_target.DOAnchorPos(_baseAnchoredPosition, duration).SetEase(Ease.OutQuad));
            seq.Join(_target.DOScale(_baseLocalScale, duration).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                if (gen != _effectGeneration || _target == null)
                {
                    return;
                }

                ApplyRestPose();
                _restoreAfterContinuousStop = false;
            });
        }

        private void KillScaleTweensOnly()
        {
            if (_target != null)
            {
                DOTween.Kill(_target, TweenIdScale);
            }
        }

        private void LateUpdate()
        {
            if (!_continuousActive || _target == null || _hitShakeActive)
            {
                return;
            }

            float t = Time.unscaledTime * Mathf.Max(0.01f, descentFrequencyHz);
            Vector2 offset = new Vector2(
                Mathf.Sin(t * Mathf.PI * 2f) * descentStrengthPixels,
                Mathf.Cos(t * Mathf.PI * 2f * 0.73f) * descentStrengthPixels * 0.65f);
            _target.anchoredPosition = _baseAnchoredPosition + offset;
        }

        private void CaptureRestPose()
        {
            if (_target == null)
            {
                return;
            }

            _baseAnchoredPosition = _target.anchoredPosition;
            _baseLocalScale = _target.localScale;
        }

        private void ApplyRestPose()
        {
            if (_target == null)
            {
                return;
            }

            _target.anchoredPosition = _baseAnchoredPosition;
            _target.localScale = _baseLocalScale;
        }

        private void OnDisable()
        {
            ShutdownImmediate();
        }
    }
}
