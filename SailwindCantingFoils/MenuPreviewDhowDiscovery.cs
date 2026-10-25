using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace SailwindCantingFoils
{
    /// <summary>
    /// Finds Sailwind's actual dhow boat component in the main-menu scene, then
    /// uses BoatRefs.boatModel as the renderable model root rather than assuming
    /// the component's GameObject is itself the visible ship model.
    /// </summary>
    internal static class MenuPreviewDhowDiscovery
    {
        private const string BoatModelFieldName = "boatModel";
        private const float RetryIntervalSeconds = 0.5f;

        internal static IEnumerator Watch(ManualLogSource logger)
        {
            FieldInfo boatModelField = typeof(BoatRefs).GetField(
                BoatModelFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (boatModelField == null)
            {
                logger.LogError("Could not resolve BoatRefs.boatModel; visibility test remains inactive.");
                yield break;
            }

            var wait = new WaitForSeconds(RetryIntervalSeconds);
            bool waitingMessageLogged = false;
            bool ambiguityMessageLogged = false;
            bool cameraMessageLogged = false;
            bool layerMessageLogged = false;
            int lastReportedInstanceId = 0;
            while (true)
            {
                StartMenu menu = Object.FindObjectOfType(typeof(StartMenu)) as StartMenu;
                if (menu != null && StartMenu.menuEnabled)
                {
                    List<BoatRefs> dhowCandidates = FindMenuDhowCandidates(menu, boatModelField);
                    if (dhowCandidates.Count == 1)
                    {
                        BoatRefs boat = dhowCandidates[0];
                        Transform model = boatModelField.GetValue(boat) as Transform;
                        Camera menuCamera = FindMenuCamera(menu);
                        Renderer visibleRenderer = FoilSystem.FindCameraRenderedModelRenderer(model, menuCamera);

                        if (model != null && menuCamera != null && visibleRenderer != null)
                        {
                            waitingMessageLogged = false;
                            ambiguityMessageLogged = false;
                            cameraMessageLogged = false;
                            layerMessageLogged = false;

                            int instanceId = model.GetInstanceID();
                            if (instanceId != lastReportedInstanceId)
                            {
                                lastReportedInstanceId = instanceId;
                                logger.LogInfo(
                                    "Found main-menu dhow BoatRefs '" + boat.gameObject.name +
                                    "' with boatModel '" + model.name + "'; menu camera '" +
                                    menuCamera.name + "' renders model layer " +
                                    visibleRenderer.gameObject.layer + " (" +
                                    LayerMask.LayerToName(visibleRenderer.gameObject.layer) +
                                    "). Runtime boatModel hierarchy follows:\n" +
                                    DescribeHierarchy(model) +
                                    "Visible model bounds: center=" + visibleRenderer.bounds.center +
                                    ", size=" + visibleRenderer.bounds.size);
                            }

                            bool available = FoilSystem.TryCreateVisibilityCubes(
                                model,
                                menuCamera,
                                visibleRenderer,
                                logger,
                                out bool created);
                            if (available && created)
                                logger.LogInfo("Created magenta and yellow visibility cubes under BoatRefs.boatModel.");
                        }
                        else if (menuCamera == null && !cameraMessageLogged)
                        {
                            cameraMessageLogged = true;
                            logger.LogWarning("Dhow candidate found, but no active menu camera is available yet.");
                        }
                        else if (model != null && visibleRenderer == null && !layerMessageLogged)
                        {
                            layerMessageLogged = true;
                            logger.LogWarning("No active dhow model renderer uses a layer included by the menu camera; visibility cubes were not created.");
                        }
                    }
                    else if (dhowCandidates.Count > 1 && !ambiguityMessageLogged)
                    {
                        ambiguityMessageLogged = true;
                        logger.LogWarning(
                            "Multiple dhow BoatRefs candidates exist in the main-menu scene; no visibility cubes were created: " +
                            DescribeCandidates(dhowCandidates, boatModelField));
                    }
                    else if (dhowCandidates.Count == 0 && !waitingMessageLogged)
                    {
                        waitingMessageLogged = true;
                        logger.LogInfo("Main menu is active; waiting for a dhow BoatRefs.boatModel with a rendered mesh.");
                    }
                }

                yield return wait;
            }
        }

        private static List<BoatRefs> FindMenuDhowCandidates(StartMenu menu, FieldInfo boatModelField)
        {
            var candidates = new List<BoatRefs>();
            BoatRefs[] boats = Object.FindObjectsOfType<BoatRefs>();
            for (int i = 0; i < boats.Length; i++)
            {
                BoatRefs boat = boats[i];
                if (boat == null || !boat.gameObject.activeInHierarchy ||
                    boat.gameObject.scene.handle != menu.gameObject.scene.handle)
                    continue;

                Transform model = boatModelField.GetValue(boat) as Transform;
                if (model == null || !model.gameObject.activeInHierarchy)
                    continue;

                bool nameIdentifiesDhow =
                    boat.gameObject.name.IndexOf("dhow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    model.name.IndexOf("dhow", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (nameIdentifiesDhow)
                    candidates.Add(boat);
            }
            return candidates;
        }

        private static Camera FindMenuCamera(StartMenu menu)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
                return mainCamera;

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera != null && camera.isActiveAndEnabled &&
                    camera.gameObject.scene.handle == menu.gameObject.scene.handle)
                    return camera;
            }
            return null;
        }

        private static string DescribeCandidates(List<BoatRefs> candidates, FieldInfo boatModelField)
        {
            var description = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
            {
                Transform model = boatModelField.GetValue(candidates[i]) as Transform;
                if (i > 0)
                    description.Append("; ");
                description.Append(candidates[i].gameObject.name)
                    .Append(" -> ")
                    .Append(model != null ? model.name : "<no boatModel>");
            }
            return description.ToString();
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
