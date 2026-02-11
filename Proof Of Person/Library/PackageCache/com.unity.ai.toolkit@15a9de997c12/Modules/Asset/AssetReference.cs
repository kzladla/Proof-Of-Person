using System;
using System.ComponentModel;

namespace Unity.AI.Toolkit.Asset
{
    [Serializable, EditorBrowsable(EditorBrowsableState.Never)]
    public record AssetReference
    {
        public string guid = string.Empty;
    }
}
