using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.VM;
#if VRC_CLIENT
using VRC.Udon.Security;
#endif
#if UNITY_EDITOR && !VRC_CLIENT
using UnityEditor.SceneManagement;

#endif

namespace VRC.Udon
{
    public class UdonBehaviour : VRC.SDKBase.VRC_Interactable, ISerializationCallbackReceiver, IUdonEventReceiver, IUdonSyncTarget, VRC.SDKBase.INetworkID
    {
        #region Odin Serialized Fields

        public IUdonVariableTable publicVariables = new UdonVariableTable();

        #endregion

        #region Serialized Public Fields

        public bool SynchronizePosition = false;
        public readonly bool SynchronizeAnimation = false; //We don't support animation sync yet, coming soon.
        public bool AllowCollisionOwnershipTransfer = true;

        #endregion

        #region Serialized Private Fields

        [SerializeField]
        private AbstractSerializedUdonProgramAsset serializedProgramAsset;

        #if UNITY_EDITOR && !VRC_CLIENT
        [SerializeField]
        public AbstractUdonProgramSource programSource;

        #endif

        #endregion

        #region Public Fields and Properties

        [PublicAPI]
        public static System.Action<UdonBehaviour, IUdonProgram> OnInit = null;

        [PublicAPI]
        public bool HasInteractiveEvents { get; private set; } = false;

        [HideInInspector]
        public int NetworkID { get; set; }

        #endregion

        #region Private Fields

        private IUdonProgram program;
        private IUdonVM _udonVM;
        private bool _isNetworkReady;
        private int _debugLevel;
        private bool _hasError;

        #endregion

        #region Editor Only

        #if UNITY_EDITOR && !VRC_CLIENT

        public void RunEditorUpdate(ref bool dirty)
        {
            if(programSource == null)
            {
                return;
            }

            programSource.RunEditorUpdate(this, ref dirty);

            if(!dirty)
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        #endif

        #endregion

        #region Private Methods

        private bool LoadProgram()
        {
            if(serializedProgramAsset == null)
            {
                return false;
            }

            program = serializedProgramAsset.RetrieveProgram();

            IUdonSymbolTable symbolTable = program?.SymbolTable;
            IUdonHeap heap = program?.Heap;
            if(symbolTable == null || heap == null)
            {
                return false;
            }

            foreach(string variableSymbol in publicVariables.VariableSymbols)
            {
                if(!symbolTable.HasAddressForSymbol(variableSymbol))
                {
                    continue;
                }

                uint symbolAddress = symbolTable.GetAddressFromSymbol(variableSymbol);

                if(!publicVariables.TryGetVariableType(variableSymbol, out Type declaredType))
                {
                    continue;
                }

                publicVariables.TryGetVariableValue(variableSymbol, out object value);
                if(declaredType == typeof(GameObject) || declaredType == typeof(UdonBehaviour) ||
                   declaredType == typeof(Transform))
                {
                    if(value == null)
                    {
                        value = new UdonGameObjectComponentHeapReference(declaredType);
                        declaredType = typeof(UdonGameObjectComponentHeapReference);
                    }
                }

                heap.SetHeapVariable(symbolAddress, value, declaredType);
            }

            return true;
        }

        #endregion

        #region Unity Events

        private readonly List<uint> _startPoints = new List<uint>();

        public override void Start()
        {
            InitializeUdonContent();
        }

        [PublicAPI]
        public void InitializeUdonContent()
        {
            SetupLogging();

            UdonManager udonManager = UdonManager.Instance;
            if(udonManager == null)
            {
                enabled = false;
                VRC.Core.Logger.LogError($"Could not find the UdonManager; the UdonBehaviour on '{gameObject.name}' will not run.", _debugLevel, this);
                return;
            }

            if(!LoadProgram())
            {
                enabled = false;
                VRC.Core.Logger.Log($"Could not load the program; the UdonBehaviour on '{gameObject.name}' will not run.", _debugLevel, this);

                if(OnInit != null)
                {
                    try
                    {
                        OnInit(this, null);
                    }
                    catch(Exception exception)
                    {
                        VRC.Core.Logger.LogError(
                            $"An exception '{exception.Message}' occurred during initialization; the UdonBehaviour on '{gameObject.name}' will not run. Exception:\n{exception}",
                            _debugLevel,
                            this
                        );
                    }
                }

                return;
            }

            IUdonSymbolTable symbolTable = program?.SymbolTable;
            IUdonHeap heap = program?.Heap;
            if(symbolTable == null || heap == null)
            {
                enabled = false;
                VRC.Core.Logger.Log($"Invalid program; the UdonBehaviour on '{gameObject.name}' will not run.", _debugLevel, this);
                return;
            }

            if(!ResolveUdonHeapReferences(symbolTable, heap))
            {
                enabled = false;
                VRC.Core.Logger.Log($"Failed to resolve a GameObject/Component Reference; the UdonBehaviour on '{gameObject.name}' will not run.", _debugLevel, this);
                return;
            }

            _udonVM = udonManager.ConstructUdonVM();

            if(_udonVM == null)
            {
                enabled = false;
                VRC.Core.Logger.LogError($"No UdonVM; the UdonBehaviour on '{gameObject.name}' will not run.", _debugLevel, this);
                return;
            }

            #if VRC_CLIENT
            program = new UdonProgram(
                program.InstructionSetIdentifier,
                program.InstructionSetVersion,
                program.ByteCode,
                new UdonSecureHeap(program.Heap, udonManager),
                program.EntryPoints,
                program.SymbolTable,
                program.SyncMetadataTable
            );
            #endif

            _udonVM.LoadProgram(program);

            ProcessEntryPoints();

            #if !VRC_CLIENT
            _isNetworkReady = true;
            #endif

            if(OnInit != null)
            {
                try
                {
                    OnInit(this, program);
                }
                catch(Exception exception)
                {
                    enabled = false;
                    VRC.Core.Logger.LogError(
                        $"An exception '{exception.Message}' occurred during initialization; the UdonBehaviour on '{gameObject.name}' will not run. Exception:\n{exception}",
                        _debugLevel,
                        this
                    );
                }
            }
        }

        private void ProcessEntryPoints()
        {
            foreach(string entryPoint in program.EntryPoints.GetExportedSymbols())
            {
                uint address = program.EntryPoints.GetAddressFromSymbol(entryPoint);
                switch(entryPoint)
                {
                    case "_start":
                    {
                        _startPoints.Add(address);
                        break;
                    }

                    case "_update":
                    {
                        _updatePoints.Add(address);
                        break;
                    }

                    case "_lateUpdate":
                    {
                        _lateUpdatePoints.Add(address);
                        break;
                    }

                    case "_interact":
                    {
                        HasInteractiveEvents = true;
                        _interactPoints.Add(address);
                        break;
                    }

                    case "_fixedUpdate":
                    {
                        _fixedUpdatePoints.Add(address);
                        break;
                    }

                    case "_onAnimatorIk":
                    {
                        _onAnimatorIkPoints.Add(address);
                        break;
                    }

                    case "_onAnimatorMove":
                    {
                        _onAnimatorMovePoints.Add(address);
                        break;
                    }

                    case "_onAudioFilterRead":
                    {
                        _onAudioFilterReadPoints.Add(address);
                        break;
                    }

                    case "_onBecameInvisible":
                    {
                        _onBecameInvisiblePoints.Add(address);
                        break;
                    }

                    case "_onBecameVisible":
                    {
                        _onBecameVisiblePoints.Add(address);
                        break;
                    }

                    case "_onCollisionEnter":
                    {
                        _onCollisionEnterPoints.Add(address);
                        break;
                    }

                    case "_onCollisionEnter2D":
                    {
                        _onCollisionEnter2DPoints.Add(address);
                        break;
                    }

                    case "_onCollisionExit":
                    {
                        _onCollisionExitPoints.Add(address);
                        break;
                    }

                    case "_onCollisionExit2D":
                    {
                        _onCollisionExit2DPoints.Add(address);
                        break;
                    }

                    case "_onCollisionStay":
                    {
                        _onCollisionStayPoints.Add(address);
                        break;
                    }

                    case "_onCollisionStay2D":
                    {
                        _onCollisionStay2DPoints.Add(address);
                        break;
                    }

                    case "_onControllerColliderHit":
                    {
                        _onControllerColliderHitPoints.Add(address);
                        break;
                    }

                    case "_onDestroy":
                    {
                        _onDestroyPoints.Add(address);
                        break;
                    }

                    case "_onDisable":
                    {
                        _onDisablePoints.Add(address);
                        break;
                    }

                    case "_onDrawGizmos":
                    {
                        _onDrawGizmosPoints.Add(address);
                        break;
                    }

                    case "_onDrawGizmosSelected":
                    {
                        _onDrawGizmosSelectedPoints.Add(address);
                        break;
                    }

                    case "_onEnable":
                    {
                        _onEnablePoints.Add(address);
                        break;
                    }

                    case "_onGUI":
                    {
                        _onGUIPoints.Add(address);
                        break;
                    }

                    case "_onJointBreak":
                    {
                        _onJointBreakPoints.Add(address);
                        break;
                    }

                    case "_onJointBreak2D":
                    {
                        _onJointBreak2DPoints.Add(address);
                        break;
                    }

                    case "_onMouseDown":
                    {
                        _onMouseDownPoints.Add(address);
                        break;
                    }

                    case "_onMouseDrag":
                    {
                        _onMouseDragPoints.Add(address);
                        break;
                    }

                    case "_onMouseEnter":
                    {
                        _onMouseEnterPoints.Add(address);
                        break;
                    }

                    case "_onMouseExit":
                    {
                        _onMouseExitPoints.Add(address);
                        break;
                    }

                    case "_onMouseOver":
                    {
                        _onMouseOverPoints.Add(address);
                        break;
                    }

                    case "_onMouseUp":
                    {
                        _onMouseUpPoints.Add(address);
                        break;
                    }

                    case "_onMouseUpAsButton":
                    {
                        _onMouseUpAsButtonPoints.Add(address);
                        break;
                    }

                    case "_onParticleCollision":
                    {
                        _onParticleCollisionPoints.Add(address);
                        break;
                    }

                    case "_onParticleTrigger":
                    {
                        _onParticleTriggerPoints.Add(address);
                        break;
                    }

                    case "_onPostRender":
                    {
                        _onPostRenderPoints.Add(address);
                        break;
                    }

                    case "_onPreCull":
                    {
                        _onPreCullPoints.Add(address);
                        break;
                    }

                    case "_onPreRender":
                    {
                        _onPreRenderPoints.Add(address);
                        break;
                    }

                    case "_onRenderImage":
                    {
                        _onRenderImagePoints.Add(address);
                        break;
                    }

                    case "_onRenderObject":
                    {
                        _onRenderObjectPoints.Add(address);
                        break;
                    }

                    case "_onTransformChildrenChanged":
                    {
                        _onTransformChildrenChangedPoints.Add(address);
                        break;
                    }

                    case "_onTransformParentChanged":
                    {
                        _onTransformParentChangedPoints.Add(address);
                        break;
                    }

                    case "_onTriggerEnter":
                    {
                        _onTriggerEnterPoints.Add(address);
                        break;
                    }

                    case "_onTriggerEnter2D":
                    {
                        _onTriggerEnter2DPoints.Add(address);
                        break;
                    }

                    case "_onTriggerExit":
                    {
                        _onTriggerExitPoints.Add(address);
                        break;
                    }

                    case "_onTriggerExit2D":
                    {
                        _onTriggerExit2DPoints.Add(address);
                        break;
                    }

                    case "_onTriggerStay":
                    {
                        _onTriggerStayPoints.Add(address);
                        break;
                    }

                    case "_onTriggerStay2D":
                    {
                        _onTriggerStay2DPoints.Add(address);
                        break;
                    }

                    case "_onValidate":
                    {
                        _onValidatePoints.Add(address);
                        break;
                    }

                    case "_onWillRenderObject":
                    {
                        _onWillRenderObjectPoints.Add(address);
                        break;
                    }

                    //case "_onDataStorageAdded":
                    //{
                    //    _onDataStorageAddedPoints.Add(address);
                    //    break;
                    //}

                    //case "_onDataStorageChanged":
                    //{
                    //    _onDataStorageChangedPoints.Add(address);
                    //    break;
                    //}

                    //case "_onDataStorageRemoved":
                    //{
                    //    _onDataStorageRemovedPoints.Add(address);
                    //    break;
                    //}

                    case "_onDrop":
                    {
                        _onDropPoints.Add(address);
                        break;
                    }

                    case "_onOwnershipTransferred":
                    {
                        _onOwnershipTransferredPoints.Add(address);
                        break;
                    }

                    case "_onPickup":
                    {
                        _onPickupPoints.Add(address);
                        break;
                    }

                    case "_onPickupUseDown":
                    {
                        _onPickupUseDownPoints.Add(address);
                        break;
                    }

                    case "_onPickupUseUp":
                    {
                        _onPickupUseUpPoints.Add(address);
                        break;
                    }

                    case "_onPlayerJoined":
                    {
                        _onPlayerJoinedPoints.Add(address);
                        break;
                    }

                    case "_onPlayerLeft":
                    {
                        _onPlayerLeftPoints.Add(address);
                        break;
                    }

                    case "_onSpawn":
                    {
                        _onSpawnPoints.Add(address);
                        break;
                    }

                    case "_onStationEntered":
                    {
                        _onStationEnteredPoints.Add(address);
                        break;
                    }

                    case "_onStationExited":
                    {
                        _onStationExitedPoints.Add(address);
                        break;
                    }

                    case "_onVideoEnd":
                    {
                        _onVideoEndPoints.Add(address);
                        break;
                    }

                    case "_onVideoPause":
                    {
                        _onVideoPausePoints.Add(address);
                        break;
                    }

                    case "_onVideoPlay":
                    {
                        _onVideoPlayPoints.Add(address);
                        break;
                    }

                    case "_onVideoStart":
                    {
                        _onVideoStartPoints.Add(address);
                        break;
                    }
                    case "_onPreSerialization":
                    {
                        _onPreSerializationStartPoints.Add(address);
                        break;
                    }
                    case "_onDeserialization":
                    {
                        _onDeserializationStartPoints.Add(address);
                        break;
                    }
                }
            }
        }

        private bool ResolveUdonHeapReferences(IUdonSymbolTable symbolTable, IUdonHeap heap)
        {
            bool success = true;
            foreach(string symbolName in symbolTable.GetSymbols())
            {
                uint symbolAddress = symbolTable.GetAddressFromSymbol(symbolName);
                object heapValue = heap.GetHeapVariable(symbolAddress);
                if(!(heapValue is UdonBaseHeapReference udonBaseHeapReference))
                {
                    continue;
                }

                if(!ResolveUdonHeapReference(heap, symbolAddress, udonBaseHeapReference))
                {
                    success = false;
                }
            }

            return success;
        }

        private bool ResolveUdonHeapReference(IUdonHeap heap, uint symbolAddress, UdonBaseHeapReference udonBaseHeapReference)
        {
            switch(udonBaseHeapReference)
            {
                case UdonGameObjectComponentHeapReference udonGameObjectComponentHeapReference:
                {
                    Type referenceType = udonGameObjectComponentHeapReference.type;
                    if(referenceType == typeof(GameObject))
                    {
                        heap.SetHeapVariable(symbolAddress, gameObject);
                        return true;
                    }
                    else if(referenceType == typeof(Transform))
                    {
                        heap.SetHeapVariable(symbolAddress, gameObject.transform);
                        return true;
                    }
                    else if(referenceType == typeof(UdonBehaviour))
                    {
                        heap.SetHeapVariable(symbolAddress, this);
                        return true;
                    }
                    else if(referenceType == typeof(UnityEngine.Object))
                    {
                        heap.SetHeapVariable(symbolAddress, this);
                        return true;
                    }
                    else
                    {
                        VRC.Core.Logger.Log(
                            $"Unsupported GameObject/Component reference type: {udonBaseHeapReference.GetType().Name}. Only GameObject, Transform, and UdonBehaviour are supported.",
                            _debugLevel,
                            this);

                        return false;
                    }
                }
                default:
                {
                    VRC.Core.Logger.Log($"Unknown heap reference type: {udonBaseHeapReference.GetType().Name}", _debugLevel, this);
                    return false;
                }
            }
        }

        private readonly List<uint> _updatePoints = new List<uint>();
        private bool _hasDoneStart;

        private void Update()
        {
            if(!_isNetworkReady)
            {
                return;
            }

            if(!_hasDoneStart)
            {
                foreach(uint startPoint in _startPoints)
                {
                    RunProgram(startPoint);
                }

                _hasDoneStart = true;
            }

            foreach(uint updatePoint in _updatePoints)
            {
                RunProgram(updatePoint);
            }
        }

        private readonly List<uint> _lateUpdatePoints = new List<uint>();

        private void LateUpdate()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint lateUpdatePoint in _lateUpdatePoints)
            {
                RunProgram(lateUpdatePoint);
            }
        }

        private readonly List<uint> _fixedUpdatePoints = new List<uint>();

        public void FixedUpdate()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint fixedUpdatePoint in _fixedUpdatePoints)
            {
                RunProgram(fixedUpdatePoint);
            }
        }

        private readonly List<uint> _onAnimatorIkPoints = new List<uint>();

        public void OnAnimatorIK(int layerIndex)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onAnimatorIkLayerIndex", layerIndex);
            foreach(uint onAnimatorIkPoint in _onAnimatorIkPoints)
            {
                RunProgram(onAnimatorIkPoint);
            }
        }

        private readonly List<uint> _onAnimatorMovePoints = new List<uint>();

        public void OnAnimatorMove()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onAnimatorMovePoint in _onAnimatorMovePoints)
            {
                RunProgram(onAnimatorMovePoint);
            }
        }

        private readonly List<uint> _onAudioFilterReadPoints = new List<uint>();

        public void OnAudioFilterRead(float[] data, int channels)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onAudioFilterReadData", data);
            SetProgramVariable("onAudioFilterReadChannels", channels);
            foreach(uint onAudioFilterReadPoint in _onAudioFilterReadPoints)
            {
                RunProgram(onAudioFilterReadPoint);
            }
        }

        private readonly List<uint> _onBecameInvisiblePoints = new List<uint>();

        public void OnBecameInvisible()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onBecameInvisiblePoint in _onBecameInvisiblePoints)
            {
                RunProgram(onBecameInvisiblePoint);
            }
        }

        private readonly List<uint> _onBecameVisiblePoints = new List<uint>();

        public void OnBecameVisible()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onBecameVisiblePoint in _onBecameVisiblePoints)
            {
                RunProgram(onBecameVisiblePoint);
            }
        }

        private readonly List<uint> _onCollisionEnterPoints = new List<uint>();

        public void OnCollisionEnter(Collision other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onCollisionEnterOther", other);
            foreach(uint onCollisionEnterPoint in _onCollisionEnterPoints)
            {
                RunProgram(onCollisionEnterPoint);
            }

            SetProgramVariable("onCollisionEnterOther", null);
        }

        private readonly List<uint> _onCollisionEnter2DPoints = new List<uint>();

        public void OnCollisionEnter2D(Collision2D other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onCollisionEnter2DOther", other);
            foreach(uint onCollisionEnter2DPoint in _onCollisionEnter2DPoints)
            {
                RunProgram(onCollisionEnter2DPoint);
            }

            SetProgramVariable("onCollisionEnter2DOther", null);
        }

        private readonly List<uint> _onCollisionExitPoints = new List<uint>();

        public void OnCollisionExit(Collision other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onCollisionExitOther", other);
            foreach(uint onCollisionExitPoint in _onCollisionExitPoints)
            {
                RunProgram(onCollisionExitPoint);
            }

            SetProgramVariable("onCollisionExitOther", null);
        }

        private readonly List<uint> _onCollisionExit2DPoints = new List<uint>();

        public void OnCollisionExit2D(Collision2D other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onCollisionExit2DOther", other);
            foreach(uint onCollisionExit2DPoint in _onCollisionExit2DPoints)
            {
                RunProgram(onCollisionExit2DPoint);
            }

            SetProgramVariable("onCollisionExit2DOther", null);
        }

        private readonly List<uint> _onCollisionStayPoints = new List<uint>();

        public void OnCollisionStay(Collision other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onCollisionStayOther", other);
            foreach(uint onCollisionStayPoint in _onCollisionStayPoints)
            {
                RunProgram(onCollisionStayPoint);
            }

            SetProgramVariable("onCollisionStayOther", null);
        }

        private readonly List<uint> _onCollisionStay2DPoints = new List<uint>();

        public void OnCollisionStay2D(Collision2D other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onCollisionStay2DOther", other);
            foreach(uint onCollisionStay2DPoint in _onCollisionStay2DPoints)
            {
                RunProgram(onCollisionStay2DPoint);
            }

            SetProgramVariable("onCollisionStay2DOther", null);
        }

        private readonly List<uint> _onControllerColliderHitPoints = new List<uint>();

        public void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onControllerColliderHitHit", hit);
            foreach(uint onControllerColliderHitPoint in _onControllerColliderHitPoints)
            {
                RunProgram(onControllerColliderHitPoint);
            }

            SetProgramVariable("onControllerColliderHitHit", null);
        }

        private readonly List<uint> _onDestroyPoints = new List<uint>();

        public void OnDestroy()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onDestroyPoint in _onDestroyPoints)
            {
                RunProgram(onDestroyPoint);
            }
        }

        private readonly List<uint> _onDisablePoints = new List<uint>();

        public void OnDisable()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onDisablePoint in _onDisablePoints)
            {
                RunProgram(onDisablePoint);
            }
        }

        private readonly List<uint> _onDrawGizmosPoints = new List<uint>();

        public void OnDrawGizmos()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onDrawGizmosPoint in _onDrawGizmosPoints)
            {
                RunProgram(onDrawGizmosPoint);
            }
        }

        private readonly List<uint> _onDrawGizmosSelectedPoints = new List<uint>();

        public void OnDrawGizmosSelected()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onDrawGizmosSelectedPoint in _onDrawGizmosSelectedPoints)
            {
                RunProgram(onDrawGizmosSelectedPoint);
            }
        }

        private readonly List<uint> _onEnablePoints = new List<uint>();

        public void OnEnable()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onEnablePoint in _onEnablePoints)
            {
                RunProgram(onEnablePoint);
            }
        }

        private readonly List<uint> _onGUIPoints = new List<uint>();

        public void OnGUI()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onGUIPoint in _onGUIPoints)
            {
                RunProgram(onGUIPoint);
            }
        }

        private readonly List<uint> _onJointBreakPoints = new List<uint>();

        public void OnJointBreak(float breakForce)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onJointBreakBreakForce", breakForce);
            foreach(uint onJointBreakPoint in _onJointBreakPoints)
            {
                RunProgram(onJointBreakPoint);
            }
        }

        private readonly List<uint> _onJointBreak2DPoints = new List<uint>();

        public void OnJointBreak2D(Joint2D brokenJoint)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onJointBreak2DBrokenJoint", brokenJoint);
            foreach(uint onJointBreak2DPoint in _onJointBreak2DPoints)
            {
                RunProgram(onJointBreak2DPoint);
            }
        }

        private readonly List<uint> _onMouseDownPoints = new List<uint>();

        public void OnMouseDown()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onMouseDownPoint in _onMouseDownPoints)
            {
                RunProgram(onMouseDownPoint);
            }
        }

        private readonly List<uint> _onMouseDragPoints = new List<uint>();

        public void OnMouseDrag()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onMouseDragPoint in _onMouseDragPoints)
            {
                RunProgram(onMouseDragPoint);
            }
        }

        private readonly List<uint> _onMouseEnterPoints = new List<uint>();

        public void OnMouseEnter()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onMouseEnterPoint in _onMouseEnterPoints)
            {
                RunProgram(onMouseEnterPoint);
            }
        }

        private readonly List<uint> _onMouseExitPoints = new List<uint>();

        public void OnMouseExit()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onMouseExitPoint in _onMouseExitPoints)
            {
                RunProgram(onMouseExitPoint);
            }
        }

        private readonly List<uint> _onMouseOverPoints = new List<uint>();

        public void OnMouseOver()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onMouseOverPoint in _onMouseOverPoints)
            {
                RunProgram(onMouseOverPoint);
            }
        }

        private readonly List<uint> _onMouseUpPoints = new List<uint>();

        public void OnMouseUp()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onMouseUpPoint in _onMouseUpPoints)
            {
                RunProgram(onMouseUpPoint);
            }
        }

        private readonly List<uint> _onMouseUpAsButtonPoints = new List<uint>();

        public void OnMouseUpAsButton()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onMouseUpAsButtonPoint in _onMouseUpAsButtonPoints)
            {
                RunProgram(onMouseUpAsButtonPoint);
            }
        }

        private readonly List<uint> _onParticleCollisionPoints = new List<uint>();

        public void OnParticleCollision(GameObject other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onParticleCollisionOther", other);
            foreach(uint onParticleCollisionPoint in _onParticleCollisionPoints)
            {
                RunProgram(onParticleCollisionPoint);
            }
        }

        private readonly List<uint> _onParticleTriggerPoints = new List<uint>();

        public void OnParticleTrigger()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onParticleTriggerPoint in _onParticleTriggerPoints)
            {
                RunProgram(onParticleTriggerPoint);
            }
        }

        private readonly List<uint> _onPostRenderPoints = new List<uint>();

        public void OnPostRender()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onPostRenderPoint in _onPostRenderPoints)
            {
                RunProgram(onPostRenderPoint);
            }
        }

        private readonly List<uint> _onPreCullPoints = new List<uint>();

        public void OnPreCull()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onPreCullPoint in _onPreCullPoints)
            {
                RunProgram(onPreCullPoint);
            }
        }

        private readonly List<uint> _onPreRenderPoints = new List<uint>();

        public void OnPreRender()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onPreRenderPoint in _onPreRenderPoints)
            {
                RunProgram(onPreRenderPoint);
            }
        }

        private readonly List<uint> _onRenderImagePoints = new List<uint>();

        public void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            if(_onRenderImagePoints.Count == 0)
            {
                Graphics.Blit(src, dest);
                return;
            }

            SetProgramVariable("onRenderImageSrc", src);
            SetProgramVariable("onRenderImageDest", dest);
            foreach(uint onRenderImagePoint in _onRenderImagePoints)
            {
                RunProgram(onRenderImagePoint);
            }
        }

        private readonly List<uint> _onRenderObjectPoints = new List<uint>();

        public void OnRenderObject()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onRenderObjectPoint in _onRenderObjectPoints)
            {
                RunProgram(onRenderObjectPoint);
            }
        }

        private readonly List<uint> _onTransformChildrenChangedPoints = new List<uint>();

        public void OnTransformChildrenChanged()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onTransformChildrenChangedPoint in _onTransformChildrenChangedPoints)
            {
                RunProgram(onTransformChildrenChangedPoint);
            }
        }

        private readonly List<uint> _onTransformParentChangedPoints = new List<uint>();

        public void OnTransformParentChanged()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onTransformParentChangedPoint in _onTransformParentChangedPoints)
            {
                RunProgram(onTransformParentChangedPoint);
            }
        }

        private readonly List<uint> _onTriggerEnterPoints = new List<uint>();

        public void OnTriggerEnter(Collider other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onTriggerEnterOther", other);
            foreach(uint onTriggerEnterPoint in _onTriggerEnterPoints)
            {
                RunProgram(onTriggerEnterPoint);
            }

            SetProgramVariable("onTriggerEnterOther", null);
        }

        private readonly List<uint> _onTriggerEnter2DPoints = new List<uint>();

        public void OnTriggerEnter2D(Collider2D other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onTriggerEnter2DOther", other);
            foreach(uint onTriggerEnter2DPoint in _onTriggerEnter2DPoints)
            {
                RunProgram(onTriggerEnter2DPoint);
            }

            SetProgramVariable("onTriggerEnter2DOther", null);
        }

        private readonly List<uint> _onTriggerExitPoints = new List<uint>();

        public void OnTriggerExit(Collider other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onTriggerExitOther", other);
            foreach(uint onTriggerExitPoint in _onTriggerExitPoints)
            {
                RunProgram(onTriggerExitPoint);
            }

            SetProgramVariable("onTriggerExitOther", null);
        }

        private readonly List<uint> _onTriggerExit2DPoints = new List<uint>();

        public void OnTriggerExit2D(Collider2D other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onTriggerExit2DOther", other);
            foreach(uint onTriggerExit2DPoint in _onTriggerExit2DPoints)
            {
                RunProgram(onTriggerExit2DPoint);
            }

            SetProgramVariable("onTriggerExit2DOther", null);
        }

        private readonly List<uint> _onTriggerStayPoints = new List<uint>();

        public void OnTriggerStay(Collider other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onTriggerStayOther", other);
            foreach(uint onTriggerStayPoint in _onTriggerStayPoints)
            {
                RunProgram(onTriggerStayPoint);
            }

            SetProgramVariable("onTriggerStayOther", null);
        }

        private readonly List<uint> _onTriggerStay2DPoints = new List<uint>();

        public void OnTriggerStay2D(Collider2D other)
        {
            if(!_hasDoneStart)
            {
                return;
            }

            SetProgramVariable("onTriggerStay2DOther", other);
            foreach(uint onTriggerStay2DPoint in _onTriggerStay2DPoints)
            {
                RunProgram(onTriggerStay2DPoint);
            }

            SetProgramVariable("onTriggerStay2DOther", null);
        }

        private readonly List<uint> _onValidatePoints = new List<uint>();

        public void OnValidate()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onValidatePoint in _onValidatePoints)
            {
                RunProgram(onValidatePoint);
            }
        }

        private readonly List<uint> _onWillRenderObjectPoints = new List<uint>();

        public void OnWillRenderObject()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onWillRenderObjectPoint in _onWillRenderObjectPoints)
            {
                RunProgram(onWillRenderObjectPoint);
            }
        }

        #endregion

        #region VRCSDK Events

        #if VRC_CLIENT
        private void OnNetworkReady()
        {
            _isNetworkReady = true;
        }
        #endif

        private readonly List<uint> _interactPoints = new List<uint>();

        public override void Interact()
        {
            foreach(uint interactPoint in _interactPoints)
            {
                RunProgram(interactPoint);
            }
        }

        //private readonly List<uint> _onDataStorageAddedPoints = new List<uint>();

        //public void OnDataStorageAdded(VRC_DataStorage ds, int idx)
        //{
        //    if(!_hasDoneStart)
        //    {
        //        return;
        //    }

        //    SetHeapVariable("onDataStorageAddedDs", ds);
        //    SetHeapVariable("onDataStorageAddedIdx", idx);
        //    foreach(uint onDataStorageAddedPoint in _onDataStorageAddedPoints)
        //    {
        //        RunProgram(onDataStorageAddedPoint);
        //    }
        //}

        //private readonly List<uint> _onDataStorageChangedPoints = new List<uint>();

        //public void OnDataStorageChanged(VRC_DataStorage ds, int idx)
        //{
        //    if(!_hasDoneStart)
        //    {
        //        return;
        //    }

        //    SetHeapVariable("onDataStorageChangedDs", ds);
        //    SetHeapVariable("onDataStorageChangedIdx", idx);
        //    foreach(uint onDataStorageChangedPoint in _onDataStorageChangedPoints)
        //    {
        //        RunProgram(onDataStorageChangedPoint);
        //    }
        //}

        //private readonly List<uint> _onDataStorageRemovedPoints = new List<uint>();

        //public void OnDataStorageRemoved(VRC_DataStorage ds, int idx)
        //{
        //    if(!_hasDoneStart)
        //    {
        //        return;
        //    }

        //    SetHeapVariable("onDataStorageRemovedDs", ds);
        //    SetHeapVariable("onDataStorageRemovedIdx", idx);
        //    foreach(uint onDataStorageRemovedPoint in _onDataStorageRemovedPoints)
        //    {
        //        RunProgram(onDataStorageRemovedPoint);
        //    }
        //}

        private readonly List<uint> _onDropPoints = new List<uint>();

        public void OnDrop()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onDropPoint in _onDropPoints)
            {
                RunProgram(onDropPoint);
            }
        }

        private readonly List<uint> _onOwnershipTransferredPoints = new List<uint>();

        public void OnOwnershipTransferred()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onOwnershipTransferredPoint in _onOwnershipTransferredPoints)
            {
                RunProgram(onOwnershipTransferredPoint);
            }
        }

        private readonly List<uint> _onPickupPoints = new List<uint>();

        public void OnPickup()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onPickupPoint in _onPickupPoints)
            {
                RunProgram(onPickupPoint);
            }
        }

        private readonly List<uint> _onPickupUseDownPoints = new List<uint>();

        public void OnPickupUseDown()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onPickupUseDownPoint in _onPickupUseDownPoints)
            {
                RunProgram(onPickupUseDownPoint);
            }
        }

        private readonly List<uint> _onPickupUseUpPoints = new List<uint>();

        public void OnPickupUseUp()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onPickupUseUpPoint in _onPickupUseUpPoints)
            {
                RunProgram(onPickupUseUpPoint);
            }
        }

        private readonly List<uint> _onPlayerJoinedPoints = new List<uint>();

        public void OnPlayerJoined(VRC.SDKBase.VRCPlayerApi player)
        {
            SetProgramVariable("onPlayerJoinedPlayer", player);
            foreach(uint onPlayerJoinedPoint in _onPlayerJoinedPoints)
            {
                RunProgram(onPlayerJoinedPoint);
            }
        }

        private readonly List<uint> _onPlayerLeftPoints = new List<uint>();

        public void OnPlayerLeft(VRC.SDKBase.VRCPlayerApi player)
        {
            SetProgramVariable("onPlayerLeftPlayer", player);
            foreach(uint onPlayerLeftPoint in _onPlayerLeftPoints)
            {
                RunProgram(onPlayerLeftPoint);
            }
        }

        private readonly List<uint> _onSpawnPoints = new List<uint>();

        public void OnSpawn()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onSpawnPoint in _onSpawnPoints)
            {
                RunProgram(onSpawnPoint);
            }
        }

        private readonly List<uint> _onStationEnteredPoints = new List<uint>();

        public void OnStationEntered()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onStationEnteredPoint in _onStationEnteredPoints)
            {
                RunProgram(onStationEnteredPoint);
            }
        }

        private readonly List<uint> _onStationExitedPoints = new List<uint>();

        public void OnStationExited()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onStationExitedPoint in _onStationExitedPoints)
            {
                RunProgram(onStationExitedPoint);
            }
        }

        private readonly List<uint> _onVideoEndPoints = new List<uint>();

        public void OnVideoEnd()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onVideoEndPoint in _onVideoEndPoints)
            {
                RunProgram(onVideoEndPoint);
            }
        }

        private readonly List<uint> _onVideoPausePoints = new List<uint>();

        public void OnVideoPause()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onVideoPausePoint in _onVideoPausePoints)
            {
                RunProgram(onVideoPausePoint);
            }
        }

        private readonly List<uint> _onVideoPlayPoints = new List<uint>();

        public void OnVideoPlay()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onVideoPlayPoint in _onVideoPlayPoints)
            {
                RunProgram(onVideoPlayPoint);
            }
        }

        private readonly List<uint> _onVideoStartPoints = new List<uint>();

        public void OnVideoStart()
        {
            if(!_hasDoneStart)
            {
                return;
            }

            foreach(uint onVideoStartPoint in _onVideoStartPoints)
            {
                RunProgram(onVideoStartPoint);
            }
        }

        private readonly List<uint> _onPreSerializationStartPoints = new List<uint>();

        public void OnPreSerialization()
        {
            if(!_isNetworkReady)
            {
                return;
            }

            foreach(uint OnPreSerializationStartPoint in _onPreSerializationStartPoints)
            {
                RunProgram(OnPreSerializationStartPoint);
            }
        }

        private readonly List<uint> _onDeserializationStartPoints = new List<uint>();

        public void OnDeserialization()
        {
            if(!_isNetworkReady)
            {
                return;
            }

            foreach(uint OnDeserializationStartPoint in _onDeserializationStartPoints)
            {
                RunProgram(OnDeserializationStartPoint);
            }
        }

        #endregion

        #region RunProgram Methods

        [PublicAPI]
        public static System.Action<UdonBehaviour, NetworkEventTarget, string> RunProgramAsRPCHook = null;

        [PublicAPI]
        public void RunProgramAsRPC(NetworkEventTarget target, string eventName)
        {
            RunProgramAsRPCHook?.Invoke(this, target, eventName);
        }

        public void RunProgram(string eventName)
        {
            if(program == null)
            {
                return;
            }

            foreach(string entryPoint in program.EntryPoints.GetExportedSymbols())
            {
                if(entryPoint != eventName)
                {
                    continue;
                }

                uint address = program.EntryPoints.GetAddressFromSymbol(entryPoint);
                RunProgram(address);
            }
        }

        private void RunProgram(uint entryPoint)
        {
            if(_hasError)
            {
                return;
            }

            if(_udonVM == null)
            {
                return;
            }

            uint originalAddress = _udonVM.GetProgramCounter();
            UdonBehaviour originalExecuting = UdonManager.Instance.currentlyExecuting;

            _udonVM.SetProgramCounter(entryPoint);
            UdonManager.Instance.currentlyExecuting = this;

            _udonVM.DebugLogging = UdonManager.Instance.DebugLogging;

            try
            {
                uint result = _udonVM.Interpret();
                if(result != 0)
                {
                    VRC.Core.Logger.LogError($"Udon VM execution errored, this UdonBehaviour will be halted.", _debugLevel, this);
                    _hasError = true;
                    enabled = false;
                }
            }
            catch(UdonVMException error)
            {
                VRC.Core.Logger.LogError($"An exception occurred during Udon execution, this UdonBehaviour will be halted.\n{error}", _debugLevel, this);
                _hasError = true;
                enabled = false;
            }

            UdonManager.Instance.currentlyExecuting = originalExecuting;
            if(originalAddress < 0xFFFFFFFC)
            {
                _udonVM.SetProgramCounter(originalAddress);
            }
        }

        [PublicAPI]
        public string[] GetPrograms()
        {
            if(program == null)
            {
                return new string[0];
            }

            return program.EntryPoints.GetExportedSymbols();
        }

        #endregion

        #region Serialization

        [SerializeField]
        private string serializedPublicVariablesBytesString;

        [SerializeField]
        private List<UnityEngine.Object> publicVariablesUnityEngineObjects;

        [SerializeField]
        private DataFormat publicVariablesSerializationDataFormat = DataFormat.Binary;

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            DeserializePublicVariables();
        }

        private void DeserializePublicVariables()
        {
            byte[] serializedPublicVariablesBytes = Convert.FromBase64String(serializedPublicVariablesBytesString ?? "");
            publicVariables = SerializationUtility.DeserializeValue<IUdonVariableTable>(
                                  serializedPublicVariablesBytes,
                                  publicVariablesSerializationDataFormat,
                                  publicVariablesUnityEngineObjects
                              ) ?? new UdonVariableTable();

            // Validate that the type of the value can actually be cast to the declaredType to avoid InvalidCastExceptions later.
            foreach(string publicVariableSymbol in publicVariables.VariableSymbols.ToArray())
            {
                if(!publicVariables.TryGetVariableValue(publicVariableSymbol, out object value))
                {
                    continue;
                }

                if(value == null)
                {
                    continue;
                }

                if(!publicVariables.TryGetVariableType(publicVariableSymbol, out Type declaredType))
                {
                    continue;
                }

                if(declaredType.IsInstanceOfType(value))
                {
                    continue;
                }

                if(declaredType.IsValueType)
                {
                    publicVariables.TrySetVariableValue(publicVariableSymbol, Activator.CreateInstance(declaredType));
                }
                else
                {
                    publicVariables.TrySetVariableValue(publicVariableSymbol, null);
                }
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            SerializePublicVariables();
        }

        private void SerializePublicVariables()
        {
            byte[] serializedPublicVariablesBytes = SerializationUtility.SerializeValue(publicVariables, publicVariablesSerializationDataFormat, out publicVariablesUnityEngineObjects);
            serializedPublicVariablesBytesString = Convert.ToBase64String(serializedPublicVariablesBytes);
        }

        #endregion

        #region IUdonEventReceiver and IUdonSyncTarget Interface

        #region IUdonEventReceiver Only

        public void SendCustomEvent(string eventName)
        {
            RunProgram(eventName);
        }

        public void SendCustomNetworkEvent(NetworkEventTarget target, string eventName)
        {
            RunProgramAsRPC(target, eventName);
        }

        #endregion

        #region IUdonSyncTarget Only

        public IUdonSyncMetadataTable SyncMetadataTable => program?.SyncMetadataTable;

        public Type GetHeapVariableType(string symbolName)
        {
            if(!program.SymbolTable.HasAddressForSymbol(symbolName))
            {
                return null;
            }

            uint symbolAddress = program.SymbolTable.GetAddressFromSymbol(symbolName);
            return program.Heap.GetHeapVariableType(symbolAddress);
        }

        public void SetHeapVariable(string symbolName, object value)
        {
            SetProgramVariable(symbolName, value);
        }

        public object GetHeapVariable(string symbolName)
        {
            return GetProgramVariable(symbolName);
        }

        #endregion

        #region Shared

        public void SetProgramVariable(string symbolName, object value)
        {
            if(program == null)
            {
                return;
            }

            if(!program.SymbolTable.TryGetAddressFromSymbol(symbolName, out uint symbolAddress))
            {
                return;
            }

            program.Heap.SetHeapVariable(symbolAddress, value);
        }

        public object GetProgramVariable(string symbolName)
        {
            if(program == null)
            {
                return null;
            }

            if(!program.SymbolTable.TryGetAddressFromSymbol(symbolName, out uint symbolAddress))
            {
                return null;
            }

            return program.Heap.GetHeapVariable(symbolAddress);
        }

        #endregion

        #endregion

        #region Logging Methods

        private void SetupLogging()
        {
            _debugLevel = GetType().GetHashCode();
            if(VRC.Core.Logger.DebugLevelIsDescribed(_debugLevel))
            {
                return;
            }

            VRC.Core.Logger.DescribeDebugLevel(_debugLevel, "UdonBehaviour");
            VRC.Core.Logger.AddDebugLevel(_debugLevel);
        }

        #endregion

        #region Manual Initialization Methods
        public void AssignProgramAndVariables(VRC.Udon.AbstractSerializedUdonProgramAsset compiledAsset, IUdonVariableTable variables)
        {
            serializedProgramAsset = compiledAsset;
            publicVariables = variables;
        }
        #endregion
    }
}
