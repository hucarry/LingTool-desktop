import { afterEach } from 'vitest'

afterEach(() => {
  window.localStorage.clear()
  document.documentElement.removeAttribute('data-theme')
  document.body.innerHTML = ''
})
