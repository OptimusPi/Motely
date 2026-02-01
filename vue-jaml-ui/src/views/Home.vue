<template>
  <div class="home">
    <section class="hero">
      <p class="eyebrow">Balatro Seed Search Tools</p>
      <h1>One home for every experiment</h1>
      <p class="subtitle">Launch the new JAML grid or jump into other UIs, games, or searchers.</p>
    </section>

    <section class="local">
      <h2>Local Tools</h2>
      <div class="grid">
        <RouterLink class="card card-primary" to="/">
          <h3>JAML Grid (Vue 3)</h3>
          <p>Modular panel system with manilla tabs, drag layouts, and Vue state.</p>
        </RouterLink>
        <a class="card" :href="localTools[0].href" target="_blank" rel="noreferrer">
          <h3>JAML WebUI</h3>
          <p>Commander control for JAML filters and live seed searches.</p>
        </a>
        <a v-for="tool in localTools.slice(1)" :key="tool.href" class="card" :href="tool.href">
          <h3>{{ tool.label }}</h3>
          <p>{{ tool.description }}</p>
        </a>
      </div>
    </section>

    <section class="games">
      <h2>Games</h2>
      <div class="grid">
        <a v-for="game in games" :key="game.href" class="card" :href="game.href" target="_blank" rel="noreferrer">
          <h3>{{ game.label }}</h3>
          <p>{{ game.description }}</p>
        </a>
      </div>
    </section>

    <section class="searchers">
      <h2>Other Searchers</h2>
      <div class="grid">
        <a v-for="searcher in searchers" :key="searcher.href" class="card" :href="searcher.href" target="_blank" rel="noreferrer">
          <h3>{{ searcher.label }}</h3>
          <p>{{ searcher.description }}</p>
        </a>
      </div>
    </section>

    <section class="mobile">
      <h2>Mobile</h2>
      <div class="grid">
        <a v-for="app in mobile" :key="app.href" class="card" :href="app.href" target="_blank" rel="noreferrer">
          <h3>{{ app.label }}</h3>
          <p>{{ app.description }}</p>
        </a>
      </div>
    </section>

  </div>
</template>

<script setup>
const localTools = [
  { label: 'JAML WebUI', description: 'Commander control for JAML filters and live seed searches.', href: 'http://localhost:8080/JAML/' },
  { label: 'BSO Browser', description: 'Avalonia UI compiled to WebAssembly. Full oracle in browser.', href: 'http://localhost:8080/BSO/' },
  { label: 'JamlGenie (Legacy)', description: 'Prototype JAML filter generator and helper tool.', href: 'http://localhost:8080/JamlGenie/' }
]

const games = [
  { label: 'The Daily Wee', description: 'Daily challenge on WeeJoker.app', href: 'https://weejoker.app/' },
  { label: 'ErraticDeck', description: 'Deck building and analysis.', href: 'https://erraticdeck.app/' }
]

const searchers = [
  { label: 'Blueprint', description: 'Online seed analyzer by miaklwalker.', href: 'https://miaklwalker.github.io/Blueprint/' },
  { label: 'The Egg', description: 'Balatro seed searcher by exitatmosphere.', href: 'https://exitatmosphere.github.io/TheEgg/' },
  { label: 'Legendary Seeds', description: 'Seed searcher powered by balatro4j.', href: 'https://legendaryseeds.app/' },
  { label: 'Motely by tacodiva', description: 'Original fast CPU SIMD seed searcher.', href: 'https://github.com/tacodiva/motely' }
]

const mobile = [
  { label: 'Perkeo', description: 'Balatro companion app for iOS by alex-cova.', href: 'https://github.com/alex-cova/perkeoapp' }
]
</script>

<style scoped>
.home {
  max-width: 960px;
  margin: 0 auto;
  padding: 48px 16px 64px;
}

.hero {
  text-align: center;
  margin-bottom: 48px;
}

.eyebrow {
  font-size: 0.85rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: var(--light-grey);
  margin-bottom: 12px;
}

.hero h1 {
  font-size: clamp(2rem, 4vw, 2.75rem);
  color: var(--balatro-gold);
  margin-bottom: 12px;
}

.subtitle {
  color: var(--light-grey);
  font-size: 1rem;
}

section {
  margin-bottom: 40px;
}

h2 {
  font-size: 0.85rem;
  text-transform: uppercase;
  letter-spacing: 0.15em;
  margin-bottom: 16px;
  color: var(--light-grey);
}

.local h2 {
  color: var(--balatro-green);
}

.games h2 {
  color: var(--balatro-gold);
}

.searchers h2 {
  color: var(--balatro-blue);
}

.mobile h2 {
  color: var(--balatro-green);
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 16px;
}

.card,
.card-primary {
  display: block;
  text-decoration: none;
  padding: 16px;
  border-radius: 10px;
  border: 1px solid var(--border-color);
  background: rgba(0, 0, 0, 0.25);
  color: var(--text-color);
  transition: background 0.15s, border-color 0.15s;
}

.card-primary {
  border-color: var(--balatro-blue);
  box-shadow: 0 10px 25px rgba(0, 147, 255, 0.2);
}

.card:hover,
.card-primary:hover {
  background: rgba(0, 0, 0, 0.3);
  border-color: var(--balatro-gold);
}

.card h3 {
  font-size: 1.05rem;
  margin-bottom: 0.35rem;
  color: var(--text-color);
}

.card p {
  color: var(--light-grey);
  font-size: 0.85rem;
}

</style>
