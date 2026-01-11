# JAML UI Reimplementation PRD
## React TypeScript Web UI for Motely.API Administration & Multiplayer Balatro Seed Searching

---

## Executive Summary

**Project Goal**: Reimplement the existing Vue 3 JAML UI using React TypeScript to achieve consistency with ErraticDeck.app and WeeJoker.app, improve development experience, and enable shared tech stack across applications.

**Current State**: Vue 3 + Vite application with draggable panels, Monaco editor, SignalR real-time features, and Balatro-themed styling.

**Target State**: React TypeScript application with identical functionality, modern UI patterns, and enhanced developer experience.

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Technical Requirements](#technical-requirements)
3. [Core Features](#core-features)
4. [User Interface Design](#user-interface-design)
5. [Architecture](#architecture)
6. [Implementation Plan](#implementation-plan)
7. [Performance Requirements](#performance-requirements)
8. [Testing Strategy](#testing-strategy)
9. [Deployment & DevOps](#deployment--devops)
10. [Migration Strategy](#migration-strategy)
11. [Success Metrics](#success-metrics)

---

## Project Overview

### Problem Statement

The current Vue 3 JAML UI, while functional, presents several challenges:
- **Development Experience**: Vue 3 ecosystem doesn't align with other applications
- **Code Consistency**: Divergent tech stack from ErraticDeck.app and WeeJoker.app  
- **Maintenance Burden**: Separate frameworks increase complexity
- **Developer Velocity**: Team more productive in React TypeScript

### Solution Overview

Reimplement JAML UI using React TypeScript while preserving all existing functionality:
- **Tech Stack Alignment**: Match ErraticDeck.app and WeeJoker.app
- **Enhanced Developer Experience**: Leverage React ecosystem and TypeScript benefits
- **Maintained Functionality**: Zero feature loss during migration
- **Improved Performance**: Modern React patterns and optimizations

### Success Criteria

- [ ] 100% feature parity with existing Vue 3 implementation
- [ ] Improved developer satisfaction and velocity
- [ ] Shared component library across applications
- [ ] Enhanced performance and user experience
- [ ] Seamless migration for existing users

---

## Technical Requirements

### Technology Stack

#### Frontend Framework
- **React 18+**: Latest stable version with concurrent features
- **TypeScript 5+**: Strict mode for type safety
- **Vite**: Build tool for fast development and optimized builds

#### UI Framework & Styling
- **TailwindCSS**: Utility-first CSS framework
- **Headless UI**: Accessible component primitives
- **Framer Motion**: Smooth animations and transitions
- **Lucide React**: Consistent icon system

#### State Management
- **Zustand**: Lightweight state management
- **React Query (TanStack Query)**: Server state management and caching
- **React Hook Form**: Form state management with validation

#### Real-time Communication
- **SignalR Client**: Real-time multiplayer functionality
- **WebSocket Fallback**: For environments requiring WebSocket API

#### Code Editing & Data Display
- **Monaco Editor React**: Code editing with syntax highlighting
- **React Table (TanStack Table)**: Advanced data tables
- **React Virtualized**: Efficient rendering of large datasets

#### Development Tools
- **ESLint + Prettier**: Code quality and formatting
- **Husky**: Git hooks for pre-commit checks
- **Storybook**: Component development and documentation
- **Vitest**: Unit testing framework
- **Playwright**: End-to-end testing

### Browser Support
- **Modern Browsers**: Chrome 90+, Firefox 88+, Safari 14+, Edge 90+
- **Mobile Support**: Responsive design for tablets and large phones
- **Progressive Enhancement**: Core functionality without JavaScript

---

## Core Features

### 1. Panel Management System

#### Draggable Panel Architecture
```typescript
interface PanelConfig {
  id: string;
  type: 'editor' | 'results' | 'chat' | 'config' | 'preview';
  position: { x: number; y: number };
  size: { width: number; height: number };
  isMinimized: boolean;
  isMaximized: boolean;
  zIndex: number;
  color: 'red' | 'blue' | 'green' | 'purple' | 'orange';
}
```

#### Panel Types
- **Editor Panel**: Monaco-based JAML/YAML editor
- **Results Panel**: Seed search results with filtering
- **Chat Panel**: Real-time multiplayer communication
- **Config Panel**: Search configuration and filters
- **Preview Panel**: Live preview of filter results

#### Panel Interactions
- **Drag & Drop**: Smooth panel repositioning
- **Resize**: Resizable panels with constraints
- **Snap-to-Grid**: Optional alignment system
- **Tab Management**: Manila-style tabs with drag reordering
- **Panel States**: Minimized, maximized, docked, floating

### 2. JAML Editor

#### Monaco Editor Integration
```typescript
interface EditorConfig {
  language: 'yaml' | 'json';
  theme: 'vs-dark' | 'balatro-theme';
  fontSize: number;
  wordWrap: boolean;
  minimap: boolean;
  lineNumbers: boolean;
}
```

#### Features
- **Syntax Highlighting**: JAML/YAML with custom schema validation
- **Auto-completion**: Context-aware suggestions
- **Error Highlighting**: Real-time validation feedback
- **Code Folding**: Collapsible sections for complex filters
- **Multiple Cursors**: Advanced editing capabilities
- **Search & Replace**: Powerful text manipulation

#### JAML Schema Support
- **Validation**: Real-time schema checking
- **Hover Information**: Property descriptions and examples
- **IntelliSense**: Smart completion for JAML syntax
- **Error Messages**: Clear, actionable validation feedback

### 3. Real-time Multiplayer

#### SignalR Integration
```typescript
interface MultiplayerState {
  isConnected: boolean;
  roomId: string;
  users: User[];
  sharedState: SharedFilterState;
  chatMessages: ChatMessage[];
}
```

#### Features
- **Live Collaboration**: Multiple users editing simultaneously
- **Cursor Tracking**: See other users' cursor positions
- **Change Indicators**: Visual feedback for modifications
- **Presence Awareness**: Online user status and activity
- **Chat System**: Real-time text communication

#### Conflict Resolution
- **Operational Transformation**: Merge concurrent edits
- **User Awareness**: Clear indication of who changed what
- **Rollback Capability**: Undo/redo with user attribution

### 4. Seed Search & Results

#### Search Configuration
```typescript
interface SearchConfig {
  deck: string;
  stake: string;
  filters: JamlFilter[];
  anteRange: [number, number];
  seedRange?: [number, number];
  maxResults: number;
}
```

#### Results Display
- **Data Tables**: Sortable, filterable result grids
- **Virtual Scrolling**: Efficient handling of large result sets
- **Export Options**: CSV, JSON, and custom formats
- **Visualization**: Charts and graphs for result analysis
- **Bookmarking**: Save and share interesting seeds

#### Performance Features
- **Streaming Results**: Real-time result updates
- **Pagination**: Efficient memory usage
- **Caching**: Intelligent result caching
- **Background Processing**: Non-blocking search operations

### 5. Admin Interface

#### User Management
- **Authentication**: Secure user login system
- **Permissions**: Role-based access control
- **Session Management**: Secure session handling
- **Audit Logging**: Track all administrative actions

#### System Monitoring
- **Performance Metrics**: Real-time system health
- **Search Statistics**: Usage analytics and insights
- **Error Tracking**: Comprehensive error monitoring
- **Resource Usage**: CPU, memory, and storage metrics

---

## User Interface Design

### Design System

#### Color Palette (Balatro Theme)
```css
:root {
  /* Primary Colors */
  --balatro-red: #ff4c40;
  --balatro-dark-red: #a02721;
  --balatro-blue: #0093ff;
  --balatro-dark-blue: #0057a1;
  --balatro-purple: #9b59b6;
  --balatro-dark-purple: #5D3570;
  --balatro-green: #429f79;
  --balatro-dark-green: #215f46;
  --balatro-gold: #eaba44;
  --balatro-dark-gold: #b89435;
  --balatro-orange: #ff9800;
  --balatro-dark-orange: #cc7700;

  /* Neutral Colors */
  --bg-primary: #33464b;
  --bg-secondary: #3a5055;
  --bg-tertiary: #1e2b2d;
  --text-primary: #ffffff;
  --text-secondary: #b9c2d2;
  --text-muted: #777e89;
}
```

#### Typography
- **Font Family**: 'm6x11plus', monospace (Balatro pixel font)
- **Font Sizes**: 12px, 14px, 16px, 18px, 24px, 32px
- **Font Weights**: Normal (400), Medium (500) - **NO BOLD**
- **Line Height**: 1.5 for readability

#### Component Library
```typescript
// Reusable UI components
export const Button = ({ variant, size, children, ...props }) => { /* ... */ };
export const Input = ({ type, error, ...props }) => { /* ... */ };
export const Panel = ({ color, children, ...props }) => { /* ... */ };
export const Table = ({ data, columns, ...props }) => { /* ... */ };
export const Modal = ({ isOpen, onClose, children, ...props }) => { /* ... */ };
```

### Layout System

#### Panel Grid System
- **CSS Grid**: Flexible panel positioning
- **Responsive Design**: Adaptive layouts for different screen sizes
- **Breakpoints**: Mobile (<768px), Tablet (768px-1024px), Desktop (>1024px)
- **Panel Constraints**: Minimum/maximum sizes and positions

#### Navigation Structure
- **Top Bar**: Application title, user menu, settings
- **Panel Tabs**: Manila-style draggable tabs
- **Context Menus**: Right-click actions and shortcuts
- **Keyboard Shortcuts**: Power user productivity features

### Interaction Patterns

#### Drag & Drop
- **Visual Feedback**: Ghost images and drop zones
- **Constraints**: Prevent invalid panel positions
- **Snapping**: Optional magnetic alignment
- **Animations**: Smooth transitions and micro-interactions

#### Form Interactions
- **Real-time Validation**: Immediate feedback
- **Auto-save**: Prevent data loss
- **Progressive Disclosure**: Show advanced options as needed
- **Keyboard Navigation**: Full accessibility support

---

## Architecture

### Application Structure

```
src/
├── components/          # Reusable UI components
│   ├── ui/             # Basic UI primitives
│   ├── panels/         # Panel-specific components
│   ├── forms/          # Form components
│   └── charts/         # Data visualization
├── features/           # Feature-based modules
│   ├── editor/         # JAML editor
│   ├── search/         # Seed search
│   ├── multiplayer/    # Real-time features
│   └── admin/          # Admin interface
├── hooks/              # Custom React hooks
├── services/           # API and external services
├── stores/             # State management
├── utils/              # Utility functions
├── types/              # TypeScript definitions
└── styles/             # Global styles and themes
```

### State Management Architecture

#### Global State (Zustand)
```typescript
interface AppState {
  // Panel management
  panels: PanelStore;
  
  // User session
  auth: AuthStore;
  
  // Application settings
  settings: SettingsStore;
  
  // Multiplayer state
  multiplayer: MultiplayerStore;
}
```

#### Server State (React Query)
```typescript
// API queries and mutations
const useSearchResults = (config: SearchConfig) => { /* ... */ };
const useUserFilters = () => { /* ... */ };
const useSavedSeeds = () => { /* ... */ };
```

#### Component State
- **Local State**: Form inputs, UI toggles
- **Derived State**: Computed values from props/state
- **Persistent State**: Local storage for user preferences

### Data Flow

#### Unidirectional Data Flow
1. **User Action** → Component Event Handler
2. **State Update** → Store/Reducer
3. **Side Effects** → API calls, WebSocket messages
4. **State Reflection** → UI re-render

#### Real-time Data Flow
1. **WebSocket Message** → SignalR Handler
2. **State Update** → Store mutation
3. **UI Update** → Component re-render
4. **Conflict Resolution** → Operational transformation

### API Integration

#### REST API Endpoints
```typescript
// Authentication
POST /api/auth/login
POST /api/auth/logout
GET  /api/auth/profile

// Search operations
POST /api/search/start
GET  /api/search/status/:id
GET  /api/search/results/:id

// Data management
GET  /api/filters
POST /api/filters
PUT  /api/filters/:id
DELETE /api/filters/:id
```

#### SignalR Hub Methods
```typescript
// Real-time collaboration
hubConnection.on('FilterChanged', (change: FilterChange) => { /* ... */ });
hubConnection.on('UserJoined', (user: User) => { /* ... */ });
hubConnection.on('SearchProgress', (progress: SearchProgress) => { /* ... */ });

// Hub methods
hubConnection.invoke('JoinRoom', roomId);
hubConnection.invoke('SendFilterChange', change);
hubConnection.invoke('StartSearch', config);
```

---

## Implementation Plan

### Phase 1: Foundation (Weeks 1-2)

#### Week 1: Project Setup
- [ ] Initialize React TypeScript project with Vite
- [ ] Configure development environment (ESLint, Prettier, Husky)
- [ ] Set up Storybook for component development
- [ ] Create basic project structure and routing
- [ ] Implement design system tokens (colors, typography, spacing)

#### Week 2: Core Components
- [ ] Build foundational UI components (Button, Input, Panel)
- [ ] Implement panel management system (drag, resize, tabs)
- [ ] Create layout system with responsive design
- [ ] Set up state management (Zustand stores)
- [ ] Configure Monaco Editor integration

### Phase 2: Core Features (Weeks 3-4)

#### Week 3: Editor & Search
- [ ] Implement JAML editor with syntax highlighting
- [ ] Add YAML schema validation and auto-completion
- [ ] Build search configuration interface
- [ ] Integrate with Motely.API for seed searching
- [ ] Implement results display with data tables

#### Week 4: Real-time Features
- [ ] Set up SignalR client and connection management
- [ ] Implement multiplayer room system
- [ ] Add real-time collaboration features
- [ ] Build chat interface and presence indicators
- [ ] Implement conflict resolution for concurrent edits

### Phase 3: Advanced Features (Weeks 5-6)

#### Week 5: Admin & Analytics
- [ ] Build user authentication and authorization
- [ ] Implement admin dashboard and user management
- [ ] Add system monitoring and performance metrics
- [ ] Create audit logging and error tracking
- [ ] Implement data export and reporting features

#### Week 6: Polish & Optimization
- [ ] Performance optimization and code splitting
- [ ] Accessibility improvements and testing
- [ ] Mobile responsiveness and touch interactions
- [ ] Error boundaries and graceful degradation
- [ ] Documentation and deployment preparation

### Phase 4: Testing & Migration (Weeks 7-8)

#### Week 7: Comprehensive Testing
- [ ] Unit testing with Vitest and React Testing Library
- [ ] Integration testing for API and WebSocket connections
- [ ] End-to-end testing with Playwright
- [ ] Performance testing and optimization
- [ ] Security testing and vulnerability scanning

#### Week 8: Migration & Launch
- [ ] Data migration from Vue 3 application
- [ ] User acceptance testing and feedback incorporation
- [ ] Production deployment and monitoring setup
- [ ] User training and documentation
- [ ] Post-launch support and maintenance planning

---

## Performance Requirements

### Performance Targets

#### Core Web Vitals
- **First Contentful Paint (FCP)**: < 1.5 seconds
- **Largest Contentful Paint (LCP)**: < 2.5 seconds
- **First Input Delay (FID)**: < 100 milliseconds
- **Cumulative Layout Shift (CLS)**: < 0.1

#### Application Performance
- **Initial Load**: < 3 seconds on 3G connection
- **Panel Switching**: < 200 milliseconds
- **Search Results**: < 500 milliseconds for 10,000 results
- **Real-time Updates**: < 50 milliseconds latency
- **Memory Usage**: < 100MB for typical usage

### Optimization Strategies

#### Code Optimization
- **Tree Shaking**: Remove unused code
- **Code Splitting**: Lazy load features and routes
- **Bundle Analysis**: Regular bundle size monitoring
- **Asset Optimization**: Compressed images and fonts

#### Runtime Optimization
- **React.memo**: Prevent unnecessary re-renders
- **useMemo/useCallback**: Optimize expensive computations
- **Virtual Scrolling**: Efficient large list rendering
- **Web Workers**: Offload heavy computations

#### Network Optimization
- **HTTP/2**: Multiplexed requests
- **Service Worker**: Offline capability and caching
- **CDN Distribution**: Global content delivery
- **Compression**: Gzip/Brotli response compression

---

## Testing Strategy

### Testing Pyramid

#### Unit Tests (70%)
- **Component Testing**: React Testing Library
- **Hook Testing**: Custom hook validation
- **Utility Testing**: Pure function verification
- **Coverage Target**: 90%+ code coverage

#### Integration Tests (20%)
- **API Integration**: Service and repository testing
- **WebSocket Testing**: SignalR connection validation
- **State Management**: Store interaction testing
- **Component Integration**: Multi-component workflows

#### End-to-End Tests (10%)
- **User Journeys**: Critical path testing
- **Cross-browser Testing**: Browser compatibility
- **Mobile Testing**: Responsive design validation
- **Performance Testing**: Load and stress testing

### Test Automation

#### Continuous Integration
- **Pre-commit Hooks**: Linting and formatting checks
- **Pull Request Tests**: Full test suite execution
- **Regression Testing**: Automated visual regression
- **Performance Monitoring**: Bundle size and performance metrics

#### Quality Gates
- **Code Coverage**: Minimum 90% threshold
- **Performance Budget**: Bundle size limits
- **Accessibility**: WCAG 2.1 AA compliance
- **Security**: OWASP vulnerability scanning

---

## Deployment & DevOps

### Deployment Architecture

#### Production Environment
- **Static Hosting**: Vercel, Netlify, or AWS S3
- **CDN**: Global content delivery network
- **Domain**: HTTPS with automatic certificate renewal
- **Monitoring**: Real-time performance and error tracking

#### Development Workflow
- **Feature Branches**: GitFlow branching strategy
- **Pull Requests**: Code review and automated testing
- **Staging Environment**: Production-like testing environment
- **Rollback Strategy**: Instantaneous rollback capability

### Infrastructure as Code

#### Configuration Management
```yaml
# docker-compose.yml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "3000:3000"
    environment:
      - NODE_ENV=production
      - API_URL=https://api.motely.app
```

#### CI/CD Pipeline
```yaml
# .github/workflows/deploy.yml
name: Deploy
on:
  push:
    branches: [main]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run tests
        run: npm test
  deploy:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - name: Deploy to production
        run: npm run deploy
```

---

## Migration Strategy

### Data Migration

#### User Data Migration
- **Authentication**: Transfer user accounts and sessions
- **Preferences**: Migrate user settings and customizations
- **Saved Filters**: Convert and preserve filter configurations
- **Search History**: Maintain user search history and bookmarks

#### System Migration
- **API Compatibility**: Ensure backward compatibility
- **Database Migration**: Zero-downtime database updates
- **Feature Flags**: Gradual feature rollout
- **Monitoring**: Track migration success and errors

### User Migration

#### Communication Plan
- **Advance Notice**: 30-day migration announcement
- **Feature Comparison**: Highlight improvements and benefits
- **Training Materials**: Video tutorials and documentation
- **Support Channels**: Dedicated migration support team

#### Transition Strategy
- **Parallel Running**: Both systems available during transition
- **Gradual Migration**: User cohorts migrated in phases
- **Feedback Collection**: Continuous user feedback incorporation
- **Rollback Plan**: Emergency rollback procedures

---

## Success Metrics

### Technical Metrics

#### Performance Metrics
- [ ] **Page Load Time**: < 3 seconds (95th percentile)
- [ ] **Time to Interactive**: < 5 seconds
- [ ] **Bundle Size**: < 1MB initial, < 300KB per route
- [ ] **Error Rate**: < 0.1% of user sessions

#### Quality Metrics
- [ ] **Code Coverage**: > 90% test coverage
- [ ] **Accessibility Score**: 100% WCAG 2.1 AA compliance
- [ ] **Security Score**: 0 critical vulnerabilities
- [ ] **Performance Score**: > 90 Lighthouse score

### Business Metrics

#### User Experience Metrics
- [ ] **User Satisfaction**: > 4.5/5 rating
- [ ] **Task Completion Rate**: > 95%
- [ ] **User Retention**: > 90% monthly retention
- [ ] **Support Tickets**: < 5% increase vs. current

#### Development Metrics
- [ ] **Development Velocity**: 2x faster feature delivery
- [ ] **Bug Resolution Time**: < 24 hours for critical issues
- [ ] **Code Review Time**: < 4 hours average
- [ ] **Deployment Frequency**: Weekly production deployments

### Adoption Metrics

#### Migration Success
- [ ] **User Migration Rate**: > 95% within 30 days
- [ ] **Feature Parity**: 100% of Vue features available
- [ ] **Downtime**: < 5 minutes total during migration
- [ ] **Data Loss**: 0 data corruption or loss incidents

---

## Conclusion

This PRD outlines a comprehensive plan to reimplement the JAML UI using React TypeScript, achieving technical consistency across applications while improving the developer experience and user satisfaction. The phased approach ensures a smooth migration with minimal risk and maximum benefit.

### Key Success Factors

1. **Technical Excellence**: Modern React patterns and best practices
2. **User Experience**: Seamless migration with enhanced features
3. **Developer Productivity**: Improved tools and workflows
4. **System Reliability**: Robust testing and monitoring
5. **Future-Proofing**: Scalable architecture for continued growth

### Next Steps

1. **Stakeholder Review**: Gather feedback and approval on this PRD
2. **Resource Planning**: Allocate development team and timeline
3. **Technical Discovery**: Deep dive into existing Vue implementation
4. **Prototype Development**: Build proof-of-concept for key features
5. **Implementation Kickoff**: Begin Phase 1 development activities

---

*This document serves as the authoritative guide for the JAML UI reimplementation project and should be referenced throughout the development lifecycle.*
