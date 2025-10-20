// Assets/Editor/UDIMBuilder.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

public class UDIMBuilder : EditorWindow
{
    [MenuItem("Tools/UDIM/Build Texture2DArray & Material")]
    public static void ShowWindow() => GetWindow<UDIMBuilder>("UDIM Builder");

    DefaultAsset folderAsset;
    Shader targetShader;
    string arrayAssetName = "UDIM_TextureArray.asset";
    string materialNamePrefix = "M_";

    void OnGUI()
    {
        GUILayout.Label("UDIM Texture2DArray Builder", EditorStyles.boldLabel);
        folderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Textures Folder", folderAsset, typeof(DefaultAsset), false);
        targetShader = (Shader)EditorGUILayout.ObjectField("Target Shader (URP UDIM)", targetShader, typeof(Shader), false);
        arrayAssetName = EditorGUILayout.TextField("Array Asset Name", arrayAssetName);
        materialNamePrefix = EditorGUILayout.TextField("Material name prefix", materialNamePrefix);

        if (GUILayout.Button("Build"))
        {
            if (folderAsset == null || targetShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Assign a texture folder and a shader.", "OK");
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(folderAsset);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                EditorUtility.DisplayDialog("Error", "Select a valid folder (not a file).", "OK");
                return;
            }

            BuildFromFolder(folderPath, targetShader, arrayAssetName, materialNamePrefix);
        }
    }

    static void BuildFromFolder(string folderPath, Shader shader, string arrayName, string matPrefix)
    {
        // find png/jpg/tga etc
        string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                  .Where(s => s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".tga")).ToArray();

        var rex = new Regex(@"\.(10\d{2})\.", RegexOptions.IgnoreCase);

        // map udimNumber -> assetPath
        var map = new SortedDictionary<int, string>();
        foreach (var f in files)
        {
            var fileName = Path.GetFileName(f);
            var m = rex.Match(fileName);
            if (m.Success)
            {
                if (int.TryParse(m.Groups[1].Value, out int udim))
                {
                    string assetPath = f.Replace(Application.dataPath, "Assets").Replace("\\", "/");
                    map[udim] = assetPath;
                }
            }
        }

        if (map.Count == 0)
        {
            Debug.LogError("No UDIM textures found in folder: " + folderPath);
            return;
        }

        // load textures and verify sizes/formats
        List<Texture2D> textures = new List<Texture2D>();
        foreach (var kv in map)
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(kv.Value);
            if (t == null)
            {
                Debug.LogError("Failed to load texture at " + kv.Value);
                return;
            }
            textures.Add(t);
        }

        int w = textures[0].width;
        int h = textures[0].height;
        TextureFormat format = GetReadableFormat(textures[0]);
        for (int i = 0; i < textures.Count; i++)
        {
            if (textures[i].width != w || textures[i].height != h)
            {
                Debug.LogError("All UDIM textures must have same dimensions. Mismatch: " + textures[i].name);
                return;
            }
            // Optionally check format equality if needed
        }

        // Create Texture2DArray
        Texture2DArray texArray = new Texture2DArray(w, h, textures.Count, format, false, false);
        texArray.wrapMode = textures[0].wrapMode;
        texArray.filterMode = textures[0].filterMode;

        // Copy each texture into array slice using Graphics.CopyTexture (fast)
        for (int i = 0; i < textures.Count; i++)
        {
            try
            {
                // copy into slice
                Graphics.CopyTexture(textures[i], 0, 0, texArray, i, 0);
            }
            catch
            {
                // fallback: try to read pixels (requires Read/Write enabled)
                Texture2D src = textures[i];
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                Graphics.Blit(src, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tmp.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Color[] cols = tmp.GetPixels();
                texArray.SetPixels(cols, i);
            }
        }

        // Apply and save
        texArray.Apply(false, false);
        string arrayPath = folderPath + "/" + arrayName;
        AssetDatabase.CreateAsset(texArray, arrayPath);

        // Create material
        Material mat = new Material(shader);
        mat.name = matPrefix + new DirectoryInfo(folderPath).Name;
        mat.SetTexture("_UDIMArray", AssetDatabase.LoadAssetAtPath<Texture2DArray>(arrayPath));
        string matPath = folderPath + "/" + mat.name + ".mat";
        AssetDatabase.CreateAsset(mat, matPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"UDIM array created: {arrayPath} and material: {matPath}");
    }

    static TextureFormat GetReadableFormat(Texture2D t)
    {
        // Try to guess an appropriate TextureFormat to create the array.
        // Use RGBA32 as safe fallback.
        return TextureFormat.RGBA32;
    }
}
