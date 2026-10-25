using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace SailwindCantingFoils
{
    /// <summary>
    /// Finds the unique active BoatRefs model whose renderer bounds intersect
    /// the menu camera frustum. Layer-mask membership alone is not visibility.
    /// </summary>
    internal static class MenuPreviewDhowDiscovery
    {
        private const float RetryIntervalSeconds = 0.5f;

        internal static IEnumerator Watch(ManualLogSource logger)
        {
            var wait = new WaitForSeconds(RetryIntervalSeconds);
            string lastInventorySignature = null;
            string lastAmbiguitySignature = null;
            string lastNoCandidateSignature = null;
            bool cameraWarningLogged = false;

            while (true)
            {
                StartMenu menu = Object.FindObjectOfType(typeof(StartMenu)) as StartMenu;
                if (menu != null && StartMenu.menuEnabled)
                {
                    Camera camera = FindMenuCamera(menu);
                    List<BoatRefs> inventory = FindLoadedBoatRefs();
                    string signature = BuildSituationSignature(inventory, menu, camera);
                    if (signature != lastInventorySignature)
                    {
                        lastInventorySignature = signature;
                        logger.LogInfo("Main-menu BoatRefs candidate diagnostics (menu path='" +
                            GetHierarchyPath(menu.transform) + "', camera='" +
                            (camera != null ? GetHierarchyPath(camera.transform) : "<none>") + "'):\n" +
                            DescribeCandidates(inventory, menu, camera));
                    }

                    if (camera == null)
                    {
                        if (!cameraWarningLogged)
                        {
                            logger.LogWarning("StartMenu is active, but no enabled menu camera is available; dhow discovery will retry.");
                            cameraWarningLogged = true;
                        }
                    }
                    else
                    {
                        cameraWarningLogged = false;
                        List<BoatRefs> inViewCandidates = FindFrustumCandidates(inventory, menu, camera);
                        if (inViewCandidates.Count == 1)
                        {
                            BoatRefs boat = inViewCandidates[0];
                            Transform model = boat.boatModel;
                            Renderer renderer = FoilSystem.FindCameraRenderedModelRenderer(model, camera);
                            if (renderer != null)
                            {
                                logger.LogInfo("Selected unique active main-menu boat candidate at '" +
                                    GetHierarchyPath(boat.transform) + "'; visible model='" +
                                    GetHierarchyPath(model) + "', renderer bounds=" + renderer.bounds +
                                    ", camera distance=" + Vector3.Distance(camera.transform.position, renderer.bounds.center) +
                                    ". It is the only active BoatRefs candidate in the menu scene whose camera-renderable model bounds intersect the menu camera frustum.");

                                bool available = FoilSystem.TryCreateVisibilityCubes(
                                    model, camera, renderer, logger, out bool created);
                                if (available)
                                {
                                    if (created)
                                        logger.LogInfo("Created magenta and yellow visibility cubes under the uniquely selected menu boat model.");
                                    else
                                        logger.LogInfo("Visibility test geometry already exists under the selected model; not creating duplicates.");
                                    yield break;
                                }
                            }
                        }
                        else if (inViewCandidates.Count > 1)
                        {
                            string ambiguity = BuildCandidateIdSignature(inViewCandidates);
                            if (ambiguity != lastAmbiguitySignature)
                            {
                                lastAmbiguitySignature = ambiguity;
                                logger.LogWarning("Multiple active BoatRefs model bounds intersect the menu camera frustum; no cubes created. Candidate diagnostics above show hierarchy, parent path, renderer bounds, and camera distance. Selection remains ambiguous; discovery will retry without repeating this warning unless the candidate set changes.");
                            }
                        }
                        else
                        {
                            lastAmbiguitySignature = null;
                            string noCandidate = signature + "|camera=" + camera.GetInstanceID();
                            if (noCandidate != lastNoCandidateSignature)
                            {
                                lastNoCandidateSignature = noCandidate;
                                logger.LogInfo("No active dhow BoatRefs model bounds currently intersect the menu camera frustum; discovery will retry.");
                            }
                        }
                    }
                }

                yield return wait;
            }
        }

        private static List<BoatRefs> FindLoadedBoatRefs()
        {
            var candidates = new List<BoatRefs>();
            BoatRefs[] boats = Resources.FindObjectsOfTypeAll<BoatRefs>();
            for (int i = 0; i < boats.Length; i++)
            {
                BoatRefs boat = boats[i];
                if (boat == null || boat.gameObject.scene.handle == 0)
                    continue;
                candidates.Add(boat);
            }
            return candidates;
        }

        private static List<BoatRefs> FindFrustumCandidates(List<BoatRefs> inventory, StartMenu menu, Camera camera)
        {
            var candidates = new List<BoatRefs>();
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
            for (int i = 0; i < inventory.Count; i++)
            {
                BoatRefs boat = inventory[i];
                if (boat == null || !boat.gameObject.activeInHierarchy || boat.boatModel == null ||
                    !boat.boatModel.gameObject.activeInHierarchy ||
                    boat.gameObject.scene.handle != menu.gameObject.scene.handle ||
                    !IsDhowCandidate(boat))
                    continue;

                Bounds bounds = new Bounds();
                if (FoilSystem.TryGetCameraRenderedModelBounds(boat.boatModel, camera, out bounds) &&
                    GeometryUtility.TestPlanesAABB(frustum, bounds))
                    candidates.Add(boat);
            }
            return candidates;
        }

        private static Camera FindMenuCamera(StartMenu menu)
        {
            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
                return main;

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

        private static string DescribeCandidates(List<BoatRefs> inventory, StartMenu menu, Camera camera)
        {
            if (inventory.Count == 0)
                return "<no BoatRefs objects found in loaded scenes>";

            var result = new StringBuilder();
            Plane[] frustum = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            for (int i = 0; i < inventory.Count; i++)
            {
                BoatRefs boat = inventory[i];
                Transform model = boat != null ? boat.boatModel : null;
                if (i > 0)
                    result.AppendLine();
                result.Append("- BoatRefs path='").Append(boat != null ? GetHierarchyPath(boat.transform) : "<destroyed>")
                    .Append("', parent='").Append(boat != null && boat.transform.parent != null ? GetHierarchyPath(boat.transform.parent) : "<none>")
                    .Append("', scene='").Append(boat != null ? boat.gameObject.scene.name : "<none>")
                    .Append("', active=").Append(boat != null && boat.gameObject.activeInHierarchy)
                    .Append(", model='").Append(model != null ? GetHierarchyPath(model) : "<null>")
                    .Append("', modelActive=").Append(model != null && model.gameObject.activeInHierarchy);

                Bounds bounds = new Bounds();
                bool hasBounds = camera != null && model != null &&
                    FoilSystem.TryGetCameraRenderedModelBounds(model, camera, out bounds);
                if (hasBounds)
                {
                    bool inFrustum = GeometryUtility.TestPlanesAABB(frustum, bounds);
                    result.Append(", rendererBounds=").Append(bounds)
                        .Append(", cameraDistance=").Append(Vector3.Distance(camera.transform.position, bounds.center).ToString("F2"))
                        .Append(", intersectsCameraFrustum=").Append(inFrustum);
                }
                else
                {
                    result.Append(", cameraRendererBounds=<none>");
                }
                result.Append(", sameSceneAsStartMenu=")
                    .Append(menu != null && boat != null && boat.gameObject.scene.handle == menu.gameObject.scene.handle);
            }
            return result.ToString();
        }

        private static string BuildSituationSignature(List<BoatRefs> inventory, StartMenu menu, Camera camera)
        {
            var signature = new StringBuilder();
            signature.Append(menu.GetInstanceID()).Append('|').Append(camera != null ? camera.GetInstanceID() : 0);
            for (int i = 0; i < inventory.Count; i++)
            {
                BoatRefs boat = inventory[i];
                if (boat == null)
                    continue;
                signature.Append('|').Append(boat.GetInstanceID()).Append(':').Append(boat.gameObject.activeInHierarchy);
                Transform model = boat.boatModel;
                signature.Append(':').Append(model != null ? model.GetInstanceID() : 0)
                    .Append(':').Append(model != null && model.gameObject.activeInHierarchy);
                if (camera != null && model != null)
                {
                    Bounds bounds;
                    bool hasBounds = FoilSystem.TryGetCameraRenderedModelBounds(model, camera, out bounds);
                    signature.Append(':').Append(hasBounds);
                    if (hasBounds)
                        signature.Append(':').Append(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds));
                }
            }
            return signature.ToString();
        }

        private static string BuildCandidateIdSignature(List<BoatRefs> candidates)
        {
            var signature = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
                signature.Append(candidates[i].GetInstanceID()).Append('|');
            return signature.ToString();
        }

        // Sailwind's loaded scene inventory identifies vessel families in the
        // actual runtime hierarchy names (for example "BOAT dhow medium").
        // Keep all size variants eligible; frustum visibility must reduce the
        // dhow family to exactly one candidate before anything is attached.
        private static bool IsDhowCandidate(BoatRefs boat)
        {
            return boat != null &&
                (GetHierarchyPath(boat.transform).IndexOf("dhow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (boat.boatModel != null && GetHierarchyPath(boat.boatModel).IndexOf("dhow", System.StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";
            var path = new StringBuilder(transform.name);
            Transform parent = transform.parent;
            while (parent != null)
            {
                path.Insert(0, parent.name + "/");
                parent = parent.parent;
            }
            return path.ToString();
        }
    }
}
