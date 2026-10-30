using UnityEditor;
using UnityEngine;

public static class SettlementTools
{
    [MenuItem("Tools/Age of Stickman/Reset Free Mode Save")]
    private static void ResetSave()
    {
        if (EditorUtility.DisplayDialog(
                "Reset free mode save",
                "Delete the free mode settlement save? The settlement starts fresh next time free mode is entered.",
                "Delete",
                "Cancel"))
        {
            Settlement.DeleteSave();
            Debug.Log("Free mode save deleted.");
        }
    }

    [MenuItem("Tools/Age of Stickman/Open Save Folder")]
    private static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
