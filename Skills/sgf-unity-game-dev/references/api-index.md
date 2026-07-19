# SGFCore API 索引

本索引记录 2026-07 仓库快照中的公开入口，用于快速定位；使用前必须查看当前源码的重载、参数默认值和返回语义。

## 总入口

| 类型 | 主要公开 API | 源码 |
| --- | --- | --- |
| `FrameworkEntry` | `InitFrameworkModulesAsync`、`ShutdownFrameworkAsync`、`RegisterModule`、`GetModule<T>`、`TryGetModule<T>`、`GetModuleDependencies<T>`、`GetModuleGraphSnapshot` | `Base/FrameworkEntry.cs` |
| `FrameworkConfig` | `ConfigureCryptoOnStartup`、`CryptoKey`、`CryptoIV` | `Base/FrameworkConfig.cs` |
| `GameApp` | `Event`、`FileSystem`、`Save`、`Crypto`、`Res`、`Timer`、`Time`、`Pool`、`UI`、`RedPoint`、`Guide`、`Config`、`Audio`、`Fsm`、`BT`、`Loc`、`Http`、`Scene`、`Procedure`、`Broadcast<T>` | `Base/GameApp.cs` |

## 数据与基础服务

| 门面/类型 | 主要公开 API | 源码 |
| --- | --- | --- |
| `Log` | `Info`、`Warning`、`Error`、`Fatal`、`Module` | `Modules/Debugger/Log.cs` |
| `GameApp.FileSystem` | `GetPersistentDataPath`、`Exists`、`ReadText`、`WriteText`、`WriteTextAtomic`、`ReadBytes`、`WriteBytes`、`DeleteFile` | `Modules/FileIO/FileSystemModule.cs` |
| `GameApp.Event` | `AddListener<T>`、`RemoveListener<T>`、`RemoveListener(Type, Delegate)`、`Broadcast<T>`、`GetListenerCount<T>` | `Modules/Event/EventModule.cs` |
| `GameApp.Crypto` | `IsInitialized`、`SetCryptoKey`、`EncryptString`、`DecryptString`、`EncryptAuthenticatedString`、`DecryptAuthenticatedString`、`EncryptBytes`、`DecryptBytes` | `Modules/Crypto/CryptoModule.cs` |
| `GameApp.Save` | `SetCurrentSlot`、`GetSlots`、`RegisterMigration`、`TrackAutoSave`、`StopAutoSave`、`SaveData`、`TrySaveData`、`LoadData`、`SaveModuleData`、`LoadModuleData`、`HasSave`、`DeleteSave`、`DeleteSlot`、路径查询 | `Modules/Save/SaveModule.cs` |
| `SaveDataBase` | `MarkDirty`、`ClearDirty`、`CheckIsDirty`、`OnBindContext`、`OnBeforeSave`、`OnAfterLoad` | `Modules/Save/SaveBase.cs` |
| `GameApp.Pool` | `AllocateClass<T>`、`ReleaseClass`、`SpawnGameObject`、`RecycleGameObject`、`SetPoolConfig`、`PrewarmGameObject`、`GetPoolCount`、`ClearPool`、`ClearAllGameObjectPools` | `Modules/Pool/PoolModule.cs` |
| `GameApp.Timer` | `AddTimer`、`CancelTimer`、`PauseTimer`、`ResumeTimer`、`GetRemainingTime`、`CancelAllTimers`、`GetActiveTimerCount` | `Modules/Timer/TimerModule.cs` |
| `GameApp.Time` | `SetDailyResetHour`、`SetServerTimeZone`、`SyncServerTime`、`SyncServerTimestampSeconds`、`ClearServerTime`、`GetOfflineDuration`、`IsSameGameDay`、`GetNextDailyResetTime`、`GetSecondsToNextDailyReset` | `Modules/Time/TimeModule.cs` |
| `GameApp.Http` | `SetAuthToken`、`SetDefaultHeader`、`ClearDefaultHeaders`、`GetAsync`、`GetResultAsync`、`PostJsonAsync`、`PostJsonResultAsync`、`GetTextAsync` | `Modules/Network/WebRequest/HttpModule.cs` |

## 内容与表现

| 门面/类型 | 主要公开 API | 源码 |
| --- | --- | --- |
| `GameApp.Res` | `CreateScope`、`EnsureInitializedAsync`、`LoadAssetAsync<T>`、`InstantiateAsync`、`ReleaseAsset`、`TryReleaseAsset`、`ReleaseInstance`、`TryReleaseInstance`、`GetUsageSnapshot` | `Modules/Res/ResourceModule.cs` |
| `ResourceScope` | `TrackAsset`、`TrackInstance`、`LoadAssetAsync<T>`、`InstantiateAsync`、`Dispose` | `Modules/Res/ResourceScope.cs` |
| `GameApp.Config` | `RegisterConfig`、`TryRegisterConfig`、`IsRegistered`、`IsLoaded`、`LoadConfigAsync`、`TryLoadConfigAsync`、`LoadConfigsBatchAsync`、`TryLoadConfigsBatchAsync`、`LoadConfigsAsync`、`TryLoadConfigsAsync` | `Modules/Config/ConfigModule.cs` |
| `ConfigManagerBase` | `List`、`Dict`、`Count`、`Clear`、`TryGet`、`Get`、`Contains` | `Modules/Config/ConfigManagerBase.cs` |
| `GameApp.Loc` | `SetLanguageTablePrefix`、`SetLanguageSuffix`、`ChangeLanguageAsync`、`TryChangeLanguageAsync`、`LoadPreferredLanguageAsync`、`GetPreferredLanguage`、`GetString`、`TryGetString`、`Format`、`GetLanguageSuffix`、`SetCultureName` | `Modules/Localization/LocalizationModule.cs` |
| `GameApp.Scene` | `LoadSceneAsync`、`TryLoadSceneAsync`、`SwitchSceneAsync`、`TrySwitchSceneAsync`、`ActivateSceneAsync`、`UnloadSceneAsync`、`TryUnloadSceneAsync`、`GetUsageSnapshot` | `Modules/Scene/SceneModule.cs` |
| `GameApp.Audio` | `PlayBGMAsync`、`StopBGM`、`StopBGMAsync`、`PlaySFX`、`PlaySFXEx`、`StopAudio`、`PauseAudio`、`ResumeAudio`、`StopGroup`、`StopAllSFX`、`StopAll`、`PauseGroup`、`ResumeGroup`、`PauseAll`、`ResumeAll`、音量/静音/Mixer API | `Modules/Audio/AudioModule.cs` |
| `GameApp.UI` | `RegisterUI`、`OpenUIAsync`、`CloseUI`、`CloseUIAsync`、泛型打开/关闭 | `Modules/UI/UIModule.cs` |
| `UIFormBase` | `OnInit`、`OnOpen`、`OnClose`、`OnDestroyUI`、`PlayOpenAnimationAsync`、`PlayCloseAnimationAsync`；protected `Subscribe`、`LoadAssetAsync`、`InstantiateAsync` | `Modules/UI/UIFormBase.cs` |

## 玩法系统

| 门面/类型 | 主要公开 API | 源码 |
| --- | --- | --- |
| `GameApp.Fsm` | `CreateFsm<T>`、`DestroyFsm` | `Modules/FSM/FSMModule.cs` |
| `IFsm<T>` | `Start`、`ChangeState`、黑板 `SetData/GetData/TryGetData/RemoveData` | `Modules/FSM/IFSM.cs` |
| `FsmState<T>` | `OnEnter`、`OnUpdate`、`OnLeave`、`OnDestroy`、protected `ChangeState` | `Modules/FSM/FSMState.cs` |
| `GameApp.Procedure` | `Start`、`RegisterProcedures`、`ChangeProcedure`、`HasProcedure`、`GetProcedure`、`Stop`、黑板 API | `Modules/Procedure/ProcedureModule.cs` |
| `ProcedureBase` | `OnEnter`、`OnEnterAsync`、`OnUpdate`、`OnLeave`、`OnDestroy`、protected 切换和黑板 API | `Modules/Procedure/ProcedureBase.cs` |
| `GameApp.RedPoint` | `GetCount`、`BeginBatch`、`GetSelfCount`、`IsActive`、`GetSnapshot`、`SetCount`、`ClearCount`、条件/评估/owner/listener/child API | `Modules/RedPoint/RedPointModule.cs` |
| `GameApp.Guide` | `StartGuide`、`StopGuide`、注册 Trigger/Condition/Action、`Fire`、`CompleteCurrentStep`、`SkipCurrentStep`、进度查询/重置、`RefreshViewTarget`、`ValidateDefinitions` | `Modules/Guide/GuideModule.cs` |
| `GameApp.BT` | `AttachTreeAsync`、`DetachTree`、`PauseAllAI`、`ResumeAllAI` | `Modules/BehaviorTree/BTModule.cs` |

## 常用事件与结果

| 领域 | 类型 |
| --- | --- |
| Config | `ConfigLoadResult`、`ConfigBatchLoadResult`、`ConfigLoadedEvent` |
| Resource | `ResourceUsageSnapshot` |
| Scene | `SceneLoadOptions`、`SceneLoadResult`、`SceneUsageSnapshot`、`SceneLoadStartedEvent`、`SceneLoadCompletedEvent`、`SceneUnloadedEvent` |
| Save | `SaveOperationResult`、Loaded/Saved/Dirty/Migrated/Recovered 事件 |
| Localization | `LanguageChangedEvent` |
| Audio | `AudioHandle`、`AudioVolumeChangedEvent`、`AudioPlaybackEvent` |
| Http | `HttpRequestOptions`、`HttpResult<T>`、`HttpErrorType`、`HttpRequestCompletedEvent` |
| Procedure | `ProcedureChangedEvent`、`ProcedureStoppedEvent` |
| RedPoint | `RedPointSnapshot`、`RedPointChangedEvent` |
| Guide | Started/StepStarted/StepCompleted/Completed/ProgressChanged 事件 |
| Time | `ServerTimeSyncedEvent`、`DailyResetPassedEvent` |

## UI 与 Utility 类型

UI 组件位于 `GameFramework.Core.UI`：`UIRoot`、`UILayer`、`UIFormBase<TData>`、`UITweenElement`、`UITweenProfile`、`LocalizedText`、`LocalizedTextTmp`、`LocalizedImage`、`RedPointBadge`、`RedPointConditionBadge`、`UIVirtualList`、`UINetImage`、`UIToast`、`UILoadingOverlay`、`UIConfirmDialog`、`UIBindTrs`、`UIBindPos` 等。

工具位于 `GameFramework.Core.Utility`：`TransformExtension`、`RectTransformExtension`、`CollectionExtension`、`NumberExtension`、`StringExtension`、`RandomUtility`、`ColorExtension`、`TimeUtility`。调用前读取对应文件确认边界和默认参数。
