# BalatroSeedOracle DuckDB Consolidation

## Context
We're consolidating all DuckDB table creation and schema management into **Motely core library** as the single source of truth.

## What's Happening in Motely
- Creating `Motely/DuckDB/` namespace with:
  - `DuckDBSchema.cs` - All table schema definitions
  - `DuckDBConnectionFactory.cs` - Centralized connection creation
  - `DuckDBTableManager.cs` - Table operations (create, validate, sanitize)
- Refactoring all Motely projects (API, CLI, TUI) to use these centralized helpers
- Removing duplicate `CREATE TABLE` SQL scattered across the codebase

## What Needs to Happen in BalatroSeedOracle

### 1. Audit DuckDB Usage
Find all places where BalatroSeedOracle creates DuckDB tables or connections:
- Search for `CREATE TABLE` statements
- Search for `new DuckDBConnection`
- Search for `DuckDB.NET.Data` imports

### 2. Update to Use Motely Core
Once Motely consolidation is complete:
- Reference `Motely.DuckDB` namespace
- Use `DuckDBSchema` for table definitions
- Use `DuckDBConnectionFactory` for connections
- Use `DuckDBTableManager` for table operations

### 3. Remove Duplicate Code
- Delete any inline `CREATE TABLE` SQL
- Remove custom DuckDB connection creation
- Remove duplicate schema definitions

## Benefits
- Single source of truth for DuckDB schemas
- Consistent connection management across all projects
- Easier maintenance - update schemas in one place
- Reduced code duplication

## Timeline
- **Motely consolidation**: In progress
- **BalatroSeedOracle update**: After Motely is complete (reference the consolidated Motely core)

## Notes
- Scripts in `scripts/` folder are temp/migration - can be ignored
- Focus on production code that creates/manages DuckDB databases
