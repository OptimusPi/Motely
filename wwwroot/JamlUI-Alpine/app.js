function app() {
  return {
    isPortrait: false,
    filterJaml: '',
    selectedFilter: '',
    availableFilters: [],
    seedSource: 'all',
    seedCount: 0,
    startBatch: 0,
    cutoff: 0,
    blueprintSeed: '',
    searchResults: [],
    columns: ['seed', 'score'],
    sortColumn: '',
    sortDirection: 'asc',
    progressPercent: 0,
    isSearching: false,
    currentSearchId: '',
    connection: null,

    // Panel management
    draggedPanel: null,
    panelOrder: ['jamlPanel', 'blueprintPanel', 'resultsPanel'],

    // Vertical splitter state
    splitterPosition: 50, // percentage (50 = center)

    init() {
      // Initialize with default values
      this.selectedFilter = '';
      this.availableFilters = [];
      this.blueprintSeed = '';
      this.searchResults = [];
      this.columns = ['seed', 'score'];
      this.sortColumn = '';
      this.sortDirection = 'asc';
      this.progressPercent = 0;
      this.isSearching = false;
      this.currentSearchId = '';
      this.connection = null;
      this.splitterPosition = 50;

      this.checkOrientation();
      this.loadFilters();
      this.loadPanelState();
      this.loadSplitterState();
      this.setupPanelDragAndDrop();
      this.setupVerticalSplitter();
      this.setupSignalR();
      window.addEventListener('resize', () => this.checkOrientation());
    },

    checkOrientation() {
      this.isPortrait = window.innerHeight > window.innerWidth;
    },

    loadFilters() {
      fetch('/routes')
        .then(r => r.json())
        .then(() => {
          this.availableFilters = [
            { id: 'basic', name: 'Basic Filter' },
            { id: 'advanced', name: 'Advanced Filter' }
          ];
        })
        .catch(err => console.error('Failed to load routes:', err));
    },

    setupPanelDragAndDrop() {
      // Panel drag handles
      const dragHandles = ['jamlDragHandle', 'blueprintDragHandle', 'resultsDragHandle'];

      dragHandles.forEach(handleId => {
        const handle = document.getElementById(handleId);
        if (handle) {
          this.setupDragHandle(handle);
        }
      });
    },

    setupDragHandle(handle) {
      let isDragging = false;
      let dragOffset = { x: 0, y: 0 };
      let panelElement = null;

      const startDrag = (e) => {
        if (e.target !== handle && !handle.contains(e.target)) return;

        isDragging = true;
        panelElement = handle.closest('.panel');

        if (!panelElement) return;

        const rect = panelElement.getBoundingClientRect();
        dragOffset.x = e.clientX - rect.left;
        dragOffset.y = e.clientY - rect.top;

        panelElement.classList.add('dragging');
        panelElement.style.position = 'fixed';
        panelElement.style.zIndex = '1000';
        panelElement.style.width = rect.width + 'px';

        document.body.style.cursor = 'grabbing';
        document.body.style.userSelect = 'none';

        e.preventDefault();
      };

      const drag = (e) => {
        if (!isDragging || !panelElement) return;

        const x = e.clientX - dragOffset.x;
        const y = e.clientY - dragOffset.y;

        panelElement.style.left = x + 'px';
        panelElement.style.top = y + 'px';

        // Find drop target
        this.updateDropTarget(e.clientY);
      };

      const endDrag = () => {
        if (!isDragging || !panelElement) return;

        isDragging = false;
        panelElement.classList.remove('dragging');
        panelElement.style.position = '';
        panelElement.style.zIndex = '';
        panelElement.style.width = '';
        panelElement.style.left = '';
        panelElement.style.top = '';

        document.body.style.cursor = '';
        document.body.style.userSelect = '';

        // Complete the drop
        this.completeDrop(panelElement);
        this.savePanelState();
      };

      handle.addEventListener('mousedown', startDrag);
      document.addEventListener('mousemove', drag);
      document.addEventListener('mouseup', endDrag);
    },

    updateDropTarget(mouseY) {
      const panels = Array.from(document.querySelectorAll('.panel:not(.dragging)'));
      const container = document.getElementById('panelsContainer');

      // Remove existing drop indicators
      container.querySelectorAll('.drop-indicator').forEach(el => el.remove());

      if (panels.length === 0) return;

      // Find the closest panel to drop before/after
      let closestPanel = null;
      let closestDistance = Infinity;
      let insertBefore = false;

      panels.forEach(panel => {
        const rect = panel.getBoundingClientRect();
        const panelCenter = rect.top + rect.height / 2;
        const distance = Math.abs(mouseY - panelCenter);

        if (distance < closestDistance) {
          closestDistance = distance;
          closestPanel = panel;
          insertBefore = mouseY < panelCenter;
        }
      });

      if (closestPanel) {
        const indicator = document.createElement('div');
        indicator.className = 'drop-indicator';
        indicator.style.height = '4px';
        indicator.style.background = 'var(--blue)';
        indicator.style.borderRadius = '2px';
        indicator.style.margin = '4px 0';

        if (insertBefore) {
          closestPanel.before(indicator);
        } else {
          closestPanel.after(indicator);
        }
      }
    },

    completeDrop(draggedPanel) {
      const indicator = document.querySelector('.drop-indicator');
      const container = document.getElementById('panelsContainer');

      if (indicator) {
        // Move the panel to the indicator position
        indicator.replaceWith(draggedPanel);
      } else {
        // Fallback: append to end
        container.appendChild(draggedPanel);
      }

      // Update panel order
      this.updatePanelOrder();
    },

    updatePanelOrder() {
      const panels = Array.from(document.querySelectorAll('.panel'));
      this.panelOrder = panels.map(panel => panel.id);
    },

    togglePanel(panelType) {
      const panel = document.getElementById(`${panelType}Panel`);
      if (!panel) return;

      panel.classList.toggle('minimized');
      this.savePanelState();
    },

    loadPanelState() {
      const saved = localStorage.getItem('jaml-panels-state');
      if (saved) {
        try {
          const state = JSON.parse(saved);
          this.panelOrder = state.panelOrder || this.panelOrder;

          // Apply minimized states
          if (state.minimized) {
            Object.entries(state.minimized).forEach(([panelId, isMinimized]) => {
              const panel = document.getElementById(panelId);
              if (panel && isMinimized) {
                panel.classList.add('minimized');
              }
            });
          }

          // Reorder panels
          this.reorderPanels();
        } catch (e) {
          console.warn('Failed to load panel state:', e);
        }
      }
    },

    reorderPanels() {
      const container = document.getElementById('panelsContainer');
      if (!container) return;

      // Clear container
      const panels = Array.from(container.children);

      // Reorder based on saved order
      this.panelOrder.forEach(panelId => {
        const panel = panels.find(p => p.id === panelId);
        if (panel) {
          container.appendChild(panel);
        }
      });
    },

    savePanelState() {
      const state = {
        panelOrder: this.panelOrder,
        minimized: {}
      };

      // Save minimized states
      document.querySelectorAll('.panel').forEach(panel => {
        state.minimized[panel.id] = panel.classList.contains('minimized');
      });

      localStorage.setItem('jaml-panels-state', JSON.stringify(state));
    },

    setupVerticalSplitter() {
      const splitter = document.getElementById('verticalSplitter');
      if (!splitter) return;

      let isDragging = false;
      let startX = 0;
      let startPosition = this.splitterPosition;

      const startDrag = (e) => {
        isDragging = true;
        startX = e.clientX;
        startPosition = this.splitterPosition;
        document.body.style.cursor = 'ew-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
      };

      const drag = (e) => {
        if (!isDragging) return;

        const deltaX = e.clientX - startX;
        const windowWidth = window.innerWidth;
        const deltaPercent = (deltaX / windowWidth) * 100;
        let newPosition = startPosition + deltaPercent;

        // Snap to edges (10% threshold)
        if (newPosition <= 10) {
          newPosition = 0; // Full left
        } else if (newPosition >= 90) {
          newPosition = 100; // Full right
        } else {
          // Clamp between 20% and 80%
          newPosition = Math.max(20, Math.min(80, newPosition));
        }

        this.splitterPosition = newPosition;
        this.updateSplitterPosition();
      };

      const endDrag = () => {
        if (isDragging) {
          isDragging = false;
          document.body.style.cursor = '';
          document.body.style.userSelect = '';
          this.saveSplitterState();

          // Snap to nearest edge if close
          if (this.splitterPosition <= 15) {
            this.splitterPosition = 0;
          } else if (this.splitterPosition >= 85) {
            this.splitterPosition = 100;
          }
          this.updateSplitterPosition();
        }
      };

      splitter.addEventListener('mousedown', startDrag);
      document.addEventListener('mousemove', drag);
      document.addEventListener('mouseup', endDrag);
    },

    updateSplitterPosition() {
      const splitter = document.getElementById('verticalSplitter');
      if (splitter) {
        splitter.style.left = `${this.splitterPosition}%`;
      }
    },

    loadSplitterState() {
      const saved = localStorage.getItem('jaml-splitter-state');
      if (saved) {
        try {
          const state = JSON.parse(saved);
          this.splitterPosition = state.position || 50;
          this.updateSplitterPosition();
        } catch (e) {
          console.warn('Failed to load splitter state:', e);
        }
      }
    },

    saveSplitterState() {
      const state = { position: this.splitterPosition };
      localStorage.setItem('jaml-splitter-state', JSON.stringify(state));
    },

    goHome() {
      window.location.href = '/';
    },

    toggleSettings() {
      console.log('Settings toggle placeholder');
    },

    startSearch() {
      if (!this.filterJaml.trim()) return;

      this.isSearching = true;
      this.searchResults = [];
      this.progressPercent = 0;

      setTimeout(() => {
        this.searchResults = [
          { seed: 'ABC123', score: 1500 },
          { seed: 'DEF456', score: 1200 },
          { seed: 'GHI789', score: 1800 }
        ];
        this.isSearching = false;
        this.progressPercent = 100;
      }, 2000);
    },

    stopSearch() {
      this.isSearching = false;
      this.currentSearchId = '';
      this.progressPercent = 0;
    },

    sortBy(column) {
      if (this.sortColumn === column) {
        this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
      } else {
        this.sortColumn = column;
        this.sortDirection = 'asc';
      }

      this.searchResults.sort((a, b) => {
        let aVal = a[column];
        let bVal = b[column];

        if (typeof aVal === 'number' && typeof bVal === 'number') {
          return this.sortDirection === 'asc' ? aVal - bVal : bVal - aVal;
        }

        aVal = String(aVal).toLowerCase();
        bVal = String(bVal).toLowerCase();

        if (this.sortDirection === 'asc') {
          return aVal < bVal ? -1 : aVal > bVal ? 1 : 0;
        } else {
          return aVal > bVal ? -1 : aVal < bVal ? 1 : 0;
        }
      });
    },

    exportResults() {
      if (this.searchResults.length === 0) return;

      const csv = [
        this.columns.join(','),
        ...this.searchResults.map(row => this.columns.map(col => row[col]).join(','))
      ].join('\n');

      const blob = new Blob([csv], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'search-results.csv';
      a.click();
      URL.revokeObjectURL(url);
    },

    clearResults() {
      if (confirm('Clear all search results?')) {
        this.searchResults = [];
        this.progressPercent = 0;
      }
    },

    analyzeBlueprint() {
      if (!this.blueprintSeed.trim()) return;

      const iframe = document.querySelector('.blueprint-iframe');
      if (iframe) {
        const blueprintUrl = `https://miaklwalker.github.io/Blueprint/?seed=${encodeURIComponent(this.blueprintSeed)}`;
        iframe.src = blueprintUrl;
      }
    },

    get paginatedResults() {
      return this.searchResults;
    }
  };
}
