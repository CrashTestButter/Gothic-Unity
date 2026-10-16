using System;

namespace Gothic.Core.Models.Marvin
{
    /// <summary>
    /// Value out of a finite list. The list is created on first use (the Daedalus VM isn't loaded earlier) and
    /// again after Invalidate().
    /// </summary>
    public class MarvinClosedSetValue : IMarvinValueProvider
    {
        private readonly Func<MarvinPrefixIndex> _factory;
        private MarvinPrefixIndex _index;

        public string Placeholder { get; }
        public bool IsClosedSet => true;

        public MarvinClosedSetValue(string placeholder, Func<MarvinPrefixIndex> factory)
        {
            Placeholder = placeholder;
            _factory = factory;
        }

        public MarvinPrefixIndex GetCandidates()
        {
            return _index ??= _factory();
        }

        public bool IsValid(string token)
        {
            return GetCandidates().Contains(token);
        }

        public bool IsValidPrefix(string partial)
        {
            return GetCandidates().HasPrefix(partial);
        }

        public void Invalidate()
        {
            _index = null;
        }
    }
}
