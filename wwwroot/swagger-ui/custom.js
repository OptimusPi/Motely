// Custom JavaScript for enhanced Scala/Swagger UI experience

window.addEventListener('load', function() {
    // Add performance metrics display
    const originalFetch = window.fetch;
    window.fetch = function(...args) {
        const start = performance.now();
        return originalFetch.apply(this, args).then(response => {
            const end = performance.now();
            const duration = Math.round(end - start);
            console.log(`API call took ${duration}ms`);
            
            // Show request duration in UI
            const durationElement = document.querySelector('.request-duration');
            if (durationElement) {
                durationElement.textContent = `${duration}ms`;
                durationElement.style.color = duration > 1000 ? '#f93e3e' : '#49cc90';
            }
            
            return response;
        });
    };

    // Add copy to clipboard functionality for API responses
    setTimeout(() => {
        const responses = document.querySelectorAll('.highlight-code');
        responses.forEach(response => {
            const button = document.createElement('button');
            button.textContent = '📋 Copy';
            button.style.cssText = `
                position: absolute;
                top: 8px;
                right: 8px;
                background: #61dafb;
                color: #1a1a1a;
                border: none;
                padding: 4px 8px;
                border-radius: 4px;
                cursor: pointer;
                font-size: 12px;
                font-weight: 600;
                z-index: 10;
            `;
            
            button.addEventListener('click', () => {
                navigator.clipboard.writeText(response.textContent);
                button.textContent = '✅ Copied!';
                setTimeout(() => button.textContent = '📋 Copy', 2000);
            });
            
            response.parentElement.style.position = 'relative';
            response.parentElement.appendChild(button);
        });
    }, 1000);

    // Add keyboard shortcuts
    document.addEventListener('keydown', function(e) {
        // Ctrl+K to focus search
        if (e.ctrlKey && e.key === 'k') {
            e.preventDefault();
            const searchInput = document.querySelector('input[placeholder="Filter by tag"]');
            if (searchInput) searchInput.focus();
        }
        
        // Ctrl+/ to expand/collapse all operations
        if (e.ctrlKey && e.key === '/') {
            e.preventDefault();
            const expandBtn = document.querySelector('.opblock-tag-section .expand-operation');
            if (expandBtn) expandBtn.click();
        }
    });

    // Add API endpoint badges
    const addBadges = () => {
        const operations = document.querySelectorAll('.opblock');
        operations.forEach(op => {
            const method = op.querySelector('.opblock-summary-method');
            if (method && !method.querySelector('.performance-badge')) {
                const badge = document.createElement('span');
                badge.className = 'performance-badge';
                badge.style.cssText = `
                    margin-left: 8px;
                    padding: 2px 6px;
                    border-radius: 12px;
                    font-size: 10px;
                    font-weight: 600;
                    background: #2d2d2d;
                    color: #ccc;
                `;
                
                // Add performance hints based on endpoint
                const path = op.querySelector('.opblock-summary-path');
                if (path) {
                    const pathText = path.textContent;
                    if (pathText.includes('search')) {
                        badge.textContent = 'SIMD';
                        badge.style.background = '#49cc90';
                        badge.style.color = 'white';
                    } else if (pathText.includes('filter')) {
                        badge.textContent = 'OPTIMIZED';
                        badge.style.background = '#fca130';
                        badge.style.color = 'white';
                    }
                }
                
                method.appendChild(badge);
            }
        });
    };

    // Initialize badges and refresh on UI changes
    addBadges();
    const observer = new MutationObserver(addBadges);
    observer.observe(document.body, { childList: true, subtree: true });

    // Add dark mode toggle
    const darkModeToggle = document.createElement('button');
    darkModeToggle.textContent = '🌙';
    darkModeToggle.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        background: #61dafb;
        color: #1a1a1a;
        border: none;
        padding: 8px 12px;
        border-radius: 20px;
        cursor: pointer;
        font-size: 16px;
        z-index: 9999;
        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
    `;
    
    darkModeToggle.addEventListener('click', () => {
        document.body.classList.toggle('light-theme');
        darkModeToggle.textContent = document.body.classList.contains('light-theme') ? '☀️' : '🌙';
    });
    
    document.body.appendChild(darkModeToggle);

    // Add light theme styles
    const lightThemeStyles = document.createElement('style');
    lightThemeStyles.textContent = `
        body.light-theme .swagger-ui {
            filter: invert(1) hue-rotate(180deg);
        }
        body.light-theme .swagger-ui img,
        body.light-theme .swagger-ui .highlight-code {
            filter: invert(1) hue-rotate(180deg);
        }
    `;
    document.head.appendChild(lightThemeStyles);

    console.log('🚀 Enhanced Motely API Documentation loaded successfully!');
});
