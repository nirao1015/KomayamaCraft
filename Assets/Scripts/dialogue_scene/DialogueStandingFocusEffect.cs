using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueScene
{
    /// <summary>
    /// L/R 立ち絵が両方表示されているとき、話していない側の <see cref="Image.color"/> を暗くする（スプライト形状のまま）。
    /// </summary>
    public sealed class DialogueStandingFocusEffect
    {
        private enum Slot
        {
            Left,
            Right
        }

        private static readonly Color ActiveTint = Color.white;

        private static readonly HashSet<string> NonSwitchingSpeakers = new HashSet<string>(StringComparer.Ordinal)
        {
            "語り部",
            "歓声",
            "司会",
            "会場アナウンス",
            "?????",
        };

        private readonly bool _enabled;
        private readonly Color _dimTint;
        private readonly HashSet<string> _defaultRightSpeakers;
        private readonly Dictionary<string, Slot> _learnedSpeakerSlot = new Dictionary<string, Slot>(StringComparer.Ordinal);

        private Image _leftStanding;
        private Image _rightStanding;
        private string _lastFocusedSpeaker;

        public DialogueStandingFocusEffect(bool enabled, Color dimTint, string[] defaultRightSpeakers)
        {
            _enabled = enabled;
            _dimTint = dimTint;
            _defaultRightSpeakers = new HashSet<string>(StringComparer.Ordinal);
            if (defaultRightSpeakers != null)
            {
                foreach (string name in defaultRightSpeakers)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _defaultRightSpeakers.Add(name.Trim());
                    }
                }
            }
        }

        public void Bind(Image leftStanding, Image rightStanding)
        {
            _leftStanding = leftStanding;
            _rightStanding = rightStanding;
            RemoveLegacyDimOverlays(_leftStanding);
            RemoveLegacyDimOverlays(_rightStanding);
            ClearDim();
        }

        public void ApplyForLine(string speaker)
        {
            if (!_enabled || _leftStanding == null || _rightStanding == null)
            {
                return;
            }

            bool leftOn = _leftStanding.gameObject.activeSelf;
            bool rightOn = _rightStanding.gameObject.activeSelf;
            string trimmed = speaker?.Trim() ?? string.Empty;

            LearnSpeakerSlot(trimmed, leftOn, rightOn);

            if (!leftOn || !rightOn)
            {
                ClearDim();
                return;
            }

            if (ShouldKeepPreviousFocus(trimmed))
            {
                ApplySlot(ResolveSlot(_lastFocusedSpeaker));
                return;
            }

            Slot? active = ResolveSlot(trimmed);
            ApplySlot(active);
            if (active.HasValue)
            {
                _lastFocusedSpeaker = trimmed;
            }
        }

        public void RefreshAfterStandChange()
        {
            if (string.IsNullOrEmpty(_lastFocusedSpeaker))
            {
                ClearDim();
                return;
            }

            ApplyForLine(_lastFocusedSpeaker);
        }

        private void LearnSpeakerSlot(string speaker, bool leftOn, bool rightOn)
        {
            if (string.IsNullOrEmpty(speaker))
            {
                return;
            }

            if (ShouldKeepPreviousFocus(speaker))
            {
                return;
            }

            if (leftOn && !rightOn)
            {
                _learnedSpeakerSlot[speaker] = Slot.Left;
            }
            else if (rightOn && !leftOn)
            {
                _learnedSpeakerSlot[speaker] = Slot.Right;
            }
        }

        private static bool ShouldKeepPreviousFocus(string speaker)
        {
            if (string.IsNullOrEmpty(speaker))
            {
                return true;
            }

            return NonSwitchingSpeakers.Contains(speaker);
        }

        private Slot? ResolveSlot(string speaker)
        {
            if (string.IsNullOrEmpty(speaker) || ShouldKeepPreviousFocus(speaker))
            {
                return null;
            }

            if (_learnedSpeakerSlot.TryGetValue(speaker, out Slot learned))
            {
                return learned;
            }

            if (_defaultRightSpeakers.Contains(speaker))
            {
                return Slot.Right;
            }

            // R 既定以外は L キャラ（仲間）とみなす
            return Slot.Left;
        }

        private void ApplySlot(Slot? active)
        {
            SetStandingTint(_leftStanding, active.HasValue && active.Value == Slot.Left);
            SetStandingTint(_rightStanding, active.HasValue && active.Value == Slot.Right);
        }

        private void ClearDim()
        {
            SetStandingTint(_leftStanding, true);
            SetStandingTint(_rightStanding, true);
        }

        private void SetStandingTint(Image standing, bool active)
        {
            if (standing == null)
            {
                return;
            }

            standing.color = active ? ActiveTint : _dimTint;
        }

        private static void RemoveLegacyDimOverlays(Image standing)
        {
            if (standing == null)
            {
                return;
            }

            Transform root = standing.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == "DimOverlay")
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }
    }
}
