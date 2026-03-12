import { createApp } from 'vue'
import DevUI from 'vue-devui'
import 'vue-devui/style.css'
import '@devui-design/icons/icomoon/devui-icon.css'
import './style.css'
import App from './App.vue'
import { router } from './router'

const app = createApp(App)
app.use(DevUI)
app.use(router)
app.mount('#app')
