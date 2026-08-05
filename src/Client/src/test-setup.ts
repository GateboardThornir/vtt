// Adds jest-dom's DOM matchers (toBeInTheDocument, toHaveTextContent, …) to Vitest's expect.
// This file sits under src/, so it is inside tsconfig.app.json's include and its type
// augmentation reaches the test files without a "types" entry in the compiler options.
import '@testing-library/jest-dom/vitest'
