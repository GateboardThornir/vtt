import { type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, Route, Routes } from 'react-router'
import { AdminAccountsPage } from './accounts/AdminAccountsPage'
import { CampaignPage } from './campaigns/CampaignPage'
import { CampaignsPage } from './campaigns/CampaignsPage'
import { CharacterSheetPage } from './characters/CharacterSheetPage'
import { CharactersPage } from './characters/CharactersPage'
import { TablePage } from './table/TablePage'
import { NotificationBell } from './campaigns/NotificationBell'
import { RegisterPage } from './accounts/RegisterPage'
import { SessionProvider } from './accounts/SessionProvider'
import { useSession } from './accounts/sessionContext'
import { SignInPage } from './accounts/SignInPage'
import { languages } from './i18n'

export default function App(): JSX.Element {
  return (
    <SessionProvider>
      <Shell />
    </SessionProvider>
  )
}

function Shell(): JSX.Element {
  const { t } = useTranslation()
  const { session, loading, unreachable, signOut } = useSession()

  return (
    <main>
      <header>
        <strong>{t('common.appName')}</strong>
        <LanguageSwitcher />
        {session !== null && <NotificationBell />}
        {session !== null && (
          <button type="button" onClick={() => void signOut()}>
            {t('common.signOut')}
          </button>
        )}
      </header>

      <Routes>
        {/* Registration is reachable signed out, and is the only route that is. */}
        <Route path="/register" element={<RegisterPage />} />
        <Route
          path="*"
          element={
            loading ? (
              <p>{t('common.loading')}</p>
            ) : unreachable ? (
              // Not a sign-in form: the server is not there, and offering one would invite
              // typing a password at nothing and concluding the credentials were wrong.
              <p role="alert">{t('common.serverUnreachable')}</p>
            ) : session === null ? (
              <SignInPage />
            ) : (
              <SignedIn />
            )
          }
        />
      </Routes>
    </main>
  )
}

function SignedIn(): JSX.Element {
  const { t } = useTranslation()
  const { session } = useSession()

  return (
    <Routes>
      <Route path="/admin/accounts" element={<AdminAccountsPage />} />
      <Route path="/campaigns" element={<CampaignsPage />} />
      <Route path="/campaigns/:id" element={<CampaignPage />} />
      <Route path="/campaigns/:id/characters" element={<CharactersPage />} />
      <Route path="/campaigns/:id/sessions/:sessionId" element={<TablePage />} />
      <Route path="/campaigns/:id/characters/:characterId" element={<CharacterSheetPage />} />
      <Route
        path="/"
        element={
          <section>
            <p>{t('home.signedInAs', { username: session?.username ?? '' })}</p>
            <Link to="/campaigns">{t('home.campaignsLink')}</Link>
            {/* Offered to everyone: the server refuses members regardless, and the client has
                no business deciding permissions. Hiding it would be a guess about the answer. */}
            <Link to="/admin/accounts">{t('home.adminLink')}</Link>
          </section>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

function LanguageSwitcher(): JSX.Element {
  const { t, i18n } = useTranslation()

  return (
    <label>
      {t('common.language')}
      <select value={i18n.language} onChange={(event) => void i18n.changeLanguage(event.target.value)}>
        {languages.map((language) => (
          <option key={language} value={language}>
            {language.toUpperCase()}
          </option>
        ))}
      </select>
    </label>
  )
}
