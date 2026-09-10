using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CasualKit.Editor
{
    public class SceneLoaderWindow : EditorWindow
    {
        [MenuItem("CasualKit/Scene Loader")]
        public static void ShowWindow()
        {
            GetWindow<SceneLoaderWindow>("Scene Loader");
        }

        private Vector2 scroll;

        private void OnGUI()
        {
            GUILayout.Label("Scenes ", EditorStyles.boldLabel);
            GUILayout.Space(5);

            var scenes = EditorBuildSettings.scenes;

            if (scenes.Length == 0)
            {
                EditorGUILayout.HelpBox("No scenes in Build Settings.", MessageType.Warning);
            }
            else
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);

                foreach (var scene in scenes)
                {
                    if (!scene.enabled) continue;

                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);

                    if (GUILayout.Button(sceneName, GUILayout.Height(28)))
                        OpenScene(scene.path);
                }

                EditorGUILayout.EndScrollView();
            }

            GUILayout.Space(12f);
        }

        private static void OpenScene(string scenePath)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog("Scene Loader", "Scene not found:\n" + scenePath, "OK");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(scenePath);
        }
    }
}