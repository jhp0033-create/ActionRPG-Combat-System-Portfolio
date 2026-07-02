using UnityEngine;
using UnityEditor;

public class ValidateSkeleton : MonoBehaviour
{
    [MenuItem("Tools/Validate Skeleton")]
    public static void Validate()
    {
        string path = "Assets/_Core/Prefabs/Skeleton.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Prefab not found!");
            return;
        }

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogError("No Animator found anywhere in the prefab!");
            return;
        }

        Debug.Log($"Animator found on: {animator.gameObject.name}");
        Debug.Log($"Animator is enabled: {animator.enabled}");
        
        if (animator.runtimeAnimatorController == null)
            Debug.LogError("RuntimeAnimatorController is NULL!");
        else
            Debug.Log($"Controller: {animator.runtimeAnimatorController.name}");

        if (animator.avatar == null)
            Debug.LogError("Avatar is NULL! T-pose will happen for Humanoid/Generic rigs!");
        else
            Debug.Log($"Avatar: {animator.avatar.name} (isHuman: {animator.avatar.isHuman})");
    }
}
