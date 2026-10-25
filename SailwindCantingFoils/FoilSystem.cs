using UnityEngine;

namespace SailwindCantingFoils
{
    /// <summary>
    /// Creates temporary visual test geometry under any ship transform.
    /// Kept independent of menu discovery so it can be reused by later systems.
    /// </summary>
    internal static class FoilSystem
    {
        private const string AssemblyName = "SailwindCantingFoils_TestFoils";
        private static Material foilMaterial;

        // Adjust these values to tune the first visual prototype.
        internal static float ArmLength = 5.0f;
        internal static float OutboardOffset = 0.55f;
        internal static float ArmHeight = -0.25f;
        internal static float ArmCantDegrees = 0.0f;
        internal static float ArmThickness = 0.18f;
        internal static float ArmChord = 0.32f;
        internal static float FoilHeight = 0.9f;
        internal static float FoilChord = 0.48f;

        internal static bool TryCreateTestAssembly(Transform shipRoot, out bool created)
        {
            created = false;
            if (shipRoot == null)
                return false;

            if (shipRoot.Find(AssemblyName) != null)
                return true;

            var assembly = new GameObject(AssemblyName);
            assembly.transform.SetParent(shipRoot, false);
            assembly.transform.localPosition = Vector3.zero;
            assembly.transform.localRotation = Quaternion.identity;
            assembly.transform.localScale = Vector3.one;

            CreateSide(assembly.transform, -1, "Port");
            CreateSide(assembly.transform, 1, "Starboard");
            created = true;
            return true;
        }

        private static void CreateSide(Transform parent, int side, string sideName)
        {
            var arm = new GameObject(sideName + "Arm");
            arm.transform.SetParent(parent, false);
            arm.transform.localPosition = new Vector3(side * OutboardOffset, ArmHeight, 0f);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, side * ArmCantDegrees);
            arm.transform.localScale = Vector3.one;

            CreatePrimitive(
                sideName + "ArmBeam",
                arm.transform,
                new Vector3(side * ArmLength * 0.5f, 0f, 0f),
                new Vector3(ArmLength, ArmThickness, ArmChord));

            CreatePrimitive(
                sideName + "Foil",
                arm.transform,
                new Vector3(side * ArmLength, 0f, 0f),
                new Vector3(ArmChord, FoilHeight, FoilChord));
        }

        private static GameObject CreatePrimitive(
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            // These markers are visual only and must not affect collisions.
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            Renderer renderer = part.GetComponent<Renderer>();
            Material material = GetFoilMaterial();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            return part;
        }

        private static Material GetFoilMaterial()
        {
            if (foilMaterial != null)
                return foilMaterial;

            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null)
                return null;

            foilMaterial = new Material(shader);
            foilMaterial.name = "SailwindCantingFoils_TestMaterial";
            foilMaterial.color = new Color(1f, 0.32f, 0.08f, 1f);
            return foilMaterial;
        }
    }
}
