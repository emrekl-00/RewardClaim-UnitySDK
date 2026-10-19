using System;
using System.IO;
using System.Linq;
using RewardClaims;
using UnityEditor;
using UnityEditor.AssetPackage;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Exports the SDK and the Coin Catcher sample as .unitypackage files. Lives in an Editor folder
    /// outside both exported roots, so build tooling is never shipped to the games that import them.
    /// </summary>
    public static class PackageExporter
    {
        private const string SdkRoot = "Assets/RewardClaimsSDK";
        private const string SampleRoot = "Assets/SampleGame";
        private const string TmpEssentialsRoot = "Assets/TextMesh Pro";

        /// <summary>
        /// Menu entry and command-line entry point:
        /// <c>unity run RewardClaimsProject -- -executeMethod BuildTools.PackageExporter.Export</c>
        /// </summary>
        [MenuItem("Build/Export Packages")]
        public static void Export()
        {
            var succeeded = false;
            try
            {
                // Packages-out sits at the repo root, next to the Unity project folder.
                var outputFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Packages-out"));
                Directory.CreateDirectory(outputFolder);

                // Tests stay out of the SDK package: an importing game would otherwise need the
                // Test Framework package just to compile, and our tests are of no use to it.
                ExportFolders(new[] { SdkRoot }, $"{SdkRoot}/Tests",
                    Path.Combine(outputFolder, $"RewardClaimsSDK-{SdkInfo.Version}.unitypackage"));

                // The sample ships TMP Essential Resources (font, shaders, TMP Settings). Without them
                // every label in the scene has no font and renders blank, with no error and no
                // importer prompt. The files keep Unity's own GUIDs, so a project that already has
                // them sees them as unchanged in the import dialog.
                ExportFolders(new[] { SampleRoot, TmpEssentialsRoot }, null,
                    Path.Combine(outputFolder, $"CoinCatcherSample-{SdkInfo.Version}.unitypackage"));
                succeeded = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            // In batch mode the exit code is how the CLI (or CI) learns the export failed.
            if (Application.isBatchMode)
                EditorApplication.Exit(succeeded ? 0 : 1);
        }

        private static void ExportFolders(string[] roots, string excludedFolder, string packagePath)
        {
            // Paths are collected by hand instead of using ExportPackageOptions.Recurse, because
            // Recurse on the root would pull the excluded folder back in. FindAssets also returns
            // folders, so the package keeps the folder structure. IncludeDependencies is not used
            // on purpose: it would copy the SDK's scripts into the sample package.
            var paths = AssetDatabase.FindAssets("", roots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => excludedFolder == null || !IsInside(path, excludedFolder))
                .Concat(roots)
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            if (paths.Any(path => path.Contains("/Tests/") || path.Contains("/BuildTools/")))
                throw new InvalidOperationException($"Refusing to export tests or build tools to {packagePath}.");

            // ExportPackage writes each asset together with its .meta file. The .meta holds the
            // asset's GUID, and scenes, prefabs and assets reference each other by GUID, so the
            // references still resolve in the importing project. (Package.Export replaces the
            // AssetDatabase.ExportPackage call that Unity 6.6 marks obsolete; no owner org id.)
            Package.Export(new ExportPackageParameters(paths, packagePath, null, ExportPackageOptions.Default));

            var size = new FileInfo(packagePath).Length / 1024f;
            var folders = paths.Count(AssetDatabase.IsValidFolder);
            Debug.Log($"[PackageExporter] {Path.GetFileName(packagePath)}: {paths.Length} entries " +
                      $"({paths.Length - folders} files, {folders} folders), {size:0.0} KB -> {packagePath}");
        }

        private static bool IsInside(string path, string folder) =>
            path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal);
    }
}
