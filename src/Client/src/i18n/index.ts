import i18next from 'i18next'
import { initReactI18next } from 'react-i18next'
import { en } from './en'
import { it } from './it'

export const languages = ['en', 'it'] as const

export type Language = (typeof languages)[number]

/**
 * Sets up translation before anything renders.
 *
 * Every user-facing string goes through here from the first one, per
 * `.claude/rules/frontend.md`. The infrastructure was moved out of Phase 3 for exactly this
 * moment: retrofitting translation is expensive because it is boring, so it never happens.
 */
export function initialiseI18n(language: Language = 'en'): typeof i18next {
  void i18next.use(initReactI18next).init({
    resources: {
      en: { translation: en },
      it: { translation: it },
    },
    lng: language,
    fallbackLng: 'en',
    interpolation: {
      // React already escapes everything it renders; doing it twice turns an apostrophe into
      // &#39; on screen.
      escapeValue: false,
    },
  })

  return i18next
}
