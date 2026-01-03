import { createRouter, createWebHashHistory } from 'vue-router'
import Home from './views/Home.vue'
import JamlUI from './views/JamlUI.vue'
import JamlGenie from './views/JamlGenie.vue'

const routes = [
  {
    path: '/',
    name: 'JAML',
    component: JamlUI
  },
  {
    path: '/home',
    name: 'Home',
    component: Home
  },
  {
    path: '/genie',
    name: 'Genie',
    component: JamlGenie
  }
]

const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
  routes
})

export default router
