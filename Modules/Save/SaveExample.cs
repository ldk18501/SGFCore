using System;
using UnityEngine;

namespace GameFramework.Core
{
    // 子结构直接继承 SaveDataNode
    [Serializable]
    public class TestSaveData : SaveDataNode 
    {
        public int Level = 1;
        public float Count = 0;

        public void AddOre(float amount)
        {
            Count += amount;
        
            // 子节点数据变动，直接调用自身的 MarkDirty()
            // 底层会自动顺着 _rootContext 告诉 SimulationSaveData：你要存盘了！
            MarkDirty(); 
        }
    }

    // 主存档继承 SaveDataBase
    [Serializable]
    public class SimulationSaveData : SaveDataBase
    {
        public int PlayerGold = 0;
    
        // 子节点
        public TestSaveData dataA = new TestSaveData();
        public TestSaveData dataB = new TestSaveData();

        public SimulationSaveData()
        {
            IsAutoSaveEnabled = true;
            AutoSaveInterval = 10f;
        }

        // 重写绑定钩子，这比在每一次 Update 里去 CheckIsDirty() 性能高出无数倍！
        public override void OnBindContext()
        {
            dataA.BindContext(this);
            dataB.BindContext(this);
        }

        public void AddGold(int amount)
        {
            PlayerGold += amount;
            MarkDirty(); // 自身数据变动，直接标记
        }
    }
}