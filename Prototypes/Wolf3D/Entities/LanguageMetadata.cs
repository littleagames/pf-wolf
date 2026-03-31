using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Entities;

internal class LanguageMetadata
{
    // dictionary of string keys
    // strings might have "placeholders" which need to be replaced with actual values at runtime
    public Dictionary<string, string> TextStrings { get; set; } = new Dictionary<string, string>();
}
