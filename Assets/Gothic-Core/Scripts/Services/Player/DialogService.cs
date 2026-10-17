using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gothic.Core.Adapters.Properties;
using Gothic.Core.Const;
using Gothic.Core.Domain.Npc.Actions;
using Gothic.Core.Domain.Npc.Actions.AnimationActions;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Models.Container;
using Gothic.Core.Models.Dialog;
using Gothic.Core.Services;
using Gothic.Core.Services.Config;
using Gothic.Core.Services.Context;
using Gothic.Core.Services.Npc;
using Gothic.Core.Services.World;
using JetBrains.Annotations;
using Reflex.Attributes;
using UnityEngine;
using ZenKit;
using ZenKit.Daedalus;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Manager
{
    public class DialogService
    {
        [Inject] private readonly GameStateService _gameStateService;
        [Inject] private readonly ContextDialogService _contextDialogService;
        [Inject] private readonly NpcService _npcService;
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly SaveGameService _saveGameService;
        [Inject] private readonly ContextInteractionService _contextInteractionService;
        [Inject] private readonly UnityMonoService _unityMonoService;

        // Vanilla G1/G2 END entries: nr = 999, description = DIALOG_ENDE.
        private const int _exitInfoNr = 999;
        private const string _exitDescriptionSymbol = "DIALOG_ENDE";
        // Pushed back a little inside the bubble's edge, so the hero isn't held right at the stop distance.
        private const float _bubbleEdgeMargin = 0.2f;
        // The hero can't be held inside (falling, climbing, teleported, blocked by a wall) - the dialog ends after
        // being outside this long, or at once when this far beyond the bubble's edge.
        private const float _bubbleGiveUpSeconds = 1f;
        private const float _bubbleGiveUpOvershoot = 2f;

        private Coroutine _bubbleWatcher;

        /// <summary>
        /// TextToSpeech toggle. So that the hero isn't repeating what we said already.
        /// </summary>
        public bool SkipNextOutput;
        
        /// <summary>
        /// Check if NPC has at least one dialog which isn't told and Hero should know about.
        /// important
        ///     - TRUE - check if there's one important dialog untold.
        ///     - FALSE - check if there's one unimportant dialog untold. (unused in G1)
        /// </summary>
        public bool ExtCheckInfo(NpcInstance npc, bool important)
        {
            if (!important)
            {
                // Don't worry. I assume this even makes no sense at all, as also the "END" dialog would always trigger a return true.
                Logger.LogError("Npc_CheckInfo isn't implemented for important=0.", LogCat.Dialog);
            }

            _npcService.SetDialogs(npc.GetUserData());
            
            return TryGetImportant(npc.GetUserData(), out _);
        }

        /// <summary>
        /// initialDialogStarting - We only stop current AI routine if this is the first time the dialog box opens/NPC
        ///     talks important things. Otherwise, the ZS_*_End will get called every time we re-open a dialog in between.
        /// </summary>
        public void StartDialog(NpcContainer npcContainer, bool initialDialogStarting)
        {
            var isFreeMovement = _configService.Dev.EnableDialogFreeMovement;

            // DeveloperConfig.EnableDialogFreeMovement: a dialog closed in between (e.g. the NPC drew its weapon) isn't
            // reopened by the NPC's queued StartProcessInfos after its info function.
            if (isFreeMovement && !initialDialogStarting && !_gameStateService.Dialogs.IsInDialog)
                return;

            // ...and it doesn't start at all when the hero is too far away to read the dialog box - come closer.
            if (isFreeMovement && initialDialogStarting && !IsHeroInDialogRange(npcContainer))
            {
                Logger.Log($"[Dialog] {npcContainer.Instance.GetName(NpcNameSlot.Slot0)}: hero farther than " +
                           $"{Constants.DialogMaxDistance} m - dialog not started", LogCat.Dialog);
                // Not the cleanup of a dialog with someone else that is going on.
                if (!_gameStateService.Dialogs.IsInDialog || _gameStateService.Dialogs.CurrentDialogNpc == npcContainer)
                    StopDialog(npcContainer);
                return;
            }

            if (initialDialogStarting)
            {
                // Optimization: We expect, that AmbientInfos are assigned before Ai_ProcessInfos() is called.
                _npcService.SetDialogs(npcContainer);

                _contextDialogService.StartDialogInitially();
            }

            _gameStateService.Dialogs.IsInDialog = true;
            _gameStateService.Dialogs.CurrentDialogNpc = npcContainer;

            if (isFreeMovement)
            {
                if (initialDialogStarting)
                {
                    StopBubbleWatcher();
                    _bubbleWatcher = _unityMonoService.StartCoroutine(WatchDialogBubble(npcContainer));
                }
            }
            else
            {
                // WIP: locking movement
                _contextInteractionService.LockPlayerInPlace();
            }

            // We are already inside a sub-dialog
            if (_gameStateService.Dialogs.CurrentOptions.Any())
            {
                _contextDialogService.FillDialog(npcContainer.Instance, _gameStateService.Dialogs.CurrentOptions);
                _contextDialogService.ShowDialog(npcContainer.Go);
                _gameStateService.Dialogs.IsChoosing = true;
                _gameStateService.Dialogs.ExitInfo = null;
            }
            // There is at least one important entry, the NPC wants to talk to the hero about.
            else if (initialDialogStarting && TryGetImportant(npcContainer, out var infoInstance))
            {
                _gameStateService.Dialogs.CurrentInstance = infoInstance;
                CallMainInformation(npcContainer, infoInstance);
            }
            else
            {
                var selectableDialogs = new List<InfoInstance>();

                foreach (var dialog in npcContainer.Props.Dialogs)
                {
                    // Dialog is non-permanent and already been told
                    if (dialog.Permanent == 0 && GetInfoState(dialog.Index).Told)
                    {
                        continue;
                    }

                    // TODO - Should be outsourced to some VmManager.Call<int> function which sets and resets values.
                    var oldSelf = _gameStateService.GothicVm.GlobalSelf;
                    var oldOther = _gameStateService.GothicVm.GlobalOther;
                    _gameStateService.GothicVm.GlobalSelf = npcContainer.Instance;
                    _gameStateService.GothicVm.GlobalOther = _gameStateService.GothicVm.GlobalHero;
                    var conditionResult = _gameStateService.GothicVm.Call<int>(dialog.Condition);
                    _gameStateService.GothicVm.GlobalSelf = oldSelf;
                    _gameStateService.GothicVm.GlobalOther = oldOther;

                    // Dialog condition is false
                    if (conditionResult == 0)
                    {
                        continue;
                    }

                    // We can now add the dialog
                    selectableDialogs.Add(dialog);
                }

                selectableDialogs = selectableDialogs.OrderBy(d => d.Nr).ToList();

                if (!selectableDialogs.Any())
                {
                    StopDialog(npcContainer);
                    return;
                }

                _contextDialogService.FillDialog(npcContainer.Instance, selectableDialogs);
                _contextDialogService.ShowDialog(npcContainer.Go);
                _gameStateService.Dialogs.IsChoosing = true;
                _gameStateService.Dialogs.ExitInfo = FindExitInfo(selectableDialogs);
            }
        }

        /// <summary>
        /// If something is important, then call it automatically.
        /// </summary>
        private bool TryGetImportant(NpcContainer npcContainer, out InfoInstance item)
        {
            // Like the engine: lowest nr first.
            foreach (var dialog in npcContainer.Props.Dialogs.OrderBy(d => d.Nr))
            {
                // Dialog is not important.
                if (dialog.Important != 1)
                {
                    continue;
                }

                // Important dialog has already been told.
                if (dialog.Permanent != 1 && GetInfoState(dialog.Index).Told)
                {
                    continue;
                }

                // No dialog condition exists or dialog condition() is false.
                if (dialog.Condition == 0)
                {
                    continue;
                }
                
                // TODO - Should be outsourced to some VmManager.Call<int> function which sets and resets values.
                var oldSelf = _gameStateService.GothicVm.GlobalSelf;
                var oldOther = _gameStateService.GothicVm.GlobalOther;
                _gameStateService.GothicVm.GlobalSelf = npcContainer.Instance;
                _gameStateService.GothicVm.GlobalOther = _gameStateService.GothicVm.GlobalHero;
                var conditionResult = _gameStateService.GothicVm.Call<int>(dialog.Condition);
                _gameStateService.GothicVm.GlobalSelf = oldSelf;
                _gameStateService.GothicVm.GlobalOther = oldOther;

                if (conditionResult == 0)
                {
                    continue;
                }

                // Dialog is usable.
                item = dialog;
                return true;
            }

            item = null;
            return false;
        }

        public void ExtAiOutput(NpcInstance self, NpcInstance target, string outputName)
        {
            var isHero = self.Id == 0;
            // Always the NPC we're talking to!
            var speakerId = self.Id;

            var npcTalkingTo = isHero ? target : self;

            npcTalkingTo.GetUserData().Props.AnimationQueue.Enqueue(new Output(
                new AnimationAction(int0: speakerId, string0: outputName),
                npcTalkingTo.GetUserData()));
        }

        public void ExtAiOutputSvm(NpcInstance npc, NpcInstance target, string svmName)
        {
            var isHero = npc.Id == 0;

            // Hero SVM: enqueue on target's queue (hero's queue is never processed); hero container provides voice ID.
            if (isHero && target != null)
            {
                // NPC walked to player (e.g. important dialog) — hero's "hey" reactive lines make no sense.
                if (!_gameStateService.Dialogs.WasPlayerInitiated)
                    return;

                var heroContainer = GetNpcContainer(npc);
                target.GetUserData().Props.AnimationQueue.Enqueue(new OutputSvm(
                    new AnimationAction(int0: 0, string0: svmName),
                    heroContainer));
                return;
            }

            var npcContainer = GetNpcContainer(npc);
            npcContainer.Props.AnimationQueue.Enqueue(new OutputSvm(
                new AnimationAction(int0: npcContainer.Instance.Id, string0: svmName),
                npcContainer));
        }

        public bool ExtInfoManagerHasFinished()
        {
            return !_gameStateService.Dialogs.IsInDialog;
        }

        /// <summary>
        /// We update the Unity cached/created elements only.
        /// </summary>
        public void ExtInfoClearChoices(int info)
        {
            _gameStateService.Dialogs.CurrentOptions.Clear();
        }

        public void ExtInfoAddChoice(int info, string text, int function)
        {
            // Check if we need to change current instance as it wasn't cleared before.
            var oldInstance = _gameStateService.Dialogs.CurrentInstance;

            // First entry of current dialog to add
            if (oldInstance == null)
            {
                _gameStateService.Dialogs.CurrentInstance = _gameStateService.Dialogs.Instances.First(i => i.Index == info);
                _gameStateService.Dialogs.CurrentOptions.Clear();
            }
            else if (oldInstance.Index != info)
            {
                throw new Exception("Previous Dialog wasn't cleared. Gothic bug? " +
                                    $"Desc={oldInstance.Description}, Npc={oldInstance.Npc}, Info= {oldInstance.Information}");
            }

            // Add new entry
            _gameStateService.Dialogs.CurrentOptions.Add(new DialogOption
            {
                Text = text,
                Function = function
            });
        }

        public void ExtAiProcessInfos(NpcInstance npc)
        {
            var props = GetProperties(npc);

            props.AnimationQueue.Enqueue(new StartProcessInfos(new AnimationAction(bool0: true), npc.GetUserData()));
        }

        public void ExtAiStopProcessInfos(NpcInstance npc)
        {
            var props = GetProperties(npc);

            props.AnimationQueue.Enqueue(new StopProcessInfos(new AnimationAction(), npc.GetUserData()));
        }

        public void MainSelectionClicked(NpcContainer npcContainer, InfoInstance infoInstance)
        {
            CallMainInformation(npcContainer, infoInstance);
        }

        public void SubSelectionClicked(NpcContainer npcContainer, int dialogId)
        {
            CallInformation(npcContainer, dialogId);
        }

        /// <summary>
        /// Skip/Stop current Dialog's .wav entry now.
        /// </summary>
        public void SkipCurrentDialogLine(NpcProperties props)
        {

            if (props.CurrentAction.GetType() == typeof(Output))
            {
                props.CurrentAction.StopImmediately();
            }
        }

        public void StopDialog(NpcContainer npc)
        {
            _gameStateService.Dialogs.CurrentInstance = null;
            _gameStateService.Dialogs.CurrentOptions.Clear();
            _gameStateService.Dialogs.IsInDialog = false;
            _gameStateService.Dialogs.WasPlayerInitiated = false;
            _gameStateService.Dialogs.CurrentDialogNpc = null;
            _gameStateService.Dialogs.IsChoosing = false;
            _gameStateService.Dialogs.ExitInfo = null;
            StopBubbleWatcher();

            // WIP: unlocking movement
            _contextInteractionService.UnlockPlayer();

            _contextDialogService.HideDialog();
            _contextDialogService.EndDialog();

            // Hide subtitles from both dialog partners.
            _npcService.GetHeroContainer().PrefabProps.NpcSubtitles.HideSubtitles();
            npc.PrefabProps.NpcSubtitles.HideSubtitles();
        }

        private bool IsHeroInDialogRange(NpcContainer npcContainer)
        {
            return IsHeroInDialogRange(npcContainer, out _);
        }

        /// <summary>
        /// The bubble is a cylinder: horizontal distance like KeepPlayerWithin (a ramp mustn't count), plus a height cap
        /// (upper floor, cliff, ladder).
        /// </summary>
        private bool IsHeroInDialogRange(NpcContainer npcContainer, out float horizontalDistance)
        {
            horizontalDistance = 0f;
            var hero = _npcService.GetHeroContainer();
            if (hero?.Go == null || npcContainer.Go == null)
                return true;

            var offset = hero.Go.transform.position - npcContainer.Go.transform.position;
            var heightDifference = Mathf.Abs(offset.y);
            offset.y = 0f;
            horizontalDistance = offset.magnitude;

            return horizontalDistance <= Constants.DialogMaxDistance &&
                   heightDifference <= Constants.DialogMaxHeightDifference;
        }

        /// <summary>
        /// DeveloperConfig.EnableDialogFreeMovement: the hero walks freely inside the dialog bubble. Walking out of it
        /// chooses END if the shown choices offer it. Otherwise (somebody talks, sub-choices, no END) the bubble's
        /// edge holds the hero back. If that doesn't work (falling, climbing, teleported, blocked), the dialog ends.
        /// </summary>
        private IEnumerator WatchDialogBubble(NpcContainer npcContainer)
        {
            var dialogs = _gameStateService.Dialogs;
            var outsideSeconds = 0f;
            while (dialogs.IsInDialog && dialogs.CurrentDialogNpc == npcContainer)
            {
                if (IsHeroInDialogRange(npcContainer, out var horizontalDistance))
                {
                    outsideSeconds = 0f;
                }
                else if (dialogs.IsChoosing && dialogs.ExitInfo != null)
                {
                    Logger.Log($"[Dialog] {npcContainer.Instance.GetName(NpcNameSlot.Slot0)}: hero walked away - " +
                               "choosing END", LogCat.Dialog);
                    MainSelectionClicked(npcContainer, dialogs.ExitInfo);
                }
                else
                {
                    outsideSeconds += Time.deltaTime;
                    var overshoot = horizontalDistance - Constants.DialogMaxDistance;
                    if (outsideSeconds > _bubbleGiveUpSeconds || overshoot > _bubbleGiveUpOvershoot)
                    {
                        Logger.Log($"[Dialog] {npcContainer.Instance.GetName(NpcNameSlot.Slot0)}: hero can't be " +
                                   "held inside the dialog range - closing the dialog", LogCat.Dialog);
                        StopDialog(npcContainer);
                        yield break;
                    }

                    _contextInteractionService.KeepPlayerWithin(npcContainer.Go.transform.position,
                        Constants.DialogMaxDistance - _bubbleEdgeMargin);
                }
                yield return null;
            }
            _bubbleWatcher = null;
        }

        private void StopBubbleWatcher()
        {
            if (_bubbleWatcher == null)
                return;

            _unityMonoService.StopCoroutine(_bubbleWatcher);
            _bubbleWatcher = null;
        }

        [CanBeNull]
        private InfoInstance FindExitInfo(List<InfoInstance> infos)
        {
            var exitDescription = _gameStateService.GothicVm.GetSymbolByName(_exitDescriptionSymbol)?.GetString(0);
            return infos.FirstOrDefault(i => i.Nr == _exitInfoNr ||
                                             (exitDescription != null && i.Description == exitDescription));
        }

        /// <summary>
        /// A C_Info is clicked (main dialog entry)
        /// </summary>
        private void CallMainInformation(NpcContainer npcContainer, InfoInstance infoInstance)
        {
            // Set a new CurrentInstance for potential sub-dialog choices to fetch later.
            _gameStateService.Dialogs.CurrentInstance = npcContainer.Props.Dialogs
                .First(d => d.Information == infoInstance.Information);

            // Add entry to list of "told" information if it is main element only. (Sub-dialogs will never be reached again as main one (entry point) is already told)
            AddNpcInfoTold(infoInstance.Index);


            // Delegate remaining tasks to general implementation of CallInformation
            CallInformation(npcContainer, infoInstance.Information);
        }

        private void CallInformation(NpcContainer npcContainer, int information)
        {
            _contextDialogService.HideDialog();
            _gameStateService.Dialogs.IsChoosing = false;
            _gameStateService.Dialogs.ExitInfo = null;

            // We always need to set "self" before executing any Daedalus function.
            _gameStateService.GothicVm.GlobalSelf = npcContainer.Instance;
            _gameStateService.GothicVm.GlobalOther = _gameStateService.GothicVm.GlobalHero;

            // A failing dialog function (e.g. a mod's external called with an uninitialized NPC) must not skip the
            // queue handling below. Otherwise the choice box never comes back and the dialog soft-locks.
            try
            {
                _gameStateService.GothicVm.Call(information);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Dialog function threw an exception - dialog may be incomplete: {ex}",
                    LogCat.Dialog);
            }

            var animationQueue = npcContainer.Props.AnimationQueue;

            // If Daedalus tells us, that the dialog is stopped after this chat (AI_StopProcessInfos), then we're done.
            if (animationQueue.Any(i => i.GetType() == typeof(StopProcessInfos)))
            {
                return;
            }
            // Else we want to have the dialog menu back once all dialog lines are talked.
            else
            {
                animationQueue.Enqueue(new StartProcessInfos(
                    new AnimationAction(int0: information), npcContainer));
            }
        }

        public bool ExtNpcKnowsInfo(NpcInstance npc, int informationIndex)
        {
            var infoInstanceName = _gameStateService.GothicVm.GetSymbolByIndex(informationIndex)!.Name;
            var state = _saveGameService.Save.State;
            SaveInfoState foundInfo = default;
            
            for (var i = 0; i < state.InfoStateCount; i++)
            {
                if (state.GetInfoState(i).Name == infoInstanceName)
                {
                    foundInfo = state.GetInfoState(i);
                    break;
                }
            }
            
            // New entry - Add it now
            if (foundInfo.Name == null)
            {
                foundInfo = new()
                {
                    Name = infoInstanceName,
                    Told = false
                };
                _saveGameService.Save.State.AddInfoState(foundInfo);
            }

            return foundInfo.Told;
        }

        private SaveInfoState GetInfoState(int informationIndex)
        {
            var infoInstanceName = _gameStateService.GothicVm.GetSymbolByIndex(informationIndex)!.Name;
            var state = _saveGameService.Save.State;

            for (var i = 0; i < state.InfoStateCount; i++)
            {
                if (state.GetInfoState(i).Name == infoInstanceName)
                {
                    return state.GetInfoState(i);
                }
            }

            // We safely assume, that if an entry doesn't exist in list, it's simply not yet told.
            return default;
        }

        private void AddNpcInfoTold(int informationIndex)
        {
            var infoInstanceName = _gameStateService.GothicVm.GetSymbolByIndex(informationIndex)!.Name;
            var allStates = _saveGameService.Save.State;

            var infoState = new SaveInfoState
            {
                Name = infoInstanceName,
                Told = true
            };
            
            for (var i = 0; i < allStates.InfoStateCount; i++)
            {
                if (allStates.GetInfoState(i).Name == infoInstanceName)
                {
                    allStates.SetInfoState(i, infoState);
                    return;
                }
            }
            
            // If not yet existing, then create it.
            allStates.AddInfoState(infoState);
        }

        private GameObject GetGo(NpcInstance npc)
        {
            return npc.GetUserData().Go;
        }

        private NpcContainer GetNpcContainer(NpcInstance npc)
        {
            return npc.GetUserData();
        }

        private NpcProperties GetProperties(NpcInstance npc)
        {
            return npc.GetUserData().Props;
        }
    }
}
