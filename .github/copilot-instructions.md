# Copilot Instructions: VEditor Tools Package

## Package Overview
Unity editor package (`com.valenvrc.veditortools`) providing batch import configuration tools for VRChat world development. Implements rule-based systems for textures and models via custom EditorWindow interfaces.

## Architecture

### Two Parallel Tool Systems
- **Model Importer** ([Editor/Model Importer/ModelImporter.cs](../Editor/Model%20Importer/ModelImporter.cs)) - Batch configure ModelImporter settings
- **Texture Importer** ([Editor/Texture Importer/TextureImporter.cs](../Editor/Texture%20Importer/TextureImporter.cs)) - Batch configure TextureImporter settings

Both follow identical architectural patterns:
1. EditorWindow with split-panel UI (rules left, asset list right)
2. Rule-based filtering and configuration system
3. JSON persistence via EditorPrefs
4. Paginated asset browsing (50 items/page)
5. Import/Export capability for rule sets

### Key Pattern: Rule System
Rules are serializable classes with:
- `enabled` flag for activation control
- Filter criteria (file type, mesh type, texture type, alpha channel, platform)
- Override enums with `DontChange` as default (allows selective property modification)
- Per-platform settings (PC/Android/iOS for textures)

Example from [ModelImporter.cs](../Editor/Model%20Importer/ModelImporter.cs#L78-L101):
```csharp
public class ModelRule {
    public bool enabled = true;
    public string ruleName = "New Rule";
    public ModelFileType fileTypeFilter = ModelFileType.All;
    public MeshTypeFilter meshTypeFilter = MeshTypeFilter.All;
    public ScaleFactorOverride scaleFactorOverride = ScaleFactorOverride.DontChange;
    // ... override properties always default to DontChange
}
```

### Data Loading Pattern
Both tools use deferred loading:
- `needsInitialLoad` flag prevents blocking on window open
- `EditorApplication.delayCall` triggers async load after UI renders
- Assets scanned via `AssetDatabase.FindAssets("t:Model")` or `t:Texture2D`
- Metadata extraction includes file size, dimensions/polycount, scene usage

### Rule Application Flow
1. User defines rules with filters and override values
2. Rules applied top-to-bottom (first matching rule wins)
3. Only properties with non-`DontChange` values are modified
4. Batch operation uses `AssetDatabase.StartAssetEditing()`/`StopAssetEditing()` for performance
5. Changes saved with `importer.SaveAndReimport()`

## Development Conventions

### File Organization
- Editor-only tools live in `Editor/` folder (no runtime code)
- Each tool is self-contained in its subfolder with `.meta` files
- No shared utilities between tools (intentional duplication for independence)

### Unity-Specific Patterns
- All windows accessed via `[MenuItem("ValenVRC/Tools/...")]` menu items
- Asset paths use `Assets/` or `Packages/` prefixes, convert to filesystem with:
  ```csharp
  string fullPath = Path.Combine(Application.dataPath, path.Substring(7)); // Assets/
  string packagesPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages");
  fullPath = Path.Combine(packagesPath, path.Substring(9)); // Packages/
  ```
- Scene filtering finds in-use assets via `GameObject.FindObjectsOfType<>()`

### State Persistence
Rules saved to EditorPrefs on `OnDisable()`:
```csharp
const string RULES_PREFS_KEY = "ModelImporterRules"; // or "TextureImporterRules"
string json = JsonUtility.ToJson(new ModelRulesList { rules });
EditorPrefs.SetString(RULES_PREFS_KEY, json);
```

### UI/UX Patterns
- F5 hotkey refreshes asset lists
- Pagination controls use Unicode arrows (◄◄ ◄ ► ►►)
- Search filter applies client-side on loaded assets
- Foldout panels for rule editing with `rule.isExpanded`
- Warning dialogs before destructive operations

## Platform-Specific Considerations
- **VRChat context**: Texture compression (ASTC/ETC2) and model optimization critical for performance
- **Mobile platforms**: Separate compression formats for Android/iOS
- **Lightmap UV generation**: Special handling with `YesOnlyWithoutUV2` option

## Adding New Features
When extending either tool:
1. Add new override enum (include `DontChange` option)
2. Add corresponding fields to Rule class
3. Implement UI in `DrawRulesPanel()` with conditional visibility
4. Add application logic in `ApplyRuleTo[Asset]()` method
5. Test with export/import to verify JSON serialization

## Debugging
- Asset loading issues: Check `Debug.Log()` statements for GUID/path counts
- Rule not applying: Verify filter matches and rule is enabled
- Performance issues: Batch operations should wrap in `AssetDatabase.StartAssetEditing()`

## License Notes
Custom non-commercial license ([LICENSE.md](../LICENSE.md)) - no standalone distribution of tools, attribution required for modifications.
