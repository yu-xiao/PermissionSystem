import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import './styles/index.scss'
import App from './App.vue'
import { router } from './router'
import { pinia } from './stores'
import { setupPermissionDirective } from './directives/permission'
import { displayText, yesNo } from './utils/display'
import { useAppStore } from './stores/app'
import { useAuthStore } from './stores/auth'
import { configureAuthorizationStateReloader } from './utils/request'

const app = createApp(App)

app.config.globalProperties.$displayText = displayText
app.config.globalProperties.$yesNo = yesNo

app.use(pinia)
useAppStore().applyTheme()
configureAuthorizationStateReloader(() => useAuthStore().reloadAuthorizationState())
app.use(router)
app.use(ElementPlus, { locale: zhCn })
setupPermissionDirective(app)
app.mount('#app')
