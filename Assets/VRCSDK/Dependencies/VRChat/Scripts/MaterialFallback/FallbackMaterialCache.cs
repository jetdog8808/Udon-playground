using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Core;

public class FallbackMaterialCache
{
    private readonly Dictionary<Material, Material> _fallbackMaterialCache = new Dictionary<Material, Material>();

    public void AddFallbackMaterial(Material material, Material fallbackMaterial)
    {
        if(!_fallbackMaterialCache.ContainsKey(material))
        {
            _fallbackMaterialCache.Add(material, fallbackMaterial);
        }
        else
        {
            Debug.LogError(string.Format("Attempted to add a duplicate fallback material '{0}' for original material '{1}'.", fallbackMaterial.name, material.name));
        }
    }

    public bool HasFallbackMaterial(Material material)
    {
        return _fallbackMaterialCache.ContainsKey(material);
    }

    public Material GetFallBackMaterial(Material material)
    {
        return _fallbackMaterialCache[material];
    }

    public void Clear()
    {
        Material[] cachedFallbackMaterials = _fallbackMaterialCache.Values.ToArray();
        for(int i = cachedFallbackMaterials.Length - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(cachedFallbackMaterials[i]);
        }

        _fallbackMaterialCache.Clear();
    }
}
