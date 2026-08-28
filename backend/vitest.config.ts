import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    // The logic under test is pure (validators, anti-cheat rules, HMAC), so it
    // runs directly on Node's WebCrypto rather than spinning up workerd.
    environment: 'node',
    include: ['test/**/*.test.ts'],
  },
})
