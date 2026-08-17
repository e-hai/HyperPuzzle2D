using System.Collections;
using HyperPuzzle2D.Art;
using HyperPuzzle2D.Board;
using UnityEngine;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>
    /// One strength-driven presentation gateway. Gameplay reports measured impacts here instead
    /// of scattering fixed particle/shake/haptic constants through the director.
    /// </summary>
    public sealed class ImpactFeedback : MonoBehaviour
    {
        Transform _effectsRoot;
        bool _hitStopRunning;

        public void Bind(Transform effectsRoot)
        {
            _effectsRoot = effectsRoot;
        }

        public void PlayContact(ImpactEvent impact, Color tint)
        {
            var strength = Mathf.Clamp01(impact.Energy / 130f);
            var direction = impact.Velocity.sqrMagnitude > 0.01f ? impact.Velocity.normalized : -impact.Normal;
            HitBurst.PlayDirectional(_effectsRoot, impact.Point, tint, 5 + Mathf.RoundToInt(strength * 7f), 3.5f + strength * 3f, direction, 100f);
            if (strength > 0.45f)
            {
                HitBurst.PlayRing(_effectsRoot, impact.Point, Palette.Projectile, 0.55f + strength * 0.55f);
            }

            CameraShake.Instance?.Shake(Mathf.Lerp(0.07f, 0.20f, strength));
            Sfx.Instance?.Hit(strength);
            if (strength > 0.7f) Haptics.Medium();
            else Haptics.Light();
        }

        public void PlayBreak(Vector3 position, Color tint, int chain)
        {
            HitBurst.Play(_effectsRoot, position, tint, 10 + Mathf.Min(chain, 6) * 2, 5.5f + chain * 0.3f);
            HitBurst.PlayRing(_effectsRoot, position, tint, 0.9f + Mathf.Min(chain, 5) * 0.12f);
            CameraShake.Instance?.Shake(Mathf.Min(0.32f, 0.14f + chain * 0.025f));
            Sfx.Instance?.Break(chain);
            if (chain >= 4) Haptics.Heavy();
            else Haptics.Medium();
            StartHitStop(chain >= 4 ? 0.065f : 0.04f);
        }

        public void PlayExplosion(Vector3 position, int chain)
        {
            HitBurst.Play(_effectsRoot, position, Palette.ExplosionCore, 24, 8.5f);
            HitBurst.PlayRing(_effectsRoot, position, Palette.Explosive, 2.2f);
            CameraShake.Instance?.Shake(Mathf.Min(0.46f, 0.3f + chain * 0.025f));
            Sfx.Instance?.Explosion(chain);
            Haptics.Heavy();
            StartHitStop(0.07f);
        }

        public void PlayKnockOff(Vector3 position, Color tint, int chain)
        {
            HitBurst.Play(_effectsRoot, position, tint, 5, 3.8f);
            Sfx.Instance?.Break(chain);
            CameraShake.Instance?.Shake(0.07f);
        }

        void StartHitStop(float seconds)
        {
            if (!_hitStopRunning)
            {
                StartCoroutine(HitStop(seconds));
            }
        }

        IEnumerator HitStop(float seconds)
        {
            // Skip while paused: freezing at 0.08 and restoring would fight the pause panel.
            if (Time.timeScale <= 0.001f)
            {
                yield break;
            }

            _hitStopRunning = true;
            Time.timeScale = 0.08f;
            yield return new WaitForSecondsRealtime(seconds);
            // Honour a pause that opened during the freeze instead of snapping back to 1.
            Time.timeScale = Core.GameDirector.Instance != null && Core.GameDirector.Instance.IsPaused
                ? 0f
                : 1f;
            _hitStopRunning = false;
        }

        void OnDisable()
        {
            if (_hitStopRunning)
            {
                Time.timeScale = Core.GameDirector.Instance != null && Core.GameDirector.Instance.IsPaused
                    ? 0f
                    : 1f;
                _hitStopRunning = false;
            }
        }
    }
}
