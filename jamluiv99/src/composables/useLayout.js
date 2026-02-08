import { ref, onMounted, onUnmounted } from 'vue'

export function useLayout() {
  const isPortrait = ref(window.innerHeight > window.innerWidth)
  const windowWidth = ref(window.innerWidth)
  const windowHeight = ref(window.innerHeight)

  const updateOrientation = () => {
    isPortrait.value = window.innerHeight > window.innerWidth
    windowWidth.value = window.innerWidth
    windowHeight.value = window.innerHeight
  }

  onMounted(() => {
    window.addEventListener('resize', updateOrientation)
    window.addEventListener('orientationchange', updateOrientation)
    updateOrientation()
  })

  onUnmounted(() => {
    window.removeEventListener('resize', updateOrientation)
    window.removeEventListener('orientationchange', updateOrientation)
  })

  return {
    isPortrait,
    windowWidth,
    windowHeight
  }
}


