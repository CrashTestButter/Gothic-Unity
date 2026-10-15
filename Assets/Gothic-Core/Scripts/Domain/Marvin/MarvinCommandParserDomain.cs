using System;
using System.Collections.Generic;
using Gothic.Core.Models.Marvin;
using ZenKit;

namespace Gothic.Core.Domain.Marvin
{
    /// <summary>
    /// Parses a Marvin command line (one string, tokens separated by single spaces, case-insensitive) against the
    /// command grammar. The last token is the partial one the user is currently typing.
    /// </summary>
    public class MarvinCommandParserDomain
    {
        private readonly MarvinCommandNode _root;
        private readonly GameVersion _version;

        public MarvinCommandParserDomain(MarvinCommandNode root, GameVersion version)
        {
            _root = root;
            _version = version;
        }

        public MarvinParseResult Parse(string line)
        {
            line ??= string.Empty;
            var result = new MarvinParseResult { Current = _root };

            var lastSpace = line.LastIndexOf(' ');
            result.Partial = line.Substring(lastSpace + 1);
            if (lastSpace < 0)
                return result;

            var complete = line.Substring(0, lastSpace);
            var start = 0;
            while (start <= complete.Length)
            {
                var end = complete.IndexOf(' ', start);
                if (end < 0)
                    end = complete.Length;

                if (!Consume(result, complete.Substring(start, end - start)))
                {
                    result.IsValid = false;
                    return result;
                }

                start = end + 1;
            }

            return result;
        }

        /// <summary>
        /// Completions of the partial token: words of the children and candidates of their closed-set values.
        /// Free-input values contribute nothing, see GetPlaceholder().
        /// </summary>
        public List<string> GetOptions(string line, int offset, int count, out int total)
        {
            var options = new List<string>();
            total = 0;

            var result = Parse(line);
            if (!result.IsValid)
                return options;

            var upperPartial = result.Partial.ToUpperInvariant();
            var skip = Math.Max(offset, 0);
            var remaining = Math.Max(count, 0);

            foreach (var child in result.Current.Children)
            {
                if (!child.IsAvailable(_version))
                    continue;

                if (child.IsWord)
                {
                    if (!child.Word.StartsWith(upperPartial, StringComparison.OrdinalIgnoreCase))
                        continue;

                    total++;
                    if (skip > 0)
                        skip--;
                    else if (remaining > 0)
                    {
                        options.Add(child.Word);
                        remaining--;
                    }
                }
                else if (child.Value.IsClosedSet)
                {
                    var before = options.Count;
                    var matches = child.Value.GetCandidates().GetMatches(upperPartial, skip, remaining, options);
                    total += matches;
                    skip = Math.Max(0, skip - matches);
                    remaining -= options.Count - before;
                }
            }

            return options;
        }

        /// <summary>
        /// Placeholder of the value the line expects next, e.g. "&lt;HOUR&gt;". Null if no value is expected.
        /// </summary>
        public string GetPlaceholder(string line)
        {
            var result = Parse(line);
            if (!result.IsValid)
                return null;

            foreach (var child in result.Current.Children)
            {
                if (child.IsAvailable(_version) && !child.IsWord)
                    return child.Value.Placeholder;
            }

            return null;
        }

        /// <summary>
        /// True, if appending the char keeps the line on a valid path. Used to grey out keyboard keys.
        /// </summary>
        public bool IsAllowedNextChar(string line, char c)
        {
            var result = Parse(line);
            if (!result.IsValid)
                return false;

            // A space ends the partial token: it has to be complete, and something has to follow.
            if (c == ' ')
            {
                var matched = FindChild(result.Current, result.Partial);
                return matched != null && HasAvailableChildren(matched);
            }

            var extended = result.Partial + c;
            var upperExtended = extended.ToUpperInvariant();
            foreach (var child in result.Current.Children)
            {
                if (!child.IsAvailable(_version))
                    continue;

                if (child.IsWord)
                {
                    if (child.Word.StartsWith(extended, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (child.Value.IsValidPrefix(upperExtended))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Replaces the partial token with the option. A space follows, if more input is possible. Then auto-completes
        /// (e.g. "goto" -> "goto WAYPOINT ").
        /// </summary>
        public string ApplyOption(string line, string option)
        {
            line ??= string.Empty;
            var partial = line.Substring(line.LastIndexOf(' ') + 1);
            var applied = line.Substring(0, line.Length - partial.Length) + option;

            var result = Parse(applied + " ");
            return AutoComplete(result.IsValid && HasAvailableChildren(result.Current) ? applied + " " : applied);
        }

        /// <summary>
        /// Extends the partial token to the longest common prefix of its continuations (words and closed-set
        /// candidates). Is the token then complete, nothing longer possible and more input expected, a space follows
        /// and the next token is extended the same way, as long as there is nothing to choose
        /// (e.g. "G" -> "GOTO WAYPOINT "). Free values (numbers) and empty value tokens are never extended.
        /// Appended chars are upper-case.
        /// </summary>
        public string AutoComplete(string line)
        {
            line ??= string.Empty;
            var nextChars = new HashSet<char>();

            while (true)
            {
                var result = Parse(line);
                if (!result.IsValid || !IsAutoCompletable(result))
                    return line;

                // The prefix index expects upper-case. Only appended chars go into the line, the typed case stays.
                var partial = result.Partial.ToUpperInvariant();
                bool isComplete;
                while (true)
                {
                    nextChars.Clear();
                    isComplete = CollectNextChars(result.Current, partial, nextChars);
                    if (isComplete || nextChars.Count != 1)
                        break;

                    foreach (var next in nextChars)
                    {
                        partial += next;
                        line += next;
                    }
                }

                // OC1 stays without space, if OC10 exists.
                if (!isComplete || nextChars.Count > 0 || !HasAvailableChildren(FindChild(result.Current, partial)))
                    return line;

                line += " ";
            }
        }

        /// <summary>
        /// After deleting chars: removes further chars back to the last point the user had a choice (more than one
        /// continuation, a free value or nothing left to complete). Otherwise AutoComplete() would just extend the line
        /// again, e.g. "CHEAT GUILD" -> "CHEAT ", "GOTO WAYPOINT" -> "".
        /// </summary>
        public string RevertToLastBranch(string line)
        {
            line ??= string.Empty;
            while (line.Length > 0 && CanAutoComplete(line))
                line = line.Substring(0, line.Length - 1);

            return line;
        }

        /// <summary>
        /// Removes the partial token. If there is none, the last complete token ("back"). Tokens without a choice
        /// (e.g. "waypoint" after "goto") go as well, see RevertToLastBranch().
        /// </summary>
        public string RemoveLastToken(string line)
        {
            var reverted = RemoveOneToken(line);
            while (reverted.Length > 0 && CanAutoComplete(reverted))
                reverted = RemoveOneToken(reverted);

            return reverted;
        }

        private string RemoveOneToken(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            var lastSpace = line.LastIndexOf(' ');
            if (lastSpace < line.Length - 1)
                return line.Substring(0, lastSpace + 1);

            var trimmed = line.TrimEnd(' ');
            return trimmed.Substring(0, trimmed.LastIndexOf(' ') + 1);
        }

        /// <summary>
        /// AutoComplete() only appends, so a longer line means there was nothing to choose.
        /// </summary>
        private bool CanAutoComplete(string line)
        {
            return AutoComplete(line).Length != line.Length;
        }

        /// <summary>
        /// Free values accept anything. An empty value token (e.g. right after "goto waypoint ") isn't extended
        /// either, even if all candidates share a prefix - the user picks it.
        /// </summary>
        private bool IsAutoCompletable(MarvinParseResult result)
        {
            foreach (var child in result.Current.Children)
            {
                if (child.IsAvailable(_version) && !child.IsWord
                    && (!child.Value.IsClosedSet || result.Partial.Length == 0))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Adds the upper-case chars directly following the prefix in any word or closed-set candidate.
        /// </summary>
        /// <returns>The prefix itself is a complete word or candidate.</returns>
        private bool CollectNextChars(MarvinCommandNode node, string upperPrefix, HashSet<char> into)
        {
            var isComplete = false;
            foreach (var child in node.Children)
            {
                if (!child.IsAvailable(_version))
                    continue;

                if (child.IsWord)
                {
                    if (!child.Word.StartsWith(upperPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (child.Word.Length == upperPrefix.Length)
                        isComplete = true;
                    else
                        into.Add(char.ToUpperInvariant(child.Word[upperPrefix.Length]));
                }
                else if (child.Value.IsClosedSet)
                {
                    var candidates = child.Value.GetCandidates();
                    if (candidates.Contains(upperPrefix))
                        isComplete = true;
                    candidates.CollectNextChars(upperPrefix, into);
                }
            }

            return isComplete;
        }

        /// <summary>
        /// The line is runnable if it ends at a node with an Execute action. A trailing space is ignored.
        /// </summary>
        public bool TryResolve(string line, out MarvinParseResult result)
        {
            result = null;
            var trimmed = line?.TrimEnd(' ');
            if (string.IsNullOrEmpty(trimmed))
                return false;

            // The trailing space turns the last token into a complete one.
            result = Parse(trimmed + " ");
            return result.IsValid && result.Current.Execute != null;
        }

        public bool CanExecute(string line)
        {
            return TryResolve(line, out _);
        }

        private bool Consume(MarvinParseResult result, string token)
        {
            var child = FindChild(result.Current, token);
            if (child == null)
                return false;

            var upperToken = token.ToUpperInvariant();
            result.Tokens.Add(child.IsWord ? child.Word : upperToken);
            if (!child.IsWord)
                result.Values.Add(upperToken);

            result.Current = child;
            return true;
        }

        /// <summary>
        /// Words win over values.
        /// </summary>
        private MarvinCommandNode FindChild(MarvinCommandNode node, string token)
        {
            if (token.Length == 0)
                return null;

            foreach (var child in node.Children)
            {
                if (child.IsAvailable(_version) && child.IsWord
                    && string.Equals(child.Word, token, StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            var upperToken = token.ToUpperInvariant();
            foreach (var child in node.Children)
            {
                if (child.IsAvailable(_version) && !child.IsWord && child.Value.IsValid(upperToken))
                    return child;
            }

            return null;
        }

        private bool HasAvailableChildren(MarvinCommandNode node)
        {
            foreach (var child in node.Children)
            {
                if (child.IsAvailable(_version))
                    return true;
            }

            return false;
        }
    }
}
