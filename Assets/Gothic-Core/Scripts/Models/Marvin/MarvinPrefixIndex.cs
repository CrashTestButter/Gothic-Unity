using System;
using System.Collections.Generic;

namespace Gothic.Core.Models.Marvin
{
    /// <summary>
    /// Sorted, upper-case string list with prefix lookup via binary search. Prefixes have to be upper-case.
    /// </summary>
    public class MarvinPrefixIndex
    {
        private readonly string[] _entries;

        public int Count => _entries.Length;

        public MarvinPrefixIndex(IEnumerable<string> entries)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry))
                    unique.Add(entry.ToUpperInvariant());
            }

            _entries = new string[unique.Count];
            unique.CopyTo(_entries);
            Array.Sort(_entries, StringComparer.Ordinal);
        }

        public bool Contains(string entry)
        {
            return Array.BinarySearch(_entries, entry, StringComparer.Ordinal) >= 0;
        }

        public bool HasPrefix(string prefix)
        {
            return LowerBound(prefix) < UpperBound(prefix);
        }

        public int CountPrefix(string prefix)
        {
            return UpperBound(prefix) - LowerBound(prefix);
        }

        /// <summary>
        /// Appends a page of the entries starting with prefix.
        /// </summary>
        /// <returns>Total amount of matches (independent of offset/count).</returns>
        public int GetMatches(string prefix, int offset, int count, List<string> into)
        {
            var lower = LowerBound(prefix);
            var total = UpperBound(prefix) - lower;
            var start = lower + Math.Max(offset, 0);
            var end = Math.Min(start + Math.Max(count, 0), lower + total);

            for (var i = start; i < end; i++)
                into.Add(_entries[i]);

            return total;
        }

        /// <summary>
        /// Adds every character that directly follows prefix in at least one entry. Jumps from char group to char
        /// group, so the cost depends on the distinct chars and not on the amount of entries.
        /// </summary>
        public void CollectNextChars(string prefix, HashSet<char> into)
        {
            var index = LowerBound(prefix);
            var end = UpperBound(prefix);

            while (index < end)
            {
                var entry = _entries[index];
                if (entry.Length <= prefix.Length)
                {
                    index++;
                    continue;
                }

                var next = entry[prefix.Length];
                into.Add(next);
                index = UpperBound(prefix + next);
            }
        }

        private int LowerBound(string prefix)
        {
            var lo = 0;
            var hi = _entries.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (string.CompareOrdinal(_entries[mid], 0, prefix, 0, prefix.Length) < 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        private int UpperBound(string prefix)
        {
            var lo = 0;
            var hi = _entries.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (string.CompareOrdinal(_entries[mid], 0, prefix, 0, prefix.Length) <= 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }
    }
}
