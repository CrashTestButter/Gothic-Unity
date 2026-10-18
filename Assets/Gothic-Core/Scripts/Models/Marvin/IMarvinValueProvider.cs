namespace Gothic.Core.Models.Marvin
{
    /// <summary>
    /// A value slot inside a Marvin command, e.g. the waypoint of "goto waypoint &lt;WAYPOINT&gt;".
    /// All strings handed in are expected upper-case.
    /// </summary>
    public interface IMarvinValueProvider
    {
        /// <summary>Shown to the user while the value is expected, e.g. "&lt;HOUR&gt;".</summary>
        string Placeholder { get; }

        /// <summary>True: a finite candidate list exists. False: free input (e.g. numbers).</summary>
        bool IsClosedSet { get; }

        /// <summary>Sorted candidates of a closed set. Null for free input.</summary>
        MarvinPrefixIndex GetCandidates();

        /// <summary>The complete token is a valid value.</summary>
        bool IsValid(string token);

        /// <summary>The partial token can still grow into a valid value. Drives the allowed keyboard keys.</summary>
        bool IsValidPrefix(string partial);
    }
}
