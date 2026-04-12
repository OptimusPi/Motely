import { defineCatalog } from "@json-render/core";
import { schema } from "@json-render/react/schema";
import { z } from "zod";

export const jamlSearchCatalog = defineCatalog(schema, {
  components: {
    Stack: {
      props: z.object({ heading: z.string() }),
      description: "Vertical layout with a gold title",
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
  },
  actions: {
    rerunSearch: {
      description: "Re-roll: run search_seeds again with the same filter",
    },
  },
});
