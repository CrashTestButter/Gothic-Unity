using Gothic.Core.Models.Container;
using Gothic.Core.Extensions;
using Gothic.Core.Services.Caches;

namespace Gothic.Core.Domain.Npc.Actions.AnimationActions
{
    public class OutputSvm : Output
    {
        private string _preparedSvmFileName;

        // Overwriting this lookup as it let's us reuse the inherited Output class.
        protected override string OutputName => _preparedSvmFileName;

        public OutputSvm(AnimationAction action, NpcContainer npcContainer) : base(action, npcContainer)
        {
        }

        public override void Start()
        {
            var svm = VmCacheService.TryGetSvmData(NpcInstance.Voice);
            _preparedSvmFileName = svm?.GetAudioName(Action.String0);

            // ZenKit's SvmInstance only knows G1's C_SVM members. Vanilla scripts name every entry SVM_<voice>_<key>,
            // like the OU.BIN subtitle.
            if (_preparedSvmFileName == null && svm != null && !string.IsNullOrEmpty(Action.String0))
                _preparedSvmFileName = $"SVM_{NpcInstance.Voice}_{Action.String0.TrimStart('$')}";

            if (_preparedSvmFileName == null)
            {
                IsFinishedFlag = true;
                return;
            }

            base.Start();

            // Hero SVM (e.g. "Hej ty!") is fire-and-forget — NPC queue continues immediately
            // so the NPC can turn and greet in parallel. Regular Output dialog lines stay blocking.
            if (Action.Int0 == 0)
                StartHeroFireAndForget();
        }
    }
}
