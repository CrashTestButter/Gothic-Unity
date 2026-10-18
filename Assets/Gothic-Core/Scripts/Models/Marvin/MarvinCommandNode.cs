using System;
using System.Collections.Generic;
using ZenKit;

namespace Gothic.Core.Models.Marvin
{
    /// <summary>
    /// Node of the Marvin command grammar. Either a literal word or a value slot. Built fluently:
    /// root.Add("goto").Add("waypoint").Add(waypoints).Runs(values => ...);
    /// </summary>
    public class MarvinCommandNode
    {
        /// <summary>Lowercase literal. Null for the root and value nodes.</summary>
        public readonly string Word;
        public readonly IMarvinValueProvider Value;
        public readonly List<MarvinCommandNode> Children = new();

        public string Description;

        /// <summary>
        /// Set: the command line is runnable when it ends at this node. Gets the collected values in order and
        /// returns whether it succeeded.
        /// </summary>
        public Func<IReadOnlyList<string>, bool> Execute;

        /// <summary>Game versions the node exists in. Null or empty: all.</summary>
        public GameVersion[] Versions;

        public bool IsWord => Word != null;

        /// <summary>Creates the root.</summary>
        public MarvinCommandNode()
        {
        }

        private MarvinCommandNode(string word, IMarvinValueProvider value)
        {
            Word = word;
            Value = value;
        }

        /// <summary>Returns the child for this word. Creates it, if it doesn't exist yet.</summary>
        public MarvinCommandNode Add(string word)
        {
            word = word.ToLowerInvariant();
            foreach (var child in Children)
            {
                if (child.Word == word)
                    return child;
            }

            var node = new MarvinCommandNode(word, null);
            Children.Add(node);
            return node;
        }

        /// <summary>Adds a value slot as child.</summary>
        public MarvinCommandNode Add(IMarvinValueProvider value)
        {
            var node = new MarvinCommandNode(null, value);
            Children.Add(node);
            return node;
        }

        public MarvinCommandNode Runs(Func<IReadOnlyList<string>, bool> execute)
        {
            Execute = execute;
            return this;
        }

        public MarvinCommandNode Describe(string description)
        {
            Description = description;
            return this;
        }

        public MarvinCommandNode Only(params GameVersion[] versions)
        {
            Versions = versions;
            return this;
        }

        public bool IsAvailable(GameVersion version)
        {
            return Versions == null || Versions.Length == 0 || Array.IndexOf(Versions, version) >= 0;
        }
    }
}
