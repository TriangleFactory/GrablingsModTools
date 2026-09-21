using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Grablings.ModTools.Editor
{
	public class ModTools : EditorWindow
	{
		private const float IconWidth = 20f;
		private const int MaxProblemsInDialog = 10;

		private const string NoSceneTooltip = "This AssetBundle contains no scene (.unity). Mods are loaded as scenes, so this bundle will not be playable.";

		[Serializable]
		private struct BundleEntry
		{
			public string Name;
			public bool ContainsScene;
			public bool Selected;
		}


		[SerializeField] private List<BundleEntry> _bundles = new List<BundleEntry>();
		[SerializeField] private Vector2 _scrollPosition;

		private GUIContent _warningIcon;

		[MenuItem("Grablings/ModTools")]
		private static void ShowWindow()
		{
			var window = GetWindow<ModTools>(false, "ModTools", true);
			window.RefreshBundleNames();
			window.Show();
		}

		private void OnEnable()
		{
			// A domain reload keeps the serialized list, so only fill it when there is nothing to keep.
			if (_bundles.Count == 0)
				RefreshBundleNames();
		}

		private void OnGUI()
		{
			EditorGUILayout.Space();

			if (GUILayout.Button("Open Modio"))
			{
				Application.OpenURL("https://mod.io/g/grablings");
			}

			EditorGUILayout.Space();

			if (_bundles.Count == 0)
			{
				EditorGUILayout.HelpBox("No AssetBundle names found. Tag assets with an AssetBundle name first.", MessageType.Warning);
				return;
			}

			EditorGUILayout.LabelField("AssetBundles", EditorStyles.boldLabel);

			if (GUILayout.Button("Refresh AssetBundle Names"))
			{
				RefreshBundleNames();
			}
			EditorGUILayout.Space();

			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
			for (int i = 0; i < _bundles.Count; i++)
			{
				BundleEntry entry = _bundles[i];

				EditorGUILayout.BeginHorizontal();

				// Reserve the same rect on every row so the toggles line up, warned or not.
				Rect iconRect = GUILayoutUtility.GetRect(IconWidth, EditorGUIUtility.singleLineHeight, GUIStyle.none, GUILayout.Width(IconWidth));
				if (!entry.ContainsScene)
					GUI.Label(iconRect, GetWarningIcon());

				using (new EditorGUI.DisabledScope(!entry.ContainsScene))
				{
					bool selected = EditorGUILayout.ToggleLeft(entry.Name, entry.Selected);
					if (selected != entry.Selected)
					{
						entry.Selected = selected;
						_bundles[i] = entry; // The entry is a copy, so the change has to be written back.
					}
				}

				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndScrollView();


			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Select All"))
			{
				SetAllSelected(true);
			}
			if (GUILayout.Button("Select None"))
			{
				SetAllSelected(false);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			
			string[] selection = _bundles.Where(bundle => bundle.Selected).Select(bundle => bundle.Name).ToArray();
			using (new EditorGUI.DisabledScope(selection.Length == 0))
			{
				if (GUILayout.Button($"Build {selection.Length} AssetBundle(s)"))
				{
					// Validation opens scenes, which must not happen in the middle of a GUI pass.
					EditorApplication.delayCall += () => BuildAssetBundles(selection);
				}
			}
		}

		private void SetAllSelected(bool selected)
		{
			for (int i = 0; i < _bundles.Count; i++)
			{
				BundleEntry entry = _bundles[i];
				entry.Selected = selected && entry.ContainsScene;
				_bundles[i] = entry;
			}
		}

		private GUIContent GetWarningIcon()
		{
			if (_warningIcon == null || _warningIcon.image == null)
				_warningIcon = new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image);

			_warningIcon.tooltip = NoSceneTooltip;
			return _warningIcon;
		}

		private void RefreshBundleNames()
		{
			List<string> wasSelected = _bundles.Where(bundle => bundle.Selected).Select(bundle => bundle.Name).ToList();

			_bundles.Clear();
			foreach (string bundleName in AssetDatabase.GetAllAssetBundleNames())
			{
				const string RaytracingBundle = "unifiedraytracing";
				if (bundleName == RaytracingBundle)
					continue;

				bool hasScene = ContainsScene(bundleName);
				_bundles.Add(new BundleEntry
				{
					Name = bundleName,
					ContainsScene = hasScene,
					// A bundle can lose its scene between refreshes, so only keep a selection that is still buildable.
					Selected = hasScene && wasSelected.Contains(bundleName)
				});
			}
		}

		private static bool ContainsScene(string bundleName)
			=> AssetDatabase.GetAssetPathsFromAssetBundle(bundleName).Any(AssetBundleUtils.IsScene);

		private static void BuildAssetBundles(string[] bundleNames)
		{
			if (!ConfirmSceneValidation(bundleNames))
				return;

			var builds = new List<AssetBundleBuild>(bundleNames.Length);

			foreach (string bundleName in bundleNames)
			{
				string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
				if (assetPaths.Length == 0)
				{
					Debug.LogError($"[AssetbundleBuilder] No assets tagged with AssetBundle name '{bundleName}'.");
					continue;
				}

				builds.Add(new AssetBundleBuild
				{
					assetBundleName = bundleName + AssetBundleUtils.BundleExtension,
					assetNames = assetPaths
				});
			}

			if (builds.Count == 0) 
				return;

			
			BuildForPlatform(builds, AssetBundleUtils.StandaloneFolder, BuildTarget.StandaloneWindows64);
			BuildForPlatform(builds, AssetBundleUtils.AndroidFolder, BuildTarget.Android);

			Debug.Log($"[AssetbundleBuilder] Built {builds.Count} AssetBundle(s).");
			EditorUtility.RevealInFinder(AssetBundleUtils.BuildRoot + Path.DirectorySeparatorChar);
		}

		private static bool ConfirmSceneValidation(string[] bundleNames)
		{
			if (!SceneValidator.TryValidate(bundleNames, out List<string> problems))
				return false;

			if (problems.Count == 0)
				return true;

			foreach (string problem in problems)
				Debug.LogWarning($"[AssetbundleBuilder] {problem}");

			string summary = string.Join("\n\n", problems.Take(MaxProblemsInDialog));
			if (problems.Count > MaxProblemsInDialog)
				summary += $"\n\n...and {problems.Count - MaxProblemsInDialog} more, see the Console.";

			return EditorUtility.DisplayDialog(
				"Scene validation",
				$"{problems.Count} issue(s) found:\n\n{summary}",
				"Build Anyway",
				"Cancel");
		}

		private static void BuildForPlatform(List<AssetBundleBuild> builds, string platformFolder, BuildTarget target)
		{
			string dir = AssetBundleUtils.BuildDirectory(platformFolder);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			BuildPipeline.BuildAssetBundles(
				dir,
				builds.ToArray(),
				BuildAssetBundleOptions.ChunkBasedCompression,
				target
			);
		}
	}
}
