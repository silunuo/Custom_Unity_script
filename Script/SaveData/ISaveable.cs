// ============================================================================
// ISaveable.cs — 可存档组件接口
//
// 实现此接口的 MonoBehaviour 可以自动参与存档的收集和恢复。
// SaveManager 会在保存时遍历所有 ISaveable，收集各模块数据；
// 加载时则将对应数据分发回各模块。
//
// 适合场景：背包系统、任务系统、技能系统等各自管理自己的存档数据。
// 如果你的游戏比较简单，直接用 SaveData 子类集中管理也完全没问题。
//
// 使用示例：
//   public class InventoryManager : MonoBehaviour, ISaveable
//   {
//       public string SaveID => "Inventory";
//
//       [Serializable]
//       private class InventorySaveData { public List<string> items; }
//
//       public string OnSave()
//       {
//           var data = new InventorySaveData { items = currentItems };
//           return JsonUtility.ToJson(data);
//       }
//
//       public void OnLoad(string json)
//       {
//           if (string.IsNullOrEmpty(json)) return;
//           var data = JsonUtility.FromJson<InventorySaveData>(json);
//           currentItems = data.items;
//       }
//   }
// ============================================================================

/// <summary>
/// 可存档组件接口。
/// 实现此接口后，SaveManager 会自动收集和分发该组件的存档数据。
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// 该模块的唯一标识符。
    /// 用作 SaveData.moduleData 字典的 Key，不同模块不能重复。
    /// 建议使用有意义的名称，如 "Inventory"、"QuestLog"、"SkillTree"。
    /// </summary>
    string SaveID { get; }

    /// <summary>
    /// 保存时调用。将当前状态序列化为 JSON 字符串返回。
    /// SaveManager 会将返回值存入 SaveData.moduleData[SaveID]。
    /// </summary>
    /// <returns>模块数据的 JSON 字符串</returns>
    string OnSave();

    /// <summary>
    /// 加载时调用。从 JSON 字符串恢复状态。
    /// 参数可能为 null 或空字符串（该模块从未保存过），需做好防御。
    /// </summary>
    /// <param name="json">之前保存的 JSON 字符串，可能为 null</param>
    void OnLoad(string json);
}
