# JAML Integration Plan

## Overview
The goal is to integrate JAML (Jimbo's Ante Markup Language) into the `soul_joker_filter_search.cu` app. This will allow users to define seed filters using a structured schema, enabling more flexible and powerful searches.

## Steps

### 1. Parsing JAML Files
- Use a JSON parser to load `.jaml` files.
- Validate the file against the provided JAML schema (`jaml.schema.json`).
- Extract relevant fields such as `must`, `should`, and `mustNot` clauses.

### 2. Mapping JAML to Filters
- Convert JAML clauses into filter structures used by the app.
- Support nested conditions (`and`, `or`) and attributes like `edition`, `rank`, and `suit`.

### 3. Integrating Filters
- Modify the `check_soul_joker_filter()` function to evaluate seeds against JAML-defined filters.
- Ensure compatibility with existing GPU kernel logic.

### 4. Testing
- Create sample `.jaml` files to test various scenarios.
- Validate that the app correctly applies filters and produces expected results.

### 5. Documentation
- Update the documentation to include:
  - Instructions for creating `.jaml` files.
  - Examples of JAML filters.
  - Steps for running the app with JAML integration.

## Challenges
- Ensuring efficient parsing and validation on the GPU.
- Maintaining performance while supporting complex filter logic.

## Next Steps
- Implement JAML parsing and validation.
- Integrate JAML filters into the app.
- Test and document the integration.