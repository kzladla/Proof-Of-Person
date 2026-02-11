using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    public partial class AIToolkitDisabledBanner : BasicBannerContent
    {
        public AIToolkitDisabledBanner() : base("Assistant can be used for prompting. Asset generations might not work.") { }
    }
}