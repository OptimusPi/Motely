class JAMLWorkspace {
    constructor() {
        this.panels = new Map();
        this.panelIdCounter = 0;
        this.draggedPanel = null;
        this.init();
    }

    init() {
        this.setupEventListeners();
        this.createDefaultPanels();
        this.updateStatus();
    }

    setupEventListeners() {
        const addPanelBtn = document.getElementById('add-panel');
        if (addPanelBtn) {
            addPanelBtn.addEventListener('click', () => this.showAddPanelDialog());
        }
        
        const resetLayoutBtn = document.getElementById('reset-layout');
        if (resetLayoutBtn) {
            resetLayoutBtn.addEventListener('click', () => this.resetLayout());
        }
        
        const panelsContainer = document.getElementById('panels');
        if (panelsContainer) {
            panelsContainer.addEventListener('dragover', this.handleDragOver.bind(this));
            panelsContainer.addEventListener('drop', this.handleDrop.bind(this));
        }
    }

    createDefaultPanels() {
        this.createPanel('editor', 'JAML Editor');
        this.createPanel('preview', 'Preview');
        this.createPanel('properties', 'Properties');
    }

    createPanel(type, title) {
        const panelId = `panel-${this.panelIdCounter++}`;
        const panel = this.createPanelElement(panelId, type, title);
        
        this.panels.set(panelId, {
            id: panelId,
            type: type,
            title: title,
            element: panel,
            minimized: false,
            maximized: false
        });

        document.getElementById('panels').appendChild(panel);
        this.setupPanelDragAndDrop(panel);
        this.updatePanelCount();
        return panelId;
    }

    createPanelElement(panelId, type, title) {
        const panel = document.createElement('div');
        panel.className = `panel ${type}`;
        panel.id = panelId;
        panel.draggable = true;
        
        panel.innerHTML = `
            <div class="panel-header">
                <span class="panel-title">${title}</span>
                <div class="panel-controls">
                    <button class="panel-btn minimize" title="Minimize">−</button>
                    <button class="panel-btn maximize" title="Maximize">□</button>
                    <button class="panel-btn close" title="Close">×</button>
                </div>
            </div>
            <div class="panel-content" id="${panelId}-content">
                ${this.getPanelContent(type)}
            </div>
        `;

        // Add event listeners to panel controls
        const header = panel.querySelector('.panel-header');
        const minimizeBtn = panel.querySelector('.minimize');
        const maximizeBtn = panel.querySelector('.maximize');
        const closeBtn = panel.querySelector('.close');

        minimizeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleMinimize(panelId);
        });

        maximizeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleMaximize(panelId);
        });

        closeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.removePanel(panelId);
        });

        // Make header draggable
        header.addEventListener('mousedown', (e) => {
            if (e.target === header || header.contains(e.target)) {
                this.startDrag(panelId, e);
            }
        });

        return panel;
    }

    getPanelContent(type) {
        switch(type) {
            case 'editor':
                return '<textarea class="code-editor" placeholder="Enter JAML code here..."></textarea>';
            case 'preview':
                return '<div class="preview-content"></div>';
            case 'properties':
                return `
                    <div class="property-group">
                        <label>Property 1</label>
                        <input type="text" class="property-input">
                    </div>
                    <div class="property-group">
                        <label>Property 2</label>
                        <select class="property-select">
                            <option>Option 1</option>
                            <option>Option 2</option>
                        </select>
                    </div>
                `;
            default:
                return '<div class="panel-message">Panel content</div>';
        }
    }

    setupPanelDragAndDrop(panel) {
        panel.addEventListener('dragstart', (e) => {
            this.draggedPanel = panel;
            setTimeout(() => {
                panel.classList.add('dragging');
            }, 0);
        });

        panel.addEventListener('dragend', () => {
            this.draggedPanel = null;
            panel.classList.remove('dragging');
        });
    }

    handleDragOver(e) {
        e.preventDefault();
        const afterElement = this.getDragAfterElement(e.clientY);
        const panelsContainer = document.getElementById('panels');
        
        if (afterElement == null) {
            panelsContainer.appendChild(this.draggedPanel);
        } else {
            panelsContainer.insertBefore(this.draggedPanel, afterElement);
        }
    }

    handleDrop(e) {
        e.preventDefault();
    }

    getDragAfterElement(y) {
        const panels = [...document.querySelectorAll('.panel:not(.dragging)')];
        
        return panels.reduce((closest, child) => {
            const box = child.getBoundingClientRect();
            const offset = y - box.top - box.height / 2;
            
            if (offset < 0 && offset > closest.offset) {
                return { offset: offset, element: child };
            } else {
                return closest;
            }
        }, { offset: Number.NEGATIVE_INFINITY }).element;
    }

    toggleMinimize(panelId) {
        const panel = this.panels.get(panelId);
        if (!panel) return;

        panel.minimized = !panel.minimized;
        panel.element.classList.toggle('minimized', panel.minimized);
        
        // If maximizing, ensure it's not minimized
        if (panel.maximized && panel.minimized) {
            panel.maximized = false;
            panel.element.classList.remove('maximized');
        }
    }

    toggleMaximize(panelId) {
        const panel = this.panels.get(panelId);
        if (!panel) return;

        panel.maximized = !panel.maximized;
        panel.element.classList.toggle('maximized', panel.maximized);
        
        // If maximizing, ensure it's not minimized
        if (panel.maximized && panel.minimized) {
            panel.minimized = false;
            panel.element.classList.remove('minimized');
        }
    }

    removePanel(panelId) {
        const panel = this.panels.get(panelId);
        if (!panel) return;

        panel.element.remove();
        this.panels.delete(panelId);
        this.updatePanelCount();
    }

    showAddPanelDialog() {
        const panelTypes = [
            { id: 'editor', name: 'JAML Editor' },
            { id: 'preview', name: 'Preview' },
            { id: 'properties', name: 'Properties' },
            { id: 'console', name: 'Console' },
            { id: 'explorer', name: 'File Explorer' }
        ];

        const dialog = document.createElement('div');
        dialog.className = 'dialog-overlay';
        dialog.innerHTML = `
            <div class="dialog">
                <h3>Add New Panel</h3>
                <div class="dialog-content">
                    ${panelTypes.map(type => `
                        <div class="panel-type" data-type="${type.id}">
                            <strong>${type.name}</strong>
                            <span>${type.id}</span>
                        </div>
                    `).join('')}
                </div>
                <div class="dialog-actions">
                    <button id="cancel-dialog">Cancel</button>
                </div>
            </div>
        `;

        document.body.appendChild(dialog);

        // Add event listeners
        dialog.querySelectorAll('.panel-type').forEach(item => {
            item.addEventListener('click', (e) => {
                const type = e.currentTarget.dataset.type;
                const name = panelTypes.find(t => t.id === type)?.name || type;
                this.createPanel(type, name);
                dialog.remove();
            });
        });

        dialog.querySelector('#cancel-dialog').addEventListener('click', () => {
            dialog.remove();
        });
    }

    resetLayout() {
        if (confirm('Are you sure you want to reset the layout to default?')) {
            this.panels.forEach((panel, id) => {
                panel.element.remove();
                this.panels.delete(id);
            });
            this.createDefaultPanels();
            this.updateStatus('Layout has been reset');
        }
    }

    updateStatus(message) {
        const status = document.getElementById('status');
        if (message) {
            status.textContent = message;
            setTimeout(() => {
                status.textContent = 'Ready';
            }, 3000);
        }
    }

    updatePanelCount() {
        document.getElementById('panel-count').textContent = `${this.panels.size} panels`;
    }

    startDrag(panelId, e) {
        const panel = this.panels.get(panelId);
        if (!panel) return;

        const startX = e.clientX;
        const startY = e.clientY;
        const startLeft = panel.element.offsetLeft;
        const startTop = panel.element.offsetTop;

        function onMouseMove(e) {
            const dx = e.clientX - startX;
            const dy = e.clientY - startY;
            
            panel.element.style.left = `${startLeft + dx}px`;
            panel.element.style.top = `${startTop + dy}px`;
            panel.element.style.position = 'absolute';
        }

        function onMouseUp() {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
        }

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
    }
}

// Initialize the workspace when the DOM is fully loaded
document.addEventListener('DOMContentLoaded', () => {
    window.workspace = new JAMLWorkspace();
});