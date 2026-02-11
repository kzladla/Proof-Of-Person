using System;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    public partial class InsufficientPointsBanner : BasicBannerContent
    {
        public InsufficientPointsBanner() : base("Insufficient Points") { }
    }
}
