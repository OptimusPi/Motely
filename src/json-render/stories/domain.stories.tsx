import type { Meta, StoryObj } from "@storybook/react-vite";
import { Panel } from "../components/layout.js";
import { SearchStats, SeedCard, SeedList, JokerBadge, EditionBadge } from "../components/domain.js";

const meta: Meta<typeof SearchStats> = {
  title: "json-render/Domain",
};

export default meta;

export const SearchStatsRunning: StoryObj<typeof SearchStats> = {
  render: () => (
    <SearchStats
      status="running"
      seedsSearched="12,345,678"
      matchesFound={42}
      seedsPerSecond={1_250_000}
      elapsed="00:00:09"
    />
  ),
};

export const SearchStatsCompleted: StoryObj<typeof SearchStats> = {
  render: () => (
    <SearchStats
      status="completed"
      seedsSearched="100,000,000"
      matchesFound={1337}
      seedsPerSecond={0}
      elapsed="00:01:23"
    />
  ),
};

export const SeedCardDefault: StoryObj<typeof SeedCard> = {
  render: () => (
    <SeedCard
      seed="ALEEB"
      score={42}
      rank={1}
      jokers={["Blueprint", "Brainstorm"]}
      highlights={["Foil"]}
      edition="Foil"
    />
  ),
};

export const SeedListShort: StoryObj<typeof SeedList> = {
  render: () => (
    <SeedList
      seeds={["ALEEB", "FROG", "JAMMY", "PIFREK"]}
      scores={[42, 31, 27, 19]}
      total={4}
      pageSize={2}
    />
  ),
};

export const JokerBadges: StoryObj<typeof JokerBadge> = {
  render: () => (
    <Panel>
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        <JokerBadge name="Blueprint" rarity="Common" />
        <JokerBadge name="Brainstorm" rarity="Rare" edition="Foil" />
        <JokerBadge name="Perkeo" rarity="Legendary" edition="Polychrome" />
      </div>
    </Panel>
  ),
};

export const EditionBadges: StoryObj<typeof EditionBadge> = {
  render: () => (
    <Panel>
      <div style={{ display: "flex", gap: 8 }}>
        <EditionBadge edition="Foil" />
        <EditionBadge edition="Holographic" />
        <EditionBadge edition="Polychrome" />
        <EditionBadge edition="Negative" />
      </div>
    </Panel>
  ),
};
