using System;
using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    public partial class LowPointsBanner : BasicBannerContent
    {
        const string k_Tooltip = "Only 10% of your org’s points remain. Request a points top-up to continue generating content without interruption.";

        public LowPointsBanner()
            : base(
                "Only 10% of your org’s points remain. <link=get-points><color=#7BAEFA>Request a points top-up</color></link>. Points refresh automatically each week.",
                new[] { new LabelLink("get-points", AccountLinks.GetPoints) },
            true)
        {
            this.Query<VisualElement>().ForEach(ve => ve.tooltip = k_Tooltip);
        }
    }
}
