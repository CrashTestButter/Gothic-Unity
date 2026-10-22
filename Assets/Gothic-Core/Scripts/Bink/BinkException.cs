// Bink video decoder - C# port of OpenGothic common/bink (https://github.com/Try/OpenGothic),
// codec based on FFmpeg libavcodec/bink.c - licensed LGPL 2.1 or later. See README.md in this folder.
// Copyright (c) 2019 Try (OpenGothic, MIT). FFmpeg code: LGPL-2.1-or-later - copyright holders listed in README.md.
// Modified: ported from C++ to C# by the Gothic Unity contributors, 2026 (see git history for changes).
using System;

namespace Gothic.Core.Bink
{
    /// <summary>
    /// Thrown by the Bink decoder when a file or packet is corrupt or uses an unsupported feature.
    /// </summary>
    public class BinkException : Exception
    {
        public BinkException(string message) : base(message)
        {
        }
    }
}
