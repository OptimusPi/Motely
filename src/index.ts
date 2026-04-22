"use client";

export {
    JAML_ASSET_FILES,
    clearJamlAssetBaseUrl,
    getDefaultJamlAssetUrlMap,
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
export {
    JimboText,
    type JimboTextProps,
    type JimboTextTone,
    type JimboTextSize,
} from "./ui/jimboText.js";
export {
    JamlIdeToolbar,
    type JamlIdeMode,
    type JamlIdeToolbarProps,
} from "./components/JamlIdeToolbar.js";
export { CardList, type CardListProps } from "./components/CardList.js";

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
