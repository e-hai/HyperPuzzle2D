using UnityEngine;

namespace HyperPuzzle2D.Board
{
    /// <summary>
    /// Physical facts for one projectile/block contact. Keeping this independent from feedback
    /// lets damage, scoring and presentation all react to the same measured impact.
    /// </summary>
    public readonly struct ImpactEvent
    {
        public readonly Vector2 Point;
        public readonly Vector2 Normal;
        public readonly Vector2 Velocity;
        public readonly float Energy;
        public readonly bool IsExplosion;

        public ImpactEvent(Vector2 point, Vector2 normal, Vector2 velocity, float energy, bool isExplosion = false)
        {
            Point = point;
            Normal = normal;
            Velocity = velocity;
            Energy = Mathf.Max(0f, energy);
            IsExplosion = isExplosion;
        }
    }

    public enum DestructionMaterial
    {
        Normal,
        Brittle,
        Heavy,
        Support,
        Beam,
        Ball,
        Explosive,
    }
}
