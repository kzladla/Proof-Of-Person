using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementBlockThought : ChatElementBlockMarkdown<ThoughtBlockModel>
    {
        VisualElement m_Container;

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            m_Container = view.Q("textContent");
        }

        protected override void OnBlockModelChanged()
        {
            BuildMarkdownChunks(BlockModel.Content, true);

            RefreshText(m_Container);
        }
    }
}
