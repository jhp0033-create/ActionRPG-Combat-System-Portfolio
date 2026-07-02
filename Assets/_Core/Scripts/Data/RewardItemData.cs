using UnityEngine;

namespace ActionRPG.Data
{
    [CreateAssetMenu(fileName = "RewardItem_", menuName = "ActionRPG/Data/Reward Item")]
    public sealed class RewardItemData : ScriptableObject
    {
        [Header("Display")]
        public string rewardTitle = "New Reward";
        [TextArea(2, 4)]
        public string rewardDescription = "";
        public Sprite icon;
    }
}
