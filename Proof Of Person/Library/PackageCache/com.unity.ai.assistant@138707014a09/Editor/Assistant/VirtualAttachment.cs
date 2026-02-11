using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Context;

namespace Unity.AI.Assistant.Editor
{
    internal class VirtualAttachment
    {
        public VirtualAttachment(string payload, string type, string displayName, object metadata)
        {
            Payload = payload;
            Type = type;
            DisplayName = displayName;
            Metadata =  metadata;
        }

        public readonly string Payload;
        public string Type;
        public string DisplayName;
        public object Metadata;

        public AssistantContextEntry ToContextEntry()
        {
            return new AssistantContextEntry
            {
                Value = Payload,
                ValueType = Type,
                DisplayValue = DisplayName,
                EntryType = AssistantContextType.Virtual,
                Metadata = Metadata
            };
        }

        public VirtualContextSelection ToContextSelection()
        {
            return new VirtualContextSelection(Payload,
                DisplayName,
                string.Empty,
                Type,
                metadata: Metadata);
        }

        public static VirtualAttachment FromContextEntry(AssistantContextEntry entry)
        {
            return new VirtualAttachment(entry.Value, entry.ValueType, entry.DisplayValue, entry.Metadata);
        }
    }
}
