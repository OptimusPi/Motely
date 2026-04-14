import { defineCatalog } from "@json-render/core";
import { schema } from "@json-render/react/schema";
import { z } from "zod";

export const jamlSearchCatalog = defineCatalog(schema, {
  components: {
    Stack: {
      props: z.object({ heading: z.string() }),
      description: "Vertical layout with a gold title",
    },
    FilterDisplay: {
      props: z.object({
        jummy: z.string().optional(),
        jaml: z.string().optional(),
      }),
      description: "Shows the current search filter",
    },
    StatsBlock: {
      props: z.object({
        status: z.string(),
        seedsSearched: z.string(),
        matchesFound: z.string(),
        resultsShown: z.string().optional(),
      }),
      description: "Search run summary bar",
    },
    SeedTable: {
      props: z.object({
        rows: z.array(
          z.object({
            seed: z.string(),
            score: z.string(),
            tally: z.array(z.number()).optional(),
          })
        ),
      }),
      description: "Table of matching seeds with scores and tally breakdown",
    },
    SeedDetail: {
      props: z.object({
        seed: z.string(),
        loading: z.string().optional(),
        error: z.string().optional(),
        analysisJson: z.string().optional(),
      }),
      description: "Expanded ante-by-ante breakdown for a single seed",
    },
    Button: {
      props: z.object({
        label: z.string(),
        disabled: z.string().optional(),
      }),
      description: "Red primary action button",
    },
    Text: {
      props: z.object({
        body: z.string(),
        variant: z.string().optional(),
      }),
      description: "Plain text or error message",
    },
    EmptyState: {
      props: z.object({ message: z.string() }),
      description: "Shown when no results found",
    },
    Spinner: {
      props: z.object({ message: z.string().optional() }),
      description: "Animated loading spinner",
    },
  },
  actions: {
    rerunSearch: {
      description: "Re-roll: run search_seeds again with the same filter",
    },
    analyzeSeed: {
      description: "Drill into a seed for full ante-by-ante breakdown",
    },
    closeSeedDetail: {
      description: "Close the seed detail overlay",
    },
  },
});
