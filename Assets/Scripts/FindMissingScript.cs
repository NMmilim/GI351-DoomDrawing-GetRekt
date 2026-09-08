using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


public static class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts In Scene")]
    public static void FindInScene()
    {
        var results = new List<Object>();
        GameObject[] all = Object.FindObjectsOfType<GameObject>();
        foreach (var go in all)
        {
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    Debug.LogWarning($"Missing script on: {GetHierarchyPath(go)} (gameObject), component index: {i}", go);
                    results.Add(go);
                }
            }
        }

        if (results.Count == 0)
            Debug.Log("No missing scripts found in the open scenes.");
        else
            Selection.objects = results.ToArray();
    }

    [MenuItem("Tools/Find Missing Scripts In Project (Prefabs)")]
    public static void FindInProjectPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        var results = new List<Object>();

        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var components = prefab.GetComponentsInChildren<Component>(true);
            foreach (var comp in components)
            {
                if (comp == null)
                {
                    Debug.LogWarning($"Missing script in prefab: {path}", prefab);
                    results.Add(prefab);
                    break;
                }
            }
        }

        if (results.Count == 0)
            Debug.Log("No missing scripts found in project prefabs.");
        else
            Selection.objects = results.ToArray();
    }

    private static string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}