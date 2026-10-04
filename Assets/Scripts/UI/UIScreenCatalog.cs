using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The UXML layout of every screen and menu station board (ARCHITECTURE §6.2), one asset at Resources/<see cref="ScreenStack.CatalogResource"/>
    /// next to the panel settings, so screens get their template without a path lookup. The layouts themselves live in
    /// Assets/UI/Screens and are edited in UI Builder.
    /// </summary>
    [CreateAssetMenu(menuName = "HotPatata/UI Screen Catalog", fileName = "HotPatataScreens")]
    public class UIScreenCatalog : ScriptableObject
    {
        [Header("Menu stations (world-space boards, MenuView)")]
        public VisualTreeAsset stationTitle;
        public VisualTreeAsset stationPlay;
        public VisualTreeAsset stationLevel;
        public VisualTreeAsset stationLobby;
        public VisualTreeAsset nameplate;

        [Header("Screens (ScreenStack)")]
        public VisualTreeAsset pause;
        public VisualTreeAsset settings;
        public VisualTreeAsset confirm;
        public VisualTreeAsset results;
        public VisualTreeAsset uiSample;
    }
}
