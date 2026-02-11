using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Editor.Utils
{
    static class PromptUtils
    {
        public static EditorContextReport GetContextModel(int maxLength, AssistantPrompt prompt)
        {
            // Initialize all context, if any context has changed, add it all
            var contextBuilder = new ContextBuilder();
            GetAttachedContextString(prompt, ref contextBuilder);

            var finalContext = contextBuilder.BuildContext(maxLength);

            InternalLog.Log($"Final Context ({contextBuilder.PredictedLength} character):\n\n {finalContext.ToJson()}");

            return finalContext;
        }

        /// <summary>
        /// Get the context string from the selected objects and selected console logs.
        /// </summary>
        /// <param name="prompt">The prompt to get the context string for</param>
        /// <param name="contextBuilder"> The context builder reference for temporary context string creation. </param>
        /// <param name="stopAtLimit">Stop processing context once the limit has reached</param>
        /// <returns></returns>
        public static void GetAttachedContextString(AssistantPrompt prompt, ref ContextBuilder contextBuilder, bool stopAtLimit = false)
        {
            if (prompt == null)
            {
                return;
            }

            // Grab any selected objects
            var attachment = AttachmentUtils.GetValidAttachment(prompt.ObjectAttachments);
            if (attachment.Count > 0)
            {
                foreach (var currentObject in attachment)
                {
                    var objectContext = new UnityObjectContextSelection();
                    objectContext.SetTarget(currentObject);

                    contextBuilder.InjectContext(objectContext);

                    if (stopAtLimit && contextBuilder.PredictedLength > AssistantMessageSizeConstraints.ContextLimit)
                    {
                        break;
                    }
                }
            }

            if (prompt.VirtualAttachments.Count > 0)
            {
                foreach (var virtualAttachment in prompt.VirtualAttachments)
                {
                    contextBuilder.InjectContext(virtualAttachment.ToContextSelection());

                    if (stopAtLimit && contextBuilder.PredictedLength > AssistantMessageSizeConstraints.ContextLimit)
                    {
                        break;
                    }
                }
            }

            // Grab any console logs
            var consoleAttachments = prompt.ConsoleAttachments;
            if (consoleAttachments != null)
            {
                foreach (var currentLog in consoleAttachments)
                {
                    var consoleContext = new ConsoleContextSelection();
                    consoleContext.SetTarget(currentLog);
                    contextBuilder.InjectContext(consoleContext);

                    if (stopAtLimit && contextBuilder.PredictedLength > AssistantMessageSizeConstraints.ContextLimit)
                    {
                        break;
                    }
                }
            }
        }
    }
}
