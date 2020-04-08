﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UdonSharp
{
    public enum ExpressionCaptureArchetype
    {
        Unknown, // This could be multiple things, the reference is currently ambiguous
        Namespace,
        Property,
        Field, // For Udon, fields are largely treated the same way as properties, they have getters and setters defined.
        LocalSymbol,
        Method,
        Type,
        ArrayIndexer,
        This, // this.<next token> indicates that this must be a local symbol or method
        Enum,
        LocalMethod,
        ExternUserField,
        ExternUserMethod,
        InternalUdonSharpMethod,
    }

    /// <summary>
    /// There's probably a better way to handle this, 
    ///   but this class tracks the most recent symbol/type/namespace/method group that was last potentially referenced.
    /// This is currently used to resolve chains of statements where the user can do something like UnityEngine.Color.Red.r, 
    ///     in this case, the user first specifies a namespace, then a type in that namespace, then a static property on that type, then a field in that property
    ///     However, all we have to go off from this token-wise is a tree of SimpleMemberAccessExpression.
    ///     So obviously in this case, the first actionable thing we need to do is run the internal function get_red() on the Color type.
    ///     Then we need to get the value of the r field from that output.
    ///     But what if the user was setting a color on a light or something? Then you'd have light.color = Color.Red; 
    ///     So you also need to handle the difference between set and get on properties 
    /// </summary>
    public class ExpressionCaptureScope : System.IDisposable
    {
        public string unresolvedAccessChain { get; private set; } = "";
        public ExpressionCaptureArchetype captureArchetype { get; private set; } = ExpressionCaptureArchetype.Unknown;

        public bool isAttributeCaptureScope { get; set; } = false;

        // Only the parameters corresponding with the current captureArchetype are guaranteed to be valid
        public string captureNamespace { get; private set; } = "";
        public PropertyInfo captureProperty { get; private set; } = null;
        public FieldInfo captureField { get; private set; } = null;
        public SymbolDefinition captureLocalSymbol { get; private set; } = null;
        public MethodBase[] captureMethods { get; private set; } = null; // If there are override methods we do not know which to use until we invoke them with the arguments
        public System.Type captureType { get; private set; } = null;
        public string captureEnum { get; private set; } = "";
        public MethodDefinition captureLocalMethod { get; private set; } = null;
        public FieldDefinition captureExternUserField { get; private set; } = null;
        public MethodDefinition captureExternUserMethod { get; private set; } = null;
        public InternalMethodHandler InternalMethodHandler { get; private set; } = null;

        public SymbolDefinition accessSymbol { get; private set; } = null;

        // Used for array indexers
        private SymbolDefinition arrayBacktraceSymbol = null;
        private SymbolDefinition arrayIndexerIndexSymbol = null;

        // Used for resolving generic methods, and eventually types when Udon adds support
        private List<System.Type> genericTypeArguments = null;

        // Namespace lookup to check if something is a namespace
        private static HashSet<string> allLinkedNamespaces;

        // The current visitor context
        ASTVisitorContext visitorContext;

        ExpressionCaptureScope parentScope;

        bool disposed = false;

        private void FindValidNamespaces()
        {
            if (allLinkedNamespaces != null)
                return;

            allLinkedNamespaces = new HashSet<string>();

            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                allLinkedNamespaces.UnionWith(assembly.GetTypes().Select(e => e.Namespace).Distinct());
            }
        }

        public ExpressionCaptureScope(ASTVisitorContext context, ExpressionCaptureScope parentScopeIn)
        {
            FindValidNamespaces();

            visitorContext = context;
            parentScope = parentScopeIn;
            InternalMethodHandler = new InternalMethodHandler(context, this);

            visitorContext.PushCaptureScope(this);
        }

        ~ExpressionCaptureScope()
        {
            Debug.Assert(disposed, "Expression capture scope was not disposed!");
        }
        
        public void Dispose()
        {
            if (parentScope != null)
                parentScope.InheretScope(this);
            
            Debug.Assert(visitorContext.topCaptureScope == this);
            if (visitorContext.topCaptureScope == this)
                visitorContext.PopCaptureScope();

            disposed = true;
        }

        private void InheretScope(ExpressionCaptureScope childScope)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown ||
                childScope.captureArchetype == ExpressionCaptureArchetype.Unknown ||
                childScope.captureArchetype == ExpressionCaptureArchetype.This ||
                childScope.captureArchetype == ExpressionCaptureArchetype.Method ||
                childScope.captureArchetype == ExpressionCaptureArchetype.Namespace)
                return;

            captureArchetype = childScope.captureArchetype;
            captureField = childScope.captureField;
            captureLocalSymbol = childScope.captureLocalSymbol;
            captureProperty = childScope.captureProperty;
            captureType = childScope.captureType;
            accessSymbol = childScope.accessSymbol;
            captureEnum = childScope.captureEnum;
            arrayIndexerIndexSymbol = childScope.arrayIndexerIndexSymbol;
            captureLocalMethod = childScope.captureLocalMethod;
            captureExternUserField = childScope.captureExternUserField;
            captureExternUserMethod = childScope.captureExternUserMethod;
            InternalMethodHandler = childScope.InternalMethodHandler;
        }

        public void SetToLocalSymbol(SymbolDefinition symbol)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown)
                throw new System.ArgumentException("Cannot set non-unknown symbol scope to a local symbol");

            captureArchetype = ExpressionCaptureArchetype.LocalSymbol;
            captureLocalSymbol = symbol;
            accessSymbol = symbol;
        }

        public void SetToType(System.Type type)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown)
                throw new System.ArgumentException("Cannot set non-unknown symbol scope to a type");

            captureArchetype = ExpressionCaptureArchetype.Type;
            captureType = type;
        }

        public void SetToMethods(MethodBase[] methods)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown)
                throw new System.ArgumentException("Cannot set non-unknown symbol scope to a method");
            
            foreach (MethodBase method in methods)
            {
                if (!method.IsStatic && !(method is ConstructorInfo))
                    throw new System.ArgumentException("All methods set in SetToMethods must be static");
            }

            captureArchetype = ExpressionCaptureArchetype.Method;
            captureMethods = methods;
        }

        public void MakeArrayType()
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Capture scope must have the 'Type' archetype to convert to an array");

            captureType = captureType.MakeArrayType();
        }

        #region Archetype check functions
        public bool HasBeenAssigned()
        {
            return !(IsUnknownArchetype() && unresolvedAccessChain.Length == 0);
        }

        public bool IsUnknownArchetype()
        {
            return captureArchetype == ExpressionCaptureArchetype.Unknown;
        }

        public bool IsNamespace()
        {
            return captureArchetype == ExpressionCaptureArchetype.Namespace;
        }

        public bool IsMethod()
        {
            return captureArchetype == ExpressionCaptureArchetype.Method || 
                   captureArchetype == ExpressionCaptureArchetype.LocalMethod || 
                   captureArchetype == ExpressionCaptureArchetype.ExternUserMethod ||
                   captureArchetype == ExpressionCaptureArchetype.InternalUdonSharpMethod;
        }

        public bool IsProperty()
        {
            return captureArchetype == ExpressionCaptureArchetype.Property;
        }

        public bool IsField()
        {
            return captureArchetype == ExpressionCaptureArchetype.Field ||
                   captureArchetype == ExpressionCaptureArchetype.ExternUserField;
        }

        public bool IsType()
        {
            return captureArchetype == ExpressionCaptureArchetype.Type;
        }

        public bool IsThis()
        {
            return captureArchetype == ExpressionCaptureArchetype.This;
        }

        public bool IsArrayIndexer()
        {
            return captureArchetype == ExpressionCaptureArchetype.ArrayIndexer;
        }

        public bool IsLocalSymbol()
        {
            return captureArchetype == ExpressionCaptureArchetype.LocalSymbol;
        }

        public bool IsEnum()
        {
            return captureArchetype == ExpressionCaptureArchetype.Enum;
        }
        #endregion

        public object GetEnumValue()
        {
            if (!IsEnum())
                throw new System.Exception("Cannot get enum value from non-enum capture");

            return System.Enum.Parse(captureType, captureEnum);
        }

        private MethodInfo GetUdonGetMethodInfo()
        {
            if (!IsProperty())
                throw new System.Exception("Cannot get property get method on non-properties");

            if (captureProperty.ReflectedType == typeof(VRC.Udon.UdonBehaviour))
            {
                PropertyInfo property = typeof(Component).GetProperty(captureProperty.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (property == null)
                    return null;

                return property.GetGetMethod();
            }

            return captureProperty.GetGetMethod();
        }

        private MethodInfo GetUdonSetMethodInfo()
        {
            if (!IsProperty())
                throw new System.Exception("Cannot get property get method on non-properties");

            if (captureProperty.ReflectedType == typeof(VRC.Udon.UdonBehaviour))
            {
                PropertyInfo property = typeof(Component).GetProperty(captureProperty.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (property == null)
                    return null;

                return property.GetGetMethod();
            }

            return captureProperty.GetSetMethod();
        }

        // Inserts uasm instructions to get the value stored in the current localSymbol, property, or field
        public SymbolDefinition ExecuteGet()
        {
            arrayBacktraceSymbol = null;

            if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
                return captureLocalSymbol;

            SymbolDefinition outSymbol = null;

            if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                MethodInfo getMethod = GetUdonGetMethodInfo();

                if (getMethod == null)
                    throw new System.MemberAccessException($"Property or indexer '{captureProperty.DeclaringType.Name}.{captureProperty.Name}' doesn't exist");

                if (getMethod.ReturnType == typeof(void))
                    throw new System.TypeLoadException("Cannot return type of void from a get statement");
                
                outSymbol = visitorContext.topTable.CreateUnnamedSymbol(getMethod.ReturnType, SymbolDeclTypeFlags.Internal);

                string methodUdonString = visitorContext.resolverContext.GetUdonMethodName(getMethod);

                if (!getMethod.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(outSymbol);
                visitorContext.uasmBuilder.AddExternCall(methodUdonString);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                outSymbol = visitorContext.topTable.CreateUnnamedSymbol(captureField.FieldType, SymbolDeclTypeFlags.Internal);

                string fieldAccessorUdonName = visitorContext.resolverContext.GetUdonFieldAccessorName(captureField, FieldAccessorType.Get);

                if (!captureField.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(outSymbol);
                visitorContext.uasmBuilder.AddExternCall(fieldAccessorUdonName);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserField)
            {
                outSymbol = visitorContext.topTable.CreateUnnamedSymbol(captureExternUserField.fieldSymbol.symbolCsType, SymbolDeclTypeFlags.Internal);

                using (ExpressionCaptureScope getVariableMethodScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    getVariableMethodScope.SetToLocalSymbol(accessSymbol);
                    getVariableMethodScope.ResolveAccessToken("GetProgramVariable");

                    SymbolDefinition externVarReturn = getVariableMethodScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserField.fieldSymbol.symbolUniqueName) });
                    outSymbol = CastSymbolToType(externVarReturn, captureExternUserField.fieldSymbol.userCsType, true, true);
                }
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ArrayIndexer)
            {
                System.Type elementType = null;

                string getIndexerUdonName;
                if (accessSymbol.symbolCsType == typeof(string))
                {
                    getIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(accessSymbol.symbolCsType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == "get_Chars").First());
                    elementType = typeof(char);
                }
                else
                {
                    getIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(accessSymbol.symbolCsType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == "Get").First());
                    elementType = accessSymbol.userCsType.GetElementType();
                }

                arrayBacktraceSymbol = accessSymbol;

                outSymbol = visitorContext.topTable.CreateUnnamedSymbol(elementType, SymbolDeclTypeFlags.Internal);

                visitorContext.uasmBuilder.AddPush(accessSymbol);
                visitorContext.uasmBuilder.AddPush(arrayIndexerIndexSymbol);
                visitorContext.uasmBuilder.AddPush(outSymbol);
                visitorContext.uasmBuilder.AddExternCall(getIndexerUdonName);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.This)
            {
                outSymbol = visitorContext.topTable.CreateThisSymbol(visitorContext.behaviourUserType);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Enum)
            {
                // Capture type should still be valid from the last transition
                outSymbol = visitorContext.topTable.CreateConstSymbol(captureType, GetEnumValue());
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Unknown)
            {
                string[] unresolvedTokens = unresolvedAccessChain.Split('.');
                string invalidName = unresolvedTokens.Length > 1 ? unresolvedTokens[unresolvedTokens.Length - 2] : unresolvedTokens[0];
                throw new System.Exception($"The name '{invalidName}' does not exist in the current context");
            }
            else
            {
                throw new System.Exception("Get can only be run on Fields, Properties, Local Symbols, array indexers, and the `this` keyword");
            }

            return outSymbol;
        }

        public void ExecuteSet(SymbolDefinition value, bool explicitCast = false)
        {
            SymbolDefinition convertedValue = CastSymbolToType(value, GetReturnType(true), explicitCast);

            // If it's a local symbol, it's just a simple COPY
            if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
            {
                if (captureLocalSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) || captureLocalSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.This))
                    throw new System.Exception("Cannot execute set on constant or this symbols");

                visitorContext.uasmBuilder.AddCopy(captureLocalSymbol, convertedValue);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                MethodInfo setMethod = GetUdonSetMethodInfo();

                if (setMethod == null)
                    throw new System.MemberAccessException($"Property or indexer '{captureProperty.DeclaringType.Name}.{captureProperty.Name}' cannot be assigned to -- it is read only or doesn't exist");

                string udonMethodString = visitorContext.resolverContext.GetUdonMethodName(setMethod);

                if (!setMethod.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(convertedValue);
                visitorContext.uasmBuilder.AddExternCall(udonMethodString);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                string fieldSetterUdonName = visitorContext.resolverContext.GetUdonFieldAccessorName(captureField, FieldAccessorType.Set);

                if (!captureField.IsStatic)
                    visitorContext.uasmBuilder.AddPush(accessSymbol);

                visitorContext.uasmBuilder.AddPush(convertedValue);
                visitorContext.uasmBuilder.AddExternCall(fieldSetterUdonName);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserField)
            {
                using (ExpressionCaptureScope setVariableMethodScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    setVariableMethodScope.SetToLocalSymbol(accessSymbol);
                    setVariableMethodScope.ResolveAccessToken("SetProgramVariable");

                    setVariableMethodScope.Invoke(new SymbolDefinition[] {
                        visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserField.fieldSymbol.symbolUniqueName),
                        convertedValue
                    });
                }
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ArrayIndexer)
            {
                string setIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(accessSymbol.symbolCsType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == "Set").First());

                visitorContext.uasmBuilder.AddPush(accessSymbol);
                visitorContext.uasmBuilder.AddPush(arrayIndexerIndexSymbol);
                visitorContext.uasmBuilder.AddPush(convertedValue);
                visitorContext.uasmBuilder.AddExternCall(setIndexerUdonName);
            }
            else
            {
                throw new System.Exception("Set can only be run on Fields, Properties, and local Symbols");
            }

            // Copy the result back into the array if it's a value type
            if (captureArchetype != ExpressionCaptureArchetype.ArrayIndexer && arrayBacktraceSymbol != null && accessSymbol.symbolCsType.IsValueType)
            {
                using (ExpressionCaptureScope arraySetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arraySetScope.SetToLocalSymbol(arrayBacktraceSymbol);
                    arraySetScope.HandleArrayIndexerAccess(arrayIndexerIndexSymbol);

                    arraySetScope.ExecuteSet(accessSymbol);
                }
            }
        }

        // Just a stub for now that will be extended to avoid the COPY instruction when possible
        public void ExecuteSetDirect(ExpressionCaptureScope valueExpression, bool explicitCast = false)
        {
            ExecuteSet(valueExpression.ExecuteGet(), explicitCast);
        }

        // There's probably a better place for this function...
        public SymbolDefinition CastSymbolToType(SymbolDefinition sourceSymbol, System.Type targetType, bool isExplicit, bool needsNewSymbol = false)
        {
            if (targetType.IsByRef) // Convert ref and out args to their main types.
                targetType = targetType.GetElementType();

            // Special case for passing through user defined classes if they match
            if (sourceSymbol.IsUserDefinedType() && 
                (targetType.IsAssignableFrom(sourceSymbol.userCsType) || (targetType.IsArray && targetType == sourceSymbol.userCsType)))
                return sourceSymbol;
            
            // Special case for assigning objects to non-value types so we can assign and the output of things that return a generic object
            // This lets the user potentially break their stuff if they assign an object return value from some function to a heap variable with a non-matching type. 
            // For instance you could assign a Transform component to a Renderer component variable, and you'd only realize the error when you tried to treat the Renderer as a Renderer.
            // This can't be trivially type checked at runtime with what Udon exposes in System.Type at the moment.
            bool isObjectAssignable = !targetType.IsValueType && sourceSymbol.symbolCsType == typeof(object);

            bool isNumericCastValid = UdonSharpUtils.IsNumericImplicitCastValid(targetType, sourceSymbol.symbolCsType) ||
                 (sourceSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) && sourceSymbol.symbolCsType == typeof(int)); // Handle Roslyn giving us ints constant expressions

            if ((!isExplicit && !targetType.IsImplicitlyAssignableFrom(sourceSymbol.userCsType)) &&
                !isObjectAssignable && !isNumericCastValid)
                throw new System.ArgumentException($"Cannot implicitly convert type '{UdonSharpUtils.PrettifyTypeName(sourceSymbol.userCsType)}' to '{UdonSharpUtils.PrettifyTypeName(targetType)}'");

            // Exact type match, just return the symbol, this is what will happen a majority of the time.
            if (targetType == sourceSymbol.symbolCsType || (isObjectAssignable && !needsNewSymbol))
                return sourceSymbol;

            // We can just return the symbol as-is
            // This might need revisiting since there may be *some* case where you want the type to be explicitly casted to a specific type regardless of the source type
            if (targetType.IsAssignableFrom(sourceSymbol.symbolCsType))
                return sourceSymbol;

            // Numeric conversion handling
            MethodInfo conversionFunction = UdonSharpUtils.GetNumericConversionMethod(targetType, sourceSymbol.symbolCsType);
            
            
            if (conversionFunction != null && (isExplicit || isNumericCastValid))
            {
                // This code is copied 3 times, todo: find a decent way to refactor it
                SymbolDefinition castOutput = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                visitorContext.uasmBuilder.AddPush(sourceSymbol);
                visitorContext.uasmBuilder.AddPush(castOutput);
                visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(conversionFunction));

                return castOutput;
            }

            // Implicit user-defined conversion handling
            List<System.Type> operatorTypes = new List<System.Type>();
            operatorTypes.Add(targetType);

            System.Type currentSourceType = sourceSymbol.symbolCsType;
            while (currentSourceType != null)
            {
                operatorTypes.Add(currentSourceType);
                currentSourceType = currentSourceType.BaseType;
            }

            MethodInfo foundConversion = null;

            foreach (System.Type operatorType in operatorTypes)
            {
                IEnumerable<MethodInfo> methods = operatorType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == "op_Implicit");

                foreach (MethodInfo methodInfo in methods)
                {
                    if (methodInfo.ReturnType == targetType && (methodInfo.GetParameters()[0].ParameterType == sourceSymbol.symbolCsType || methodInfo.GetParameters()[0].ParameterType == typeof(UnityEngine.Object)))
                    {
                        foundConversion = methodInfo;
                        break;
                    }
                }

                if (foundConversion != null)
                    break;
            }

            if (foundConversion != null)
            {
                SymbolDefinition castOutput = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                visitorContext.uasmBuilder.AddPush(sourceSymbol);
                visitorContext.uasmBuilder.AddPush(castOutput);
                visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(foundConversion));

                return castOutput;
            }

            // Explicit user-defined conversion handling
            if (isExplicit)
            {
                foreach (System.Type operatorType in operatorTypes)
                {
                    IEnumerable<MethodInfo> methods = operatorType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == "op_Explicit");

                    foreach (MethodInfo methodInfo in methods)
                    {
                        if (methodInfo.ReturnType == targetType && methodInfo.GetParameters()[0].ParameterType == sourceSymbol.symbolCsType)
                        {
                            foundConversion = methodInfo;
                            break;
                        }
                    }

                    if (foundConversion != null)
                        break;
                }

                if (foundConversion != null)
                {
                    SymbolDefinition castOutput = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                    visitorContext.uasmBuilder.AddPush(sourceSymbol);
                    visitorContext.uasmBuilder.AddPush(castOutput);
                    visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(foundConversion));

                    return castOutput;
                }

                // All other casts have failed, just try to straight assign it to a new symbol
                if (needsNewSymbol)
                {
                    SymbolDefinition copyCastOutput = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);
                    visitorContext.uasmBuilder.AddCopy(copyCastOutput, sourceSymbol);
                    return copyCastOutput;
                }
                else
                {
                    // Copying to an invalid type won't throw exceptions sadly so just return the symbol...
                    return sourceSymbol;
                }
            }

            throw new System.Exception($"Cannot find cast for {sourceSymbol.symbolCsType} to {targetType}");
        }

        /// <summary>
        /// Runs implicit conversions on method arguments, creates symbols for default values, and handles conversion of parameters for a `params` argument.
        /// </summary>
        /// <param name="targetMethod"></param>
        /// <param name="invokeParams"></param>
        /// <returns></returns>
        private SymbolDefinition[] GetExpandedInvokeParams(MethodBase targetMethod, SymbolDefinition[] invokeParams)
        {
            ParameterInfo[] methodParams = targetMethod.GetParameters();

            List<SymbolDefinition> newInvokeParams = new List<SymbolDefinition>();

            for (int i = 0; i < methodParams.Length; ++i)
            {
                // Handle default args
                if (invokeParams.Length <= i)
                {
                    if (!methodParams[i].HasDefaultValue)
                        throw new System.Exception("Overran valid parameters without default default value to use");

                    SymbolDefinition defaultArg = visitorContext.topTable.CreateConstSymbol(methodParams[i].ParameterType, methodParams[i].DefaultValue);

                    newInvokeParams.Add(defaultArg);
                    continue;
                }

                // If this method has a params array then we need to take the remaining invoke parameters and make an array to use as input
                if (methodParams[i].HasParamsParameter())
                {
                    int paramCount = invokeParams.Length - i;

                    if (paramCount == 1 && methodParams[i].ParameterType.IsImplicitlyAssignableFrom(invokeParams[i].userCsType))
                    {
                        newInvokeParams.Add(invokeParams[i]);
                    }
                    else
                    {
                        SymbolDefinition paramsArraySymbol = visitorContext.topTable.CreateConstSymbol(methodParams[i].ParameterType,
                                        System.Activator.CreateInstance(methodParams[i].ParameterType, new object[] { paramCount }));

                        for (int j = i; j < invokeParams.Length; ++j)
                        {
                            int paramArrayIndex = j - i;

                            // This can potentially grow unbounded, but we'll hope that the user doesn't go insane with the param count
                            SymbolDefinition arrayIndexSymbol = visitorContext.topTable.CreateConstSymbol(typeof(int), paramArrayIndex);

                            using (ExpressionCaptureScope paramArraySetterScope = new ExpressionCaptureScope(visitorContext, null))
                            {
                                paramArraySetterScope.SetToLocalSymbol(paramsArraySymbol);
                                paramArraySetterScope.HandleArrayIndexerAccess(arrayIndexSymbol);
                                paramArraySetterScope.ExecuteSet(invokeParams[j]);
                            }
                        }

                        newInvokeParams.Add(paramsArraySymbol);
                    }
                    break;
                }

                newInvokeParams.Add(CastSymbolToType(invokeParams[i], methodParams[i].ParameterType, false));
            }

            return newInvokeParams.ToArray();
        }

        public MethodBase GetInvokeMethod(SymbolDefinition[] invokeParams)
        {
            return visitorContext.resolverContext.FindBestOverloadFunction(captureMethods, invokeParams.Select(e => e.symbolCsType).ToList());
        }

        private bool AllMethodParametersMatch(MethodInfo methodInfo, System.Type[] parameterTypes)
        {
            ParameterInfo[] parameters = methodInfo.GetParameters();

            if (parameters.Length != parameterTypes.Length)
                return false;

            for (int i = 0; i < parameters.Length; ++i)
            {
                if (parameters[i].ParameterType != parameterTypes[i])
                    return false;
            }

            return true;
        }

        private MethodInfo ConvertGetComponentToGetComponents(MethodInfo targetMethod)
        {
            if (targetMethod.GetParameters().FirstOrDefault() != null && targetMethod.GetParameters().FirstOrDefault().ParameterType == typeof(System.Type))
                throw new System.ArgumentException("Cannot use GetComponent with type arguments on generic versions");

            System.Type declaringType = targetMethod.DeclaringType;

            string searchString = targetMethod.Name;
            if (!searchString.Contains("GetComponents"))
                searchString = searchString.Replace("GetComponent", "GetComponents");

            System.Type[] targetParameters = targetMethod.GetParameters().Select(e => e.ParameterType).ToArray();

            if (targetParameters.Count() == 0 || targetParameters.First() != typeof(System.Type))
            {
                targetParameters = new System.Type[] { typeof(System.Type) }.Concat(targetParameters).ToArray();
            }

            MethodInfo[] foundMethods = declaringType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == searchString && AllMethodParametersMatch(e, targetParameters)).ToArray();
            
            if (foundMethods.Length != 1)
            {
                throw new System.ArgumentException($"No valid typeof override found for function {targetMethod}");
            }

            return foundMethods.First();
        }

        // Metaprogramming using expression captures, very fun. It would be cool to have a scripting thing setup to generate an AST and code from just an input string of code at some point in the future.
        // Handles getting a single user component type by looping through all components of the type UdonBehaviour and checking their internal type ID
        private SymbolDefinition HandleGenericGetComponentSingle(SymbolDefinition componentArray, System.Type udonSharpType)
        {
            SymbolDefinition resultSymbol = visitorContext.topTable.CreateUnnamedSymbol(udonSharpType, SymbolDeclTypeFlags.Internal);
            
            visitorContext.PushTable(new SymbolTable(visitorContext.resolverContext, visitorContext.topTable));

            using (ExpressionCaptureScope resultResetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                resultResetScope.SetToLocalSymbol(resultSymbol);
                resultResetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(udonSharpType, null));
            }

            SymbolDefinition componentTypeID = visitorContext.topTable.CreateConstSymbol(typeof(long), Internal.UdonSharpInternalUtility.GetTypeID(udonSharpType));

            SymbolDefinition componentCount = null;

            using (ExpressionCaptureScope lengthGetterScope = new ExpressionCaptureScope(visitorContext, null))
            {
                lengthGetterScope.SetToLocalSymbol(componentArray);
                lengthGetterScope.ResolveAccessToken("Length");

                componentCount = lengthGetterScope.ExecuteGet();
            }

            SymbolDefinition arrayIndex = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal);
            using (ExpressionCaptureScope arrayIndexResetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                arrayIndexResetScope.SetToLocalSymbol(arrayIndex);
                arrayIndexResetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
            }

            JumpLabel loopStartJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentLoop");
            visitorContext.uasmBuilder.AddJumpLabel(loopStartJumpPoint);

            JumpLabel loopExitJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentLoopEnd");

            SymbolDefinition loopConditionSymbol = null;

            using (ExpressionCaptureScope loopConditionCheck = new ExpressionCaptureScope(visitorContext, null))
            {
                loopConditionCheck.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.LessThan));
                loopConditionSymbol = loopConditionCheck.Invoke(new SymbolDefinition[] { arrayIndex, componentCount });
            }
            
            visitorContext.uasmBuilder.AddJumpIfFalse(loopExitJumpPoint, loopConditionSymbol);


            SymbolDefinition componentValue = null;

            using (ExpressionCaptureScope componentArrayGetter = new ExpressionCaptureScope(visitorContext, null))
            {
                componentArrayGetter.SetToLocalSymbol(componentArray);
                componentArrayGetter.HandleArrayIndexerAccess(arrayIndex);
                
                componentValue = CastSymbolToType(componentArrayGetter.ExecuteGet(), udonSharpType, true, true);
            }

            SymbolDefinition objectTypeId = null;

            using (ExpressionCaptureScope typeIDGetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                typeIDGetScope.SetToLocalSymbol(componentValue);
                typeIDGetScope.ResolveAccessToken(nameof(UdonSharpBehaviour.GetUdonTypeID));

                objectTypeId = typeIDGetScope.Invoke(new SymbolDefinition[] { });
            }

            SymbolDefinition typeIdEqualsConditionSymbol = null;

            using (ExpressionCaptureScope equalsConditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                equalsConditionScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(long), BuiltinOperatorType.Equality));
                typeIdEqualsConditionSymbol = equalsConditionScope.Invoke(new SymbolDefinition[] { objectTypeId, componentTypeID });
            }

            JumpLabel conditionFalseJumpLoc = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentIdNotEqual");

            visitorContext.uasmBuilder.AddJumpIfFalse(conditionFalseJumpLoc, typeIdEqualsConditionSymbol);

            using (ExpressionCaptureScope setResultScope = new ExpressionCaptureScope(visitorContext, null))
            {
                setResultScope.SetToLocalSymbol(resultSymbol);
                setResultScope.ExecuteSet(componentValue);
            }

            visitorContext.uasmBuilder.AddJump(loopExitJumpPoint); // Exit early

            visitorContext.uasmBuilder.AddJumpLabel(conditionFalseJumpLoc);

            using (ExpressionCaptureScope indexIncrementScope = new ExpressionCaptureScope(visitorContext, null))
            {
                indexIncrementScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                SymbolDefinition incrementResult = indexIncrementScope.Invoke(new SymbolDefinition[] { arrayIndex, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                visitorContext.uasmBuilder.AddCopy(arrayIndex, incrementResult);
            }

            visitorContext.uasmBuilder.AddJump(loopStartJumpPoint);

            visitorContext.uasmBuilder.AddJumpLabel(loopExitJumpPoint);

            visitorContext.PopTable();

            return resultSymbol;
        }

        // Handles getting an array of user components, first iterates the array of components to count how many of them are the given user type, then creates an array of that size, 
        //   then iterates the array again and assigns the components to the correct index in the array
        private SymbolDefinition HandleGenericGetComponentArray(SymbolDefinition componentArray, System.Type udonSharpType)
        {
            SymbolDefinition resultSymbol = visitorContext.topTable.CreateUnnamedSymbol(udonSharpType.MakeArrayType(), SymbolDeclTypeFlags.Internal);

            visitorContext.PushTable(new SymbolTable(visitorContext.resolverContext, visitorContext.topTable));

            using (ExpressionCaptureScope resultResetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                resultResetScope.SetToLocalSymbol(resultSymbol);
                SymbolDefinition arrayConstSymbol = visitorContext.topTable.CreateConstSymbol(udonSharpType.MakeArrayType(), null);
                arrayConstSymbol.symbolDefaultValue = new Component[0];
                resultResetScope.ExecuteSet(arrayConstSymbol);
            }

            SymbolDefinition componentTypeID = visitorContext.topTable.CreateConstSymbol(typeof(long), Internal.UdonSharpInternalUtility.GetTypeID(udonSharpType));
            
            SymbolDefinition componentCounterSymbol = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal);

            using (ExpressionCaptureScope resetComponentCountScope = new ExpressionCaptureScope(visitorContext, null))
            {
                resetComponentCountScope.SetToLocalSymbol(componentCounterSymbol);
                resetComponentCountScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
            }

            SymbolDefinition componentCount = null;

            using (ExpressionCaptureScope lengthGetterScope = new ExpressionCaptureScope(visitorContext, null))
            {
                lengthGetterScope.SetToLocalSymbol(componentArray);
                lengthGetterScope.ResolveAccessToken("Length");

                componentCount = lengthGetterScope.ExecuteGet();
            }

            SymbolDefinition arrayIndex = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal);

            // First loop to count the number of components
            {
                using (ExpressionCaptureScope arrayIndexResetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arrayIndexResetScope.SetToLocalSymbol(arrayIndex);
                    arrayIndexResetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
                }

                JumpLabel loopStartJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsLoop");
                visitorContext.uasmBuilder.AddJumpLabel(loopStartJumpPoint);

                JumpLabel loopExitJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsLoopEnd");

                SymbolDefinition loopConditionSymbol = null;

                using (ExpressionCaptureScope loopConditionCheck = new ExpressionCaptureScope(visitorContext, null))
                {
                    loopConditionCheck.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.LessThan));
                    loopConditionSymbol = loopConditionCheck.Invoke(new SymbolDefinition[] { arrayIndex, componentCount });
                }

                visitorContext.uasmBuilder.AddJumpIfFalse(loopExitJumpPoint, loopConditionSymbol);

                SymbolDefinition componentValue = null;

                using (ExpressionCaptureScope componentArrayGetter = new ExpressionCaptureScope(visitorContext, null))
                {
                    componentArrayGetter.SetToLocalSymbol(componentArray);
                    componentArrayGetter.HandleArrayIndexerAccess(arrayIndex);

                    componentValue = CastSymbolToType(componentArrayGetter.ExecuteGet(), udonSharpType, true, true);
                }

                SymbolDefinition objectTypeId = null;

                using (ExpressionCaptureScope typeIDGetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    typeIDGetScope.SetToLocalSymbol(componentValue);
                    typeIDGetScope.ResolveAccessToken(nameof(UdonSharpBehaviour.GetUdonTypeID));

                    objectTypeId = typeIDGetScope.Invoke(new SymbolDefinition[] { });
                }

                JumpLabel incrementConditionFalseJump = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsIncrementConditionFalse");

                SymbolDefinition incrementConditionSymbol = null;

                using (ExpressionCaptureScope incrementConditionScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    incrementConditionScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(long), BuiltinOperatorType.Equality));
                    incrementConditionSymbol = incrementConditionScope.Invoke(new SymbolDefinition[] { objectTypeId, componentTypeID });
                }

                visitorContext.uasmBuilder.AddJumpIfFalse(incrementConditionFalseJump, incrementConditionSymbol);

                using (ExpressionCaptureScope incrementComponentCountScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    incrementComponentCountScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementResult = incrementComponentCountScope.Invoke(new SymbolDefinition[] { componentCounterSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    visitorContext.uasmBuilder.AddCopy(componentCounterSymbol, incrementResult);
                }

                visitorContext.uasmBuilder.AddJumpLabel(incrementConditionFalseJump);


                using (ExpressionCaptureScope indexIncrementScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    indexIncrementScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementResult = indexIncrementScope.Invoke(new SymbolDefinition[] { arrayIndex, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    visitorContext.uasmBuilder.AddCopy(arrayIndex, incrementResult);
                }

                visitorContext.uasmBuilder.AddJump(loopStartJumpPoint);

                visitorContext.uasmBuilder.AddJumpLabel(loopExitJumpPoint);
            }

            // Skip the second loop if we found no valid components
            JumpLabel exitJumpLabel = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsSkipArrayIteration");
            SymbolDefinition skipSecondLoopConditionSymbol = null;
            using (ExpressionCaptureScope skipSecondLoopConditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                skipSecondLoopConditionScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.GreaterThan));
                skipSecondLoopConditionSymbol = skipSecondLoopConditionScope.Invoke(new SymbolDefinition[] { componentCounterSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 0) });
            }

            visitorContext.uasmBuilder.AddJumpIfFalse(exitJumpLabel, skipSecondLoopConditionSymbol);

            // Second loop to assign values to the array
            {
                // Initialize the new array
                using (ExpressionCaptureScope arrayVarScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arrayVarScope.SetToLocalSymbol(resultSymbol);

                    using (ExpressionCaptureScope arrayCreationScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        arrayCreationScope.SetToMethods(resultSymbol.symbolCsType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

                        SymbolDefinition newArraySymbol = arrayCreationScope.Invoke(new SymbolDefinition[] { componentCounterSymbol });
                        if (resultSymbol.IsUserDefinedType())
                            newArraySymbol.symbolCsType = resultSymbol.userCsType;

                        arrayVarScope.ExecuteSet(newArraySymbol);
                    }
                }

                using (ExpressionCaptureScope arrayIndexResetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arrayIndexResetScope.SetToLocalSymbol(arrayIndex);
                    arrayIndexResetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
                }

                SymbolDefinition destIdxSymbol = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal);

                using (ExpressionCaptureScope resetDestIdxScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    resetDestIdxScope.SetToLocalSymbol(destIdxSymbol);
                    resetDestIdxScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
                }

                JumpLabel loopStartJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsLoop2");
                visitorContext.uasmBuilder.AddJumpLabel(loopStartJumpPoint);

                JumpLabel loopExitJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsLoopEnd2");

                SymbolDefinition loopConditionSymbol = null;

                using (ExpressionCaptureScope loopConditionCheck = new ExpressionCaptureScope(visitorContext, null))
                {
                    loopConditionCheck.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.LessThan));
                    loopConditionSymbol = loopConditionCheck.Invoke(new SymbolDefinition[] { arrayIndex, componentCount });
                }

                visitorContext.uasmBuilder.AddJumpIfFalse(loopExitJumpPoint, loopConditionSymbol);

                SymbolDefinition componentValue = null;

                using (ExpressionCaptureScope componentArrayGetter = new ExpressionCaptureScope(visitorContext, null))
                {
                    componentArrayGetter.SetToLocalSymbol(componentArray);
                    componentArrayGetter.HandleArrayIndexerAccess(arrayIndex);

                    componentValue = CastSymbolToType(componentArrayGetter.ExecuteGet(), udonSharpType, true, true);
                }

                SymbolDefinition objectTypeId = null;

                using (ExpressionCaptureScope typeIDGetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    typeIDGetScope.SetToLocalSymbol(componentValue);
                    typeIDGetScope.ResolveAccessToken(nameof(UdonSharpBehaviour.GetUdonTypeID));

                    objectTypeId = typeIDGetScope.Invoke(new SymbolDefinition[] { });
                }

                JumpLabel addConditionFalseJump = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsAddConditionFalse");

                SymbolDefinition addConditionSymbol = null;

                using (ExpressionCaptureScope addConditionScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    addConditionScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(long), BuiltinOperatorType.Equality));
                    addConditionSymbol = addConditionScope.Invoke(new SymbolDefinition[] { objectTypeId, componentTypeID });
                }

                visitorContext.uasmBuilder.AddJumpIfFalse(addConditionFalseJump, addConditionSymbol);

                using (ExpressionCaptureScope setArrayValueScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    setArrayValueScope.SetToLocalSymbol(resultSymbol);
                    setArrayValueScope.HandleArrayIndexerAccess(destIdxSymbol);

                    using (ExpressionCaptureScope sourceValueGetScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        sourceValueGetScope.SetToLocalSymbol(componentArray);
                        sourceValueGetScope.HandleArrayIndexerAccess(arrayIndex);

                        SymbolDefinition arrayValue = sourceValueGetScope.ExecuteGet();
                        arrayValue.symbolCsType = udonSharpType;
                        setArrayValueScope.ExecuteSet(arrayValue);
                    }
                }

                using (ExpressionCaptureScope incrementTargetCountScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    incrementTargetCountScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementResult = incrementTargetCountScope.Invoke(new SymbolDefinition[] { destIdxSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    visitorContext.uasmBuilder.AddCopy(destIdxSymbol, incrementResult);
                }

                visitorContext.uasmBuilder.AddJumpLabel(addConditionFalseJump);

                using (ExpressionCaptureScope indexIncrementScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    indexIncrementScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementResult = indexIncrementScope.Invoke(new SymbolDefinition[] { arrayIndex, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    visitorContext.uasmBuilder.AddCopy(arrayIndex, incrementResult);
                }

                visitorContext.uasmBuilder.AddJump(loopStartJumpPoint);

                visitorContext.uasmBuilder.AddJumpLabel(loopExitJumpPoint);
            }

            visitorContext.uasmBuilder.AddJumpLabel(exitJumpLabel);

            visitorContext.PopTable();

            return resultSymbol;
        }

        private SymbolDefinition HandleGenericUSharpGetComponent(MethodInfo targetMethod, System.Type udonSharpType, SymbolDefinition[] invokeParams)
        {
            bool isArray = targetMethod.ReturnType.IsArray;

            targetMethod = ConvertGetComponentToGetComponents(targetMethod);

            SymbolDefinition udonBehaviourType = visitorContext.topTable.CreateConstSymbol(typeof(System.Type), typeof(VRC.Udon.UdonBehaviour));

            visitorContext.uasmBuilder.AddPush(udonBehaviourType);

            foreach (SymbolDefinition invokeParam in invokeParams)
                visitorContext.uasmBuilder.AddPush(invokeParam);

            SymbolDefinition resultComponents = visitorContext.topTable.CreateUnnamedSymbol(typeof(Component[]), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);

            visitorContext.uasmBuilder.AddPush(resultComponents);

            visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(targetMethod));

            if (isArray)
            {
                return HandleGenericGetComponentArray(resultComponents, udonSharpType);
            }
            else
            {
                return HandleGenericGetComponentSingle(resultComponents, udonSharpType);
            }
        }

        private SymbolDefinition InvokeExtern(SymbolDefinition[] invokeParams)
        {
            // Find valid overrides
            MethodBase targetMethod = visitorContext.resolverContext.FindBestOverloadFunction(captureMethods, invokeParams.Select(e => e.symbolCsType).ToList());

            if (targetMethod == null)
            {
                targetMethod = visitorContext.resolverContext.FindBestOverloadFunction(captureMethods, invokeParams.Select(e => e.symbolCsType).ToList(), false);
                if (targetMethod != null)
                {
                    throw new System.Exception($"Method is not exposed to Udon: {targetMethod}, Udon signature: {visitorContext.resolverContext.GetUdonMethodName(targetMethod, false)}");
                }

                string udonFilteredMethods = "";

                udonFilteredMethods = string.Join("\n", captureMethods
                    .Select(e => new System.Tuple<MethodBase, string>(e, visitorContext.resolverContext.GetUdonMethodName(e, false)))
                    .Where(e => !visitorContext.resolverContext.IsValidUdonMethod(e.Item2))
                    .Select(e => e.Item1));

                if (udonFilteredMethods.Length > 0)
                {
                    throw new System.Exception($"Could not find valid method that exists in Udon.\nList of applicable methods that do not exist:\n{udonFilteredMethods}");
                }
                else
                {
                    throw new System.Exception("Could not find valid method for given parameters!");
                }
            }

            SymbolDefinition[] expandedParams = GetExpandedInvokeParams(targetMethod, invokeParams);
            bool isUserTypeGetComponent = targetMethod.Name.StartsWith("GetComponent") && genericTypeArguments != null && genericTypeArguments.First().IsSubclassOf(typeof(UdonSharpBehaviour));

            // Now make the needed symbol definitions and run the invoke
            if (!targetMethod.IsStatic && !(targetMethod is ConstructorInfo)/* && targetMethod.Name != "Instantiate"*/) // Constructors don't take an instance argument, but are still classified as an instance method
            {
                if (genericTypeArguments != null && typeof(GameObject).IsAssignableFrom(accessSymbol.symbolCsType) && !isUserTypeGetComponent) // Handle GetComponent<T> on gameobjects by getting their transform first
                {
                    using (ExpressionCaptureScope transformComponentGetScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        transformComponentGetScope.SetToLocalSymbol(accessSymbol);
                        transformComponentGetScope.ResolveAccessToken("transform");

                        visitorContext.uasmBuilder.AddPush(transformComponentGetScope.ExecuteGet());
                    }
                }
                else
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }
            }

            SymbolDefinition returnSymbol = null;

            System.Type returnType = typeof(void);

            if (targetMethod is MethodInfo)
                returnType = ((MethodInfo)targetMethod).ReturnType;
            else if (targetMethod is ConstructorInfo)
                returnType = targetMethod.DeclaringType;
            else
                throw new System.Exception("Invalid target method type");

            if (isUserTypeGetComponent)
            {
                returnSymbol = HandleGenericUSharpGetComponent(targetMethod as MethodInfo, genericTypeArguments.First(), invokeParams);
            }
            else
            {
                foreach (SymbolDefinition invokeParam in expandedParams)
                    visitorContext.uasmBuilder.AddPush(invokeParam);

                if (returnType != typeof(void))
                {
                    if (genericTypeArguments != null)
                    {
                        System.Type genericType = genericTypeArguments.First();

                        if (returnType.IsArray)
                            returnType = genericType.MakeArrayType();
                        else
                            returnType = genericType;

                        visitorContext.uasmBuilder.AddPush(visitorContext.topTable.CreateConstSymbol(typeof(System.Type), genericType));
                    }

                    //returnSymbol = visitorContext.topTable.CreateNamedSymbol("returnVal", returnType, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);
                    returnSymbol = visitorContext.topTable.CreateUnnamedSymbol(returnType, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);

                    visitorContext.uasmBuilder.AddPush(returnSymbol);

                    if (visitorContext.topCaptureScope != null && visitorContext.topCaptureScope.IsUnknownArchetype())
                        visitorContext.topCaptureScope.SetToLocalSymbol(returnSymbol);
                }

                visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(targetMethod, true, genericTypeArguments));
            }

            return returnSymbol;
        }

        private SymbolDefinition InvokeLocalMethod(SymbolDefinition[] invokeParams)
        {
            if (invokeParams.Length != captureLocalMethod.parameters.Length)
                throw new System.NotSupportedException("UdonSharp custom methods currently do not support default arguments or params arguments");

            for (int i = 0; i < captureLocalMethod.parameters.Length; ++i)
            {
                using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    argAssignmentScope.SetToLocalSymbol(captureLocalMethod.parameters[i].paramSymbol);
                    argAssignmentScope.ExecuteSet(invokeParams[i]);
                }
            }

            SymbolDefinition exitJumpLocation = visitorContext.topTable.CreateNamedSymbol("exitJumpLoc", typeof(uint), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Constant);
            SymbolDefinition oldReturnLocation = visitorContext.topTable.CreateNamedSymbol("oldReturnLoc", typeof(uint), SymbolDeclTypeFlags.Internal);

            using (ExpressionCaptureScope oldReturnLocationSetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                oldReturnLocationSetScope.SetToLocalSymbol(oldReturnLocation);
                oldReturnLocationSetScope.ExecuteSet(visitorContext.returnJumpTarget);
            }

            using (ExpressionCaptureScope newReturnPointSetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                newReturnPointSetScope.SetToLocalSymbol(visitorContext.returnJumpTarget);
                newReturnPointSetScope.ExecuteSet(exitJumpLocation);
            }

            visitorContext.uasmBuilder.AddJump(captureLocalMethod.methodUserCallStart);

            JumpLabel exitLabel = visitorContext.labelTable.GetNewJumpLabel("returnLocation");

            // Now we can set this value after we have found the exit address
            visitorContext.uasmBuilder.AddJumpLabel(exitLabel);
            exitJumpLocation.symbolDefaultValue = exitLabel.resolvedAddress;

            using (ExpressionCaptureScope restoreReturnLocationScope = new ExpressionCaptureScope(visitorContext, null))
            {
                restoreReturnLocationScope.SetToLocalSymbol(visitorContext.returnJumpTarget);
                restoreReturnLocationScope.ExecuteSet(oldReturnLocation);
            }

            SymbolDefinition returnValue = null;

            if (captureLocalMethod.returnSymbol != null)
            {
                returnValue = visitorContext.topTable.CreateUnnamedSymbol(captureLocalMethod.returnSymbol.userCsType, SymbolDeclTypeFlags.Internal);

                using (ExpressionCaptureScope returnValSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    returnValSetScope.SetToLocalSymbol(returnValue);
                    returnValSetScope.ExecuteSet(captureLocalMethod.returnSymbol);
                }

                if (visitorContext.topCaptureScope != null && visitorContext.topCaptureScope.IsUnknownArchetype())
                    visitorContext.topCaptureScope.SetToLocalSymbol(returnValue);
            }

            return returnValue;
        }

        private SymbolDefinition InvokeUserExtern(SymbolDefinition[] invokeParams)
        {
            if (invokeParams.Length != captureExternUserMethod.parameters.Length)
                throw new System.NotSupportedException("UdonSharp custom methods currently do not support default arguments or params arguments");

            if (!accessSymbol.IsUserDefinedBehaviour())
                throw new System.FieldAccessException("Cannot run extern invoke on non-user symbol");

            for (int i = 0; i < captureExternUserMethod.parameters.Length; ++i)
            {
                SymbolDefinition convertedArg = CastSymbolToType(invokeParams[i], captureExternUserMethod.parameters[i].type, false);

                using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    argAssignmentScope.SetToLocalSymbol(accessSymbol);
                    argAssignmentScope.ResolveAccessToken("SetProgramVariable");

                    argAssignmentScope.Invoke(new SymbolDefinition[] {
                        visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserMethod.parameters[i].paramSymbol.symbolUniqueName),
                        convertedArg
                    });
                }
            }

            using (ExpressionCaptureScope externInvokeScope = new ExpressionCaptureScope(visitorContext, null))
            {
                externInvokeScope.SetToLocalSymbol(accessSymbol);
                externInvokeScope.ResolveAccessToken("SendCustomEvent");
                externInvokeScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserMethod.uniqueMethodName) });
            }

            SymbolDefinition returnSymbol = null;

            if (captureExternUserMethod.returnSymbol != null)
            {
                using (ExpressionCaptureScope getReturnScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    getReturnScope.SetToLocalSymbol(accessSymbol);
                    getReturnScope.ResolveAccessToken("GetProgramVariable");
                    returnSymbol = getReturnScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserMethod.returnSymbol.symbolUniqueName) });
                    returnSymbol = CastSymbolToType(returnSymbol, captureExternUserMethod.returnSymbol.userCsType, true, true);
                }

                using (ExpressionCaptureScope propagateScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
                {
                    propagateScope.SetToLocalSymbol(returnSymbol);
                }
            }

            return returnSymbol;
        }

        public SymbolDefinition Invoke(SymbolDefinition[] invokeParams)
        {
            if (!IsMethod())
            {
                throw new System.Exception("You can only invoke methods!");
            }

            if (captureArchetype == ExpressionCaptureArchetype.Method)
            {
                return InvokeExtern(invokeParams);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.LocalMethod)
            {
                return InvokeLocalMethod(invokeParams);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserMethod)
            {
                return InvokeUserExtern(invokeParams);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.InternalUdonSharpMethod)
            {
                return InternalMethodHandler.Invoke(invokeParams);
            }
            else
            {
                throw new System.Exception($"Cannot call invoke on archetype {captureArchetype}");
            }
        }

        public System.Type GetReturnType(bool getUserType = false)
        {
            if (captureArchetype == ExpressionCaptureArchetype.Method)
                throw new System.Exception("Cannot infer return type from method without function arguments");

            if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
            {
                if (getUserType)
                    return accessSymbol.userCsType;

                return accessSymbol.symbolCsType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                return captureProperty.GetGetMethod().ReturnType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                return captureField.FieldType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserField)
            {
                if (getUserType)
                    return captureExternUserField.fieldSymbol.userCsType;

                return captureExternUserField.fieldSymbol.symbolCsType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ArrayIndexer)
            {
                if (!accessSymbol.symbolCsType.IsArray)
                    throw new System.Exception("Type is not an array type");

                if (getUserType)
                    return accessSymbol.userCsType.GetElementType();

                if (accessSymbol.IsUserDefinedBehaviour() && accessSymbol.userCsType.IsArray && accessSymbol.symbolCsType == typeof(Component[]))
                {
                    // Special case for arrays since the symbolCsType needs to return a Component[], but we need to get the element type of the UdonBehaviour[]
                    return typeof(VRC.Udon.UdonBehaviour);
                }

                if (accessSymbol.userCsType.GetElementType().IsArray)
                    return typeof(object[]);

                return accessSymbol.symbolCsType.GetElementType();
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Enum)
            {
                return captureType;
            }
            else
            {
                throw new System.Exception("GetReturnType only valid for Local Symbols, Properties, Fields, and array indexers");
            }
        }

        public bool ResolveAccessToken(string accessToken)
        {
            bool resolvedToken = false;

            if (accessToken == "this")
            {
                if (captureArchetype != ExpressionCaptureArchetype.Unknown && captureArchetype != ExpressionCaptureArchetype.This)
                    throw new System.ArgumentException("Access token `this` is not valid in this context");

                captureArchetype = ExpressionCaptureArchetype.This;
                resolvedToken = true;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.This)
            {
                resolvedToken = HandleLocalSymbolLookup(accessToken) ||
                                HandleLocalMethodLookup(accessToken) ||
                                HandleLocalUdonBehaviourMethodLookup(accessToken) ||
                                HandleLocalUdonBehaviourPropertyLookup(accessToken) ||
                                HandleUdonSharpInternalMethodLookup(accessToken);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Unknown)
            {
                resolvedToken = HandleLocalSymbolLookup(accessToken) ||
                                HandleLocalMethodLookup(accessToken) ||
                                HandleLocalUdonBehaviourMethodLookup(accessToken) ||
                                HandleLocalUdonBehaviourPropertyLookup(accessToken) ||
                                HandleUdonSharpInternalMethodLookup(accessToken) ||
                                HandleTypeLookup(accessToken) ||
                                HandleNamespaceLookup(accessToken);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Type)
            {
                resolvedToken = HandleNestedTypeLookup(accessToken) ||
                                HandleEnumFieldLookup(accessToken) ||
                                HandleStaticMethodLookup(accessToken) ||
                                HandleStaticPropertyLookup(accessToken) ||
                                HandleStaticFieldLookup(accessToken) ||
                                HandleUdonSharpInternalMethodLookup(accessToken);
            }
            else if (IsMethod())
            {
                throw new System.InvalidOperationException("Cannot run an accessor on a method!");
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Namespace)
            {
                resolvedToken = HandleNamespaceLookup(accessToken) ||
                                HandleTypeLookup(accessToken);
            }
            // This is where we need to start building intermediate variables to store the input for the next statement
            else if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol || 
                     captureArchetype == ExpressionCaptureArchetype.Property || 
                     captureArchetype == ExpressionCaptureArchetype.Field ||
                     captureArchetype == ExpressionCaptureArchetype.ExternUserField ||
                     captureArchetype == ExpressionCaptureArchetype.ArrayIndexer ||
                     captureArchetype == ExpressionCaptureArchetype.Enum)
            {
                resolvedToken = HandleExternUserFieldLookup(accessToken) ||
                                HandleExternUserMethodLookup(accessToken) ||
                                HandleUdonSharpInternalMethodLookup(accessToken) ||
                                HandleMemberPropertyAccess(accessToken) ||
                                HandleMemberFieldAccess(accessToken) ||
                                HandleMemberMethodLookup(accessToken); 
            }


            // Fallback in case all other lookups fails. This would usually happen if a namespace doesn't contain any types, but has child namespaces that have types.
            if (!resolvedToken)
            {
                HandleUnknownToken(accessToken);
            }

            return resolvedToken;
        }

        #region Token lookups
        private bool HandleLocalSymbolLookup(string localSymbolName)
        {
            SymbolDefinition symbol = null;

            symbol = visitorContext.topTable.FindUserDefinedSymbol(localSymbolName);

            // Allow user to mask built-in lookups
            if (symbol == null)
            {
                if (localSymbolName == "gameObject")
                    symbol = visitorContext.topTable.CreateThisSymbol(typeof(GameObject));
                else if (localSymbolName == "transform")
                    symbol = visitorContext.topTable.CreateThisSymbol(typeof(Transform));
            }

            if (symbol == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.LocalSymbol;
            captureLocalSymbol = symbol;
            accessSymbol = symbol;

            return true;
        }
        
        private bool HandleLocalMethodLookup(string localMethodName)
        {
            if (visitorContext.definedMethods == null)
                return false;

            MethodDefinition foundMethod = null;

            foreach (MethodDefinition methodDefinition in visitorContext.definedMethods)
            {
                if (methodDefinition.originalMethodName == localMethodName)
                {
                    foundMethod = methodDefinition;
                    break;
                }
            }

            if (foundMethod == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.LocalMethod;
            captureLocalMethod = foundMethod;

            return true;
        }

        private static Dictionary<(System.Type, BindingFlags), MethodInfo[]> typeMethodCache = new Dictionary<(System.Type, BindingFlags), MethodInfo[]>();

        private static MethodInfo[] GetTypeMethods(System.Type type, BindingFlags bindingFlags)
        {
            MethodInfo[] methods;
            if (!typeMethodCache.TryGetValue((type, bindingFlags), out methods))
            {
                methods = type.GetMethods(bindingFlags);
                typeMethodCache.Add((type, bindingFlags), methods);
            }

            return methods;
        }

        private bool HandleLocalUdonBehaviourMethodLookup(string localUdonMethodName)
        {
            List<MethodInfo> methods = new List<MethodInfo>(GetTypeMethods(typeof(VRC.Udon.Common.Interfaces.IUdonEventReceiver), BindingFlags.Instance | BindingFlags.Public));
            methods.AddRange(GetTypeMethods(typeof(Component), BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
            methods.AddRange(GetTypeMethods(typeof(Object), BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));

            if (localUdonMethodName == "VRCInstantiate")
                methods.AddRange(GetTypeMethods(typeof(UdonSharpBehaviour), BindingFlags.Static | BindingFlags.Public));

            IEnumerable<MethodInfo> foundMethods = methods.Where(e => e.Name == localUdonMethodName).Distinct();

            if (foundMethods.Count() == 0)
                return false;

            accessSymbol = visitorContext.topTable.CreateThisSymbol(visitorContext.behaviourUserType);
            captureMethods = foundMethods.ToArray();
            captureArchetype = ExpressionCaptureArchetype.Method;

            return true;
        }

        private bool HandleLocalUdonBehaviourPropertyLookup(string localUdonPropertyName)
        {
            PropertyInfo[] foundProperties = typeof(Component).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(e => e.Name == localUdonPropertyName).ToArray();

            if (localUdonPropertyName == "enabled")
            {
                throw new System.NotSupportedException("Udon does not expose the `enabled` property on UdonBehaviours try using gameObject.active instead and find my post on the canny complaining about it");
            }

            if (foundProperties.Length == 0)
                return false;

            accessSymbol = visitorContext.topTable.CreateThisSymbol(visitorContext.behaviourUserType);
            captureProperty = foundProperties.First();
            captureArchetype = ExpressionCaptureArchetype.Property;

            return true;
        }

        private bool HandleTypeLookup(string typeName)
        {
            string typeQualifiedName = visitorContext.resolverContext.ParseBuiltinTypeAlias(typeName);

            if (captureArchetype == ExpressionCaptureArchetype.Namespace)
            {
                if (captureNamespace.Length > 0)
                    typeQualifiedName = $"{captureNamespace}.{typeName}";
                else
                    typeQualifiedName = typeName;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Unknown)
            {
                if (unresolvedAccessChain.Length > 0)
                    typeQualifiedName = $"{unresolvedAccessChain}.{typeName}";
                else
                    typeQualifiedName = typeName;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Type)
            {
                typeQualifiedName = $"{captureType.FullName}.{typeName}";
            }
            else
            {
                throw new System.Exception("Invalid source archetype for type lookup");
            }

            System.Type foundType = null;

            if (isAttributeCaptureScope)
                foundType = visitorContext.resolverContext.ResolveExternType(typeQualifiedName + "Attribute");

            if (foundType == null)
                foundType = visitorContext.resolverContext.ResolveExternType(typeQualifiedName);

            if (foundType == null)
                return false;
            
            unresolvedAccessChain = "";

            captureType = foundType;
            captureArchetype = ExpressionCaptureArchetype.Type;


            return true;
        }

        private bool HandleNamespaceLookup(string namespaceName)
        {
            string qualifiedNamespace = namespaceName;

            if (captureArchetype == ExpressionCaptureArchetype.Unknown)
            {
                if (unresolvedAccessChain.Length > 0)
                    qualifiedNamespace = $"{unresolvedAccessChain}.{namespaceName}";
                else
                    qualifiedNamespace = namespaceName;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Namespace)
            {
                if (captureNamespace.Length > 0)
                    qualifiedNamespace = $"{captureNamespace}.{namespaceName}";
                else
                    qualifiedNamespace = namespaceName;
            }

            if (!allLinkedNamespaces.Contains(qualifiedNamespace))
                return false;

            unresolvedAccessChain = "";
            captureArchetype = ExpressionCaptureArchetype.Namespace;
            captureNamespace = qualifiedNamespace;

            return true;
        }

        private bool HandleStaticMethodLookup(string methodToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Static method lookup only supported on Type archetypes");

            MethodInfo[] methods = captureType.GetMethods(BindingFlags.Static | BindingFlags.Public).Where(e => e.Name == methodToken).ToArray();

            if (methods.Length == 0)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Method;
            captureMethods = methods;

            return true;
        }

        private bool HandleStaticPropertyLookup(string propertyToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Static property lookup only supported on Type archetypes");

            PropertyInfo property = captureType.GetProperty(propertyToken, BindingFlags.Static | BindingFlags.Public);

            if (property == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Property;
            captureProperty = property;

            return true;
        }

        private bool HandleNestedTypeLookup(string nestedTypeToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Nested type lookup only supported on Type archetypes");

            System.Type type = captureType.GetNestedType(nestedTypeToken, BindingFlags.Public);

            if (type == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Type;
            captureType = type;

            return true;
        }

        private bool HandleEnumFieldLookup(string enumFieldToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Enum field lookup only supported on Type archetypes");

            if (!captureType.IsEnum)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Enum;
            captureEnum = enumFieldToken;

            return true;
        }

        private bool HandleStaticFieldLookup(string fieldToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Static field type lookup only supported on Type archetypes");

            FieldInfo field = captureType.GetField(fieldToken, BindingFlags.Static | BindingFlags.Public);

            if (field == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Field;
            captureField = field;

            return true;
        }

        private bool HandleMemberPropertyAccess(string propertyToken)
        {
            if (captureArchetype == ExpressionCaptureArchetype.Enum)
                return false;

            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.Field &&
                captureArchetype != ExpressionCaptureArchetype.ArrayIndexer &&
                captureArchetype != ExpressionCaptureArchetype.ExternUserField)
            {
                throw new System.Exception("Can only access properties on Local Symbols, Properties, and Fields");
            }

            System.Type currentReturnType = GetReturnType();

            PropertyInfo[] foundProperties = currentReturnType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == propertyToken).ToArray();

            if (foundProperties.Length == 0)
                return false;

            Debug.Assert(foundProperties.Length == 1);

            PropertyInfo foundProperty = foundProperties[0];

            // We have verified that this is a property token, so access the current accessSymbol to get its value and continue the chain.
            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.Property;
            captureProperty = foundProperty;

            return true;
        }

        private bool HandleMemberFieldAccess(string fieldToken)
        {
            if (captureArchetype == ExpressionCaptureArchetype.Enum)
                return false;

            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.Field &&
                captureArchetype != ExpressionCaptureArchetype.ArrayIndexer &&
                captureArchetype != ExpressionCaptureArchetype.ExternUserField)
            {
                throw new System.Exception("Can only access fields on Local Symbols, Properties, and Fields");
            }

            System.Type currentReturnType = GetReturnType();

            FieldInfo[] foundFields = currentReturnType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == fieldToken).ToArray();

            if (foundFields.Length == 0)
                return false;

            Debug.Assert(foundFields.Length == 1);

            FieldInfo foundField = foundFields[0];

            // We have verified that this is a field token, so access the current accessSymbol to get its value and continue the chain.
            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.Field;
            captureField = foundField;

            return true;
        }

        private bool HandleMemberMethodLookup(string methodToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.Field && 
                captureArchetype != ExpressionCaptureArchetype.ArrayIndexer &&
                captureArchetype != ExpressionCaptureArchetype.Enum &&
                captureArchetype != ExpressionCaptureArchetype.ExternUserField)
            {
                throw new System.Exception("Can only access member methods on Local Symbols, Properties, and Fields");
            }
            
            System.Type returnType = GetReturnType();

            List<MethodInfo> foundMethodInfos = new List<MethodInfo>(returnType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == methodToken));

            if (returnType != typeof(object))
                foundMethodInfos.AddRange(typeof(object).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == methodToken));

            if (foundMethodInfos.Count == 0)
                return false;

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.Method;
            captureMethods = foundMethodInfos.ToArray();

            return true;
        }

        private bool HandleExternUserFieldLookup(string fieldToken)
        {
            if (accessSymbol == null || !accessSymbol.IsUserDefinedBehaviour())
                return false;

            System.Type returnType = GetReturnType(true);

            ClassDefinition externClass = visitorContext.externClassDefinitions.Find(e => e.userClassType == returnType);

            if (externClass == null)
                return false;

            FieldDefinition foundDefinition = externClass.fieldDefinitions.Find(e => e.fieldSymbol.symbolOriginalName == fieldToken && e.fieldSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public));

            if (foundDefinition == null)
                return false;

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.ExternUserField;
            captureExternUserField = foundDefinition;

            return true;
        }

        private bool HandleExternUserMethodLookup(string methodToken)
        {
            if (accessSymbol == null || !accessSymbol.IsUserDefinedBehaviour())
                return false;

            System.Type returnType = GetReturnType(true);
            ClassDefinition externClass = visitorContext.externClassDefinitions.Find(e => e.userClassType == returnType);

            if (externClass == null)
                return false;

            MethodDefinition foundDefinition = externClass.methodDefinitions.Find(e => e.originalMethodName == methodToken && e.declarationFlags.HasFlag(MethodDeclFlags.Public));

            if (foundDefinition == null)
                return false;

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.ExternUserMethod;
            captureExternUserMethod = foundDefinition;

            return true;
        }

        private bool HandleUdonSharpInternalMethodLookup(string methodToken)
        {
            bool isInternalMethod = InternalMethodHandler.ResolveAccessToken(methodToken);

            if (!isInternalMethod)
                return false;

            if (captureArchetype == ExpressionCaptureArchetype.Unknown && unresolvedAccessChain.Length == 0)
                ResolveAccessToken("this");

            accessSymbol = ExecuteGet();
            captureArchetype = ExpressionCaptureArchetype.InternalUdonSharpMethod;

            return true;
        }

        public void HandleArrayIndexerAccess(SymbolDefinition indexerSymbol)
        {
            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                !IsField() &&
                captureArchetype != ExpressionCaptureArchetype.ArrayIndexer)
            {
                throw new System.Exception("Can only run indexers on Local Symbols, Properties, Fields, and other indexers");
            }

            System.Type returnType = GetReturnType(true);

            if (!returnType.IsArray/* && returnType != typeof(string)*/) // Uncomment the check for string when VRC has added the actual indexer function to Udon. 
                throw new System.Exception("Can only run array indexers on array types");

            bool isCastValid = (indexerSymbol.symbolCsType == typeof(int) || UdonSharpUtils.GetNumericConversionMethod(typeof(int), indexerSymbol.symbolCsType) != null) &&
                                indexerSymbol.symbolCsType.IsValueType &&
                                indexerSymbol.symbolCsType != typeof(float) && indexerSymbol.symbolCsType != typeof(float) && indexerSymbol.symbolCsType != typeof(decimal);

            // This will need to be changed if Udon ever exposes collections with non-int indexers
            if (isCastValid)
                indexerSymbol = CastSymbolToType(indexerSymbol, typeof(int), true);
            else
                indexerSymbol = CastSymbolToType(indexerSymbol, typeof(int), false); // Non-explicit cast to handle if any types have implicit conversion operators and throw an error otherwise

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.ArrayIndexer;
            arrayIndexerIndexSymbol = indexerSymbol;
        }

        public void HandleGenericAccess(List<System.Type> genericArguments)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Method && captureArchetype != ExpressionCaptureArchetype.InternalUdonSharpMethod)
                throw new System.ArgumentException("Cannot resolve generic arguments on non-method expression");

            genericTypeArguments = genericArguments;

            if (captureArchetype == ExpressionCaptureArchetype.InternalUdonSharpMethod)
                InternalMethodHandler.HandleGenericAccess(genericArguments);
        }

        private void HandleUnknownToken(string unknownToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown && captureArchetype != ExpressionCaptureArchetype.Namespace)
            {
                System.Type returnType = null;

                try
                {
                    returnType = GetReturnType(true);
                }
                catch { }

                string tokenName = unresolvedAccessChain + (unresolvedAccessChain.Length != 0 ? "." : "") + unknownToken;

                if (returnType != null)
                {
                    throw new System.Exception($"'{returnType.Name}' does not contain a definition for '{tokenName}'");
                }
                else
                {
                    throw new System.Exception($"Unknown type/field/parameter/method '{tokenName}'");
                }
            }

            captureArchetype = ExpressionCaptureArchetype.Unknown;
            if (captureNamespace.Length > 0)
            {
                unresolvedAccessChain = captureNamespace;
            }

            if (unresolvedAccessChain.Length > 0)
                unresolvedAccessChain += $".{unknownToken}";
            else
                unresolvedAccessChain = unknownToken;
        }
        #endregion
    }
}
