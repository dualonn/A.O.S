using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The guided start: which beginner hints have been shown, and whether they're on at all.
/// Old saves load with hints off (on = false); only a brand-new game turns them on.
/// </summary>
[Serializable]
public class GuideState
{
    [Tooltip("Show the small beginner hints (off for saves made before hints existed).")]
    public bool on;

    [Tooltip("The hints' on/off has been settled for this save (so turning them off sticks).")]
    public bool decided;

    [Tooltip("Ids of hints already shown (each is shown once).")]
    public List<string> seen = new List<string>();
}

public partial class Settlement
{
    /// <summary>The guided start's saved state (never null).</summary>
    public GuideState Guide
    {
        get
        {
            if (State.guide == null) State.guide = new GuideState();
            if (State.guide.seen == null) State.guide.seen = new List<string>();
            return State.guide;
        }
    }
}
