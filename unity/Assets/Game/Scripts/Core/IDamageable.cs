namespace Game.Core
{
    /// <summary>
    /// Implemented by anything a bullet can meaningfully hit. Hit resolution
    /// looks for this on the collider's rigidbody or its parents, so a ragdoll
    /// made of many colliders can route every hit to one Health component.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>Returns the damage actually applied after armour and clamping.</summary>
        float ApplyDamage(in DamageInfo info);
    }
}
