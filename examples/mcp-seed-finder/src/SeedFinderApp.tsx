import { useMemo } from "react";
import { JamlIde, useSearch, type JamlIdeSearchResult } from "jaml-ui";
import { JimboApp, JimboBackground } from "jaml-ui/ui";

export interface SeedFinderAppProps {
  jaml: string;
  onChange: (next: string) => void;
  onRunRequest?: (jaml: string) => Promise<void> | void;
}

export function SeedFinderApp({ jaml, onChange, onRunRequest }: SeedFinderAppProps) {
  const search = useSearch();

  const searchResults = useMemo<JamlIdeSearchResult[]>(
    () =>
      search.results.map((result) => ({
        seed: result.seed,
        score: result.score,
        tallyColumns: result.tallyColumns,
        tallyLabels: search.tallyLabels,
      })),
    [search.results, search.tallyLabels],
  );

  const handleSearch = () => {
    if (search.status === "running") {
      search.cancel();
      return;
    }

    void onRunRequest?.(jaml);
    search.startAesthetic(jaml, 0);
  };

  const subtitle =
    search.status === "running"
      ? `Searching ${search.totalSearched.toString()} seeds at ${Math.round(search.seedsPerSecond)}/s`
      : search.status === "completed"
        ? `Done. ${search.matchingSeeds.toString()} matches.`
        : search.status === "error"
          ? `Error: ${search.error}`
          : "Ready to search.";

  return (
    <>
      <JimboBackground />
      <JimboApp>
        <JamlIde
          jaml={jaml}
          onChange={onChange}
          searchResults={searchResults}
          isSearching={search.status === "running"}
          onSearch={handleSearch}
          title="Seed search"
          subtitle={subtitle}
        />
      </JimboApp>
    </>
  );
}
