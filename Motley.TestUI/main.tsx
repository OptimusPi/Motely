import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { MotelyShopStreamAnalyzer } from './motely-ui/MotelyShopStreamAnalyzer'

const el = document.getElementById('root')
if (!el) throw new Error('#root missing')

createRoot(el).render(
  <StrictMode>
    <MotelyShopStreamAnalyzer onBack={() => window.history.back()} />
  </StrictMode>
)
