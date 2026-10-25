using BepInEx.Logging;
using UnityEngine;

namespace SailwindCantingFoils
{
    /// <summary>
    /// Creates temporary visibility markers under any ship model transform.
    /// Kept independent of menu discovery so it can be reused by later systems.
    /// </summary>
    internal static class FoilSystem
    {
        private const string AssemblyName = "SailwindCantingFoils_VisibilityTest";
        private static Material magentaMaterial;
        private static Material yellowMaterial;

        // Large enough to stand out beside the dhow; adjust for other ship scales.
        internal static float CubeSize = 3.0f;
        internal static float SideClearance = 0.75f;

        internal static Renderer FindCameraRenderedModelRenderer(Transform model, Camera camera)
        {
            if (model == null || camera == null)
                return null;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                int layerBit = 1 << renderer.gameObject.layer;
                if ((camera.cullingMask & layerBit) != 0)
                    return renderer;
            }

            return null;
        }

        /// <summary>
        /// Gets the union bounds for active, enabled model renderers whose
        /// layers are included by this camera. Frustum intersection is checked
        /// by discovery separately.
        /// </summary>
        internal static bool TryGetCameraRenderedModelBounds(Transform model, Camera camera, out Bounds bounds)
        {
            bounds = new Bounds();
            if (model == null || camera == null)
                return false;

            bool found = false;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        internal static bool TryCreateVisibilityCubes(
            Transform model,
            Camera camera,
            Renderer visibleModelRenderer,
            ManualLogSource logger,
            out bool created)
        {
            created = false;
            if (model == null || camera == null || visibleModelRenderer == null)
                return false;

            if (model.Find(AssemblyName) != null)
                return true;

            Bounds modelBounds;
            if (!TryGetVisibleBounds(model, out modelBounds))
                return false;

            var assembly = new GameObject(AssemblyName);
            assembly.transform.SetParent(model, false);
            assembly.transform.localPosition = Vector3.zero;
            assembly.transform.localRotation = Quaternion.identity;
            assembly.transform.localScale = Vector3.one;

            Vector3 side = model.right.normalized;
            float halfWidth = ProjectedHalfExtent(modelBounds, side);
            float offset = halfWidth + (CubeSize * 0.5f) + SideClearance;
            Vector3 leftCenter = modelBounds.center - side * offset;
            Vector3 rightCenter = modelBounds.center + side * offset;
            int layer = visibleModelRenderer.gameObject.layer;

            GameObject magenta = CreateCube(
                "VisibilityCube_Magenta",
                assembly.transform,
                leftCenter,
                layer,
                GetMaterial(ref magentaMaterial, "SailwindCantingFoils_Magenta", new Color(1f, 0f, 0.75f, 1f)));
            GameObject yellow = CreateCube(
                "VisibilityCube_Yellow",
                assembly.transform,
                rightCenter,
                layer,
                GetMaterial(ref yellowMaterial, "SailwindCantingFoils_Yellow", new Color(1f, 0.95f, 0f, 1f)));

            LogCube(logger, magenta, camera);
            LogCube(logger, yellow, camera);
            created = true;
            return true;
        }

        private static bool TryGetVisibleBounds(Transform model, out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static float ProjectedHalfExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 extents = bounds.extents;
            return Mathf.Abs(axis.x) * extents.x +
                   Mathf.Abs(axis.y) * extents.y +
                   Mathf.Abs(axis.z) * extents.z;
        }

        private static GameObject CreateCube(
            string objectName,
            Transform parent,
            Vector3 worldPosition,
            int layer,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.position = worldPosition;
            part.transform.localRotation = Quaternion.identity;
            Vector3 parentScale = parent.lossyScale;
            part.transform.localScale = new Vector3(
                CubeSize / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
                CubeSize / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
                CubeSize / Mathf.Max(Mathf.Abs(parentScale.z), 0.0001f));
            part.layer = layer;
            part.SetActive(true);

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.enabled = true;
            }
            return part;
        }

        private static Material GetMaterial(ref Material material, string name, Color color)
        {
            if (material != null)
                return material;

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return null;

            material = new Material(shader);
            material.name = name;
            material.color = color;
            return material;
        }

        private static void LogCube(ManualLogSource logger, GameObject cube, Camera camera)
        {
            Transform transform = cube.transform;
            Renderer renderer = cube.GetComponent<Renderer>();
            string layerName = LayerMask.LayerToName(cube.layer);
            bool cameraIncludesLayer = camera != null &&
                (camera.cullingMask & (1 << cube.layer)) != 0;

            logger.LogInfo(
                cube.name +
                " diagnostics: worldPosition=" + transform.position +
                ", localPosition=" + transform.localPosition +
                ", localScale=" + transform.localScale +
                ", active=" + cube.activeInHierarchy +
                ", layer=" + cube.layer + " (" + layerName + ")" +
                ", menuCameraIncludesLayer=" + cameraIncludesLayer +
                ", rendererEnabled=" + (renderer != null && renderer.enabled) +
                ", material=" + (renderer != null && renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.name
                    : "<none>"));
        }
    }
}
