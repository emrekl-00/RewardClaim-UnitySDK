using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Builds the Coin Catcher APK. Lives in an Editor folder, so it is never shipped in a player
    /// or in the SDK package. Player Settings (package id, IL2CPP, ARM64, portrait) are read from
    /// ProjectSettings, so this script only says WHAT to build and WHERE.
    /// </summary>
    public static class AndroidBuild
    {
        private const string DefaultOutput = "Builds/Android/CoinCatcher.apk";

        /// <summary>
        /// Menu entry and command-line entry point:
        /// <c>unity build RewardClaimsProject --target Android --execute-method BuildTools.AndroidBuild.BuildApk --output-path RewardClaimsProject/Builds/Android/CoinCatcher.apk</c>
        /// </summary>
        [MenuItem("Build/Android APK")]
        public static void BuildApk()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = ReadArgument("-buildOutput") ?? DefaultOutput,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            // An .apk installs directly with adb; an .aab (App Bundle) is what Google Play wants.
            EditorUserBuildSettings.buildAppBundle = false;

            var summary = BuildPipeline.BuildPlayer(options).summary;
            // summary.totalSize counts intermediate files too, so measure the .apk that came out instead.
            var apk = new FileInfo(options.locationPathName);
            var apkSize = apk.Exists ? $"{apk.Length / (1024f * 1024f):0.0} MB" : "no file";
            Debug.Log($"[AndroidBuild] {summary.result}: {apk.FullName}, {apkSize}, " +
                      $"{summary.totalTime:mm\\:ss}, {summary.totalErrors} errors");

            // In batch mode the exit code is how the CLI (or CI) learns the build failed.
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
