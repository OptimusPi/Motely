"use client";

import "./ui/jimbo.css";

export {
    JAML_ASSET_FILES,
    resolveJamlAssetUrl,
    setJamlAssetBaseUrl,
    type JamlAssetFile,
    type JamlAssetKey,
} from "./assets.js";

export { Layer, type LayerOptions } from "./render/Layer.js";
export { JamlCardRenderer, type JamlCardRendererProps } from "./render/CanvasRenderer.js";

export {
    JamlGameCard,
    JamlVoucher,
    JamlTag,
    JamlBoss,
    resolveAnalyzerShopItem,
    type JamlGameCardProps,
    type AnalyzerShopItem,
    type AnalyzerResolvedItem,
} from "./components/GameCard.js";

export {
    AnalyzerExplorer,
    type AnalyzerAnteView,
    type AnalyzerBadge,
    type AnalyzerExplorerProps,
    type AnalyzerFact,
    type AnalyzerHighlight,
    type AnalyzerItem,
} from "./components/AnalyzerExplorer.js";

export {
    JamlAnalyzerFullscreen,
    type JamlAnalyzerFullscreenProps,
} from "./components/JamlAnalyzerFullscreen.js";
export {
    ANALYZER_STREAM_META,
    DEFAULT_ENABLED_STREAMS,
    type AnalyzerStreamKey,
    type AnalyzerStreamMeta,
} from "./hooks/analyzerStreamRegistry.js";

export { JamlMapPreview, type JamlMapPreviewProps } from "./components/JamlMapPreview.js";
export {
    JamlIde,
    type JamlIdeProps,
    type JamlIdeSearchResult,
    type JamlVisualFilter,
    type JamlVisualClause,
    type JamlZone,
} from "./components/JamlIde.js";
export {
    JamlIdeVisual,
    type JamlIdeVisualProps,
} from "./components/JamlIdeVisual.js";
export {
    JamlCodeEditor,
    type JamlCodeEditorProps,
} from "./components/JamlCodeEditor.js";
export * from "./ui.js";
export {
    JamlIdeToolbar,
    type JamlIdeMode,
    type JamlIdeToolbarProps,
} from "./components/JamlIdeToolbar.js";
export { CardList, type CardListProps } from "./components/CardList.js";
export { CardFan, type CardFanProps } from "./components/CardFan.js";
export {
    RealStandardcard,
    type CardSuit,
    type CardRank,
    type CardEnhancement,
    type CardSeal,
    type CardEdition,
} from "./components/Standardcard.js";
export {
    DeckSprite,
    DECK_SPRITE_POS,
    STAKE_SPRITE_POS,
    type DeckSpriteProps,
} from "./components/DeckSprite.js";
export {
    MotelyVersionBadge,
    type MotelyVersionBadgeProps,
    type MotelyCapabilities,
} from "./components/MotelyVersionBadge.js";
export {
    JamlSpeedometer,
    type JamlSpeedometerProps,
    type JamlSpeedometerStatus,
} from "./components/JamlSpeedometer.js";
export {
    Showcase,
    type ShowcaseFilter,
    type ShowcaseLiveStats,
    type ShowcaseProps,
    type ShowcaseRecentFind,
} from "./ui/showcase.js";

export {
    extractVisualJamlItems,
    type JamlPreviewGroups,
    type JamlPreviewItem,
    type JamlPreviewSection,
    type JamlPreviewVisualType,
} from "./utils/jamlMapPreview.js";

export { useMotelyStream, type StreamItem, type StreamState } from "./hooks/useShopStream.js";
export {
    useSearch,
    type SearchResult,
    type SearchStatus,
    type UseSearchState,
} from "./hooks/useSearch.js";
export {
    useAnalyzer,
    type AnalyzerStatus,
    type AnalyzerLive,
    type MotelyJsRunState,
} from "./hooks/useAnalyzer.js";

// Setter pattern for motely-wasm runtime enums. Consumers boot motely-wasm
// and call setMotelyEnums(Motely) once after boot so display/decoder helpers
// can resolve enum keys without statically importing motely-wasm.
export { setMotelyEnums as setMotelyDisplayEnums } from "./motelyDisplay.js";
export { setMotelyEnums as setMotelyDecoderEnums } from "./decode/motelyItemDecoder.js";


export {
    JamlAestheticSelector,
    type JamlAestheticSelectorProps,
    type JamlAestheticOption,
} from "./components/JamlAestheticSelector.js";
export {
    JamlSeedInput,
    type JamlSeedInputProps,
} from "./components/JamlSeedInput.js";

export {
    JamlMapEditor,
    JokerPicker,
    MysterySlot,
    CategoryPicker,
    type JamlMapEditorProps,
    type JokerPickerProps,
    type JokerRarity,
    type MysterySlotProps,
    type SlotCategory,
    type SlotSelection,
    type CategoryPickerConfig,
    type CategoryPickerProps,
    VOUCHER_PICKER_CONFIG,
    TAG_PICKER_CONFIG,
    BOSS_PICKER_CONFIG,
    TAROT_PICKER_CONFIG,
    PLANET_PICKER_CONFIG,
    SPECTRAL_PICKER_CONFIG,
    PACK_PICKER_CONFIG,
} from "./components/jamlMap/index.js";
