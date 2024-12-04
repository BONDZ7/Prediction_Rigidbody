using UnityEngine;
using UnityEditor;

public class ExportPackageWithProjectSettings : MonoBehaviour
{
    [MenuItem("Export/FullProjectExport")]
    static void Export()
    {
        AssetDatabase.ExportPackage(AssetDatabase.GetAllAssetPaths(), PlayerSettings.productName + ".unitypackage", ExportPackageOptions.Interactive | ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies | ExportPackageOptions.IncludeLibraryAssets);
    }
}
