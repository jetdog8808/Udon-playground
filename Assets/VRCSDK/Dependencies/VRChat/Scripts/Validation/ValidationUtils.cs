using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace VRCSDK2.Validation
{
    public static class ValidationUtils
    {
        public static void RemoveIllegalComponents(GameObject target, System.Type[] whitelist, bool retry = true, bool onlySceneObjects = false, bool logStripping = true)
        {
            bool foundBad = false;
            FindIllegalComponents(target, whitelist, (c) =>
            {
                if (c != null)
                {
                    if (onlySceneObjects && c.GetInstanceID() < 0)
                        return;

                    if (logStripping)
                        VRC.Core.Logger.LogWarning(string.Format("Removing {0} comp from {1}", c.GetType().Name, c.gameObject.name));

                    RemoveComponent(c);

                    foundBad = true;
                }
            });

            if (retry && foundBad)
                RemoveIllegalComponents(target, whitelist, false, onlySceneObjects);
        }

        public static IEnumerable<Component> FindIllegalComponents(GameObject target, System.Type[] whitelist, System.Action<Component> onFound = null)
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

            HashSet<System.Type> typesInUse = new HashSet<System.Type>();
            Queue<GameObject> children = new Queue<GameObject>();
            children.Enqueue(target.gameObject);
            List<Component> foundComponents = new List<Component>();
            while (children.Count > 0)
            {
                GameObject child = children.Dequeue();
                if (child == null)
                    continue;

                int childCount = child.transform.childCount;
                for (int idx = 0; idx < childCount; ++idx)
                    children.Enqueue(child.transform.GetChild(idx).gameObject);
                foreach (Component c in child.transform.GetComponents<Component>())
                {
                    if (c == null)
                        continue;

                    if (typesInUse.Contains(c.GetType()) == false)
                        typesInUse.Add(c.GetType());

                    if (!whitelist.Any(allowedType => c.GetType() == allowedType || c.GetType().IsSubclassOf(allowedType)))
                    {
                        foundComponents.Add(c);
                        if (onFound != null)
                            onFound(c);
                    }
                }
            }
            return foundComponents;
        }

        private static Dictionary<string, System.Type> _typeCache = new Dictionary<string, System.Type>();
        private static Dictionary<string, System.Type[]> _whitelistCache = new Dictionary<string, System.Type[]>();
        public static System.Type[] WhitelistedTypes(string whitelistName, string[] ComponentTypeWhitelist)
        {
            if (_whitelistCache.ContainsKey(whitelistName))
                return _whitelistCache[whitelistName];

            Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            _whitelistCache[whitelistName] = ComponentTypeWhitelist.Select((name) => GetTypeFromName(name, assemblies)).Where(t => t != null).ToArray();

            return _whitelistCache[whitelistName];
        }

        public static System.Type GetTypeFromName(string name, Assembly[] assemblies = null)
        {
            if (_typeCache.ContainsKey(name))
                return _typeCache[name];

            if (assemblies == null)
                assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly a in assemblies)
            {
                System.Type found = a.GetType(name);
                if (found != null)
                {
                    _typeCache[name] = found;
                    return found;
                }
            }

            //This is really verbose for some SDK scenes, eg.
            //If they don't have FinalIK installed
#if VRC_CLIENT && UNITY_EDITOR
            Debug.LogWarningFormat("Could not find type {0}", name);
#endif
            _typeCache[name] = null;
            return null;
        }

        static void RemoveDependencies(Component component)
        {
            if (component == null)
                return;

            Component[] components = component.GetComponents<Component>();
            if (components == null || components.Length == 0)
                return;

            System.Type compType = component.GetType();
            foreach (var c in components)
            {
                if (c == null)
                    continue;

                bool deleteMe = false;
                object[] requires = c.GetType().GetCustomAttributes(typeof(RequireComponent), true);
                if (requires == null || requires.Length == 0)
                    continue;

                foreach (var r in requires)
                {
                    RequireComponent rc = r as RequireComponent;
                    if (rc == null)
                        continue;

                    if (rc.m_Type0 == compType ||
                        rc.m_Type1 == compType ||
                        rc.m_Type2 == compType)
                    {
                        deleteMe = true;
                        break;
                    }
                }

                if (deleteMe)
                    RemoveComponent(c);
            }
        }

        public static void RemoveComponent(Component comp)
        {
            if (comp == null)
                return;

            RemoveDependencies(comp);

#if VRC_CLIENT
            Object.DestroyImmediate(comp, true);
#else
            Object.DestroyImmediate(comp, false);
#endif
        }

        public static void RemoveComponentsOfType<T>(GameObject target) where T : Component
        {
            if (target == null)
                return;

            foreach (T comp in target.GetComponentsInChildren<T>(true))
            {
                if (comp == null || comp.gameObject == null)
                    continue;

                RemoveComponent(comp);
            }
        }
    }
}
