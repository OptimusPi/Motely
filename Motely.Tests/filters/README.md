# JAML Discord Filters

This folder contains JAML filter examples collected from Discord users. These filters will be embedded and used for RAG (Retrieval-Augmented Generation) to help JAMMY learn from successful user queries.

## Structure

Each filter should be a `.jaml` file with the following format:

```yaml
name: Filter Name
author: Discord Username
description: Short description of what this filter finds

must:
    - joker: Blueprint
      antes: [1, 2]
      sources:
          shopItems: [0, 1, 2]

should:
    - joker: Brainstorm
      score: 2
```

## How to Add Filters

1. Create a new `.jaml` file in this directory
2. Name it descriptively (e.g., `blueprint-brainstorm-ante1.jaml`)
3. Run the embedding script to populate the database:
    ```bash
    npm run embed-filters
    ```

## Embedding Process

The `scripts/embed-filters.ts` script will:

1. Read all `.jaml` files from this directory
2. Generate embeddings using Vercel AI SDK 6
3. Store them in the `jaml_filters` Postgres table
4. JAMMY will use these for semantic search when users ask similar questions

## Example Filters to Add

Place your collected Discord filters here. Good examples include:

- Early game legendary joker builds
- Specific deck strategies (Erratic, Plasma, etc.)
- Popular synergy combos (Blueprint + Brainstorm, Baron + Kings, etc.)
- High-scoring builds
- Challenge run setups
