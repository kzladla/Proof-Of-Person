using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    public partial class AIDisabledLegalBanner : BasicBannerContent
    {
        public AIDisabledLegalBanner() : base(LegalAgreement.data.text, LegalAgreement.data.links,
                LegalAgreement.data.installButtonText, LegalAgreement.data.installButtonAction) { }
    }
}
