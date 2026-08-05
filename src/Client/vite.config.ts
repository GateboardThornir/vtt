/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // The browser only ever talks to the Vite origin; Vite forwards /api to the ASP.NET server.
      // This is not convenience — task 013 issues HttpOnly, SameSite=Lax session cookies, and a
      // cross-origin request would not carry them without SameSite=None, which needs HTTPS in
      // development. One origin sidesteps all of it, and matches production, where Caddy serves
      // the app and proxies /api from the same host.
      //
      // changeOrigin stays at its default (false): the Host header is passed through, and the
      // server has no reason to care which of the two ports the request arrived on.
      '/api': 'http://localhost:5080',
    },
  },
  test: {
    // jsdom simulates enough of a browser to render and query a component tree. It does not lay
    // anything out or paint, so anything depending on real geometry or true event ordering — the
    // Pixi canvas at task 052, most obviously — needs a different kind of test.
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],

    // No `globals: true`: tests import describe/it/expect explicitly, which keeps tsconfig free of
    // a types entry and lets ESLint keep flagging genuinely undefined identifiers.
    globals: false,
  },
})
