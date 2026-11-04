using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SettlementZones() groups every building with every other (O(n²)), and many systems ask for it
/// every frame. The buildings rarely change, so it's kept until they do: a cheap O(n) fingerprint
/// of the building list (which list, how many, where, what, what level, plus the research count)
/// is checked once a frame, and the zones are only worked out again when it differs.
/// </summary>
public partial class Settlement
{
    private int zonesSignature;
    private List<BuildingState> zonesList;

    private int ZonesSignatureNow()
    {
        unchecked
        {
            int h = State.researched.Count * 486187739;
            foreach (var b in State.buildings)
            {
                h = h * 31 + (b.cell.x * 73856093 ^ b.cell.y * 19349663 ^ b.upgradeLevel * 83492791);
                h = h * 31 + (b.definitionId != null ? b.definitionId.GetHashCode() : 0);
            }
            return h;
        }
    }

    /// <summary>Are the cached zones still right for these buildings?</summary>
    private bool ZonesUnchanged() => ReferenceEquals(zonesList, State.buildings) && ZonesSignatureNow() == zonesSignature;

    private void ZonesRemember()
    {
        zonesList = State.buildings;
        zonesSignature = ZonesSignatureNow();
    }
}
