using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// One-shot project setup.
    ///
    /// Tags, layers and the collision matrix are stored in ProjectSettings YAML,
    /// which merges badly and is easy to get subtly wrong by hand. Doing it in
    /// code means the setup is reviewable, repeatable, and documents why each
    /// layer exists.
    ///
    /// Run once from Game > Bootstrap Project after creating the project.
    /// </summary>
    public static class ProjectBootstrap
    {
        private static readonly string[] RequiredTags =
        {
            "Player",
            "Bot",
            "Spike",
            "BombSite",
        };

        /// <summary>
        /// Layer names in the order they are assigned, starting at 8 (0-7 are
        /// Unity's built-ins and cannot be renamed).
        /// </summary>
        private static readonly string[] RequiredLayers =
        {
            // Players and bots share one layer on purpose: nothing about hit
            // resolution should care which one it is shooting at. Team identity
            // lives on the TeamMember component, not in the physics layers.
            "Character",    // 8  - player and bot bodies
            "WeakPoint",    // 9  - head colliders; ShotResolver treats hits here as critical
            "Environment",  // 10 - static geometry, blocks sight and bullets
            "Interactable", // 11 - the spike, doors
        };

        [MenuItem("Game/Bootstrap Project", priority = 0)]
        public static void Bootstrap()
        {
            int tagsAdded = EnsureTags();
            int layersAdded = EnsureLayers();
            ConfigureCollisionMatrix();

            AssetDatabase.SaveAssets();

            string summary = tagsAdded == 0 && layersAdded == 0
                ? "Nothing to add - the tags and layers were already in place.\n\n" +
                  "The collision matrix was reapplied anyway, which is harmless."
                : $"Added {tagsAdded} tag(s) and {layersAdded} layer(s), and configured the " +
                  "collision matrix.\n\nCheck them under Project Settings > Tags and Layers.";

            // A dialog, not just a Debug.Log. This is a "did that work?" action, and
            // its only feedback used to be an info-level log line - invisible the
            // moment anyone filters the Console down to errors, which is exactly
            // what you do while working through compile errors. Silence then reads
            // as "the menu item is broken".
            // Guarded: DisplayDialog has nothing to draw under -batchmode, which is
            // how this runs from the command line (-executeMethod). The log below is
            // the batch-mode output, so nothing is lost.
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Bootstrap Project",
                    summary + "\n\nSafe to run again at any time.",
                    "OK");
            }

            Debug.Log(
                $"Project bootstrap complete. Added {tagsAdded} tag(s) and {layersAdded} layer(s), " +
                "and configured the collision matrix. Safe to run again.");
        }

        private static int EnsureTags()
        {
            SerializedObject tagManager = OpenTagManager();
            SerializedProperty tags = tagManager.FindProperty("tags");
            int added = 0;

            foreach (string tag in RequiredTags)
            {
                if (HasStringValue(tags, tag)) continue;

                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
                added++;
            }

            tagManager.ApplyModifiedProperties();
            return added;
        }

        private static int EnsureLayers()
        {
            SerializedObject tagManager = OpenTagManager();
            SerializedProperty layers = tagManager.FindProperty("layers");
            int added = 0;

            foreach (string layer in RequiredLayers)
            {
                if (HasStringValue(layers, layer)) continue;

                bool placed = false;
                // Index 0-7 are Unity's built-in layers and must not be touched.
                for (int i = 8; i < layers.arraySize; i++)
                {
                    SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                    slot.stringValue = layer;
                    added++;
                    placed = true;
                    break;
                }

                if (!placed) Debug.LogWarning($"No free layer slot for '{layer}'. Free one by hand in Project Settings.");
            }

            tagManager.ApplyModifiedProperties();
            return added;
        }

        private static void ConfigureCollisionMatrix()
        {
            int character = LayerMask.NameToLayer("Character");
            int weakPoint = LayerMask.NameToLayer("WeakPoint");

            if (character < 0 || weakPoint < 0) return;

            // Weak points are hitboxes, not physical volumes. Letting them collide
            // with characters makes players bump off each other's heads, which
            // looks exactly as silly as it sounds.
            Physics.IgnoreLayerCollision(weakPoint, character, true);
            Physics.IgnoreLayerCollision(weakPoint, weakPoint, true);
        }

        private static SerializedObject OpenTagManager() =>
            new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        private static bool HasStringValue(SerializedProperty array, string value)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).stringValue == value) return true;
            }
            return false;
        }
    }
}
