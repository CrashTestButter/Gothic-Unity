using ZenKit.Vobs;

namespace Gothic.Core.Adapters.Properties.Vobs
{
    public class InteractiveProperties : MovableProperties
    {
        /// <summary>
        /// Current state of the mob (Gothic's oCMobInter state), e.g. a wheel is turned, a door is open. Starts with
        /// 0. Gothic's save format has no field for it (the VOB's State is stateNum, the number of states):
        /// MobStatesSaveSection saves it.
        /// </summary>
        public int State = 0;

        
        public InteractiveProperties(IVirtualObject vob) : base(vob)
        { }
    }
}
