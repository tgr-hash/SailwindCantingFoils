using System.Collections;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace SailwindCantingFoils
{
    /// <summary>
    /// Resolves the Al'Ankh menu decoration through StartMenu's serialized field.
    /// It retries quietly while the menu scene or field is unavailable.
    /// </summary>
    internal static class MenuPreviewDhowDiscovery
    {
        private const string AlAnkhDecorationFieldName = "decoAlAnkh";
        private const float RetryIntervalSeconds = 0.5f;

        internal static IEnumerator Watch(ManualLogSource logger)
        {
            int lastReportedInstanceId = 0;
            FieldInfo decorationField = typeof(StartMenu).GetField(
                AlAnkhDecorationFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (decorationField == null)
            {
                logger.LogError("Could not resolve StartMenu.decoAlAnkh; menu foil prototype remains inactive.");
                yield break;
            }

            var wait = new WaitForSeconds(RetryIntervalSeconds);
            while (true)
            {
                StartMenu menu = Object.FindObjectOfType(typeof(StartMenu)) as StartMenu;
                GameObject candidate = menu != null
                    ? decorationField.GetValue(menu) as GameObject
                    : null;

                if (candidate != null)
                {
                    int instanceId = candidate.GetInstanceID();
                    if (instanceId != lastReportedInstanceId)
                    {
                        lastReportedInstanceId = instanceId;
                        logger.LogInfo(
                            "Found StartMenu.decoAlAnkh candidate '" + candidate.name +
                            "' (active in hierarchy: " + candidate.activeInHierarchy + "). " +
                            "Runtime hierarchy follows:\n" + DescribeHierarchy(candidate.transform));
                    }

                    bool available = FoilSystem.TryCreateTestAssembly(candidate.transform, out bool created);
                    if (available && created)
                        logger.LogInfo("Created port and starboard foil test geometry under StartMenu.decoAlAnkh.");
                }

                yield return wait;
            }
        }

        private static string DescribeHierarchy(Transform root)
        {
            var description = new StringBuilder();
            int remainingNodes = 100;
            AppendHierarchy(root, 0, description, ref remainingNodes);
            if (remainingNodes <= 0)
                description.AppendLine("  ... hierarchy truncated at 100 objects");
            return description.ToString();
        }

        private static void AppendHierarchy(
            Transform current,
            int depth,
            StringBuilder description,
            ref int remainingNodes)
        {
            if (current == null || remainingNodes <= 0 || depth > 4)
                return;

            remainingNodes--;
            description.Append(' ', depth * 2).Append(current.name).Append(" [");
            Component[] components = current.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (i > 0)
                    description.Append(", ");
                description.Append(components[i] != null ? components[i].GetType().Name : "MissingComponent");
            }
            description.AppendLine("]");

            for (int i = 0; i < current.childCount && remainingNodes > 0; i++)
                AppendHierarchy(current.GetChild(i), depth + 1, description, ref remainingNodes);
        }
    }
}
