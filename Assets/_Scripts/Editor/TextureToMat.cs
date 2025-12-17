using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

public class TextureToMat : EditorWindow
{
    private Vector2 scroll;
    private List<string> validDirs = new List<string>();
    private List<bool> selected = new List<bool>();
    private string log = "";

    [MenuItem("Tools/Resources Texture Converter")]
    public static void OpenWindow()
    {
        GetWindow<TextureToMat>("Resources Converter");
    }

    private void OnEnable()
    {
        RefreshDirectories();
    }

    private void RefreshDirectories()
    {
        validDirs.Clear();
        selected.Clear();

        // Get all directories under Assets/Resources
        string root = Application.dataPath + "/Resources";
        if (!Directory.Exists(root))
        {
            log = "No Resources folder found under Assets.";
            return;
        }

        foreach (string dir in Directory.GetDirectories(root))
        {
            string texturesPath = Path.Combine(dir, "Textures");
            string matsPath = Path.Combine(dir, "Mats");

            if (Directory.Exists(texturesPath) && Directory.Exists(matsPath))
            {
                string unityPath = "Assets" + dir.Replace(Application.dataPath, "").Replace('\\', '/');
                validDirs.Add(unityPath);
                selected.Add(false);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Valid Resource Subfolders (with Textures & Mats)", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh List"))
        {
            RefreshDirectories();
        }

        scroll = GUILayout.BeginScrollView(scroll);

        for (int i = 0; i < validDirs.Count; i++)
        {
            selected[i] = GUILayout.Toggle(selected[i], validDirs[i]);
        }

        GUILayout.EndScrollView();

        if (GUILayout.Button("Convert Selected"))
        {
            ConvertSelected();
        }

        GUILayout.Space(10);
        GUILayout.Label(log, EditorStyles.wordWrappedLabel);
    }

    private void ConvertSelected()
    {
        int totalCreated = 0;
        log = "";

        for (int i = 0; i < validDirs.Count; i++)
        {
            if (!selected[i])
                continue;

            string dir = validDirs[i];
            string texturesFolder = Path.Combine(dir, "Textures");
            string matsFolder = Path.Combine(dir, "Mats");

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesFolder });

            int createdCount = 0;

            foreach (string guid in guids)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;

                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single)
                    continue;

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null) continue;

                string nameNoExt = Path.GetFileNameWithoutExtension(texPath);
                string matPath = $"{matsFolder}/{nameNoExt}.mat";

                // **Skip if material already exists**
                Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (existingMat != null)
                    continue;

                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit == null)
                {
                    Debug.LogError("URP Lit shader not found.");
                    continue;
                }

                Material mat = new Material(urpLit);

                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_WorkflowMode", 1);
                mat.SetTexture("_BaseMap", tex);

                AssetDatabase.CreateAsset(mat, matPath);

                createdCount++;
            }

            totalCreated += createdCount;
            log += $"{dir}: Created {createdCount} new materials.\n";
        }

        if (totalCreated == 0)
            log += "No new materials created.";
        else
            log += $"Total materials created: {totalCreated}.";

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

}
