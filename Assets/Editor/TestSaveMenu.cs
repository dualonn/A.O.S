using UnityEditor;
using UnityEngine;

/// <summary>
/// Switch free mode between your real save and a separate test save, so play tests
/// never touch your settlement.
/// </summary>
public static class TestSaveMenu
{
    private const string MenuPath = "Tools/Age of Stickman/Use Test Save";

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        Set(!Settlement.UsingTestSave);
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Settlement.UsingTestSave);
        return !EditorApplication.isPlaying;
    }

    public static void Set(bool on)
    {
        EditorPrefs.SetBool(Settlement.TestSaveKey, on);
        Debug.Log(on ? "Free mode now uses the TEST save (settlement_test.json)." : "Free mode now uses your real save (settlement.json).");
    }
}
