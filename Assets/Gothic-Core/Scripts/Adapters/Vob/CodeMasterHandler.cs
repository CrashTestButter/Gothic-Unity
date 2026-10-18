using System;
using System.Collections.Generic;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Services.Vobs;
using Reflex.Attributes;
using UnityEngine;
using ZenKit.Vobs;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Adapters.Vob
{
    /// <summary>
    /// Handles zCCodeMaster: a combination/sequence puzzle. Once all its slaves (other VOBs, e.g. switches) triggered
    /// it - in the right order if Ordered - it fires its Target. A wrong order can fire FailureTarget.
    /// </summary>
    public class CodeMasterHandler : MonoBehaviour
    {
        [Inject] private readonly VobService _vobService;

        private ICodeMaster _codeMaster;
        // Slaves is a native marshal which allocates a new list on every access. Read once.
        private List<string> _slaves;
        private HashSet<string> _distinctSlaves;
        private readonly HashSet<string> _triggeredSlaves = new(StringComparer.OrdinalIgnoreCase);
        private int _nextExpectedIndex;

        private void Awake() => this.Inject();

        public void Init(ICodeMaster codeMaster)
        {
            _codeMaster = codeMaster;
            _slaves = codeMaster.Slaves;
            _distinctSlaves = new HashSet<string>(_slaves, StringComparer.OrdinalIgnoreCase);
        }

        public void ReceiveTrigger(string senderName)
        {
            if (!_distinctSlaves.Contains(senderName))
            {
                Logger.LogWarning($"[CodeMaster] '{_codeMaster.Name}': trigger from unknown slave '{senderName}'",
                    LogCat.Vob);
                return;
            }

            if (_codeMaster.Ordered)
            {
                // Index based (not name lookup), as a slave name can appear multiple times in the sequence.
                if (_nextExpectedIndex >= _slaves.Count ||
                    !string.Equals(_slaves[_nextExpectedIndex], senderName, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log($"[CodeMaster] '{_codeMaster.Name}': wrong order - got '{senderName}', " +
                               $"expected [{_nextExpectedIndex}]", LogCat.Vob);
                    if (_codeMaster.FirstFalseIsFailure)
                        FireFailure();
                    Reset();
                    return;
                }
                _nextExpectedIndex++;
            }
            else
            {
                _triggeredSlaves.Add(senderName);
            }

            var triggeredCount = _codeMaster.Ordered ? _nextExpectedIndex : _triggeredSlaves.Count;
            var requiredCount = _codeMaster.Ordered ? _slaves.Count : _distinctSlaves.Count;
            Logger.Log($"[CodeMaster] '{_codeMaster.Name}': slave '{senderName}' OK " +
                       $"({triggeredCount}/{requiredCount})", LogCat.Vob);

            if (triggeredCount >= requiredCount)
            {
                Logger.Log($"[CodeMaster] '{_codeMaster.Name}': all slaves triggered -> '{_codeMaster.Target}'",
                    LogCat.Vob);
                Reset();
                _vobService.DispatchTrigger(_codeMaster.Target, _codeMaster.Name);
            }
        }

        public void ReceiveUntrigger(string senderName)
        {
            if (!_codeMaster.UntriggeredCancels)
                return;

            Logger.Log($"[CodeMaster] '{_codeMaster.Name}': untrigger from '{senderName}' - resetting sequence",
                LogCat.Vob);
            Reset();
        }

        private void FireFailure()
        {
            var failureTarget = _codeMaster.FailureTarget;
            if (string.IsNullOrEmpty(failureTarget))
                return;

            Logger.Log($"[CodeMaster] '{_codeMaster.Name}': firing FailureTarget '{failureTarget}'", LogCat.Vob);
            _vobService.DispatchTrigger(failureTarget, _codeMaster.Name);
        }

        private void Reset()
        {
            _triggeredSlaves.Clear();
            _nextExpectedIndex = 0;
        }
    }
}
