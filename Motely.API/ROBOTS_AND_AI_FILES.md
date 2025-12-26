# robots.txt, humans.txt, and ai.txt Files

This document explains the three standard files added to the website root for web crawlers, humans, and AI agents.

---

## Files Created

### 1. `robots.txt` (`/robots.txt`)
**Purpose:** Directs web crawlers (search engines) on what to index.

**Features:**
- Allows all crawlers to access public pages (`/JamlGenie/`, `/JAML/`, `/BSO/`)
- Blocks API endpoints (`/api/`, `/search/`, `/mcp/`, `/swagger/`)
- Explicitly allows AI bots (GPTBot, ChatGPT-User, anthropic-ai, Claude-Web)
- Includes sitemap reference

**Access:** `https://yourdomain.com/robots.txt`

---

### 2. `humans.txt` (`/humans.txt`)
**Purpose:** Credits the humans behind the project (humanstxt.org standard).

**Features:**
- Credits **Pie Freak** as developer/creator
- Lists technology stack
- Mentions MCP server information
- Credits @tacodiva (original Motely creator)

**Access:** `https://yourdomain.com/humans.txt`

---

### 3. `ai.txt` (`/ai.txt`)
**Purpose:** Provides instructions for AI agents, MCP bots, and LLM scrapers.

**Features:**
- **MCP Server Information:** Protocol version, endpoint, transport
- **Available Tools:** Complete list with input/output schemas
- **API Endpoints:** All available endpoints documented
- **Usage Guidelines:** Rate limits, attribution requirements
- **Data Formats:** JAML, JSON, results format
- **Game Information:** Balatro developer/publisher/wiki links

**Access:** `https://yourdomain.com/ai.txt`

**Why `ai.txt`?**
- While not an official standard (yet), it's becoming common practice
- Similar to `robots.txt` but specifically for AI agents
- Helps AI agents discover MCP servers and API capabilities
- Provides structured information for LLM scrapers

---

## How They Work

All three files are served as static files from `wwwroot/`:
- ASP.NET Core's `UseStaticFiles()` automatically serves them
- `.txt` files are served with `text/plain` content type (default)
- Accessible at root level: `/robots.txt`, `/humans.txt`, `/ai.txt`

---

## Benefits

1. **SEO:** `robots.txt` helps search engines index your site correctly
2. **Attribution:** `humans.txt` gives credit to developers
3. **AI Discovery:** `ai.txt` helps AI agents discover and use your MCP server
4. **Documentation:** All three files serve as documentation for crawlers/humans/AI

---

## Future Enhancements

- Add `sitemap.xml` for better SEO
- Consider adding `security.txt` (RFC 9116) for security contact info
- Update `ai.txt` as MCP protocol evolves
- Add more AI bot user-agents to `robots.txt` as they emerge

---

**Last Updated:** 2025-01-15


