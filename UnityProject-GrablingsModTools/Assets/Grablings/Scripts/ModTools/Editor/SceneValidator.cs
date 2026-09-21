using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;

using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Grablings.ModTools.Editor
{
	public static class SceneValidator
	{
		public static bool TryValidate(IEnumerable<string> bundleNames, out List<string> problems)
		{
			problems = new List<string>();

			var scenes = new List<(string Bundle, string Path)>();
			foreach (string bundleName in bundleNames)
			{
				string[] scenePaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName)
					.Where(AssetBundleUtils.IsScene)
					.ToArray();

				if (scenePaths.Length == 0)
				{
					problems.Add($"{bundleName}: contains no scene ({AssetBundleUtils.SceneExtension}).");
					continue;
				}

				scenes.AddRange(scenePaths.Select(path => (bundleName, path)));
			}

			if (scenes.Count == 0)
				return true;

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				return false;

			SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
			try
			{
				for (int i = 0; i < scenes.Count; i++)
				{
					(string bundle, string path) = scenes[i];
					EditorUtility.DisplayProgressBar("Validating scenes", path, (float)i / scenes.Count);
					ValidateScene(EditorSceneManager.OpenScene(path, OpenSceneMode.Single), bundle, problems);
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();

				if (setup.Length > 0)
					EditorSceneManager.RestoreSceneManagerSetup(setup);
				else
					EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			}

			return true;
		}

		private static void ValidateScene(Scene scene, string bundleName, List<string> errors)
		{
			string label = $"{bundleName} / {scene.name}";

			bool hasLightingData = Lightmapping.lightingDataAsset != null;
			if (hasLightingData)
			{
				if (LightmapSettings.lightmaps.Length == 0)
				{
					errors.Add($"{label}: no lightmaps found");
				}

				LightProbes probes = LightmapSettings.lightProbes;
				if (probes == null || probes.count == 0)
				{
					errors.Add($"{label}: no baked light probes, add a Light Probe Group and bake.");
				}

				bool hasLightingSettings = Lightmapping.TryGetLightingSettings(out LightingSettings settings) && settings != null;
				if (hasLightingSettings)
				{
					if (settings.mixedBakeMode != MixedLightingMode.IndirectOnly)
					{
						errors.Add($"{label}: mixed lighting mode is {settings.mixedBakeMode}, it should be Baked Indirect.");
					}

					if (settings.directionalityMode != LightmapsMode.NonDirectional)
					{
						errors.Add($"{label}:Lightmap directional mode is not set to Non-Directional.");
					}
				}
				else
				{
					errors.Add($"{label}: no Lighting Settings asset assigned");
				}
			}
			else
			{
				errors.Add($"{label}: lighting has never been baked (no Lighting Data asset).");
			}


			Camera[] cameras = scene.GetRootGameObjects()
				.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
				.ToArray();

			if (cameras.Length > 0)
				errors.Add($"{label}: contains {cameras.Length} camera(s) ({string.Join(", ", cameras.Select(camera => camera.name))})");


		}
	}
}
