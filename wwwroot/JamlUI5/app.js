import { html, render, useState, useEffect, useRef } from 'https://unpkg.com/htm@3.1.1/preact/standalone.module.js';

const apiBase = '';

function App() {
  const [filterJaml, setFilterJaml] = useState('');
  const [seedSource, setSeedSource] = useState('all');
  const [seedCount, setSeedCount] = useState('');
  const [seedSources, setSeedSources] = useState([]);
  const [currentSearchId, setCurrentSearchId] = useState(null);
  const [searchStatus, setSearchStatus] = useState(null);
  const [isSearching, setIsSearching] = useState(false);
  const [results, setResults] = useState([]);
  const [columns, setColumns] = useState(['seed','score']);
  const [progressPercent, setProgressPercent] = useState(0);
  const [seedsSearched, setSeedsSearched] = useState(0);
  const [seedsPerSecond, setSeedsPerSecond] = useState(0);
  const [lastError, setLastError] = useState(null);
  const connectionRef = useRef(null);
  const pollRef = useRef(null);
  const splitRef = useRef(parseFloat(localStorage.getItem('splitPositionReact')) || 50);

  const updateSplit = () => {
    const left = document.querySelector('.left-panel');
    const right = document.querySelector('.right-panel');
    if (!left || !right) return;
    const pct = splitRef.current;
    left.style.flex = `0 0 ${pct}%`;
    right.style.flex = `1 1 ${100 - pct}%`;
  };

  const startResize = (e) => {
    e.preventDefault();
    const container = document.querySelector('.split-container');
    const isMobile = window.innerWidth <= 768;
    let startPos = isMobile ? (e.touches ? e.touches[0].clientY : e.clientY) : (e.touches ? e.touches[0].clientX : e.clientX);
    let containerSize = isMobile ? container.offsetHeight : container.offsetWidth;
    const move = (ev) => {
      ev.preventDefault();
      const cur = isMobile ? (ev.touches ? ev.touches[0].clientY : ev.clientY) : (ev.touches ? ev.touches[0].clientX : ev.clientX);
      const delta = cur - startPos;
      const pct = (delta / containerSize) * 100;
      splitRef.current = Math.max(30, Math.min(70, splitRef.current + pct));
      updateSplit();
      startPos = cur;
    };
    const stop = () => {
      localStorage.setItem('splitPositionReact', splitRef.current.toString());
      document.removeEventListener('mousemove', move);
      document.removeEventListener('mouseup', stop);
      document.removeEventListener('touchmove', move);
      document.removeEventListener('touchend', stop);
    };
    document.addEventListener('mousemove', move);
    document.addEventListener('mouseup', stop);
    document.addEventListener('touchmove', move, { passive:false });
    document.addEventListener('touchend', stop);
  };

  useEffect(() => { updateSplit(); }, []);

  useEffect(() => {
    const fetchSeeds = async () => {
      try {
        const res = await fetch(`${apiBase}/seed-sources`);
        if (res.ok) {
          const data = await res.json();
          setSeedSources(data || []);
          if (data?.length && !seedSource) setSeedSource(data[0].key || 'all');
        }
      } catch {}
    };
    const loadActive = async () => {
      try {
        const res = await fetch(`${apiBase}/searches/active`);
        if (res.ok) {
          const data = await res.json();
          if (data.searches?.length) {
            setCurrentSearchId(data.searches[0].id);
            await loadStatus(data.searches[0].id);
          }
        }
      } catch {}
    };
    const setupSignalR = () => {
      if (!window.signalR?.HubConnectionBuilder) return;
      const conn = new window.signalR.HubConnectionBuilder().withUrl('/searchHub').withAutomaticReconnect().build();
      connectionRef.current = conn;
      conn.on('Result', (data) => {
        try {
          const msg = typeof data === 'string' ? JSON.parse(data) : data;
          if (msg.searchId !== currentSearchId) return;
          setResults(prev => {
            const map = new Map(prev.map(r=>[r.seed,r]));
            (msg.results||[]).forEach(r => { if (!map.has(r.seed)) map.set(r.seed,r); });
            return Array.from(map.values()).sort((a,b)=>(b.score||0)-(a.score||0));
          });
          if (msg.columns) setColumns(msg.columns);
          if (msg.progressPercent !== undefined) setProgressPercent(msg.progressPercent);
          if (msg.seedsSearched !== undefined) setSeedsSearched(msg.seedsSearched);
          if (msg.seedsPerSecond !== undefined) setSeedsPerSecond(msg.seedsPerSecond);
        } catch(err){ console.error(err); }
      });
      conn.start().catch(console.error);
    };
    fetchSeeds(); loadActive(); setupSignalR();
    pollRef.current = setInterval(()=> currentSearchId && loadStatus(currentSearchId),2000);
    return () => { if (pollRef.current) clearInterval(pollRef.current); };
  }, [currentSearchId]);

  const loadStatus = async (id) => {
    try {
      const res = await fetch(`${apiBase}/search?id=${encodeURIComponent(id)}`);
      if (res.ok) {
        const data = await res.json();
        setSearchStatus(data.status || data.searchStatus || 'stopped');
        setIsSearching((data.status||data.searchStatus)==='running');
        if (data.progressPercent !== undefined) setProgressPercent(data.progressPercent);
        if (data.seedsSearched !== undefined) setSeedsSearched(data.seedsSearched);
        if (data.seedsPerSecond !== undefined) setSeedsPerSecond(data.seedsPerSecond);
        setLastError(data.lastError || null);
        if (Array.isArray(data.results)) {
          setResults(prev => {
            const map = new Map(prev.map(r=>[r.seed,r]));
            data.results.forEach(r=>{ if(!map.has(r.seed)) map.set(r.seed,r); });
            return Array.from(map.values()).sort((a,b)=>(b.score||0)-(a.score||0));
          });
        }
        if (data.columns) setColumns(data.columns);
        if (data.filterJaml && !filterJaml) setFilterJaml(data.filterJaml);
      }
    } catch {}
  };

  const startSearch = async () => {
    if (!filterJaml.trim()) return;
    try {
      setIsSearching(true); setLastError(null);
      const res = await fetch(`${apiBase}/search`, {
        method:'POST', headers:{'Content-Type':'application/json'},
        body: JSON.stringify({ filterJaml: filterJaml.trim(), seedSource: seedSource || 'all', seedCount: seedCount || null })
      });
      if (!res.ok) throw new Error((await res.json()).error || 'Failed');
      const data = await res.json();
      setCurrentSearchId(data.searchId);
      setSearchStatus('running');
      setProgressPercent(data.progressPercent || 0);
      setResults(Array.isArray(data.results)? data.results: []);
      setColumns(data.columns || ['seed','score']);
      setResults(prev=> prev.sort((a,b)=>(b.score||0)-(a.score||0)));
      if (connectionRef.current && connectionRef.current.state === window.signalR.HubConnectionState.Connected) {
        await connectionRef.current.invoke('JoinSearchGroup', data.searchId);
      }
      await loadStatus(data.searchId);
    } catch(err){ setLastError(err.message); setIsSearching(false); }
  };

  const stopSearch = async () => {
    if (!currentSearchId) return;
    try {
      const res = await fetch(`${apiBase}/search/${encodeURIComponent(currentSearchId)}/panic-stop`, { method:'POST' });
      if (!res.ok) throw new Error((await res.json()).error || 'Failed');
      setIsSearching(false); setSearchStatus('stopped');
      if (connectionRef.current && connectionRef.current.state === window.signalR.HubConnectionState.Connected) {
        await connectionRef.current.invoke('LeaveSearchGroup', currentSearchId);
      }
      await loadStatus(currentSearchId);
    } catch(err){ setLastError(err.message); }
  };

  const clearResults = () => { setResults([]); setProgressPercent(0); setSeedsSearched(0); setSeedsPerSecond(0); };

  const exportCsv = () => {
    if (!results.length) return;
    const headers = columns.join(',');
    const rows = results.map(r=>{
      const vals=[]; vals.push(`"${r.seed||''}"`); vals.push(r.score||0); (r.tallies||[]).forEach(t=>vals.push(t)); return vals.join(',');
    });
    const csv=[headers,...rows].join('\\n');
    const blob=new Blob([csv],{type:'text/csv'}); const url=URL.createObjectURL(blob);
    const a=document.createElement('a'); a.href=url; a.download=`motely-results-${Date.now()}.csv`; a.click(); URL.revokeObjectURL(url);
  };

  const formatNumber = (n)=> n==null ? '0' : n.toLocaleString();
  const getTally = (r, idx)=> (r.tallies && r.tallies[idx]!==undefined ? r.tallies[idx] : '-');

  return html`
  <div class="app-layout">
    <div class="top-bar">
      <h1>🔍 Motely Search (React/Preact)</h1>
      <div class="top-bar-actions">
        ${connectionRef.current && connectionRef.current.state === window.signalR?.HubConnectionState?.Connected ? html`<span class="status-indicator">🟢</span>` : html`<span class="status-indicator">🔴</span>`}
      </div>
    </div>
    <div class="split-container">
      <div class="left-panel">
        <div class="panel-header">
          <span class="panel-tab panel-tab-red">JAML Filter</span>
        </div>
        <div class="panel-content">
          <textarea class="jaml-editor" placeholder="Enter your JAML filter configuration..." value=${filterJaml} onInput=${e=>setFilterJaml(e.target.value)} />
          <div class="controls-section">
            <div class="control-row">
              <label>Seed Source:</label>
              <select class="control-select" value=${seedSource} onChange=${e=>setSeedSource(e.target.value)}>
                ${seedSources.map(src => html`<option key=${src.key} value=${src.key}>${src.label || src.displayName || src.key}</option>`)}
              </select>
            </div>
            <div class="control-row">
              <label>Max Seeds:</label>
              <input class="control-input" type="number" min="0" placeholder="All" value=${seedCount} onInput=${e=>setSeedCount(e.target.value)} />
            </div>
          </div>
          <div class="button-row">
            <button class="btn btn-primary" disabled=${isSearching || !filterJaml.trim()} onClick=${startSearch}>${!isSearching ? '▶ Start' : '⏸ Searching...'}</button>
            <button class="btn btn-danger" disabled=${!currentSearchId} onClick=${stopSearch}>⏹ Stop</button>
          </div>
          ${currentSearchId && html`
            <div class="status-section">
              <div class="status-row"><span class="status-label">Status:</span><span class="status-value ${searchStatus==='running'?'status-running':''}">${searchStatus || 'Idle'}</span></div>
              <div class="status-row"><span class="status-label">Progress:</span><span class="status-value">${progressPercent.toFixed(1)}%</span></div>
              <div class="status-row"><span class="status-label">Searched:</span><span class="status-value">${formatNumber(seedsSearched)}</span></div>
              <div class="status-row"><span class="status-label">Found:</span><span class="status-value">${formatNumber(results.length)}</span></div>
              <div class="status-row"><span class="status-label">Speed:</span><span class="status-value">${seedsPerSecond>0? formatNumber(seedsPerSecond)+'/s':'-'}</span></div>
              ${lastError && html`<div class="error-box"><strong>Error:</strong> ${lastError}</div>`}
            </div>
          `}
        </div>
      </div>
      <div class="splitter" onMouseDown=${startResize} onTouchStart=${startResize}><div class="splitter-handle"></div></div>
      <div class="right-panel">
        <div class="panel-header">
          <span class="panel-tab panel-tab-purple">Results <span class="badge">${results.length}</span></span>
          <div class="panel-actions">
            <button class="btn-small" disabled=${results.length===0} onClick=${clearResults}>🗑️ Clear</button>
            <button class="btn-small" disabled=${results.length===0} onClick=${exportCsv}>📥 Export</button>
          </div>
        </div>
        <div class="panel-content">
          ${results.length===0 ? html`<div class="empty-state">No results yet. Start a search to see results here.</div>` : html`
            <div class="table-wrapper">
              <table class="results-table">
                <thead><tr><th>#</th>${columns.map((c,i)=> html`<th key=${i}>${c}</th>`)}</tr></thead>
                <tbody>
                  ${results.map((r,idx)=> html`
                    <tr key=${r.seed || idx}>
                      <td class="row-number">${idx+1}</td>
                      ${columns.map((c,i)=> html`
                        <td class=${c==='seed'?'seed-cell':c==='score'?'score-cell':''}>
                          ${c==='seed' ? html`<code>${r.seed}</code>` :
                           c==='score' ? (r.score||0) :
                           getTally(r, i-2)}
                        </td>`)}
                    </tr>`)}
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

render(html`<${App} />`, document.getElementById('app'));

