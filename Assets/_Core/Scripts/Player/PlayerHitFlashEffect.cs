using System;
using System.Collections;
using UnityEngine;

namespace ActionRPG.Player
{
    /// <summary>
    /// Applies the short red material pulse used when the player takes damage.
    /// </summary>
    public static class PlayerHitFlashEffect
    {
        public static IEnumerator Play(
            Renderer[] renderers,
            MaterialPropertyBlock propertyBlock,
            Func<bool> canRestoreOriginalColor)
        {
            ApplyFlashColor(renderers, propertyBlock);
            yield return new WaitForSeconds(0.12f);

            if (canRestoreOriginalColor == null || canRestoreOriginalColor())
            {
                RestoreOriginalColor(renderers, propertyBlock);
            }
        }

        private static void ApplyFlashColor(Renderer[] renderers, MaterialPropertyBlock propertyBlock)
        {
            if (renderers == null || propertyBlock == null)
            {
                return;
            }

            Color flashColor = new Color(2.5f, 0.2f, 0.2f, 1f);
            foreach (Renderer renderer in renderers)
            {
                if (!CanTint(renderer))
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", flashColor);
                propertyBlock.SetColor("_Color", flashColor);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void RestoreOriginalColor(Renderer[] renderers, MaterialPropertyBlock propertyBlock)
        {
            if (renderers == null || propertyBlock == null)
            {
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                if (!CanTint(renderer))
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                Color originalColor = Color.white;
                if (renderer.sharedMaterial != null)
                {
                    if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                    {
                        originalColor = renderer.sharedMaterial.GetColor("_BaseColor");
                    }
                    else if (renderer.sharedMaterial.HasProperty("_Color"))
                    {
                        originalColor = renderer.sharedMaterial.GetColor("_Color");
                    }
                }

                originalColor.a = 1f;
                propertyBlock.SetColor("_BaseColor", originalColor);
                propertyBlock.SetColor("_Color", originalColor);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static bool CanTint(Renderer renderer)
        {
            return renderer != null && (renderer is SkinnedMeshRenderer || renderer is MeshRenderer);
        }
    }
}
