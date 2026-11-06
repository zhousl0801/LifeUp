import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    vue(),
    VitePWA({
      registerType: 'prompt',
      injectRegister: null,
      manifest: {
        name: 'LifeUp',
        short_name: 'LifeUp',
        description: '个人资产观察与日常记账',
        lang: 'zh-CN',
        id: '/',
        scope: '/',
        prefer_related_applications: false,
        theme_color: '#FFFFFF',
        background_color: '#F6F7FB',
        display: 'standalone',
        orientation: 'portrait',
        start_url: '/',
        icons: [
          {
            src: '/icons/icon-192.png',
            sizes: '192x192',
            type: 'image/png',
            purpose: 'any'
          },
          {
            src: '/icons/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'any'
          },
          {
            src: '/icons/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable'
          }
        ]
      }
    })
  ],
  build: {
    modulePreload: {
      resolveDependencies: (_filename, dependencies) =>
        dependencies.filter((dependency) => !dependency.includes('chartEcharts'))
    }
  },
  server: {
    proxy: {
      '/api': {
        target: process.env.ASSET_KEEPER_API_PROXY ?? 'http://localhost:5069',
        changeOrigin: true
      }
    }
  }
})
