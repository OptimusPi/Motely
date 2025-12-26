# Frontend Source Projects

This directory contains all frontend source files that require building or are experimental projects.

## Structure

- **JamlUI3/** - Vue 3 + Vite project (production-ready)
- **UnderConstruction4/** - Vue 3 via CDN (experimental)
- **UnderConstruction5/** - Preact via CDN (experimental)
- **UnderConstruction6/** - HTMX/Vanilla JS (experimental)

## Build Output

All builds output directly to `../wwwroot/[ProjectName]/` where they are served by the ASP.NET Core API.

## Adding New Projects

When adding a new frontend project:
1. Create project directory in `frontend/`
2. Configure build to output to `../../wwwroot/[ProjectName]/`
3. Update this README

