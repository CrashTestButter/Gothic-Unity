using System.Collections.Generic;

namespace Gothic.Core.Models.Marvin
{
    /// <summary>
    /// State of a command line: the complete tokens are resolved, the last token (may be empty) is the partial one.
    /// </summary>
    public class MarvinParseResult
    {
        /// <summary>Node after all complete tokens. The root, if there are none.</summary>
        public MarvinCommandNode Current;

        /// <summary>Normalized complete tokens: words lowercase, values upper-case.</summary>
        public readonly List<string> Tokens = new();

        /// <summary>Values of the complete tokens in order, upper-case.</summary>
        public readonly List<string> Values = new();

        /// <summary>Text after the last space, as typed.</summary>
        public string Partial = string.Empty;

        /// <summary>All complete tokens matched.</summary>
        public bool IsValid = true;
    }
}
