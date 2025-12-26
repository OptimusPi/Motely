import { LitElement, html, css } from 'https://unpkg.com/lit@3/index.js?module';

const apiBase = '';

class MotelyLitApp extends LitElement {
  static styles = css``; // use global CSS

  static properties = {
    filterJaml: { type: String },
    seedSource: { type: String },
    seedCount: { type: String },
    seedSources: { type: Array },
    currentSearchId: { type: String },
    searchStatus: { type: String },
    isSearching: { type: Boolean },
    results: { type: Array },
    columns: { type: Array },
    progressPercent: { type: Number },
    seedsSearched: { type: Number },
    seedsPerSecond: { type: Number },
    lastError: { type: String },
    connected: { type: Boolean },
  };

  constructor() {
    super();
    this.filterJaml = '';
    this.seedSource = 'all';
    this.seedCount = '';
    this.seedSources = [];
    this.currentSearchId = null;
    this.searchStatus = null;
    this.isSearching = false;
    this.results = [];
    this.columns = ['seed','score'];
    this.progressPercent = 0;
    this.seedsSearched = 0;
    this.seedsPerSecond = 0;
    this.lastError = null;
    this.connected = false;
    this.splitPosition = parseFloat(localStorage.getItem('splitPositionLit')) || 50;
  }

  firstUpdated() {
    this.updateSplit();
    this.fetchSeedSources();
    this.loadActive();
    this.setupSignalR();
    this.poll = setInterval(()=> this.currentSearchId && this.loadStatus(), 2000);
    window.addEventListener('resize', ()=>this.updateSplit());
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this.poll) clearInterval(this.poll);
  }

  updateSplit() {
    const left = this.renderRoot?.querySelector('.left-panel') || this.ownerDocument.querySelector('.left-panel');
    const right = this.renderRoot?.querySelector('.right-panel') || this.ownerDocument.querySelector('.right-panel');
    if (!left || !right) return;
    left.style.flex = `0 0 ${this.splitPosition}%`;
    right.style.flex = `1 1 ${100 - this.splitPosition}%`;
  }

  startResize(e) {
    e.preventDefault();
    const container = this.renderRoot.querySelector('.split-container');
    const isMobile = window.innerWidth <= 768;
    let startPos = isMobile ? (e.touches ? e.touches[0].clientY : e.clientY) : (e.touches ? e.touches[0].clientX : e.clientX);
    let containerSize = isMobile ? container.offsetHeight : container.offsetWidth;
    const move = (ev) => {
      ev.preventDefault();
      const cur = isMobile ? (ev.touches ? ev.touches[0].clientY : ev.clientY) : (ev.touches ? ev.touches[0].clientX : ev.clientX);
      const delta = cur - startPos;
      const pct = (delta / containerSize) * 100;
      this.splitPosition = Math.max(30, Math.min(70, this.splitPosition + pct));
      this.updateSplit();
      startPos = cur;
    };
    const stop = () => {
      localStorage.setItem('splitPositionLit', this.splitPosition.toString());
      document.removeEventListener('mousemove', move);
      document.removeEventListener('mouseup', stop);
      document.removeEventListener('touchmove', move);
      document.removeEventListener('touchend', stop);
    };
    document.addEventListener('mousemove', move);
    document.addEventListener('mouseup', stop);
    document.addEventListener('touchmove', move, { passive:false });
    document.addEventListener('touchend', stop);
  }

  async fetchSeedSources() {
    try {
      const res = await fetch(`${apiBase}/seed-sources`);
      if (res.ok) {
        this.seedSources = await res.json();
        if (this.seedSources.length && !this.seedSource) this.seedSource = this.seedSources[0].key || 'all';
      }
    } catch {}
  }

  async loadActive() {
    try {
      const res = await fetch(`${apiBase}/searches/active`);
      if (res.ok) {
        const data = await res.json();
        if (data.searches?.length) {
          this.currentSearchId = data.searches[0].id;
          await this.loadStatus();
        }
      }
    } catch {}
  }

  setupSignalR() {
    if (!window.signalR?.HubConnectionBuilder) return;
    this.conn = new window.signalR.HubConnectionBuilder().withUrl('/searchHub').withAutomaticReconnect().build();
    this.conn.on('Result', (data) => {
      try {
        const msg = typeof data === 'string' ? JSON.parse(data) : data;
        if (msg.searchId !== this.currentSearchId) return;
        if (msg.results?.length) {
          const map = new Map(this.results.map(r=>[r.seed,r]));
          msg.results.forEach(r=>{ if(!map.has(r.seed)) map.set(r.seed,r); });
          this.results = Array.from(map.values()).sort((a,b)=>(b.score||0)-(a.score||0));
        }
        if (msg.columns) this.columns = msg.columns;
        if (msg.progressPercent !== undefined) this.progressPercent = msg.progressPercent;
        if (msg.seedsSearched !== undefined) this.seedsSearched = msg.seedsSearched;
        if (msg.seedsPerSecond !== undefined) this.seedsPerSecond = msg.seedsPerSecond;
      } catch(err){ console.error(err); }
    });
    this.conn.start().then(()=>{ this.connected=true; }).catch(console.error);
  }

  async loadStatus() {
    try {
      const res = await fetch(`${apiBase}/search?id=${encodeURIComponent(this.currentSearchId)}`);
      if (res.ok) {
        const data = await res.json();
        this.searchStatus = data.status || data.searchStatus || 'stopped';
        this.isSearching = this.searchStatus === 'running';
        if (data.progressPercent !== undefined) this.progressPercent = data.progressPercent;
        if (data.seedsSearched !== undefined) this.seedsSearched = data.seedsSearched;
        if (data.seedsPerSecond !== undefined) this.seedsPerSecond = data.seedsPerSecond;
        this.lastError = data.lastError || null;
        if (Array.isArray(data.results)) {
          const map = new Map(this.results.map(r=>[r.seed,r]));
          data.results.forEach(r=>{ if(!map.has(r.seed)) map.set(r.seed,r); });
          this.results = Array.from(map.values()).sort((a,b)=>(b.score||0)-(a.score||0));
        }
        if (data.columns) this.columns = data.columns;
        if (data.filterJaml && !this.filterJaml) this.filterJaml = data.filterJaml;
      }
    } catch {}
  }

  async startSearch() {
    if (!this.filterJaml.trim()) return;
    try {
      this.isSearching = true; this.lastError = null;
      const res = await fetch(`${apiBase}/search`, {
        method:'POST', headers:{'Content-Type':'application/json'},
        body: JSON.stringify({ filterJaml: this.filterJaml.trim(), seedSource: this.seedSource || 'all', seedCount: this.seedCount || null })
      });
      if (!res.ok) throw new Error((await res.json()).error || 'Failed');
      const data = await res.json();
      this.currentSearchId = data.searchId;
      this.searchStatus = 'running';
      this.progressPercent = data.progressPercent || 0;
      this.results = Array.isArray(data.results) ? data.results : [];
      this.columns = data.columns || ['seed','score'];
      this.results = this.results.sort((a,b)=>(b.score||0)-(a.score||0));
      if (this.conn && this.conn.state === window.signalR.HubConnectionState.Connected) {
        await this.conn.invoke('JoinSearchGroup', this.currentSearchId);
      }
      await this.loadStatus();
    } catch(err){ this.lastError = err.message; this.isSearching=false; }
  }

  async stopSearch() {
    if (!this.currentSearchId) return;
    try {
      const res = await fetch(`${apiBase}/search/${encodeURIComponent(this.currentSearchId)}/panic-stop`, { method:'POST' });
      if (!res.ok) throw new Error((await res.json()).error || 'Failed');
      this.isSearching = false; this.searchStatus = 'stopped';
      if (this.conn && this.conn.state === window.signalR.HubConnectionState.Connected) {
        await this.conn.invoke('LeaveSearchGroup', this.currentSearchId);
      }
      await this.loadStatus();
    } catch(err){ this.lastError = err.message; }
  }

  clearResults() { this.results = []; this.progressPercent = 0; this.seedsSearched = 0; this.seedsPerSecond = 0; }

  exportCsv() {
    if (!this.results.length) return;
    const headers = this.columns.join(',');
    const rows = this.results.map(r=>{
      const vals=[]; vals.push(`"${r.seed||''}"`); vals.push(r.score||0); (r.tallies||[]).forEach(t=>vals.push(t)); return vals.join(',');
    });
    const csv=[headers,...rows].join('\\n');
    const blob=new Blob([csv],{type:'text/csv'}); const url=URL.createObjectURL(blob);
    const a=document.createElement('a'); a.href=url; a.download=`motely-results-${Date.now()}.csv`; a.click(); URL.revokeObjectURL(url);
  }

  formatNumber(n){ return n==null ? '0' : n.toLocaleString(); }
  getTally(r, idx){ return (r.tallies && r.tallies[idx]!==undefined) ? r.tallies[idx] : '-'; }

  render() {
    return html`
    <div class="app-layout">
      <div class="top-bar">
        <h1>🔍 Motely Search (Lit)</h1>
        <div class="top-bar-actions">
          ${this.connected ? html`<span class="status-indicator">🟢</span>` : html`<span class="status-indicator">🔴</span>`}
        </div>
      </div>
      <div class="split-container">
        <div class="left-panel">
          <div class="panel-header"><span class="panel-tab panel-tab-red">JAML Filter</span></div>
          <div class="panel-content">
            <textarea class="jaml-editor" .value=${this.filterJaml} @input=${e=>this.filterJaml = e.target.value} placeholder="Enter your JAML filter configuration..." spellcheck="false"></textarea>
            <div class="controls-section">
              <div class="control-row">
                <label>Seed Source:</label>
                <select class="control-select" .value=${this.seedSource} @change=${e=>this.seedSource = e.target.value}>
                  ${this.seedSources.map(src => html`<option value=${src.key}>${src.label||src.displayName||src.key}</option>`)}
                </select>
              </div>
              <div class="control-row">
                <label>Max Seeds:</label>
                <input class="control-input" type="number" min="0" placeholder="All" .value=${this.seedCount} @input=${e=>this.seedCount = e.target.value} />
              </div>
            </div>
            <div class="button-row">
              <button class="btn btn-primary" ?disabled=${this.isSearching || !this.filterJaml.trim()} @click=${()=>this.startSearch()}>${this.isSearching ? '⏸ Searching...' : '▶ Start'}</button>
              <button class="btn btn-danger" ?disabled=${!this.currentSearchId} @click=${()=>this.stopSearch()}>⏹ Stop</button>
            </div>
            ${this.currentSearchId ? html`
              <div class="status-section">
                <div class="status-row"><span class="status-label">Status:</span><span class="status-value ${this.searchStatus==='running'?'status-running':''}">${this.searchStatus || 'Idle'}</span></div>
                <div class="status-row"><span class="status-label">Progress:</span><span class="status-value">${this.progressPercent.toFixed(1)}%</span></div>
                <div class="status-row"><span class="status-label">Searched:</span><span class="status-value">${this.formatNumber(this.seedsSearched)}</span></div>
                <div class="status-row"><span class="status-label">Found:</span><span class="status-value">${this.formatNumber(this.results.length)}</span></div>
                <div class="status-row"><span class="status-label">Speed:</span><span class="status-value">${this.seedsPerSecond>0? this.formatNumber(this.seedsPerSecond)+'/s':'-'}</span></div>
                ${this.lastError ? html`<div class="error-box"><strong>Error:</strong> ${this.lastError}</div>` : ''}
              </div>` : ''}
          </div>
        </div>
        <div class="splitter" @mousedown=${e=>this.startResize(e)} @touchstart=${e=>this.startResize(e)}><div class="splitter-handle"></div></div>
        <div class="right-panel">
          <div class="panel-header">
            <span class="panel-tab panel-tab-purple">Results <span class="badge">${this.results.length}</span></span>
            <div class="panel-actions">
              <button class="btn-small" ?disabled=${this.results.length===0} @click=${()=>this.clearResults()}>🗑️ Clear</button>
              <button class="btn-small" ?disabled=${this.results.length===0} @click=${()=>this.exportCsv()}>📥 Export</button>
            </div>
          </div>
          <div class="panel-content">
            ${this.results.length===0 ? html`<div class="empty-state">No results yet. Start a search to see results here.</div>` : html`
              <div class="table-wrapper">
                <table class="results-table">
                  <thead><tr><th>#</th>${this.columns.map(c=>html`<th>${c}</th>`)}</tr></thead>
                  <tbody>
                    ${this.results.map((r,idx)=> html`
                      <tr>
                        <td class="row-number">${idx+1}</td>
                        ${this.columns.map((c,i)=> html`
                          <td class=${c==='seed'?'seed-cell':c==='score'?'score-cell':''}>
                            ${c==='seed' ? html`<code>${r.seed}</code>` : c==='score' ? (r.score||0) : this.getTally(r,i-2)}
                          </td>`)}
                      </tr>
                    `)}
                  </tbody>
                </table>
              </div>
            `}
          </div>
        </div>
      </div>
    </div>
    `;
  }
}

customElements.define('motely-lit-app', MotelyLitApp);

