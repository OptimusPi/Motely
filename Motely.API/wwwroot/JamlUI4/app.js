// Minimal Alpine app for JamlUI4
if (typeof signalR !== 'undefined') {
  window.signalR = signalR;
} else {
  window.signalR = {
    HubConnectionBuilder: function() {
      return {
        withUrl: function() { return this; },
        withAutomaticReconnect: function() { return this; },
        build: function() {
          return {
            on: function() {},
            start: function() { return Promise.resolve(); },
            invoke: function() { return Promise.resolve(); },
            state: 'Disconnected'
          };
        }
      };
    },
    HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' }
  };
}

function app() {
  return {
    filterJaml: '',
    seedSource: 'all',
    seedCount: null,
    seedSources: [],
    currentSearchId: null,
    searchStatus: null,
    isSearching: false,
    results: [],
    columns: ['seed', 'score'],
    progressPercent: 0,
    seedsSearched: 0,
    seedsPerSecond: 0,
    lastError: null,
    connection: null,
    pollInterval: null,
    splitPosition: 50,
    isResizing: false,

    async init() {
      const saved = localStorage.getItem('splitPosJamlUI4');
      if (saved) {
        this.splitPosition = parseFloat(saved);
        this.applySplit();
      }

      await this.loadSeedSources();
      await this.loadActive();
      this.setupSignalR();
      this.startPolling();
    },

    // Splitter
    startResize(e) {
      e.preventDefault();
      this.isResizing = true;
      const container = document.querySelector('.split-container');
      const isMobile = window.innerWidth <= 900;
      let startPos = isMobile ? (e.touches ? e.touches[0].clientY : e.clientY) : (e.touches ? e.touches[0].clientX : e.clientX);
      const containerSize = isMobile ? container.offsetHeight : container.offsetWidth;

      const move = (ev) => {
        if (!this.isResizing) return;
        ev.preventDefault();
        const currentPos = isMobile ? (ev.touches ? ev.touches[0].clientY : ev.clientY) : (ev.touches ? ev.touches[0].clientX : ev.clientX);
        const delta = (isMobile ? (startPos - currentPos) : (currentPos - startPos)) / containerSize * 100;
        this.splitPosition = Math.max(30, Math.min(70, this.splitPosition + delta));
        localStorage.setItem('splitPosJamlUI4', this.splitPosition.toString());
        this.applySplit();
        startPos = currentPos;
      };
      const up = () => {
        this.isResizing = false;
        window.removeEventListener('mousemove', move);
        window.removeEventListener('mouseup', up);
        window.removeEventListener('touchmove', move);
        window.removeEventListener('touchend', up);
      };
      window.addEventListener('mousemove', move);
      window.addEventListener('mouseup', up);
      window.addEventListener('touchmove', move, { passive: false });
      window.addEventListener('touchend', up);
    },

    applySplit() {
      const left = document.querySelector('.left-panel');
      const right = document.querySelector('.right-panel');
      if (!left || !right) return;
      const isMobile = window.innerWidth <= 900;
      left.style.flex = `0 0 ${this.splitPosition}%`;
      right.style.flex = `1 1 ${100 - this.splitPosition}%`;
    },

    async loadSeedSources() {
      try {
        const res = await fetch('/seed-sources');
        if (res.ok) {
          const data = await res.json();
          this.seedSources = data || [];
          if (this.seedSources.length && !this.seedSource) {
            this.seedSource = this.seedSources[0].key || 'all';
          }
        }
      } catch (e) { console.error(e); }
    },

    async loadActive() {
      try {
        const res = await fetch('/searches/active');
        if (res.ok) {
          const data = await res.json();
          if (data.searches && data.searches.length) {
            this.currentSearchId = data.searches[0].id;
            await this.loadStatus();
          }
        }
      } catch (e) { console.error(e); }
    },

    setupSignalR() {
      if (!window.signalR || !window.signalR.HubConnectionBuilder) return;
      try {
        this.connection = new window.signalR.HubConnectionBuilder()
          .withUrl('/searchHub')
          .withAutomaticReconnect()
          .build();

        this.connection.on('Result', (data) => {
          try {
            const payload = typeof data === 'string' ? JSON.parse(data) : data;
            if (payload.searchId !== this.currentSearchId) return;
            if (payload.results && Array.isArray(payload.results)) {
              payload.results.forEach(r => {
                if (!this.results.find(x => x.seed === r.seed)) this.results.push(r);
              });
              this.results.sort((a, b) => (b.score || 0) - (a.score || 0));
            }
            if (payload.columns) this.columns = payload.columns;
            if (payload.progressPercent !== undefined) this.progressPercent = payload.progressPercent;
            if (payload.seedsSearched !== undefined) this.seedsSearched = payload.seedsSearched;
            if (payload.seedsPerSecond !== undefined) this.seedsPerSecond = payload.seedsPerSecond;
          } catch (err) { console.error(err); }
        });

        this.connection.start().catch(err => console.error('SignalR connection error:', err));

        this.$watch('currentSearchId', (id) => {
          if (id && this.connection && this.connection.state === window.signalR.HubConnectionState.Connected) {
            this.connection.invoke('JoinSearchGroup', id).catch(console.error);
          }
        });
      } catch (e) { console.error(e); }
    },

    startPolling() {
      this.pollInterval = setInterval(() => {
        if (this.currentSearchId) this.loadStatus();
      }, 2000);
    },

    async loadStatus() {
      if (!this.currentSearchId) return;
      try {
        const res = await fetch(`/search?id=${encodeURIComponent(this.currentSearchId)}`);
        if (res.ok) {
          const data = await res.json();
          this.searchStatus = data.status || data.searchStatus || 'stopped';
          this.isSearching = this.searchStatus === 'running';
          if (data.progressPercent !== undefined) this.progressPercent = data.progressPercent;
          if (data.seedsSearched !== undefined) this.seedsSearched = data.seedsSearched;
          if (data.seedsPerSecond !== undefined) this.seedsPerSecond = data.seedsPerSecond;
          this.lastError = data.lastError || null;
          if (data.results && Array.isArray(data.results)) {
            const seeds = new Set(this.results.map(r => r.seed));
            data.results.forEach(r => { if (!seeds.has(r.seed)) this.results.push(r); });
            this.results.sort((a, b) => (b.score || 0) - (a.score || 0));
          }
          if (data.columns) this.columns = data.columns;
          if (data.filterJaml && !this.filterJaml) this.filterJaml = data.filterJaml;
        }
      } catch (e) { console.error(e); }
    },

    async startSearch() {
      if (!this.filterJaml.trim()) { alert('Please enter a JAML filter'); return; }
      try {
        this.isSearching = true;
        this.lastError = null;
        const res = await fetch('/search', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            filterJaml: this.filterJaml.trim(),
            seedSource: this.seedSource || 'all',
            seedCount: this.seedCount || null
          })
        });
        if (!res.ok) {
          const err = await res.json().catch(() => ({}));
          throw new Error(err.error || 'Failed to start search');
        }
        const data = await res.json();
        this.currentSearchId = data.searchId;
        this.searchStatus = 'running';
        this.progressPercent = data.progressPercent || 0;
        if (this.connection && this.connection.state === window.signalR.HubConnectionState.Connected) {
          await this.connection.invoke('JoinSearchGroup', this.currentSearchId);
        }
        this.results = (data.results || []).sort((a, b) => (b.score || 0) - (a.score || 0));
        if (data.columns) this.columns = data.columns;
        await this.loadStatus();
      } catch (e) {
        this.lastError = e.message;
        this.isSearching = false;
        alert('Error starting search: ' + e.message);
      }
    },

    async stopSearch() {
      if (!this.currentSearchId) return;
      try {
        const res = await fetch(`/search/${encodeURIComponent(this.currentSearchId)}/panic-stop`, { method: 'POST' });
        if (!res.ok) {
          const err = await res.json().catch(() => ({}));
          throw new Error(err.error || 'Failed to stop search');
        }
        this.isSearching = false;
        this.searchStatus = 'stopped';
        await this.loadStatus();
      } catch (e) {
        this.lastError = e.message;
        alert('Error stopping search: ' + e.message);
      }
    },

    clearResults() {
      if (confirm('Clear all results?')) {
        this.results = [];
        this.progressPercent = 0;
        this.seedsSearched = 0;
        this.seedsPerSecond = 0;
      }
    },

    async quickStart() {
      // Only run if a JAML filter is provided; no auto-prefill to avoid hallucinated defaults
      if (!this.filterJaml || !this.filterJaml.trim()) {
        alert('Enter a JAML filter first (no autofill applied).');
        return;
      }
      this.seedSource = this.seedSource || 'all';
      this.seedCount = null;
      await this.startSearch();
    },

    exportCsv() {
      if (!this.results.length) { alert('No results to export'); return; }
      const headers = this.columns.join(',');
      const rows = this.results.map(r => {
        const vals = [];
        vals.push(`\"${r.seed || ''}\"`);
        vals.push(r.score || 0);
        if (r.tallies && Array.isArray(r.tallies)) r.tallies.forEach(t => vals.push(t));
        return vals.join(',');
      });
      const csv = [headers, ...rows].join('\n');
      const blob = new Blob([csv], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `jamlui4-results-${Date.now()}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    },

    fmt(n) {
      if (n === null || n === undefined) return '0';
      return n.toLocaleString();
    },

    getTallyValue(r, idx) {
      if (!r.tallies || !Array.isArray(r.tallies)) return '-';
      if (idx >= 0 && idx < r.tallies.length) return r.tallies[idx];
      return '-';
    }
  };
}

