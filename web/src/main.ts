import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import App from './App.vue'
import { router } from './router'
import { loadSystemSettings } from './stores/systemSettings'
import 'element-plus/dist/index.css'
import './styles/main.css'
import './modules/exam-study/workspace.css'

createApp(App).use(createPinia()).use(router).use(ElementPlus).mount('#app')

void loadSystemSettings()
window.addEventListener('focus', () => void loadSystemSettings())
