using System;
using System.IO;
using UnityEngine;

namespace Grablings.ModTools.Editor
{
	public static class AssetBundleUtils
	{
		public const string Folder = "AssetBundles";
		public const string BundleExtension = ".bundle";
		public const string SceneExtension = ".unity";
		public const string StandaloneFolder = "Standalone";
		public const string AndroidFolder = "Android";

		public static bool IsScene(string assetPath) => assetPath.EndsWith(SceneExtension, StringComparison.OrdinalIgnoreCase);

#if UNITY_EDITOR
		public static string BuildRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), Folder);

		public static string BuildDirectory(string platformFolder) => Path.Combine(BuildRoot, platformFolder);

		/// The editor runs as a desktop player whatever the active build target is, so Android bundles
		/// (ASTC textures, GLES/Vulkan shader variants) cannot be loaded here - always play the Standalone ones.
		public static string LoadDirectory => BuildDirectory(StandaloneFolder);
#else
		public static string LoadDirectory => $"{Application.streamingAssetsPath}/{Folder}";
#endif
	}
}
