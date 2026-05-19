import { useMemo, useState } from "react";
import {
  JamlIde,
  useSearch,
  type JamlIdeSearchResult,
} from "jaml-ui";
import {
  JimboApp,
  JimboBackground,
  JimboPanel,
  JimboText,
} from "jaml-ui/ui";

const STARTER_JAML = `must:
  - joker: Blueprint
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
deck: Red
stake: White
`;

export function App() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  const search = useSearch();

  const results = useMemo<JamlIdeSearchResult[]>(
    () =>
      search.results.map((r) => ({
        seed: r.seed,
        score: r.score,
        tallyColumns: r.tallyColumns,
        tallyLabels: search.tallyLabels,
      })),
    [search.results, search.tallyLabels],
  );

  const handleSearch = () => {
    if (search.status === "running") {
      search.cancel();
    } else {
      search.startAesthetic(jaml, /* aesthetic */ 0);
    }
  };

  return (
    <>
      <JimboBackground />
      <JimboApp>
        <JimboPanel>
          <JimboText size="md">jaml-ui · Seed Finder demo</JimboText>
          <JimboText size="xs" tone="grey">
            Boots motely-wasm at startup, runs real searches in-browser, renders results with JamlIde.
          </JimboText>
        </JimboPanel>

        <JamlIde
          jaml={jaml}
          onChange={setJaml}
          searchResults={results}
          isSearching={search.status === "running"}
          onSearch={handleSearch}
          title="Seed search"
          subtitle={
            search.status === "running"
              ? `Searching… ${search.totalSearched.toString()} seeds, ${Math.round(search.seedsPerSecond)}/s`
              : search.status === "completed"
                ? `Done. ${search.matchingSeeds.toString()} matches.`
                : search.status === "error"
                  ? `Error: ${search.error}`
                  : "Ready to search."
          }
        />
      </JimboApp>
    </>
  );
}
