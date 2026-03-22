import { Component, type ReactNode } from 'react'
import { Html } from '@react-three/drei'

type Props = { children: ReactNode; fallback?: ReactNode }
type State = { error: Error | null }

/**
 * Error boundary for use inside R3F Canvas trees.
 * Catches render errors (e.g. failed texture loads) and shows an Html overlay
 * instead of crashing the whole scene.
 */
export class R3FErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error) {
    console.error('[R3FErrorBoundary]', error)
  }

  render() {
    if (this.state.error) {
      return (
        this.props.fallback ?? (
          <Html center>
            <div style={{ color: '#e74c3c', fontSize: '0.85rem', textAlign: 'center' }}>
              Scene error — check console
            </div>
          </Html>
        )
      )
    }
    return this.props.children
  }
}
