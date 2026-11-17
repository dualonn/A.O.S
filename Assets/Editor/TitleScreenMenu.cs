using UnityEditor;
using UnityEngine;

/// <summary>
/// For play tests in the editor: go straight into the game instead of the title screen.
/// (Built games always open on the title screen.)
/// </summary>
public static class TitleScreenMenu
{
    private const string MenuPath = "Tools/Age of Stickman/Skip Title Screen";

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        bool on = !EditorPrefs.GetBool(Settlement.SkipTitleKey, false);
        EditorPrefs.SetBool(Settlement.SkipTitleKey, on);
        Debug.Log(on ? "Playing in the editor now skips the title screen." : "Playing in the editor now opens on the title screen.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(Settlement.SkipTitleKey, false));
        return !EditorApplication.isPlaying;
    }
}
