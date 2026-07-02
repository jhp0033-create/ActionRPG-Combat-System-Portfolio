using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Data
{
    [CreateAssetMenu(fileName = "RewardSet_", menuName = "ActionRPG/Data/Reward Set")]
    public sealed class RewardSetData : ScriptableObject
    {
        [SerializeField] private List<RewardItemData> rewardItems = new List<RewardItemData>();

        public IReadOnlyList<RewardItemData> RewardItems => rewardItems;
    }
}
