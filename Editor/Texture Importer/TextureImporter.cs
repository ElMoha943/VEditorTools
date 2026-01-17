using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public enum TexturePlatform
{
    PC,
    Android,
    iOS
}

public enum PCCompressionFormat
{
    Automatic = TextureImporterFormat.Automatic,
    DXT1 = TextureImporterFormat.DXT1,
    DXT5 = TextureImporterFormat.DXT5,
    BC5 = TextureImporterFormat.BC5,
    BC7 = TextureImporterFormat.BC7,
    RGBA32 = TextureImporterFormat.RGBA32,
    RGB24 = TextureImporterFormat.RGB24
}

public enum MobileCompressionFormat
{
    Automatic = TextureImporterFormat.Automatic,
    ASTC_4x4 = TextureImporterFormat.ASTC_4x4,
    ASTC_6x6 = TextureImporterFormat.ASTC_6x6,
    ASTC_8x8 = TextureImporterFormat.ASTC_8x8,
    ETC2_RGB4 = TextureImporterFormat.ETC2_RGB4,
    ETC2_RGBA8 = TextureImporterFormat.ETC2_RGBA8,
    PVRTC_RGB4 = TextureImporterFormat.PVRTC_RGB4,
    PVRTC_RGBA4 = TextureImporterFormat.PVRTC_RGBA4
}

public enum AlphaFilter
{
    All,
    OnlyWithAlpha,
    OnlyOpaque
}

public enum CompressionQualityOverride
{
    DontChange,
    Fast,
    Normal,
    Best
}

public enum CrunchOverride
{
    DontChange,
    Disabled,
    Enabled
}

[System.Serializable]
public class TextureRule
{
    public bool enabled = true;
    public bool isExpanded = true;
    public string ruleName = "New Rule";
    public TexturePlatform platform = TexturePlatform.PC;
    public TextureImporterType textureType = TextureImporterType.Default;
    public bool applyToAllTypes = false;
    public AlphaFilter alphaFilter = AlphaFilter.All;
    
    public bool overrideCompression = false;
    public PCCompressionFormat pcCompression = PCCompressionFormat.Automatic;
    public MobileCompressionFormat mobileCompression = MobileCompressionFormat.Automatic;
    public CompressionQualityOverride compressionQualityOverride = CompressionQualityOverride.DontChange;
    public CrunchOverride crunchOverride = CrunchOverride.DontChange;
    public int crunchQuality = 50;
    
    public bool overrideMaxSize = false;
    public int maxSize = 2048;
    public bool onlyOverrideWhenLarger = true;
    
    public bool overrideMipmaps = false;
    public bool generateMipmaps = true;
    
    public bool overrideReadWrite = false;
    public bool readWrite = false;
}

[System.Serializable]
public class TextureRulesList
{
    public List<TextureRule> rules = new List<TextureRule>();
}

public class TextureImporterWindow : EditorWindow
{
    private List<TextureData> textureFiles = new List<TextureData>();
    private List<TextureRule> rules = new List<TextureRule>();
    
    private bool showAssets = true;
    private bool showPackages = false;
    private bool showOnlyInScene = false;
    private bool isLoading = false;
    
    private Vector2 scrollPositionTextures;
    private Vector2 scrollPositionRules;
    
    private string textureSearchFilter = "";
    private int currentPage = 0;
    private int itemsPerPage = 50;
    private bool needsInitialLoad = true;
    
    [System.Serializable]
    public class TextureData
    {
        public string path;
        public string fileName;
        public UnityEditor.TextureImporter importer;
        public TextureImporterType textureType;
        public int width;
        public int height;
        public long fileSize;
        public bool hasAlpha;
        
        public Dictionary<string, TextureImporterPlatformSettings> platformSettings = new Dictionary<string, TextureImporterPlatformSettings>();
        
        public TextureData(string path, UnityEditor.TextureImporter importer)
        {
            this.path = path;
            this.fileName = Path.GetFileName(path);
            this.importer = importer;
            
            if (importer != null)
            {
                textureType = importer.textureType;
                hasAlpha = importer.DoesSourceTextureHaveAlpha();
                
                platformSettings["Standalone"] = importer.GetPlatformTextureSettings("Standalone");
                platformSettings["Android"] = importer.GetPlatformTextureSettings("Android");
                platformSettings["iPhone"] = importer.GetPlatformTextureSettings("iPhone");
            }
            
            try
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    width = texture.width;
                    height = texture.height;
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
                Debug.LogWarning($"Could not get texture info for {path}: {e.Message}");
                fileSize = 0;
                width = 0;
                height = 0;
            }
        }
    }
    
    private const string RULES_PREFS_KEY = "TextureImporterRules";
    
    [MenuItem("ValenVRC/Tools/Texture Importer")]
    public static void ShowWindow()
    {
        GetWindow<TextureImporterWindow>("Texture Importer");
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
            EditorGUILayout.LabelField("Texture Importer Tool", centerStyle);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Loading textures, please wait...", centerStyle);
            EditorGUILayout.EndVertical();
            
            needsInitialLoad = false;
            Repaint();
            EditorApplication.delayCall += () => {
                if (this != null)
                {
                    LoadTextureFiles();
                }
            };
            return;
        }
        
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F5)
        {
            LoadTextureFiles();
            Event.current.Use();
        }
        
        EditorGUILayout.BeginVertical();
        
        EditorGUILayout.LabelField("Texture Importer Tool", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Define rules to batch edit texture import settings", EditorStyles.miniLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Textures (F5)", GUILayout.Width(150)))
        {
            LoadTextureFiles();
        }
        
        EditorGUI.BeginChangeCheck();
        showAssets = EditorGUILayout.ToggleLeft("Include Assets", showAssets, GUILayout.Width(100));
        showPackages = EditorGUILayout.ToggleLeft("Include Packages", showPackages, GUILayout.Width(120));
        showOnlyInScene = EditorGUILayout.ToggleLeft("Only in Scene", showOnlyInScene, GUILayout.Width(100));
        if (EditorGUI.EndChangeCheck())
        {
            LoadTextureFiles();
        }
        
        if (isLoading)
        {
            EditorGUILayout.LabelField("Loading...", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField($"{textureFiles.Count} textures loaded", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
        DrawRulesPanel();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.BeginVertical();
        DrawTexturesPanel();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Apply Rules to All Textures", GUILayout.Height(30)))
        {
            ApplyRulesToTextures();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void SaveRules()
    {
        try
        {
            TextureRulesList rulesList = new TextureRulesList { rules = rules };
            string json = JsonUtility.ToJson(rulesList, true);
            EditorPrefs.SetString(RULES_PREFS_KEY, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save texture rules: {e.Message}");
        }
    }
    
    private void LoadRules()
    {
        try
        {
            if (EditorPrefs.HasKey(RULES_PREFS_KEY))
            {
                string json = EditorPrefs.GetString(RULES_PREFS_KEY);
                TextureRulesList rulesList = JsonUtility.FromJson<TextureRulesList>(json);
                if (rulesList != null && rulesList.rules != null)
                {
                    rules = rulesList.rules;
                    Debug.Log($"Loaded {rules.Count} texture rules");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load texture rules: {e.Message}");
            rules = new List<TextureRule>();
        }
    }
    
    private void ExportRules()
    {
        if (rules.Count == 0)
        {
            EditorUtility.DisplayDialog("No Rules", "There are no rules to export.", "OK");
            return;
        }
        
        string path = EditorUtility.SaveFilePanel("Export Texture Rules", "", "TextureRules.json", "json");
        
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                TextureRulesList rulesList = new TextureRulesList { rules = rules };
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
        string path = EditorUtility.OpenFilePanel("Import Texture Rules", "", "json");
        
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                TextureRulesList rulesList = JsonUtility.FromJson<TextureRulesList>(json);
                
                if (rulesList != null && rulesList.rules != null && rulesList.rules.Count > 0)
                {
                    bool append = false;
                    if (rules.Count > 0)
                    {
                        int choice = EditorUtility.DisplayDialogComplex("Import Rules",
                            $"Import {rulesList.rules.Count} rule(s)?\n\nCurrent rules: {rules.Count}",
                            "Replace", "Cancel", "Append");
                        
                        if (choice == 1) return; // Cancel
                        append = (choice == 2); // Append
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
        EditorGUILayout.LabelField("Texture Rules", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add New Rule"))
        {
            rules.Add(new TextureRule { ruleName = $"Rule {rules.Count + 1}" });
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
    
    private void DrawRule(TextureRule rule, int index)
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
            rule.platform = (TexturePlatform)EditorGUILayout.EnumPopup("Platform", rule.platform);
            
            EditorGUILayout.BeginHorizontal();
            rule.applyToAllTypes = EditorGUILayout.Toggle("All Texture Types", rule.applyToAllTypes);
            EditorGUILayout.EndHorizontal();
            
            if (!rule.applyToAllTypes)
            {
                rule.textureType = (TextureImporterType)EditorGUILayout.EnumPopup("Texture Type", rule.textureType);
            }
            
            rule.alphaFilter = (AlphaFilter)EditorGUILayout.EnumPopup("Alpha Filter", rule.alphaFilter);
            
            EditorGUILayout.Space();
            
            rule.overrideCompression = EditorGUILayout.Toggle("Override Compression", rule.overrideCompression);
            if (rule.overrideCompression)
            {
                EditorGUI.indentLevel++;
                
                bool isValidFormat = false;
                bool supportsQuality = false;
                bool supportsCrunch = false;
                
                if (rule.platform == TexturePlatform.PC)
                {
                    rule.pcCompression = (PCCompressionFormat)EditorGUILayout.EnumPopup("Format", rule.pcCompression);
                    isValidFormat = rule.pcCompression != PCCompressionFormat.Automatic;
                    supportsQuality = rule.pcCompression == PCCompressionFormat.BC7;
                    supportsCrunch = rule.pcCompression == PCCompressionFormat.DXT1 ||
                                    rule.pcCompression == PCCompressionFormat.DXT5;
                }
                else
                {
                    rule.mobileCompression = (MobileCompressionFormat)EditorGUILayout.EnumPopup("Format", rule.mobileCompression);
                    isValidFormat = rule.mobileCompression != MobileCompressionFormat.Automatic;
                    supportsQuality = rule.mobileCompression == MobileCompressionFormat.ASTC_4x4 ||
                                     rule.mobileCompression == MobileCompressionFormat.ASTC_6x6 ||
                                     rule.mobileCompression == MobileCompressionFormat.ASTC_8x8 ||
                                     rule.mobileCompression == MobileCompressionFormat.ETC2_RGB4 ||
                                     rule.mobileCompression == MobileCompressionFormat.ETC2_RGBA8;
                    supportsCrunch = rule.mobileCompression == MobileCompressionFormat.ETC2_RGB4 ||
                                    rule.mobileCompression == MobileCompressionFormat.ETC2_RGBA8;
                }
                
                if (isValidFormat)
                {
                    EditorGUILayout.Space(5);
                    
                    if (supportsQuality)
                    {
                        rule.compressionQualityOverride = (CompressionQualityOverride)EditorGUILayout.EnumPopup("Compression Quality", rule.compressionQualityOverride);
                    }
                    
                    if (supportsCrunch)
                    {
                        rule.crunchOverride = (CrunchOverride)EditorGUILayout.EnumPopup("Crunch Compression", rule.crunchOverride);
                        if (rule.crunchOverride == CrunchOverride.Enabled)
                        {
                            EditorGUI.indentLevel++;
                            rule.crunchQuality = EditorGUILayout.IntSlider("Crunch Quality", rule.crunchQuality, 0, 100);
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Select a compression format to configure quality and crunch settings.", MessageType.Info);
                }
                
                EditorGUI.indentLevel--;
            }
            
            rule.overrideMaxSize = EditorGUILayout.Toggle("Override Max Size", rule.overrideMaxSize);
            if (rule.overrideMaxSize)
            {
                EditorGUI.indentLevel++;
                rule.maxSize = EditorGUILayout.IntPopup("Max Size", rule.maxSize, 
                    new string[] { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192" },
                    new int[] { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 });
                rule.onlyOverrideWhenLarger = EditorGUILayout.Toggle("Only Override When Larger", rule.onlyOverrideWhenLarger);
                EditorGUI.indentLevel--;
            }
            
            rule.overrideMipmaps = EditorGUILayout.Toggle("Override Mipmaps", rule.overrideMipmaps);
            if (rule.overrideMipmaps)
            {
                EditorGUI.indentLevel++;
                rule.generateMipmaps = EditorGUILayout.Toggle("Generate Mipmaps", rule.generateMipmaps);
                EditorGUI.indentLevel--;
            }
            
            rule.overrideReadWrite = EditorGUILayout.Toggle("Override Read/Write", rule.overrideReadWrite);
            if (rule.overrideReadWrite)
            {
                EditorGUI.indentLevel++;
                rule.readWrite = EditorGUILayout.Toggle("Read/Write Enabled", rule.readWrite);
                EditorGUI.indentLevel--;
            }
            
            EditorGUI.EndDisabledGroup();;
            
            if (EditorGUI.EndChangeCheck())
            {
                SaveRules();
            }
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawTexturesPanel()
    {
        EditorGUILayout.LabelField("Textures in Project (Sorted by Size)", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        textureSearchFilter = EditorGUILayout.TextField("Search", textureSearchFilter);
        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            textureSearchFilter = "";
            currentPage = 0;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        var filteredTextures = textureFiles.Where(t => 
            string.IsNullOrEmpty(textureSearchFilter) || 
            t.fileName.IndexOf(textureSearchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            t.path.IndexOf(textureSearchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0
        ).OrderByDescending(t => t.fileSize).ToList();
        
        int totalPages = Mathf.CeilToInt((float)filteredTextures.Count / itemsPerPage);
        currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, totalPages - 1));
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Page {currentPage + 1} of {totalPages} | {filteredTextures.Count} textures", EditorStyles.miniLabel);
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
        
        scrollPositionTextures = EditorGUILayout.BeginScrollView(scrollPositionTextures, GUILayout.ExpandHeight(true));
        
        var paginatedTextures = filteredTextures.Skip(currentPage * itemsPerPage).Take(itemsPerPage);
        
        foreach (var texture in paginatedTextures)
        {
            EditorGUILayout.BeginHorizontal("box");
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(texture.fileName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{texture.width}x{texture.height} | {FormatFileSize(texture.fileSize)} | {texture.textureType}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(texture.path, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(texture.path);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void LoadTextureFiles()
    {
        isLoading = true;
        textureFiles.Clear();
        currentPage = 0;
        
        HashSet<string> sceneTexturePaths = null;
        
        if (showOnlyInScene)
        {
            sceneTexturePaths = GetSceneTexturePaths();
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
        
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", searchFolders);
        
        Debug.Log($"Found {guids.Length} texture GUIDs");
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            
            if (showOnlyInScene && (sceneTexturePaths == null || !sceneTexturePaths.Contains(assetPath)))
            {
                continue;
            }
            
            UnityEditor.TextureImporter importer = AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
            
            if (importer != null)
            {
                textureFiles.Add(new TextureData(assetPath, importer));
            }
        }
        
        Debug.Log($"Loaded {textureFiles.Count} texture files");
        
        isLoading = false;
        Repaint();
    }
    
    private HashSet<string> GetSceneTexturePaths()
    {
        HashSet<string> texturePaths = new HashSet<string>();
        
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
            Renderer[] renderers = obj.GetComponents<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer.sharedMaterials != null)
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            Shader shader = material.shader;
                            if (shader != null)
                            {
                                for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                                {
                                    if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                                    {
                                        string propertyName = ShaderUtil.GetPropertyName(shader, i);
                                        Texture texture = material.GetTexture(propertyName);
                                        if (texture != null)
                                        {
                                            string assetPath = AssetDatabase.GetAssetPath(texture);
                                            if (!string.IsNullOrEmpty(assetPath))
                                            {
                                                texturePaths.Add(assetPath);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Found {texturePaths.Count} unique texture assets in the current scene");
        return texturePaths;
    }
    
    private void ApplyRulesToTextures()
    {
        var enabledRules = rules.Where(r => r.enabled).ToList();
        
        if (enabledRules.Count == 0)
        {
            EditorUtility.DisplayDialog("No Rules", "Please create and enable at least one rule.", "OK");
            return;
        }
        
        if (!EditorUtility.DisplayDialog("Apply Rules", 
            $"Apply {enabledRules.Count} rule(s) to {textureFiles.Count} texture(s)?", 
            "Apply", "Cancel"))
        {
            return;
        }
        
        int modifiedCount = 0;
        
        try
        {
            AssetDatabase.StartAssetEditing();
            
            foreach (var texture in textureFiles)
            {
                bool modified = false;
                
                foreach (var rule in enabledRules)
                {
                    bool typeMatches = rule.applyToAllTypes || texture.textureType == rule.textureType;
                    bool alphaMatches = rule.alphaFilter == AlphaFilter.All ||
                                       (rule.alphaFilter == AlphaFilter.OnlyWithAlpha && texture.hasAlpha) ||
                                       (rule.alphaFilter == AlphaFilter.OnlyOpaque && !texture.hasAlpha);
                    
                    if (typeMatches && alphaMatches)
                    {
                        if (ApplyRuleToTexture(texture, rule))
                        {
                            modified = true;
                        }
                    }
                }
                
                if (modified)
                {
                    texture.importer.SaveAndReimport();
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
            $"Successfully applied rules to {modifiedCount} texture(s).", "OK");
        
        LoadTextureFiles();
    }
    
    private bool ApplyRuleToTexture(TextureData texture, TextureRule rule)
    {
        bool modified = false;
        string platformName = GetPlatformName(rule.platform);
        
        if (rule.overrideMipmaps)
        {
            if (texture.importer.mipmapEnabled != rule.generateMipmaps)
            {
                texture.importer.mipmapEnabled = rule.generateMipmaps;
                modified = true;
            }
        }
        
        if (rule.overrideReadWrite)
        {
            if (texture.importer.isReadable != rule.readWrite)
            {
                texture.importer.isReadable = rule.readWrite;
                modified = true;
            }
        }
        
        TextureImporterPlatformSettings platformSettings = texture.importer.GetPlatformTextureSettings(platformName);
        bool platformModified = false;
        
        if (rule.overrideCompression)
        {
            TextureImporterFormat targetFormat = rule.platform == TexturePlatform.PC 
                ? (TextureImporterFormat)rule.pcCompression 
                : (TextureImporterFormat)rule.mobileCompression;
            
            if (platformSettings.format != targetFormat)
            {
                platformSettings.overridden = true;
                platformSettings.format = targetFormat;
                platformModified = true;
            }
            
            if (rule.compressionQualityOverride != CompressionQualityOverride.DontChange)
            {
                TextureImporterCompression targetCompression = TextureImporterCompression.Compressed;
                
                switch (rule.compressionQualityOverride)
                {
                    case CompressionQualityOverride.Fast:
                        targetCompression = TextureImporterCompression.CompressedLQ;
                        break;
                    case CompressionQualityOverride.Normal:
                        targetCompression = TextureImporterCompression.Compressed;
                        break;
                    case CompressionQualityOverride.Best:
                        targetCompression = TextureImporterCompression.CompressedHQ;
                        break;
                }
                
                if (platformSettings.textureCompression != targetCompression)
                {
                    platformSettings.overridden = true;
                    platformSettings.textureCompression = targetCompression;
                    platformModified = true;
                }
            }
            
            if (rule.crunchOverride != CrunchOverride.DontChange)
            {
                bool enableCrunch = rule.crunchOverride == CrunchOverride.Enabled;
                
                if (platformSettings.crunchedCompression != enableCrunch)
                {
                    platformSettings.overridden = true;
                    platformSettings.crunchedCompression = enableCrunch;
                    platformModified = true;
                }
                
                if (enableCrunch && platformSettings.compressionQuality != rule.crunchQuality)
                {
                    platformSettings.compressionQuality = rule.crunchQuality;
                    platformModified = true;
                }
            }
        }
        
        if (rule.overrideMaxSize)
        {
            bool shouldApply = !rule.onlyOverrideWhenLarger || platformSettings.maxTextureSize > rule.maxSize;
            
            if (shouldApply && platformSettings.maxTextureSize != rule.maxSize)
            {
                platformSettings.overridden = true;
                platformSettings.maxTextureSize = rule.maxSize;
                platformModified = true;
            }
        }
        {
            texture.importer.SetPlatformTextureSettings(platformSettings);
            modified = true;
        }
        
        return modified;
    }
    
    private string GetPlatformName(TexturePlatform platform)
    {
        switch (platform)
        {
            case TexturePlatform.PC: return "Standalone";
            case TexturePlatform.Android: return "Android";
            case TexturePlatform.iOS: return "iPhone";
            default: return "Standalone";
        }
    }
    
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
        return $"{bytes / (1024 * 1024 * 1024):F1} GB";
    }
}
