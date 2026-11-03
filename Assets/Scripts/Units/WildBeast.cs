using UnityEngine;

/// <summary>
/// A wild creature fighting for real: a herd member that your people came too close to. It keeps
/// to its herd's ground (guard mode) and goes back to being part of the herd once things calm down.
/// When one falls, its herd is one smaller and the meat comes home.
/// </summary>
public class WildBeast : Enemy
{
    /// <summary>The herd it belongs to (see Settlement.Wild).</summary>
    public int HerdId { get; set; }
    public string Species { get; set; }

    protected override void Die()
    {
        base.Die();
        WildlifeView.Instance?.BeastDied(this);
    }
}
