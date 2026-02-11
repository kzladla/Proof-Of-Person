using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    public class BasicBannerContent : VisualElement
    {
        public VisualElement content = new();

        public BasicBannerContent(string message, LabelLink link) : this(message, new List<LabelLink> {link}) { }

        public BasicBannerContent(string message = "", IEnumerable<LabelLink> links = null)
            : this(message, links, false) { }

        public BasicBannerContent(string message, IEnumerable<LabelLink> links, bool useInfoIcon)
        {
            Init();

            content.AddToClassList("banner-content");
            content.Add(useInfoIcon ? CreateInfoIcon() : CreateWarningIcon());
            content.Add(new RichLabel(message, links));
        }

        public BasicBannerContent(string message, IEnumerable<LabelLink> links, string buttonText, Action buttonAction)
        {
            Init();

            content.AddToClassList("banner-content-button");
            content.Add(new RichLabel(message, links));

            if (buttonAction == null) return;

            var button = new Button { text = buttonText };
            button.clicked += buttonAction;

            content.Add(button);
        }

        public BasicBannerContent(string message, IEnumerable<LabelLink> links, string loadingMessage, TimeSpan? displayLoadingDuration = null)
        {
            Init();
            content.AddToClassList("banner-content");
            var warningIcon = CreateWarningIcon();
            var richLabel = new RichLabel(message, links);
            var dropdownLoading = new DropdownLoading(loadingMessage);

            RegisterCallback<AttachToPanelEvent>(async _ =>
            {
                content.Clear();
                content.Add(dropdownLoading);
                await EditorTask.Delay(displayLoadingDuration != null ? (int)displayLoadingDuration.Value.TotalMilliseconds : 30000);
                content.Clear();
                content.Add(warningIcon);
                content.Add(richLabel);
            });
        }

        protected void Init()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.toolkit/Modules/Accounts/Components/SessionStatusBanner/SessionStatusBanner.uss"));
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.toolkit/Modules/Accounts/Components/AIDropdownRoot/AIDropdownRoot.uss"));
            AddToClassList("banner");
            Add(content);
        }

        protected static Image CreateWarningIcon()
        {
            var warningIcon = new Image { image = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D };
            warningIcon.AddToClassList("warning-icon");
            return warningIcon;
        }

        protected static Image CreateInfoIcon()
        {
            var infoIcon = new Image { image = EditorGUIUtility.IconContent("console.infoicon").image as Texture2D };
            infoIcon.AddToClassList("info-icon");
            return infoIcon;
        }
    }
}
