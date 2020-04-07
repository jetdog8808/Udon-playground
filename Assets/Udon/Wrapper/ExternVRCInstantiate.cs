#if !VRC_CLIENT
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Common.Delegates;
using VRC.Udon.Common.Interfaces;

public class ExternVRCInstantiate : IUdonWrapperModule
{
    public string Name => "VRCInstantiate";

    private static readonly Dictionary<string, int> _parameterCounts = new Dictionary<string, int>
    {
        {"__Instantiate__UnityEngineGameObject__UnityEngineGameObject", 2},
    };

    public int GetExternFunctionParameterCount(string externFunctionSignature)
    {
        if(_parameterCounts.ContainsKey(externFunctionSignature))
        {
            return _parameterCounts[externFunctionSignature];
        }

        throw new System.NotSupportedException($"Function '{externFunctionSignature}' is not implemented yet");
    }

    public UdonExternDelegate GetExternFunctionDelegate(string externFunctionSignature)
    {
        switch(externFunctionSignature)
        {
            case "__Instantiate__UnityEngineGameObject__UnityEngineGameObject":
            {
                return __Instantiate__UnityEngineGameObject__UnityEngineGameObject;
            }

            default:
            {
                throw new System.NotSupportedException($"External function '{externFunctionSignature}' is not supported.");
            }
        }
    }

    private static void __Instantiate__UnityEngineGameObject__UnityEngineGameObject(IUdonHeap heap, uint[] parameterAddresses)
    {
        GameObject original = heap.GetHeapVariable<GameObject>(parameterAddresses[0]);
        GameObject clone = Object.Instantiate(original);

        heap.SetHeapVariable(parameterAddresses[1], clone);
    }
}
#endif
