using UnityEditor;
using UnityEngine;

public static class SettlementTools
{
    [MenuItem("Tools/Age of Stickman/Reset Free Mode Save")]
    private static void ResetSave()
    {
        if (EditorUtility.DisplayDialog(
                "Reset free mode save",
                Settlement.UsingTestSave
                    ? "Delete the TEST save (settlement_test.json)? Your real save is not touched."
                    : "Delete your REAL free mode save (settlement.json)? The settlement starts fresh next time free mode is entered.",
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
