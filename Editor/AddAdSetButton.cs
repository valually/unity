using UnityEngine;
using UnityEditor;

public class AddAdSetButton
{
    [MenuItem("Component/Valually/Create Ad Space")]
    private static void CreateAdSpaceToScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.valually.valually/Runtime/Prefabs/AdSpace.prefab");
        if (prefab != null)
        {
            GameObject obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (obj != null)
            {
                Undo.RegisterCreatedObjectUndo(obj, "Create Ad Space");
                Selection.activeGameObject = obj;
            }
        }
    }
}
