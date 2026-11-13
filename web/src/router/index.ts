import { createRouter, createWebHashHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'

const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
  scrollBehavior(_to, _from, savedPosition) {
    return savedPosition ?? { top: 0 }
  },
  routes: [
    { path: '/plans', name: 'plans', component: () => import('../views/PlanView.vue') },
    { path: '/capital', name: 'capital', component: () => import('../views/CapitalView.vue') },
    {
      path: '/',
      name: 'home',
      component: HomeView
    },
    {
      path: '/setup',
      name: 'setup',
      component: () => import('../views/SetupView.vue')
    },
    {
      path: '/assets',
      name: 'assets',
      component: () => import('../views/AssetsView.vue')
    },
    {
      path: '/ledger',
      name: 'ledger',
      component: () => import('../views/LedgerView.vue')
    },
    { path: '/categories', name: 'categories', component: () => import('../views/CategoriesView.vue') },
    {
      path: '/mine',
      name: 'mine',
      component: () => import('../views/MineView.vue')
    },
    {
      path: '/backup',
      name: 'backup',
      component: () => import('../views/BackupView.vue')
    },
    {
      path: '/salary',
      name: 'salary',
      component: () => import('../views/SalaryView.vue')
    },
    {
      path: '/cash',
      name: 'cash',
      component: () => import('../views/CashView.vue')
    },
    {
      path: '/provident',
      name: 'provident',
      component: () => import('../views/ProvidentView.vue')
    },
    {
      path: '/gold',
      name: 'gold',
      component: () => import('../views/GoldView.vue')
    },
    {
      path: '/stocks',
      name: 'stocks',
      component: () => import('../views/StocksView.vue')
    },
    {
      path: '/funds/:view?',
      name: 'funds',
      component: () => import('../views/FundsView.vue')
    },
    {
      path: '/entry',
      name: 'entry',
      component: () => import('../views/EntryView.vue')
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/'
    }
  ]
})

export default router
