using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public enum ModelFileType
{
    All,
    FBX,
    OBJ,
    Blend,
    DAE,
    Other
}

public enum MeshTypeFilter
{
    All,
    SkinnedOnly,
    StaticOnly
}

public enum MeshCompression
{
    DontChange,
    Off,
    Low,
    Medium,
    High
}

public enum NormalImportMode
{
    DontChange,
    Import,
    Calculate,
    None
}

public enum TangentImportMode
{
    DontChange,
    Import,
    CalculateMikk,
    CalculateLegacy,
    None
}

public enum ReadWriteOverride
{
    DontChange,
    Disabled,
    Enabled
}

public enum OptimizeMeshOverride
{
    DontChange,
    Disabled,
    Enabled
}

public enum ImportBlendShapesOverride
{
    DontChange,
    Disabled,
    Enabled
}

public enum GenerateCollidersOverride
{
    DontChange,
    Disabled,
    Enabled
}

public enum GenerateLightmapUVsOverride
{
    DontChange,
    No,
    Yes,
    YesOnlyWithoutUV2
}

public enum ScaleFactorOverride
{
    DontChange,
    UseCustom
}

[System.Serializable]
public class ModelRule
{
    public bool enabled = true;
    public bool isExpanded = true;
    public string ruleName = "New Rule";
    public ModelFileType fileTypeFilter = ModelFileType.All;
    public MeshTypeFilter meshTypeFilter = MeshTypeFilter.All;
    
    public ScaleFactorOverride scaleFactorOverride = ScaleFactorOverride.DontChange;
    public float scaleFactor = 1.0f;
    
    public ReadWriteOverride readWriteOverride = ReadWriteOverride.DontChange;
    public OptimizeMeshOverride optimizeMeshOverride = OptimizeMeshOverride.DontChange;
    public MeshCompression meshCompression = MeshCompression.DontChange;
    public ImportBlendShapesOverride importBlendShapesOverride = ImportBlendShapesOverride.DontChange;
    public GenerateCollidersOverride generateCollidersOverride = GenerateCollidersOverride.DontChange;
    
    public NormalImportMode normalMode = NormalImportMode.DontChange;
    public TangentImportMode tangentMode = TangentImportMode.DontChange;
    
    public GenerateLightmapUVsOverride generateLightmapUVsOverride = GenerateLightmapUVsOverride.DontChange;
}

[System.Serializable]
public class ModelRulesList
{
    public List<ModelRule> rules = new List<ModelRule>();
}

public class ModelImporterWindow : EditorWindow
{
    private List<ModelData> modelFiles = new List<ModelData>();
    private List<ModelRule> rules = new List<ModelRule>();
    
    private bool showAssets = true;
    private bool showPackages = false;
    private bool showOnlyInScene = false;
    private bool isLoading = false;
    
    private Vector2 scrollPositionModels;
    private Vector2 scrollPositionRules;
    
    private string modelSearchFilter = "";
    private int currentPage = 0;
    private int itemsPerPage = 50;
    private bool needsInitialLoad = true;
    
    [System.Serializable]
    public class ModelData
    {
        public string path;
        public string fileName;
        public ModelImporter importer;
        public ModelFileType fileType;
        public bool hasSkinnedMesh;
        public int vertexCount;
        public int triangleCount;
        public long fileSize;
        
        public ModelData(string path, ModelImporter importer)
        {
            this.path = path;
            this.fileName = Path.GetFileName(path);
            this.importer = importer;
            
            string extension = Path.GetExtension(path).ToLower();
            switch (extension)
            {
                case ".fbx": fileType = ModelFileType.FBX; break;
                case ".obj": fileType = ModelFileType.OBJ; break;
                case ".blend": fileType = ModelFileType.Blend; break;
                case ".dae": fileType = ModelFileType.DAE; break;
                default: fileType = ModelFileType.Other; break;
            }
            
            try
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    hasSkinnedMesh = model.GetComponentInChildren<SkinnedMeshRenderer>() != null;
                    
                    MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>();
                    SkinnedMeshRenderer[] skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                    
                    foreach (var mf in meshFilters)
                    {
                        if (mf.sharedMesh != null)
                        {
                            vertexCount += mf.sharedMesh.vertexCount;
                            triangleCount += mf.sharedMesh.triangles.Length / 3;
                        }
                    }
                    
                    foreach (var sr in skinnedRenderers)
                    {
                        if (sr.sharedMesh != null)
                        {
                            vertexCount += sr.sharedMesh.vertexCount;
                            triangleCount += sr.sharedMesh.triangles.Length / 3;
                        }
                    }
                }
                
                string fullPath;
                if (path.StartsWith("Assets/"))
                {
                    fullPath = Path.Combine(Application.dataPath, path.Substring(7));
                }
                else if (path.StartsWith("Packages/"))
                {
                    string packagesPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages");
                    fullPath = Path.Combine(packagesPath, path.Substring(9));
                }
                else
                {
                    fullPath = path;
                }
                
                FileInfo fileInfo = new FileInfo(fullPath);
                fileSize = fileInfo.Length;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not get model info for {path}: {e.Message}");
                fileSize = 0;
                vertexCount = 0;
                triangleCount = 0;
            }
        }
    }
    
    private const string RULES_PREFS_KEY = "ModelImporterRules";
    
    [MenuItem("ValenVRC/Tools/Model Importer")]
    public static void ShowWindow()
    {
        GetWindow<ModelImporterWindow>("Model Importer");
    }
    
    private void OnEnable()
    {
        LoadRules();
        needsInitialLoad = true;
    }
    
    private void OnDisable()
    {
        SaveRules();
    }
    
    private void OnGUI()
    {
        if (needsInitialLoad)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(20);
            GUIStyle centerStyle = new GUIStyle(EditorStyles.largeLabel);
            centerStyle.alignment = TextAnchor.MiddleCenter;
            centerStyle.fontSize = 16;
            EditorGUILayout.LabelField("Model Importer Tool", centerStyle);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Loading models, please wait...", centerStyle);
            EditorGUILayout.EndVertical();
            
            needsInitialLoad = false;
            Repaint();
            EditorApplication.delayCall += () => {
                if (this != null)
                {
                    LoadModelFiles();
                }
            };
            return;
        }
        
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F5)
        {
            LoadModelFiles();
            Event.current.Use();
        }
        
        EditorGUILayout.BeginVertical();
        
        EditorGUILayout.LabelField("Model Importer Tool", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Define rules to batch edit model import settings", EditorStyles.miniLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Models (F5)", GUILayout.Width(150)))
        {
            LoadModelFiles();
        }
        
        EditorGUI.BeginChangeCheck();
        showAssets = EditorGUILayout.ToggleLeft("Include Assets", showAssets, GUILayout.Width(100));
        showPackages = EditorGUILayout.ToggleLeft("Include Packages", showPackages, GUILayout.Width(120));
        showOnlyInScene = EditorGUILayout.ToggleLeft("Only in Scene", showOnlyInScene, GUILayout.Width(100));
        if (EditorGUI.EndChangeCheck())
        {
            LoadModelFiles();
        }
        
        if (isLoading)
        {
            EditorGUILayout.LabelField("Loading...", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField($"{modelFiles.Count} models loaded", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
        DrawRulesPanel();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.BeginVertical();
        DrawModelsPanel();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Apply Rules to All Models", GUILayout.Height(30)))
        {
            ApplyRulesToModels();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void SaveRules()
    {
        try
        {
            ModelRulesList rulesList = new ModelRulesList { rules = rules };
            string json = JsonUtility.ToJson(rulesList, true);
            EditorPrefs.SetString(RULES_PREFS_KEY, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save model rules: {e.Message}");
        }
    }
    
    private void LoadRules()
    {
        try
        {
            if (EditorPrefs.HasKey(RULES_PREFS_KEY))
            {
                string json = EditorPrefs.GetString(RULES_PREFS_KEY);
                ModelRulesList rulesList = JsonUtility.FromJson<ModelRulesList>(json);
                if (rulesList != null && rulesList.rules != null)
                {
                    rules = rulesList.rules;
                    Debug.Log($"Loaded {rules.Count} model rules");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load model rules: {e.Message}");
            rules = new List<ModelRule>();
        }
    }
    
    private void ExportRules()
    {
        if (rules.Count == 0)
        {
            EditorUtility.DisplayDialog("No Rules", "There are no rules to export.", "OK");
            return;
        }
        
        string path = EditorUtility.SaveFilePanel("Export Model Rules", "", "ModelRules.json", "json");
        
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                ModelRulesList rulesList = new ModelRulesList { rules = rules };
                string json = JsonUtility.ToJson(rulesList, true);
                File.WriteAllText(path, json);
                EditorUtility.DisplayDialog("Export Successful", $"Exported {rules.Count} rule(s) to:\n{path}", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Export Failed", $"Failed to export rules:\n{e.Message}", "OK");
            }
        }
    }
    
    private void ImportRules()
    {
        string path = EditorUtility.OpenFilePanel("Import Model Rules", "", "json");
        
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                ModelRulesList rulesList = JsonUtility.FromJson<ModelRulesList>(json);
                
                if (rulesList != null && rulesList.rules != null && rulesList.rules.Count > 0)
                {
                    bool append = false;
                    if (rules.Count > 0)
                    {
                        int choice = EditorUtility.DisplayDialogComplex("Import Rules",
                            $"Import {rulesList.rules.Count} rule(s)?\n\nCurrent rules: {rules.Count}",
                            "Replace", "Cancel", "Append");
                        
                        if (choice == 1) return;
                        append = (choice == 2);
                    }
                    
                    if (!append)
                    {
                        rules.Clear();
                    }
                    
                    rules.AddRange(rulesList.rules);
                    SaveRules();
                    EditorUtility.DisplayDialog("Import Successful", $"Imported {rulesList.rules.Count} rule(s).", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Import Failed", "The file does not contain valid rules.", "OK");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Import Failed", $"Failed to import rules:\n{e.Message}", "OK");
            }
        }
    }
    
    private void DrawRulesPanel()
    {
        EditorGUILayout.LabelField("Model Rules", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add New Rule"))
        {
            rules.Add(new ModelRule { ruleName = $"Rule {rules.Count + 1}" });
            SaveRules();
        }
        if (GUILayout.Button("Clear All Rules"))
        {
            if (EditorUtility.DisplayDialog("Clear All Rules", "Delete all rules?", "Yes", "No"))
            {
                rules.Clear();
                SaveRules();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export Rules"))
        {
            ExportRules();
        }
        if (GUILayout.Button("Import Rules"))
        {
            ImportRules();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        scrollPositionRules = EditorGUILayout.BeginScrollView(scrollPositionRules, GUILayout.ExpandHeight(true));
        
        for (int i = 0; i < rules.Count; i++)
        {
            DrawRule(rules[i], i);
            EditorGUILayout.Space();
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawRule(ModelRule rule, int index)
    {
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.BeginHorizontal();
        rule.enabled = EditorGUILayout.Toggle(rule.enabled, GUILayout.Width(20));
        rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, rule.ruleName, true);
        
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            rules.RemoveAt(index);
            SaveRules();
            return;
        }
        EditorGUILayout.EndHorizontal();
        
        if (rule.isExpanded)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(!rule.enabled);
            
            rule.ruleName = EditorGUILayout.TextField("Rule Name", rule.ruleName);
            rule.fileTypeFilter = (ModelFileType)EditorGUILayout.EnumPopup("File Type", rule.fileTypeFilter);
            rule.meshTypeFilter = (MeshTypeFilter)EditorGUILayout.EnumPopup("Mesh Type", rule.meshTypeFilter);
            
            EditorGUILayout.Space();
            
            rule.scaleFactorOverride = (ScaleFactorOverride)EditorGUILayout.EnumPopup("Scale Factor", rule.scaleFactorOverride);
            if (rule.scaleFactorOverride == ScaleFactorOverride.UseCustom)
            {
                EditorGUI.indentLevel++;
                rule.scaleFactor = EditorGUILayout.FloatField("Scale Factor", rule.scaleFactor);
                EditorGUI.indentLevel--;
            }
            
            rule.readWriteOverride = (ReadWriteOverride)EditorGUILayout.EnumPopup("Read/Write", rule.readWriteOverride);
            rule.optimizeMeshOverride = (OptimizeMeshOverride)EditorGUILayout.EnumPopup("Optimize Mesh", rule.optimizeMeshOverride);
            rule.meshCompression = (MeshCompression)EditorGUILayout.EnumPopup("Mesh Compression", rule.meshCompression);
            rule.importBlendShapesOverride = (ImportBlendShapesOverride)EditorGUILayout.EnumPopup("Import Blend Shapes", rule.importBlendShapesOverride);
            rule.generateCollidersOverride = (GenerateCollidersOverride)EditorGUILayout.EnumPopup("Generate Colliders", rule.generateCollidersOverride);
            
            rule.normalMode = (NormalImportMode)EditorGUILayout.EnumPopup("Normals", rule.normalMode);
            rule.tangentMode = (TangentImportMode)EditorGUILayout.EnumPopup("Tangents", rule.tangentMode);
            
            rule.generateLightmapUVsOverride = (GenerateLightmapUVsOverride)EditorGUILayout.EnumPopup("Generate Lightmap UVs", rule.generateLightmapUVsOverride);
            
            EditorGUI.EndDisabledGroup();
            
            if (EditorGUI.EndChangeCheck())
            {
                SaveRules();
            }
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawModelsPanel()
    {
        EditorGUILayout.LabelField("Models in Project (Sorted by Vertices)", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        modelSearchFilter = EditorGUILayout.TextField("Search", modelSearchFilter);
        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            modelSearchFilter = "";
            currentPage = 0;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        var filteredModels = modelFiles.Where(m => 
            string.IsNullOrEmpty(modelSearchFilter) || 
            m.fileName.IndexOf(modelSearchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            m.path.IndexOf(modelSearchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0
        ).OrderByDescending(m => m.vertexCount).ToList();
        
        int totalPages = Mathf.CeilToInt((float)filteredModels.Count / itemsPerPage);
        currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, totalPages - 1));
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Page {currentPage + 1} of {totalPages} | {filteredModels.Count} models", EditorStyles.miniLabel);
        itemsPerPage = EditorGUILayout.IntField("Per Page", itemsPerPage, GUILayout.Width(100));
        itemsPerPage = Mathf.Clamp(itemsPerPage, 10, 500);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(currentPage == 0);
        if (GUILayout.Button("◄◄ First", GUILayout.Width(70)))
        {
            currentPage = 0;
        }
        if (GUILayout.Button("◄ Prev", GUILayout.Width(70)))
        {
            currentPage--;
        }
        EditorGUI.EndDisabledGroup();
        
        GUILayout.FlexibleSpace();
        
        EditorGUI.BeginDisabledGroup(currentPage >= totalPages - 1);
        if (GUILayout.Button("Next ►", GUILayout.Width(70)))
        {
            currentPage++;
        }
        if (GUILayout.Button("Last ►►", GUILayout.Width(70)))
        {
            currentPage = totalPages - 1;
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        scrollPositionModels = EditorGUILayout.BeginScrollView(scrollPositionModels, GUILayout.ExpandHeight(true));
        
        var paginatedModels = filteredModels.Skip(currentPage * itemsPerPage).Take(itemsPerPage);
        
        foreach (var model in paginatedModels)
        {
            EditorGUILayout.BeginHorizontal("box");
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(model.fileName, EditorStyles.boldLabel);
            string skinnedInfo = model.hasSkinnedMesh ? "Skinned" : "Static";
            EditorGUILayout.LabelField($"{model.vertexCount:N0} verts | {model.triangleCount:N0} tris | {skinnedInfo} | {model.fileType} | {FormatFileSize(model.fileSize)}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(model.path, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(model.path);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void LoadModelFiles()
    {
        isLoading = true;
        modelFiles.Clear();
        currentPage = 0;
        
        HashSet<string> sceneModelPaths = null;
        
        if (showOnlyInScene)
        {
            sceneModelPaths = GetSceneModelPaths();
        }
        
        string[] searchFolders = null;
        
        if (showAssets && !showPackages)
        {
            searchFolders = new string[] { "Assets" };
        }
        else if (showPackages && !showAssets)
        {
            searchFolders = new string[] { "Packages" };
        }
        
        string[] guids = AssetDatabase.FindAssets("t:Model", searchFolders);
        
        Debug.Log($"Found {guids.Length} model GUIDs");
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            
            if (showOnlyInScene && (sceneModelPaths == null || !sceneModelPaths.Contains(assetPath)))
            {
                continue;
            }
            
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            
            if (importer != null)
            {
                modelFiles.Add(new ModelData(assetPath, importer));
            }
        }
        
        Debug.Log($"Loaded {modelFiles.Count} model files");
        
        isLoading = false;
        Repaint();
    }
    
    private HashSet<string> GetSceneModelPaths()
    {
        HashSet<string> modelPaths = new HashSet<string>();
        
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                if (!string.IsNullOrEmpty(assetPath) && assetPath.Contains("."))
                {
                    modelPaths.Add(assetPath);
                }
            }
            
            SkinnedMeshRenderer skinnedRenderer = obj.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(skinnedRenderer.sharedMesh);
                if (!string.IsNullOrEmpty(assetPath) && assetPath.Contains("."))
                {
                    modelPaths.Add(assetPath);
                }
            }
        }
        
        Debug.Log($"Found {modelPaths.Count} unique model assets in the current scene");
        return modelPaths;
    }
    
    private void ApplyRulesToModels()
    {
        var enabledRules = rules.Where(r => r.enabled).ToList();
        
        if (enabledRules.Count == 0)
        {
            EditorUtility.DisplayDialog("No Rules", "Please create and enable at least one rule.", "OK");
            return;
        }
        
        if (!EditorUtility.DisplayDialog("Apply Rules", 
            $"Apply {enabledRules.Count} rule(s) to {modelFiles.Count} model(s)?", 
            "Apply", "Cancel"))
        {
            return;
        }
        
        int modifiedCount = 0;
        
        try
        {
            AssetDatabase.StartAssetEditing();
            
            foreach (var model in modelFiles)
            {
                bool modified = false;
                
                foreach (var rule in enabledRules)
                {
                    bool fileTypeMatches = rule.fileTypeFilter == ModelFileType.All || model.fileType == rule.fileTypeFilter;
                    bool meshTypeMatches = rule.meshTypeFilter == MeshTypeFilter.All ||
                                          (rule.meshTypeFilter == MeshTypeFilter.SkinnedOnly && model.hasSkinnedMesh) ||
                                          (rule.meshTypeFilter == MeshTypeFilter.StaticOnly && !model.hasSkinnedMesh);
                    
                    if (fileTypeMatches && meshTypeMatches)
                    {
                        if (ApplyRuleToModel(model, rule))
                        {
                            modified = true;
                        }
                    }
                }
                
                if (modified)
                {
                    model.importer.SaveAndReimport();
                    modifiedCount++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Complete", 
            $"Successfully applied rules to {modifiedCount} model(s).", "OK");
        
        LoadModelFiles();
    }
    
    private bool ApplyRuleToModel(ModelData model, ModelRule rule)
    {
        bool modified = false;
        
        if (rule.scaleFactorOverride == ScaleFactorOverride.UseCustom)
        {
            if (model.importer.globalScale != rule.scaleFactor)
            {
                model.importer.globalScale = rule.scaleFactor;
                modified = true;
            }
        }
        
        if (rule.readWriteOverride != ReadWriteOverride.DontChange)
        {
            bool readWriteEnabled = rule.readWriteOverride == ReadWriteOverride.Enabled;
            if (model.importer.isReadable != readWriteEnabled)
            {
                model.importer.isReadable = readWriteEnabled;
                modified = true;
            }
        }
        
        if (rule.optimizeMeshOverride != OptimizeMeshOverride.DontChange)
        {
            bool optimizeMesh = rule.optimizeMeshOverride == OptimizeMeshOverride.Enabled;
            if (model.importer.optimizeMeshVertices != optimizeMesh || 
                model.importer.optimizeMeshPolygons != optimizeMesh)
            {
                model.importer.optimizeMeshVertices = optimizeMesh;
                model.importer.optimizeMeshPolygons = optimizeMesh;
                modified = true;
            }
        }
        
        if (rule.meshCompression != MeshCompression.DontChange)
        {
            ModelImporterMeshCompression targetCompression = ModelImporterMeshCompression.Off;
            
            switch (rule.meshCompression)
            {
                case MeshCompression.Off: targetCompression = ModelImporterMeshCompression.Off; break;
                case MeshCompression.Low: targetCompression = ModelImporterMeshCompression.Low; break;
                case MeshCompression.Medium: targetCompression = ModelImporterMeshCompression.Medium; break;
                case MeshCompression.High: targetCompression = ModelImporterMeshCompression.High; break;
            }
            
            if (model.importer.meshCompression != targetCompression)
            {
                model.importer.meshCompression = targetCompression;
                modified = true;
            }
        }
        
        if (rule.importBlendShapesOverride != ImportBlendShapesOverride.DontChange)
        {
            bool importBlendShapes = rule.importBlendShapesOverride == ImportBlendShapesOverride.Enabled;
            if (model.importer.importBlendShapes != importBlendShapes)
            {
                model.importer.importBlendShapes = importBlendShapes;
                modified = true;
            }
        }
        
        if (rule.generateCollidersOverride != GenerateCollidersOverride.DontChange)
        {
            bool generateColliders = rule.generateCollidersOverride == GenerateCollidersOverride.Enabled;
            if (model.importer.addCollider != generateColliders)
            {
                model.importer.addCollider = generateColliders;
                modified = true;
            }
        }
        
        if (rule.normalMode != NormalImportMode.DontChange)
        {
            ModelImporterNormals targetNormals = ModelImporterNormals.Import;
            
            switch (rule.normalMode)
            {
                case NormalImportMode.Import: targetNormals = ModelImporterNormals.Import; break;
                case NormalImportMode.Calculate: targetNormals = ModelImporterNormals.Calculate; break;
                case NormalImportMode.None: targetNormals = ModelImporterNormals.None; break;
            }
            
            if (model.importer.importNormals != targetNormals)
            {
                model.importer.importNormals = targetNormals;
                modified = true;
            }
        }
        
        if (rule.tangentMode != TangentImportMode.DontChange)
        {
            ModelImporterTangents targetTangents = ModelImporterTangents.Import;
            
            switch (rule.tangentMode)
            {
                case TangentImportMode.Import: targetTangents = ModelImporterTangents.Import; break;
                case TangentImportMode.CalculateMikk: targetTangents = ModelImporterTangents.CalculateMikk; break;
                case TangentImportMode.CalculateLegacy: targetTangents = ModelImporterTangents.CalculateLegacy; break;
                case TangentImportMode.None: targetTangents = ModelImporterTangents.None; break;
            }
            
            if (model.importer.importTangents != targetTangents)
            {
                model.importer.importTangents = targetTangents;
                modified = true;
            }
        }
        
        if (rule.generateLightmapUVsOverride != GenerateLightmapUVsOverride.DontChange)
        {
            bool shouldGenerate = false;
            
            switch (rule.generateLightmapUVsOverride)
            {
                case GenerateLightmapUVsOverride.No:
                    shouldGenerate = false;
                    break;
                case GenerateLightmapUVsOverride.Yes:
                    shouldGenerate = true;
                    break;
                case GenerateLightmapUVsOverride.YesOnlyWithoutUV2:
                    bool hasLightmapUVs = CheckModelHasLightmapUVs(model.path);
                    shouldGenerate = !hasLightmapUVs;
                    break;
            }
            
            if (model.importer.generateSecondaryUV != shouldGenerate)
            {
                model.importer.generateSecondaryUV = shouldGenerate;
                modified = true;
            }
        }
        
        return modified;
    }
    
    private bool CheckModelHasLightmapUVs(string modelPath)
    {
        try
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return false;
            
            MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null && mf.sharedMesh.uv2 != null && mf.sharedMesh.uv2.Length > 0)
                {
                    return true;
                }
            }
            
            SkinnedMeshRenderer[] skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var sr in skinnedRenderers)
            {
                if (sr.sharedMesh != null && sr.sharedMesh.uv2 != null && sr.sharedMesh.uv2.Length > 0)
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }
        
        return false;
    }
    
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
        return $"{bytes / (1024 * 1024 * 1024):F1} GB";
    }
}
