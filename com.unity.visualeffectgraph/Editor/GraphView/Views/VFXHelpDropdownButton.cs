using System.Linq;

using UnityEditor.Experimental;
using UnityEditor.PackageManager.UI;

using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.VFX.UI
{
    class VFXHelpDropdownButton : DropDownButtonBase
    {
        const string k_PackageVersion = "12.0.0";
        const string k_PackageName = "com.unity.visualeffectgraph";
        const string k_AdditionalSamples = "VisualEffectGraph Additions";
        const string k_AdditionalHelpers = "OutputEvent Helpers";
        const string k_ManualUrl = @"http://docs.unity3d.com/Packages/com.unity.visualeffectgraph@12.0/manual/index.html";
        const string k_ForumUrl = @"https://forum.unity.com/forums/visual-effect-graph.428/";
        const string k_SpaceShipUrl = @"https://github.com/Unity-Technologies/SpaceshipDemo";
        const string k_SamplesUrl = @"https://github.com/Unity-Technologies/VisualEffectGraph-Samples";
        const string k_VfxGraphUrl = @"https://unity.com/visual-effect-graph";

        readonly VFXView m_VFXView;
        readonly Button m_installSamplesButton;
        readonly Button m_installHelpersButton;

        public VFXHelpDropdownButton(VFXView vfxView)
            : base(
                "VFXHelpDropdownPanel",
                "Help",
                "help-button",
                EditorResources.iconsPath + "_Help.png",
                true)
        {
            m_VFXView = vfxView;

            m_installSamplesButton = m_PopupContent.Q<Button>("installSamples");
            m_installSamplesButton.clicked += OnInstallSamples;

            m_installHelpersButton = m_PopupContent.Q<Button>("graphAddition");
            m_installHelpersButton.clicked += OnInstallGraphAddition;

            var gotoManual = m_PopupContent.Q<Button>("gotoManual");
            gotoManual.clicked += () => GotoUrl(k_ManualUrl);

            var gotoForum = m_PopupContent.Q<Button>("gotoForum");
            gotoForum.clicked += () => GotoUrl(k_ForumUrl);

            var gotoSpaceShip = m_PopupContent.Q<Button>("gotoSpaceShip");
            gotoSpaceShip.clicked += () => GotoUrl(k_SpaceShipUrl);

            var gotoSamples = m_PopupContent.Q<Button>("gotoSamples");
            gotoSamples.clicked += () => GotoUrl(k_SamplesUrl);
        }

        protected override Vector2 GetPopupPosition() => this.m_VFXView.ViewToScreenPosition(worldBound.position);
        protected override Vector2 GetPopupSize() => new Vector2(200, 224);

        protected override void OnOpenPopup()
        {
            m_installSamplesButton.SetEnabled(!IsSampleInstalled(k_AdditionalSamples));
            m_installHelpersButton.SetEnabled(!IsSampleInstalled(k_AdditionalHelpers));
        }

        protected override void OnMainButton()
        {
            GotoUrl(k_VfxGraphUrl);
        }

        void GotoUrl(string url) => Help.BrowseURL(url);

        void OnInstallSamples()
        {
            InstallSample(k_AdditionalSamples);
        }

        void OnInstallGraphAddition()
        {
            InstallSample(k_AdditionalHelpers);
        }

        bool IsSampleInstalled(string sampleName)
        {
            return Sample.FindByPackage(k_PackageName, k_PackageVersion).SingleOrDefault(x => x.displayName == sampleName).isImported;
        }

        void InstallSample(string sampleName)
        {
            var sample = Sample.FindByPackage(k_PackageName, k_PackageVersion).SingleOrDefault(x => x.displayName == sampleName);
            if (!sample.isImported)
            {
                sample.Import();
            }
        }
    }
}
