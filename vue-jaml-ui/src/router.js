import { createRouter, createWebHistory } from 'vue-router'
import Home from './views/Home.vue'
import JamlUI from './views/JamlUI.vue'
import JamlGenie from './views/JamlGenie.vue'

const routes = [
  {
    path: '/',
    name: 'Home',
    component: Home
  },
  {
    path: '/jaml',
    name: 'JAML',
    component: JamlUI
  },
  {
    path: '/genie',
    name: 'Genie',
    component: JamlGenie
  }
]

const router = createRouter({
  history: createWebHistory('/'),
  routes
})

export default router
