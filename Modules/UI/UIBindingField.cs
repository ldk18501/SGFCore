using System;
using UnityEngine;

namespace GameFramework.Core.UI
{
    public enum UIBindingAccess
    {
        Private,
        Protected,
        Public,
        Header
    }

    [Serializable]
    public class UIBindingField
    {
        public string VarName;
        public string ComponentTypeName;
        public UIBindingAccess Access;
        public GameObject[] Targets;

        public UIBindingField(string varName, string componentTypeName, GameObject[] targets, UIBindingAccess access)
        {
            VarName = varName;
            ComponentTypeName = componentTypeName;
            Targets = targets;
            Access = access;
        }
    }
}
